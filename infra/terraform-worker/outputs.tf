output "instance_id" {
  description = "Olusturulan EC2 worker instance ID"
  value       = aws_instance.k3s_worker.id
}

output "public_ip" {
  description = "EC2 Public IP (SSH icin)"
  value       = aws_instance.k3s_worker.public_ip
}
