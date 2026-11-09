terraform {
  required_version = ">= 1.6, < 2.0"
}

variable "repository_root" {
  type        = string
  description = "Absolute path to this checkout; local Docker only."
  default     = "../.."
}

locals {
  repository_root = abspath(var.repository_root)
}

resource "terraform_data" "local_lab" {
  input = {
    repository_root = local.repository_root
  }

  provisioner "local-exec" {
    working_dir = self.input.repository_root
    command     = "python3 scripts/lab.py up"
  }

  provisioner "local-exec" {
    when        = destroy
    working_dir = self.input.repository_root
    command     = "python3 scripts/lab.py down"
  }
}

output "application_url" {
  value       = "http://localhost:5188"
  description = "Docker-local application; Terraform destroy preserves data volumes."
}
