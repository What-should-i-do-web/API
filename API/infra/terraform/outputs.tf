output "api_url" {
  description = "Public API Gateway URL"
  value       = aws_apigatewayv2_api.http_api.api_endpoint
}

output "ecr_repo_url" {
  description = "ECR Repository URL"
  value       = aws_ecr_repository.api_repo.repository_url
}

output "lambda_name" {
  description = "Lambda function name"
  value       = aws_lambda_function.api.function_name
}

output "gha_role_arn" {
  description = "GitHub Actions IAM Role ARN (for OIDC)"
  value       = aws_iam_role.gha_role.arn
}
