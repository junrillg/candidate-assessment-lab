# Future Azure deployment: Singapore

Azure is the preferred future cloud profile. Local Compose remains the learning default, and the [AWS Singapore profile](../aws/README.md) remains an alternative.

This profile runs the same application on one Ubuntu 24.04 x64 VM in `southeastasia` (Singapore). The default `Standard_D4s_v5` has four vCPUs and 16 GiB RAM. It uses a 32 GiB OS disk and a separate 100 GiB data disk, with Azure managed-disk server-side encryption using platform-managed keys. The data disk and its separate resource group have Terraform `prevent_destroy` protection. [Azure deployment diagram](../../docs/diagrams/azure.svg) · [Editable Mermaid](../../docs/diagrams/azure.mmd).

An existing SSH public key is required; no private key or password is generated in Terraform. The network security group admits TCP 22 only from a required operator IPv4 `/32`, then denies other inbound access. Use an SSH tunnel to `localhost:5188`. Application, PostgreSQL and Kafka ports stay on host loopback; Ollama is reachable by the same host's Docker network. There is no public assessment endpoint, Bastion service or managed database/broker.

The profile has not been applied or tested on Azure. Terraform validation does not establish subscription permissions, available SKU capacity, successful cloud-init, runtime performance, tunnel microphone behavior or restore. The system has one failure domain and no high-availability claim.

## Review and deploy later

Prerequisites: Azure CLI authentication for the intended subscription, Terraform 1.7 or later, an existing SSH key pair, the operator's current public IPv4 address, a published full repository commit and an account cost estimate. VM compute, managed disks, a Standard public IP and data transfer can incur charges. Check current prices, quota and `Standard_D4s_v5` capacity in the selected subscription before planning.

The following are future deployment steps; they were not run against an Azure account for this delivery. Replace the explicit placeholder values first. The SSH variable contains the **public** key only; its matching private key stays on your computer.

```sh
az login
az account set --subscription "YOUR_SUBSCRIPTION_ID"
# Ensure these providers are registered in that subscription before planning:
az provider register --namespace Microsoft.Compute
az provider register --namespace Microsoft.Network
export ARM_SUBSCRIPTION_ID="YOUR_SUBSCRIPTION_ID"
export TF_VAR_commit="FULL_40_CHARACTER_PUBLISHED_GIT_SHA"
export TF_VAR_operator_cidr="YOUR_CURRENT_PUBLIC_IPV4/32"
export TF_VAR_ssh_public_key="$(cat ~/.ssh/id_ed25519.pub)"
make configure
make azure-init
make azure-validate
make azure-plan
# Review the complete plan and account cost before explicitly running:
make azure-apply
```

| Input | Required/default | Meaning |
|---|---|---|
| `ARM_SUBSCRIPTION_ID` | Required environment value | AzureRM subscription; use the intended authenticated account |
| `TF_VAR_commit` | Required | Published full forty-character hexadecimal Git SHA, not a branch/tag |
| `TF_VAR_operator_cidr` | Required | Current operator public IPv4 `/32`; update it deliberately when your network changes |
| `TF_VAR_ssh_public_key` | Required | Single-line RSA key of at least 2048 bits or Ed25519 public key |
| `TF_VAR_data_gib` | Optional, `100` | Retained data disk, integer from 50 to 500 GiB |

`make azure-plan` saves `.local/azure.tfplan`; `make azure-apply` applies that reviewed Azure plan. Azure, AWS and Docker-local state directories and plan files are separate. State and plan files stay private and ignored. The AzureRM provider disables automatic resource-provider registration; the future operator must register `Microsoft.Compute` and `Microsoft.Network` with appropriate permissions before a real plan. No runtime password belongs in Terraform variables, custom data or shared logs.

## Bootstrap and connect

After apply, print `terraform -chdir=infra/azure output -raw ssh_command` and run that command. If the matching private key is not loaded in your SSH agent, add `-i /path/to/your/private-key` locally; never upload it to the VM or put it into Terraform.

Inside the SSH session:

```sh
sudo cloud-init status --wait
sudo bash /opt/candidate-assessment-release/infra/azure/bootstrap.sh
```

Cloud-init installs prerequisites and checks out the exact public commit at `/opt/candidate-assessment-release`. It does not wait for the separately attached data disk or start the application. The explicit bootstrap waits for Azure LUN 0, preserves an existing filesystem, mounts the data disk at `/srv/candidate-assessment`, puts Docker data on it and checks out that same commit at `/srv/candidate-assessment/app`. A new disk is initialized only after an entire-disk zero-fill check; scanning the default 100 GiB can take many minutes. Existing ext4 data is reused, and partitioned, unknown or unreadable disks are refused. Bootstrap then installs pinned Ollama `0.40.15`, stores its models on the retained disk, pulls `llama3.2:latest` and runs `make up`. Review the bootstrap and vendor installer before executing them. Container/model tags can resolve at deployment time; supply-chain reproducibility is not claimed.

Run `terraform -chdir=infra/azure output -raw forward_command` locally and keep the printed SSH command running. It binds the local tunnel specifically to `127.0.0.1:5188`, forwarding to `127.0.0.1:5188` on the VM. Open `http://localhost:5188`. Read the private `.local/credentials.json` on the VM for the fictional accounts; do not put those passwords in issues, screenshots or learning notes.

## Pause, update and recover

Deallocate the VM through Azure when pausing compute; shutting down only inside the guest is different. Disks and a retained Standard public IP can continue incurring charges. Do not delete a resource group to pause the lab. Take [a private application backup](../../docs/operations.md), record the release commit and resource output IDs, and verify the backup before replacing compute or changing an incompatible schema.

The data disk belongs to the separate `candidate-assessment-lab-data` resource group. Terraform protects both that group and `azurerm_managed_disk.data` with `prevent_destroy`, so a blanket destroy is intentionally refused. These are Terraform guards, not Azure resource locks: an authorized portal/CLI deletion can still destroy data. Never remove only the disk from state and then delete its resource group.

For a reviewed compute-only teardown, retain **both** the data resource group and disk outside Terraform state before removing compute/network. Preserve their IDs and backup first, and import retained resources into the recovery configuration before a later normal apply. Deliberately deleting retained data or its resource group is a separate irreversible action; no lab command does it automatically.

Recover onto a compatible host in the same Azure region by attaching the retained disk, mounting its existing ext4 filesystem and reusing the Docker data, model and application configuration. Never format a disk that already contains a filesystem. Confirm a durable receipt, authenticated recording playback, worker processing, published feedback and backup restoration before treating the recovered environment as usable.

References: [Azure regions](https://learn.microsoft.com/azure/reliability/regions-list), [Dsv5 sizes](https://learn.microsoft.com/azure/virtual-machines/sizes/general-purpose/dsv5-series), [Linux VM SSH access](https://learn.microsoft.com/azure/virtual-machines/linux-vm-connect), [managed disk encryption](https://learn.microsoft.com/azure/virtual-machines/disk-encryption), [resource-group deletion](https://learn.microsoft.com/azure/azure-resource-manager/management/delete-resource-group), [Azure VM states and billing](https://learn.microsoft.com/azure/virtual-machines/states-billing).
