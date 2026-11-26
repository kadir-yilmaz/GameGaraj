output "instance_id" {
  description = "Olusturulan EC2 instance ID"
  value       = aws_instance.k3s_cp.id
}

output "public_ip" {
  description = "EC2 Public IP (SSH icin)"
  value       = aws_instance.k3s_cp.public_ip
}
