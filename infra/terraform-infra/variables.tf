variable "aws_region" {
  description = "AWS bolgesi"
  type        = string
  default     = "eu-north-1"
}

variable "instance_type" {
  description = "EC2 instance tipi (Free Tier 2 vCPU / 4 GB RAM icin c7i-flex.large)"
  type        = string
  default     = "c7i-flex.large"
}

variable "disk_size_gb" {
  description = "Root disk boyutu (GB)"
  type        = number
  default     = 20
}

variable "key_name" {
  description = "AWS EC2 SSH key pair adi"
  type        = string
}

variable "allowed_ssh_cidr" {
  description = "SSH erisimi verilecek IP blogu"
  type        = string
  default     = "0.0.0.0/0"
}

variable "node_name" {
  description = "EC2 ve K3s infra node adi"
  type        = string
  default     = "aws-infra"
}
