Let's get the app running inside your actual kind cluster first — Prometheus needs something in Kubernetes to scrape, not just a container on your laptop.

Created 2 files, ran a command, read a file

Two small but important details in deployment.yaml, worth noting before you run anything:

imagePullPolicy: Never — tells Kubernetes "don't try to download this image from a registry, it should already be sitting on the node." That matters because kind runs Kubernetes inside Docker, so your locally-built image isn't automatically visible to it — we have to explicitly hand it over (next step).
The three prometheus.io/... annotations aren't magic Kubernetes fields — they're just labels. Prometheus's own scrape config (which we'll install in a minute) is written to look for them and auto-add any pod carrying them to its scrape list.

Put k8s/deployment.yaml and k8s/service.yaml into your holmes-lab/k8s/ folder. Then run this first batch — nothing Prometheus-related yet, just getting the app into the cluster:

docker build -t holmes-lab-demo-api:latest ./app
kind load docker-image holmes-lab-demo-api:latest --name holmes-lab
kubectl apply -f k8s/deployment.yaml -f k8s/service.yaml
kubectl get pods

That middle command — kind load docker-image — is the "hand it over" step I mentioned. Run those four and paste what kubectl get pods shows.



docker build -t holmes-lab-demo-api:latest ./app
This builds your Dockerfile into an actual image and tags it holmes-lab-demo-api:latest. You did this before via docker compose up --build — compose was doing this exact step for you behind the scenes. Now we're doing it directly because Kubernetes doesn't know what docker-compose is; it only understands raw images.

kind load docker-image holmes-lab-demo-api:latest --name holmes-lab
This is the one that's genuinely new. Here's the problem it solves: kind runs your whole Kubernetes cluster inside a Docker container (remember the "Docker → Kind → Kubernetes cluster" diagram from Step 2). That means the cluster has its own separate, sealed-off image storage — it can't see images sitting in your regular Docker Desktop. kind load docker-image physically copies your image into that sealed environment. Skip this step and Kubernetes will try to pull holmes-lab-demo-api:latest from the internet, fail, and your pod will sit in ErrImageNeverPull.


kubectl apply -f k8s/deployment.yaml -f k8s/service.yaml
This is where Kubernetes actually gets told what you want to exist. apply reads both YAML files and says "make the cluster's state match this" — create the Deployment (which creates a Pod running your container) and the Service (a stable internal address for it). This is the same idea as docker compose up, just Kubernetes' version of it, and split into two separate objects instead of one file.

kubectl get pods
Just a status check — lists pods and their current state (Pending, ContainerCreating, Running, CrashLoopBackOff, etc.), so you can confirm the deployment actually worked before moving on.

Order matters here: build → load → apply. If you apply before the image is loaded, the pod will fail to start, and you'd have to delete and reapply anyway.


Kubernetes Service with no type specified defaults to ClusterIP, which means: reachable only from other things inside the cluster, not from your laptop. That's deliberate — in a real cluster, most services (databases, internal APIs) should never be reachable from outside at all.