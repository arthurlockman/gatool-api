resource "azurerm_log_analytics_workspace" "main" {
  name                = "${var.containerapp_env_name}-logs"
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = "PerGB2018"
  retention_in_days   = 30
}

resource "azurerm_virtual_network" "gatool" {
  name                = "GATool-vnet"
  location            = var.location
  resource_group_name = var.resource_group_name
  address_space       = ["10.1.0.0/16", "2404:f800:8000:122::/63"]

  tags = {
    Application = "gatool"
    Environment = "production"
  }
}

resource "azurerm_subnet" "containerapps" {
  name                 = "gatool-api-containerapps"
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.gatool.name
  address_prefixes     = ["10.1.2.0/23"]
}
