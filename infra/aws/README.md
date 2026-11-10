# Future AWS deployment: Singapore

This optional single-host profile runs the existing Compose application on one ARM64 EC2 `t4g.xlarge` in `ap-southeast-1`. It creates a VPC/subnet, public egress, an instance profile for Systems Manager, an encrypted 24 GiB root disk and a protected encrypted 100 GiB data disk. There are **no security-group ingress rules**; use SSM port forwarding. The app, PostgreSQL and Kafka bind only to host loopback. Ollama is host-local infrastructure reachable by the app's Docker network.

This is a runnable deployment path, with Terraform validation evidence. It has not been applied or tested on AWS. There is one failure domain and no managed database/broker, public endpoint, TLS termination, high availability or cloud recovery claim.

## Review and deploy later

Prerequisites: an authenticated AWS CLI profile with the necessary EC2/VPC/IAM/SSM permissions, Terraform, AWS Session Manager plugin, a published repository commit and an account cost estimate. EC2, EBS, public IPv4 and data transfer can incur charges; T4g uses **standard** CPU credits, so CPU is throttled after credits are exhausted rather than silently billing surplus credits. CPU-only transcription/scoring will be slower than a GPU host.

```sh
make configure
make aws-init
make aws-validate
export TF_VAR_commit=<full-40-character-published-commit>
make aws-plan
# Review all resources and cost before explicitly running:
make aws-apply
```

The provider Region is fixed to Singapore. AMI resolution uses Amazon's public SSM parameter for current AL2023 ARM64. Terraform's numeric launch-template version is attached to the instance. User data contains no passwords; it installs Docker/Compose, mounts the selected EBS volume, puts Docker data on it and checks out the exact public source commit. Changing the commit requires reviewing the instance replacement plan. The data volume is independent of the root disk.

Get the SSM command with `terraform -chdir=infra/aws output -raw session_command`. After cloud-init finishes, use that SSM session:

```sh
sudo bash /srv/candidate-assessment/app/infra/aws/bootstrap.sh
```

Bootstrap downloads the official Ollama installer, installs pinned runtime `0.40.15`, stores models on EBS, pulls `llama3.2:latest` and runs `make up`. Review the installer before this explicit step. Container tags and model tags resolve during deployment; this profile does not claim fully reproducible supply-chain pinning. Open the private `.local/credentials.json` on the host to use the fictional accounts; never copy it into a Git commit, issue, screenshot or shared log.

Then run the printed `forward_command` locally:

```sh
terraform -chdir=infra/aws output -raw forward_command
# Run that AWS CLI command in a terminal and leave it running.
```

Open `http://localhost:5188`. Localhost preserves browser microphone access through the SSM tunnel. No public assessment endpoint is exposed. SSM operator permissions are separate from the instance role; follow account-level session logging and CloudTrail policy. The profile does not change those account settings.

## Pause, update and recover

Stop/start the instance through EC2 or the CLI when pausing the environment. Root and data EBS remain chargeable while stopped; an assigned public IPv4 can change. Docker and Ollama use systemd and the data mount. Review the exact release commit, take [a private backup](../../docs/operations.md), stop the app, check out the target commit and run `make up` from the EBS checkout. Do not assume a binary rollback can reverse a schema migration.

`aws_ebs_volume.data` has `prevent_destroy = true`. A blanket Terraform destroy intentionally refuses while that protected volume is managed. Before intentional teardown, back it up and record its output ID; explicitly retaining the volume with `terraform state rm aws_ebs_volume.data` lets a subsequent reviewed destroy remove compute/network while leaving the disk billed and available for recovery. Deleting that retained disk is a separate deliberate data-destruction action. No cleanup script does it for you.

Recover onto a compatible host by attaching the retained disk in the same Availability Zone, mounting its existing XFS filesystem and using its Docker data/configuration. Never format a disk that already has a filesystem. Verify database receipt, private media access, worker recovery and backup restoration before calling the recovered environment usable.

References: [AWS SSM session and port forwarding](https://docs.aws.amazon.com/systems-manager/latest/userguide/session-manager-working-with-sessions-start.html), [AL2023 SSM AMI parameters](https://docs.aws.amazon.com/linux/al2023/ug/ec2.html), [Ollama Linux ARM64 installation](https://docs.ollama.com/linux), [Docker Compose standalone plugin installation](https://docs.docker.com/compose/install/linux/).
