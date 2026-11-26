variable "aws_region" {
  description = "AWS bolgesi (orn: eu-central-1, eu-north-1)"
  type        = string
  default     = "eu-north-1"
}

variable "instance_type" {
  description = "EC2 instance tipi (2 GB RAM amd64 icin t3.small)"
  type        = string
  default     = "t3.small"
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
  description = "SSH erisimi verilecek IP blogu (guvenlik icin kendi IP/32 yazman onerilir)"
  type        = string
  default     = "0.0.0.0/0"
}

variable "node_name" {
  description = "EC2 ve sunucu hostname adi"
  type        = string
  default     = "aws-cp2"
}
