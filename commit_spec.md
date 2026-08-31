Different mechanism than Prometheus, so let's set expectations first: Prometheus reaches out and scrapes (pulls data on a schedule). Loki does the opposite — it doesn't know how to find your pods or read anything on its own. Something else has to collect log lines and push them to Loki. That something is Promtail — an agent that runs on every node, tails container log files, and ships them over.

So this step installs two things together, not one. Same Helm pattern as before, but a chart that bundles both:

helm install loki grafana/loki-stack --namespace monitoring --set grafana.enabled=false

That last flag matters: loki-stack normally offers to install its own Grafana too — we don't want a second one, since you already have one running and configured. grafana.enabled=false skips that part and only installs Loki + Promtail.

Run it, then:

kubectl get pods -n monitoring

You should now see two new pods — one Loki pod, and a Promtail pod running on every node (in your single-node kind cluster, that's just one, but in a real multi-node cluster you'd see one Promtail per node — it needs to be everywhere logs are being written). Paste the output.



Let's get the actual reason instead of guessing further — "Unable to connect" is Grafana's generic UI message, but the real cause will be in its logs.

kubectl logs -n monitoring deploy/grafana --tail=50

And separately, let's test connectivity directly from inside the cluster's network, bypassing the browser/Grafana UI entirely — this tells us if it's a Grafana-specific issue or a networking issue:

kubectl exec -n monitoring deploy/grafana -- wget -qO- http://loki:3100/ready

That command runs wget from inside the Grafana pod itself, hitting Loki's /ready health endpoint directly — the same request Grafana's data source test would make internally, just without any of Grafana's own UI/config layer in the way.


oki and Promtail both healthy. Now let's connect this to Grafana the same way you connected Prometheus, since the pattern is identical.

In Grafana: Connections → Data sources → Add data source → Loki.

Same reasoning as before applies to the URL — Grafana talks to Loki using Kubernetes' internal DNS, not localhost:

http://loki:3100

Quick note on why this one's shorter than prometheus-server.monitoring.svc.cluster.local — Kubernetes lets you drop the namespace and suffix when the two pods are talking within the same namespace, which Grafana and Loki both are (monitoring). The full form always works; this is just the shorthand version of the same address.

Click Save & test. Tell me what it reports.


Let's get the actual reason instead of guessing further — "Unable to connect" is Grafana's generic UI message, but the real cause will be in its logs.

kubectl logs -n monitoring deploy/grafana --tail=50

And separately, let's test connectivity directly from inside the cluster's network, bypassing the browser/Grafana UI entirely — this tells us if it's a Grafana-specific issue or a networking issue:

kubectl exec -n monitoring deploy/grafana -- wget -qO- http://loki:3100/ready

That command runs wget from inside the Grafana pod itself, hitting Loki's /ready health endpoint directly — the same request Grafana's data source test would make internally, just without any of Grafana's own UI/config layer in the way.


Two different errors are mixed in there, so let's untangle them.

Error 1 (earlier): "http: server gave HTTP response to HTTPS client" — at some point the URL field actually said https://loki:3100, not http://. Possibly autocomplete, possibly a stray edit. Loki only speaks plain HTTP here, so that failed as expected.

Error 2 (most recent, repeated): "parse error at line 1, col 1: syntax error: unexpected IDENTIFIER". This is more interesting. Look at the query Grafana is actually sending itself, in the URL: query=vector(1)+vector(1). That's not something you typed — it's Grafana's own built-in health-check probe, a synthetic query it fires automatically whenever you click Save & test, unrelated to your actual logs. And it's failing to parse on Loki's side.

The chart we installed (loki-stack) ships a fairly old Loki — version="(version=2.6.1...", visible right at the top of the logs you pasted earlier. That's from 2022. Newer versions of Grafana send a health-check query format that old Loki doesn't fully understand — a straightforward version mismatch between the two charts, not something you did wrong.

Good news: this only breaks the automated health check button, not actual usability. You already proved real connectivity works (wget → ready). So:

Fix the URL field back to plain http://loki:3100 if it still says https.
Skip "Save & test" entirely if it keeps failing — just click Save (without test), or ignore the red banner.
Go straight to Explore, pick the Loki data source, and run an actual log query:
{app="demo-api"}

If that returns real log lines from your app, Loki is fully working for our purposes — the health-check probe failing is cosmetic. Try that and tell me what comes back.

Look at what actually got sent: {app="demo-api"} | json | __error__=`` . Someone (likely Grafana's own query builder, auto-added when you clicked something like "kickstart query") appended | json — a Loki filter that says "parse each log line as JSON." But your app's console logs are plain text — lines like:

Fix: strip that off entirely. In the Explore query box, replace whatever's there with just:

{app="demo-api"}

No | json, no __error__. Plain label selector, nothing else — that's the Loki equivalent of "show me raw lines matching this label," same idea as docker logs but searchable.