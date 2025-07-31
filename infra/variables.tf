variable "resource_group_name" {
  description = "The name of the resource group"
  type        = string
  default     = "gatool"
}

variable "location" {
  description = "The Azure region for resources"
  type        = string
  default     = "eastus"
}

variable "containerapp_env_name" {
  description = "The name of the Container App environment"
  type        = string
  default     = "gatool-env"
}

variable "containerapp_name" {
  description = "The name of the Container App"
  type        = string
  default     = "gatool-api"
}

variable "key_vault_name" {
  description = "The name of the Azure Key Vault"
  type        = string
  default     = "GAToolAPIKeys"
}

variable "image_name" {
  description = "The container image to deploy"
  type        = string
  default     = "ghcr.io/arthurlockman/gatool-api:latest"
}
