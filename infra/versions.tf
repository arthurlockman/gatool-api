terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = ">=3.0.0"
    }
    cloudflare = {
      source  = "cloudflare/cloudflare"
      version = ">= 4.0.0"
    }
    onepassword = {
      source  = "1password/onepassword"
      version = ">= 1.5.0"
    }
    null = {
      source  = "hashicorp/null"
      version = ">= 3.0.0"
    }
    azapi = {
      source  = "azure/azapi"
      version = ">= 2.0.0"
    }
  }
  required_version = ">= 1.3.0"
  backend "azurerm" {
    resource_group_name  = "gatool"
    storage_account_name = "gatooltfstate"
    container_name       = "tfstate"
    key                  = "infra.terraform.tfstate"
  }
}
