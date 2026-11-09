# Docker-local Terraform profile

This profile deliberately has no external provider. Terraform manages one `terraform_data` lifecycle whose provisioners call the same tested Make/Python Compose entry point as local operation.

```sh
make configure
make deploy-init
make deploy-validate
make deploy-plan
make deploy-apply
```

Apply starts the local Docker lab; it can download container images and the checksum-verified Whisper base model. `make deploy-down` stops containers and preserves volumes. Run updates through `make up`; this minimal profile does not watch every source file or claim drift detection inside Compose. `.local/` and Terraform state are private and ignored.

The profile has no cloud claim. See [../cloud/README.md](../cloud/README.md).
