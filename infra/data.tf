data "azurerm_key_vault" "main" {
  name                = var.key_vault_name
  resource_group_name = var.resource_group_name
}

data "azurerm_key_vault_secret" "newrelic_license" {
  name         = "NewRelicLicenseKey"
  key_vault_id = data.azurerm_key_vault.main.id
}
