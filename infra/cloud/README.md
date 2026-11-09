# Deployment profiles

Local learning remains the default. Azure is the preferred future cloud deployment; AWS is retained as an alternative. No cloud profile has been applied.

| Profile | Runtime and access | Persistent data |
|---|---|---|
| [Docker-local](../local/README.md) | Existing Make/Compose lifecycle on the developer machine | Named Docker volumes; normal shutdown preserves them |
| [Azure Singapore](../azure/README.md), preferred future cloud | One Ubuntu VM in `southeastasia`; existing SSH key, operator IPv4 `/32`, local port-forward | Separate encrypted managed data disk with Terraform deletion protection |
| [AWS Singapore](../aws/README.md), alternative | One ARM64 EC2 host in `ap-southeast-1`; Session Manager, no inbound security-group rules | Separate encrypted EBS volume with Terraform deletion protection |

Each profile runs the same one-host Compose application, PostgreSQL, Kafka and transcription runtime, with host Ollama. Cloud support means these specific learning paths, not arbitrary managed databases, broker/storage vendors, Kubernetes or AI providers. There is no public assessment endpoint, managed high availability or verified cloud recovery path.

[Azure deployment SVG](../../docs/diagrams/azure.svg) · [Editable Mermaid](../../docs/diagrams/azure.mmd) · [Local runtime](../../docs/diagrams/runtime.svg).

Read the chosen profile for prerequisites, explicit plan/apply commands, backup and retained-disk recovery. Cloud compute, disks, networking and downloads can incur charges; verify the account's current prices and capacity before applying. Terraform validation is not cloud runtime evidence.
