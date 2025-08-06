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
    max_replicas = 5

    http_scale_rule {
      name                = "http-requests"
      concurrent_requests = 20
    }

    container {
      name   = var.containerapp_name
      image  = var.image_name
      cpu    = 1.0
      memory = "2Gi"

      liveness_probe {
        transport = "HTTP"
        port = 8080
        initial_delay = 15
        interval_seconds = 60
        timeout = 10
        path = "/livecheck"
        failure_count_threshold = 3
      }

      readiness_probe {
        transport = "HTTP"
        port = 8080
        initial_delay = 20
        interval_seconds = 10
        timeout = 10
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
        name  = "Redis__Host"
        value = azapi_resource.redis_cluster.output.properties.hostName
      }
      env {
        name  = "Redis__Port"
        value = "10000"
      }
      env {
        name  = "Redis__Password"
        value = data.azapi_resource_action.redis_keys.output.primaryKey
      }
      env {
        name  = "Redis__UseTls"
        value = "true"
      }
    }
  }

  ingress {
    external_enabled = true
    target_port      = 8080
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

# Configure diagnostic settings for Container Apps Environment to enable logging
resource "azurerm_monitor_diagnostic_setting" "containerapp_env" {
  name                       = "${var.containerapp_env_name}-diagnostic-settings"
  target_resource_id         = azurerm_container_app_environment.main.id
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id

  enabled_log {
    category_group = "allLogs"
  }
}
