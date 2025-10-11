variable "project" {
  description = "Project name prefix for AWS resources"
  type        = string
}

variable "aws_region" {
  description = "AWS region for deployment"
  type        = string
  default     = "eu-central-1"
}

variable "aws_account_id" {
  description = "AWS account ID (12-digit number)"
  type        = string
}

variable "github_owner" {
  description = "GitHub org or username"
  type        = string
}

variable "github_repo" {
  description = "GitHub repo name (backend API repo)"
  type        = string
}
