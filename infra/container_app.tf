resource "azurerm_container_app_environment" "main" {
  name                       = var.containerapp_env_name
  location                   = var.location
  resource_group_name        = var.resource_group_name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  infrastructure_subnet_id   = azurerm_subnet.containerapps.id
}

resource "azurerm_container_app" "main" {
  name                         = var.containerapp_name
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = var.resource_group_name
  revision_mode                = "Single"

  template {
    min_replicas = 0
    container {
      name   = var.containerapp_name
      image  = var.image_name
      cpu    = 0.5
      memory = "1Gi"

      liveness_probe {
        transport = "HTTP"
        port = 3001
        initial_delay = 5
        path = "/livecheck"
      }

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

  ingress {
    external_enabled = true
    target_port      = 3001
    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }

  identity {
    type = "SystemAssigned"
  }
}

resource "azurerm_role_assignment" "containerapp_keyvault_secrets_user" {
  scope                = data.azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_container_app.main.identity[0].principal_id
}
