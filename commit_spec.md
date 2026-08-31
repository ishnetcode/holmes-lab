Now, Prometheus. We'll install it with Helm — same tool you already confirmed you have back in Step 1. Two commands, and I'll explain each before you run them:

helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo update

helm repo add ... — Helm doesn't ship Prometheus itself; it's a package manager, like apt or npm. This command tells Helm "here's a repository (a URL) where installable packages, called charts, live" — specifically the official community-maintained Prometheus chart.

helm repo update — refreshes Helm's local list of what's available in that repo, so it knows the latest chart version before you install anything.

Neither of these installs anything into your cluster yet — they're just registering where to get it from. Run those two, then let me know it completed and I'll walk you through the actual install command next.

Let's actually define it properly instead of the one-liner I gave earlier.

Helm is a package manager for Kubernetes — the same role apt plays for Ubuntu, npm for Node, or pip for Python. The problem it solves: a real application in Kubernetes is rarely one YAML file. Prometheus, for example, needs a Deployment, a Service, a ConfigMap (its scrape config), a ServiceAccount, RBAC permissions (so it's allowed to discover pods), possibly a PersistentVolumeClaim for storage — easily 10+ YAML files that all have to agree with each other.

Writing and maintaining all of that by hand for every tool you install would be brutal. So the community pre-packages it:

A chart is that bundle of YAML files, templated so values can be swapped in (like "how much memory should Prometheus get" or "what port should it listen on").
A release is one specific installation of a chart into your cluster, with a name you choose.
helm repo add (which you just ran) points Helm at a URL that hosts charts, so it knows where to fetch from.

So when you run helm install prometheus prometheus-community/prometheus, Helm is going to fetch that whole bundle, fill in the template with sensible defaults, and apply all of those YAML files to your cluster in one shot — same end result as if you'd hand-written and kubectl apply'd 10+ files yourself, but someone already did that engineering work for you.

That's the actual install command, next. Ready to run it?

Here's what actually happens when this chart installs — worth knowing before the terminal fills up with pods you didn't expect:

helm install prometheus prometheus-community/prometheus --namespace monitoring --create-namespace

Breaking down the command itself: helm install <release-name> <repo>/<chart-name> — you're naming this installation prometheus. --namespace monitoring puts everything it creates into a separate namespace (a way of grouping/isolating resources in Kubernetes) rather than dumping it in alongside your demo-api. --create-namespace tells Helm to create that namespace first, since it doesn't exist yet.

What it will actually create, several pods, not one:

prometheus-server — the actual Prometheus process, the thing that scrapes and stores metrics.
prometheus-alertmanager — handles routing alerts (not something we'll use yet, but ships by default).
prometheus-kube-state-metrics — watches the Kubernetes API itself and turns cluster state (pod counts, deployment status) into metrics.
prometheus-prometheus-node-exporter — runs on every node, reports host-level metrics (CPU, memory, disk).

Plus, invisibly: a ServiceAccount and RBAC rules (ClusterRole/ClusterRoleBinding) that grant Prometheus permission to look at pods across your cluster — without these, it legally couldn't discover your demo-api pod at all, annotations or not.

Run it, then:

kubectl get pods -n monitoring