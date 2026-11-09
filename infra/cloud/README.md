# Deployment profiles

The user selected Singapore and authorized a runnable future cloud path while keeping this delivery local. The supplied [AWS profile](../aws/README.md) runs the same application on one ARM64 EC2 host in `ap-southeast-1`, behind Session Manager with no inbound security-group rules. Terraform configuration is validated; no AWS apply or cloud runtime verification is part of this delivery.

The [Docker-local profile](../local/README.md) remains the default. Cloud support here means this specific single-host path, not arbitrary hosted databases, Kafka providers, storage vendors, Kubernetes, or AI providers.
