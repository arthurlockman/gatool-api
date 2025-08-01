# Scheduled job to update high scores every 15 minutes
resource "azurerm_container_app_job" "update_high_scores" {
  name                         = "update-high-scores-job"
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = var.resource_group_name
  location                     = var.location
  replica_timeout_in_seconds   = 1800 # 30 minutes timeout
  replica_retry_limit          = 1

  schedule_trigger_config {
    cron_expression = "*/15 * * * *" # Every 15 minutes
    parallelism     = 1
    replica_completion_count = 1
  }

  template {
    container {
      name   = "update-high-scores"
      image  = var.image_name
      cpu    = 1.0
      memory = "2Gi"

      # Override the default command to run the high scores script
      command = ["npm", "run", "update-high-scores"]

      env {
        name  = "NEW_RELIC_LICENSE_KEY"
        value = data.azurerm_key_vault_secret.newrelic_license.value
      }
      env {
        name  = "NEW_RELIC_APP_NAME"
        value = var.containerapp_name
      }
      env {
        name  = "NODE_ENV"
        value = "production"
      }
      env {
        name  = "REDIS_HOST"
        value = azapi_resource.redis_cluster.output.properties.hostName
      }
      env {
        name  = "REDIS_PORT"
        value = "10000"
      }
      env {
        name  = "REDIS_PASSWORD"
        value = data.azapi_resource_action.redis_keys.output.primaryKey
      }
      env {
        name  = "REDIS_TLS"
        value = "true"
      }
    }
  }

  identity {
    type = "SystemAssigned"
  }
}

# Grant the job access to Key Vault for secrets
resource "azurerm_role_assignment" "job_keyvault_secrets_user" {
  scope                = data.azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_container_app_job.update_high_scores.identity[0].principal_id
}

# Scheduled job to sync users (runs daily at 2 AM)
resource "azurerm_container_app_job" "sync_users" {
  name                         = "sync-users-job"
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = var.resource_group_name
  location                     = var.location
  replica_timeout_in_seconds   = 3600 # 1 hour timeout
  replica_retry_limit          = 2

  schedule_trigger_config {
    cron_expression = "0 2 * * *" # Daily at 2 AM UTC
    parallelism     = 1
    replica_completion_count = 1
  }

  template {
    container {
      name   = "sync-users"
      image  = var.image_name
      cpu    = 0.25
      memory = "0.5Gi"

      # Override the default command to run the sync users script
      command = ["npm", "run", "sync-users"]

      env {
        name  = "NEW_RELIC_LICENSE_KEY"
        value = data.azurerm_key_vault_secret.newrelic_license.value
      }
      env {
        name  = "NEW_RELIC_APP_NAME"
        value = var.containerapp_name
      }
      env {
        name  = "NODE_ENV"
        value = "production"
      }
      env {
        name  = "REDIS_HOST"
        value = azapi_resource.redis_cluster.output.properties.hostName
      }
      env {
        name  = "REDIS_PORT"
        value = "10000"
      }
      env {
        name  = "REDIS_PASSWORD"
        value = data.azapi_resource_action.redis_keys.output.primaryKey
      }
      env {
        name  = "REDIS_TLS"
        value = "true"
      }
    }
  }

  identity {
    type = "SystemAssigned"
  }
}

# Grant the sync users job access to Key Vault for secrets
resource "azurerm_role_assignment" "sync_users_job_keyvault_secrets_user" {
  scope                = data.azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_container_app_job.sync_users.identity[0].principal_id
}
