# Azure Managed Redis (using azapi provider)
resource "azapi_resource" "redis_cluster" {
  type      = "Microsoft.Cache/redisEnterprise@2024-09-01-preview"
  name      = "gatool-redis"
  location  = var.location
  parent_id = "/subscriptions/${lookup({ for f in data.onepassword_item.keys.section[0].field : f.label => f.value }, "az-subscription-id")}/resourceGroups/${var.resource_group_name}"

  body = {
    properties = {
      minimumTlsVersion = "1.2"
      highAvailability  = "Disabled"  # Set to "Enabled" for HA if needed
    }
    sku = {
      name = "Balanced_B0"  # Cheapest SKU - perfect for development/small workloads
    }
  }

  tags = {
    Application = "gatool"
    Environment = "production"
  }

  schema_validation_enabled = false
}

# Azure Managed Redis Database
resource "azapi_resource" "redis_database" {
  type      = "Microsoft.Cache/redisEnterprise/databases@2024-09-01-preview"
  name      = "default"
  parent_id = azapi_resource.redis_cluster.id

  body = {
    properties = {
      accessKeysAuthentication = "Enabled"
      clientProtocol          = "Encrypted"
      port                    = 10000
      clusteringPolicy        = "OSSCluster"
      evictionPolicy          = "AllKeysLRU"
      persistence = {
        aofEnabled = false
        rdbEnabled = false
      }
    }
  }

  depends_on = [azapi_resource.redis_cluster]
  schema_validation_enabled = false
}

# Get the access keys for the Redis database
data "azapi_resource_action" "redis_keys" {
  type        = "Microsoft.Cache/redisEnterprise/databases@2024-09-01-preview"
  resource_id = azapi_resource.redis_database.id
  action      = "listKeys"
  depends_on  = [azapi_resource.redis_database]
  response_export_values = ["*"]
}
