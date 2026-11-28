output "instance_id" {
  description = "Olusturulan EC2 infra instance ID"
  value       = aws_instance.k3s_infra.id
}

output "public_ip" {
  description = "EC2 Public IP (SSH icin)"
  value       = aws_instance.k3s_infra.public_ip
}
