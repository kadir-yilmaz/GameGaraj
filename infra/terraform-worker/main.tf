terraform {
  required_version = ">= 1.5.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

provider "aws" {
  region = var.aws_region
}

# En güncel Ubuntu 24.04 LTS (amd64, gp3) imajını otomatik seçer
data "aws_ami" "ubuntu" {
  most_recent = true
  owners      = ["099720109477"] # Canonical

  filter {
    name   = "name"
    values = ["ubuntu/images/hvm-ssd-gp3/ubuntu-noble-24.04-amd64-server-*"]
  }

  filter {
    name   = "virtualization-type"
    values = ["hvm"]
  }
}

resource "aws_security_group" "k3s_worker_sg" {
  name        = "${var.node_name}-sg"
  description = "SSH access and outbound traffic for Tailscale + K3s worker"

  ingress {
    description = "SSH"
    from_port   = 22
    to_port     = 22
    protocol    = "tcp"
    cidr_blocks = [var.allowed_ssh_cidr]
  }

  egress {
    description = "All outbound"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Name = "${var.node_name}-sg"
  }
}

resource "aws_instance" "k3s_worker" {
  ami                    = data.aws_ami.ubuntu.id
  instance_type          = var.instance_type
  key_name               = var.key_name
  vpc_security_group_ids = [aws_security_group.k3s_worker_sg.id]

  root_block_device {
    volume_size = var.disk_size_gb
    volume_type = "gp3"
  }

  user_data = templatefile("${path.module}/cloud-init.sh.tftpl", {
    node_name = var.node_name
  })

  tags = {
    Name = var.node_name
    Role = "k3s-worker"
  }
}
