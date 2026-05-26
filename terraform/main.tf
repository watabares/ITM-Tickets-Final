# ============================================================
# ITM-Tickets Global "The World Tour 2026"
# Infraestructura como Código - Terraform
# ============================================================
# Este archivo recrea toda la infraestructura del sistema
# distribuido en AWS EKS (Kubernetes gestionado).
# ============================================================

terraform {
  required_version = ">= 1.5.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.25"
    }
  }
}

# ============================================================
# VARIABLES
# ============================================================

variable "region" {
  description = "Región AWS para el cluster"
  type        = string
  default     = "us-east-1"
}

variable "cluster_name" {
  description = "Nombre del cluster EKS"
  type        = string
  default     = "itm-tickets-cluster"
}

variable "node_instance_type" {
  description = "Tipo de instancia para los nodos"
  type        = string
  default     = "t3.medium"
}

variable "min_nodes" {
  description = "Mínimo de nodos en el cluster"
  type        = number
  default     = 2
}

variable "max_nodes" {
  description = "Máximo de nodos en el cluster (auto-escalado)"
  type        = number
  default     = 6
}

variable "desired_nodes" {
  description = "Nodos deseados inicialmente"
  type        = number
  default     = 3
}

# ============================================================
# PROVIDER
# ============================================================

provider "aws" {
  region = var.region
}

# ============================================================
# NETWORKING (VPC + Subnets)
# ============================================================

resource "aws_vpc" "main" {
  cidr_block           = "10.0.0.0/16"
  enable_dns_hostnames = true
  enable_dns_support   = true

  tags = {
    Name    = "${var.cluster_name}-vpc"
    Project = "ITM-Tickets"
    Course  = "Programacion Distribuida"
  }
}

resource "aws_subnet" "public_a" {
  vpc_id                  = aws_vpc.main.id
  cidr_block              = "10.0.1.0/24"
  availability_zone       = "${var.region}a"
  map_public_ip_on_launch = true

  tags = {
    Name                                        = "${var.cluster_name}-public-a"
    "kubernetes.io/role/elb"                     = "1"
    "kubernetes.io/cluster/${var.cluster_name}"  = "shared"
  }
}

resource "aws_subnet" "public_b" {
  vpc_id                  = aws_vpc.main.id
  cidr_block              = "10.0.2.0/24"
  availability_zone       = "${var.region}b"
  map_public_ip_on_launch = true

  tags = {
    Name                                        = "${var.cluster_name}-public-b"
    "kubernetes.io/role/elb"                     = "1"
    "kubernetes.io/cluster/${var.cluster_name}"  = "shared"
  }
}

resource "aws_internet_gateway" "main" {
  vpc_id = aws_vpc.main.id

  tags = {
    Name = "${var.cluster_name}-igw"
  }
}

resource "aws_route_table" "public" {
  vpc_id = aws_vpc.main.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.main.id
  }

  tags = {
    Name = "${var.cluster_name}-public-rt"
  }
}

resource "aws_route_table_association" "public_a" {
  subnet_id      = aws_subnet.public_a.id
  route_table_id = aws_route_table.public.id
}

resource "aws_route_table_association" "public_b" {
  subnet_id      = aws_subnet.public_b.id
  route_table_id = aws_route_table.public.id
}

# ============================================================
# IAM ROLES (EKS)
# ============================================================

resource "aws_iam_role" "eks_cluster" {
  name = "${var.cluster_name}-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action = "sts:AssumeRole"
      Effect = "Allow"
      Principal = {
        Service = "eks.amazonaws.com"
      }
    }]
  })
}

resource "aws_iam_role_policy_attachment" "eks_cluster_policy" {
  policy_arn = "arn:aws:iam::aws:policy/AmazonEKSClusterPolicy"
  role       = aws_iam_role.eks_cluster.name
}

resource "aws_iam_role" "eks_nodes" {
  name = "${var.cluster_name}-node-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action = "sts:AssumeRole"
      Effect = "Allow"
      Principal = {
        Service = "ec2.amazonaws.com"
      }
    }]
  })
}

resource "aws_iam_role_policy_attachment" "eks_worker_node" {
  policy_arn = "arn:aws:iam::aws:policy/AmazonEKSWorkerNodePolicy"
  role       = aws_iam_role.eks_nodes.name
}

resource "aws_iam_role_policy_attachment" "eks_cni" {
  policy_arn = "arn:aws:iam::aws:policy/AmazonEKS_CNI_Policy"
  role       = aws_iam_role.eks_nodes.name
}

resource "aws_iam_role_policy_attachment" "eks_ecr" {
  policy_arn = "arn:aws:iam::aws:policy/AmazonEC2ContainerRegistryReadOnly"
  role       = aws_iam_role.eks_nodes.name
}

# ============================================================
# EKS CLUSTER
# ============================================================

resource "aws_eks_cluster" "main" {
  name     = var.cluster_name
  role_arn = aws_iam_role.eks_cluster.arn
  version  = "1.29"

  vpc_config {
    subnet_ids = [
      aws_subnet.public_a.id,
      aws_subnet.public_b.id
    ]
  }

  depends_on = [
    aws_iam_role_policy_attachment.eks_cluster_policy
  ]

  tags = {
    Project = "ITM-Tickets"
    Env     = "production"
  }
}

# ============================================================
# EKS NODE GROUP (Auto-escalado)
# ============================================================

resource "aws_eks_node_group" "workers" {
  cluster_name    = aws_eks_cluster.main.name
  node_group_name = "${var.cluster_name}-workers"
  node_role_arn   = aws_iam_role.eks_nodes.arn
  subnet_ids      = [aws_subnet.public_a.id, aws_subnet.public_b.id]

  instance_types = [var.node_instance_type]

  scaling_config {
    desired_size = var.desired_nodes
    max_size     = var.max_nodes
    min_size     = var.min_nodes
  }

  update_config {
    max_unavailable = 1
  }

  depends_on = [
    aws_iam_role_policy_attachment.eks_worker_node,
    aws_iam_role_policy_attachment.eks_cni,
    aws_iam_role_policy_attachment.eks_ecr,
  ]

  tags = {
    Project = "ITM-Tickets"
    Role    = "worker"
  }
}

# ============================================================
# OUTPUTS
# ============================================================

output "cluster_endpoint" {
  description = "Endpoint del cluster EKS"
  value       = aws_eks_cluster.main.endpoint
}

output "cluster_name" {
  description = "Nombre del cluster"
  value       = aws_eks_cluster.main.name
}

output "cluster_security_group_id" {
  description = "Security Group del cluster"
  value       = aws_eks_cluster.main.vpc_config[0].cluster_security_group_id
}

output "node_group_status" {
  description = "Estado del Node Group"
  value       = aws_eks_node_group.workers.status
}

output "kubectl_config_command" {
  description = "Comando para configurar kubectl"
  value       = "aws eks update-kubeconfig --region ${var.region} --name ${aws_eks_cluster.main.name}"
}
