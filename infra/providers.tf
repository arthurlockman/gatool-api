provider "onepassword" {}

data "onepassword_item" "keys" {
  vault = "Infrastructure"
  title = "gatool-terraform-keys"
}

provider "azurerm" {
  subscription_id = lookup({ for f in data.onepassword_item.keys.section[0].field : f.label => f.value }, "az-subscription-id")
  features {}
}

provider "cloudflare" {
  api_token = lookup({ for f in data.onepassword_item.keys.section[0].field : f.label => f.value }, "cf-api-token")
}
