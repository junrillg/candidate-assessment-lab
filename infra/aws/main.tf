terraform {
  required_version = ">= 1.6, < 2.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }
}

provider "aws" {
  region = "ap-southeast-1"
  default_tags {
    tags = {
      Project   = "candidate-assessment-lab"
      Purpose   = "learning"
      ManagedBy = "terraform"
    }
  }
}

variable "commit" {
  type        = string
  description = "Published full 40-character Git commit to deploy; never a branch or mutable tag."
  validation {
    condition     = can(regex("^[0-9a-f]{40}$", var.commit))
    error_message = "Set commit to an existing published full Git SHA."
  }
}

variable "data_gib" {
  type        = number
  default     = 100
  description = "Encrypted persistent Docker, recordings, model and configuration storage."
  validation {
    condition     = var.data_gib >= 50 && var.data_gib <= 500 && floor(var.data_gib) == var.data_gib
    error_message = "data_gib must be an integer from 50 to 500."
  }
}

data "aws_availability_zones" "available" {
  state = "available"
}

data "aws_ssm_parameter" "al2023_arm64" {
  name = "/aws/service/ami-amazon-linux-latest/al2023-ami-kernel-default-arm64"
}

resource "aws_vpc" "lab" {
  cidr_block           = "10.74.0.0/24"
  enable_dns_support   = true
  enable_dns_hostnames = true
  tags                 = { Name = "candidate-assessment-lab" }
}

resource "aws_subnet" "lab" {
  vpc_id                  = aws_vpc.lab.id
  cidr_block              = "10.74.0.0/26"
  availability_zone       = data.aws_availability_zones.available.names[0]
  map_public_ip_on_launch = true
}

resource "aws_internet_gateway" "egress" {
  vpc_id = aws_vpc.lab.id
}

resource "aws_route_table" "egress" {
  vpc_id = aws_vpc.lab.id
  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.egress.id
  }
}

resource "aws_route_table_association" "lab" {
  subnet_id      = aws_subnet.lab.id
  route_table_id = aws_route_table.egress.id
}

resource "aws_security_group" "lab" {
  name_prefix = "candidate-assessment-"
  description = "No inbound access. Outbound package/model downloads and SSM only."
  vpc_id      = aws_vpc.lab.id
  egress {
    description = "HTTPS/HTTP downloads, DNS and AWS managed-node connectivity"
    protocol    = "-1"
    from_port   = 0
    to_port     = 0
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_iam_role" "ssm" {
  name_prefix = "candidate-assessment-ssm-"
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action    = "sts:AssumeRole"
      Effect    = "Allow"
      Principal = { Service = "ec2.amazonaws.com" }
    }]
  })
}

resource "aws_iam_role_policy_attachment" "ssm" {
  role       = aws_iam_role.ssm.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore"
}

resource "aws_iam_instance_profile" "lab" {
  name_prefix = "candidate-assessment-"
  role        = aws_iam_role.ssm.name
}

resource "aws_ebs_volume" "data" {
  availability_zone = aws_subnet.lab.availability_zone
  size              = var.data_gib
  type              = "gp3"
  encrypted         = true
  tags              = { Name = "candidate-assessment-lab-retained-data" }
  lifecycle {
    prevent_destroy = true
  }
}

resource "aws_launch_template" "lab" {
  name_prefix   = "candidate-assessment-"
  image_id      = data.aws_ssm_parameter.al2023_arm64.value
  instance_type = "t4g.xlarge"
  iam_instance_profile {
    name = aws_iam_instance_profile.lab.name
  }
  vpc_security_group_ids = [aws_security_group.lab.id]
  metadata_options {
    http_endpoint               = "enabled"
    http_tokens                 = "required"
    http_put_response_hop_limit = 2
  }
  credit_specification {
    cpu_credits = "standard"
  }
  block_device_mappings {
    device_name = "/dev/xvda"
    ebs {
      volume_size           = 24
      volume_type           = "gp3"
      encrypted             = true
      delete_on_termination = true
    }
  }
  user_data = base64encode(templatefile("${path.module}/userdata.sh.tftpl", {
    commit    = var.commit
    volume_id = aws_ebs_volume.data.id
  }))
  tag_specifications {
    resource_type = "instance"
    tags          = { Name = "candidate-assessment-lab" }
  }
}

resource "aws_instance" "lab" {
  subnet_id = aws_subnet.lab.id
  launch_template {
    id      = aws_launch_template.lab.id
    version = tostring(aws_launch_template.lab.latest_version)
  }
  depends_on = [aws_iam_role_policy_attachment.ssm, aws_route_table_association.lab]
}

resource "aws_volume_attachment" "data" {
  device_name = "/dev/sdf"
  volume_id   = aws_ebs_volume.data.id
  instance_id = aws_instance.lab.id
}

output "instance_id" {
  value = aws_instance.lab.id
}

output "retained_data_volume_id" {
  value       = aws_ebs_volume.data.id
  description = "Retained and prevent_destroy protected; it keeps billing when compute is stopped."
}

output "session_command" {
  value = "aws ssm start-session --region ap-southeast-1 --target ${aws_instance.lab.id}"
}

output "forward_command" {
  value = "aws ssm start-session --region ap-southeast-1 --target ${aws_instance.lab.id} --document-name AWS-StartPortForwardingSession --parameters '{\"portNumber\":[\"5188\"],\"localPortNumber\":[\"5188\"]}'"
}
