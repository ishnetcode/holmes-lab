Grafana is not another metrics database — it's a visualization layer that can read from many different backends (Prometheus, Loki, and others) and turn queries into dashboards, panels, and eventually alerts. You've technically already queried Prometheus directly through its own basic UI (localhost:9090) — Grafana's job is to make that same data readable at a glance, and later, to show metrics and logs side by side, which Prometheus's own UI can't do.

First, register Grafana's chart repo — same pattern as before, just a different source:

helm repo add grafana https://grafana.github.io/helm-charts
helm repo update

Run those two, then I'll walk through the install command and what it actually creates before you run it.


Two things worth reading closely here, since they matter beyond just this lab.

Getting the password:

kubectl get secret --namespace monitoring grafana -o jsonpath="{.data.admin-password}" | base64 --decode

This is your first real look at a Kubernetes Secret. When Helm installed Grafana, it auto-generated a random admin password and stored it as a Secret object in the cluster — not in a file on disk, not in the chart, inside Kubernetes itself, base64-encoded (not encrypted — base64 is just an encoding, easily reversible, which is exactly what --decode is undoing here). This command reaches into that Secret and pulls the raw password back out so you can actually use it.

The warning is the important part, don't skip past it: "Persistence is disabled — you will lose your data when the Grafana pod is terminated." Right now, any dashboards you build live only inside this one running pod's filesystem. If it restarts — like every pod in this cluster already did once, a few hours ago — everything you built is gone. For this lab that's fine, low stakes, rebuild in a minute. But it's worth clocking now: this is the same "amnesia" problem you already understood with Prometheus counters, just one layer up — pods are inherently disposable in Kubernetes, and anything that needs to survive a restart has to be explicitly told to persist (a PersistentVolume, a real database, etc.).

Go run that password command, then port-forward:

kubectl --namespace monitoring port-forward svc/grafana 3000:80

(I used the Service name svc/grafana rather than the $POD_NAME dance in the notes — same result, less typing, and you already know this pattern from demo-api and prometheus-server.)

Log into http://localhost:3000 with admin and that password, and tell me you're in.

Right — base64 is a Linux/macOS command-line tool; PowerShell doesn't ship it. PowerShell has its own way to do the same decode, using .NET directly:

powershell
$secret = kubectl get secret --namespace monitoring grafana -o jsonpath="{.data.admin-password}"
[System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($secret))

Line 1 does exactly what it did before — pulls the base64-encoded password out of the Secret. Line 2 is PowerShell's equivalent of base64 --decode: FromBase64String converts it back to raw bytes, and UTF8.GetString turns those bytes into readable text.

Run both lines, copy the password it prints, then port-forward and log in:

kubectl --namespace monitoring port-forward svc/grafana 3000:80

Go to http://localhost:3000, username admin, and that password. Let me know once you're in.


Connected. Now let's build one real panel, using a metric you already understand cold — no point starting with something new.

Click that "building a dashboard" link Grafana gave you (or: Dashboards → New → New Dashboard → Add visualization → pick the Prometheus data source you just created).

You'll land on a panel editor with a query box. In it, enter the same query you already ran and understood directly in Prometheus:

rate(http_requests_failed_total[5m])

This is worth noticing explicitly: you're not learning a new query language for Grafana. It's the exact same PromQL you already used in Prometheus's own UI. Grafana isn't a separate data system — it's a nicer window onto the same data, using the same queries. That's the whole value proposition in one sentence.

Run it, and you should see a line graph appear — probably flat at 0 right now unless you've hit /api/failure recently.

Give it a title (top right, something like "Failure Rate"), and save the dashboard.

Then, to actually watch it move: open a new terminal, port-forward demo-api again, and curl /api/failure a handful of times in a loop. Watch the Grafana panel — it should tick upward within ~15-30 seconds (scrape interval, plus Grafana's own refresh).


You're in — Grafana's running, but it's an empty shell right now: no data source, no dashboards. Two steps left to make it useful.

Step 1: Connect Grafana to Prometheus.

In the Grafana UI: left sidebar → Connections → Data sources → Add data source → choose Prometheus.

It'll ask for a URL. This is worth pausing on, because it's a different address than the one you use in your browser. You reach Prometheus at localhost:9090 — but that only works because of your personal port-forward tunnel. Grafana isn't on your laptop; it's a pod running inside the cluster, right alongside Prometheus. So it should talk to Prometheus using Kubernetes' own internal DNS name, the same pattern you saw in the Helm install notes earlier:

http://prometheus-server.monitoring.svc.cluster.local

Break that down quickly since you'll see this pattern everywhere in Kubernetes: prometheus-server (Service name) .monitoring (the namespace it lives in) .svc.cluster.local (fixed suffix meaning "this is a Service, inside this cluster"). Any pod anywhere in the cluster can reach any Service using exactly this pattern, regardless of which node either one is running on.

Enter that URL, scroll down, click Save & test. It should say something like "Successfully queried the Prometheus API."

onnected. Now let's build one real panel, using a metric you already understand cold — no point starting with something new.

Click that "building a dashboard" link Grafana gave you (or: Dashboards → New → New Dashboard → Add visualization → pick the Prometheus data source you just created).

You'll land on a panel editor with a query box. In it, enter the same query you already ran and understood directly in Prometheus:

rate(http_requests_failed_total[5m])

This is worth noticing explicitly: you're not learning a new query language for Grafana. It's the exact same PromQL you already used in Prometheus's own UI. Grafana isn't a separate data system — it's a nicer window onto the same data, using the same queries. That's the whole value proposition in one sentence.

Run it, and you should see a line graph appear — probably flat at 0 right now unless you've hit /api/failure recently.

Give it a title (top right, something like "Failure Rate"), and save the dashboard.