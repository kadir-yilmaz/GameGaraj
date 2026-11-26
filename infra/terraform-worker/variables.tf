variable "aws_region" {
  description = "AWS bolgesi"
  type        = string
  default     = "eu-north-1"
}

variable "instance_type" {
  description = "EC2 instance tipi (8 GB RAM icin m7i-flex.large veya t3.large)"
  type        = string
  default     = "m7i-flex.large"
}

variable "disk_size_gb" {
  description = "Root disk boyutu (GB)"
  type        = number
  default     = 15
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
  description = "EC2 ve K3s worker node adi"
  type        = string
  default     = "aws-wk2"
}
