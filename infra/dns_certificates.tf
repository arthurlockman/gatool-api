resource "cloudflare_dns_record" "asuid_api" {
  zone_id = lookup({ for f in data.onepassword_item.keys.section[0].field : f.label => f.value }, "cf-zone-id")
  name    = "asuid.api.gatool.org"
  type    = "TXT"
  content = azurerm_container_app.main.custom_domain_verification_id
  ttl     = 300
  proxied = false
}

resource "cloudflare_dns_record" "api_gatool_org_cname" {
  zone_id = lookup({ for f in data.onepassword_item.keys.section[0].field : f.label => f.value }, "cf-zone-id")
  name    = "api.gatool.org"
  type    = "CNAME"
  content = azurerm_container_app.main.ingress[0].fqdn
  ttl     = 300
  proxied = false
}

resource "azurerm_container_app_custom_domain" "api_gatool_org" {
  name             = "api.gatool.org"
  container_app_id = azurerm_container_app.main.id

  # These will be populated automatically by Azure when the managed certificate is created
  lifecycle {
    ignore_changes = [
      certificate_binding_type,
      container_app_environment_certificate_id
    ]
  }

  depends_on = [
    cloudflare_dns_record.asuid_api,
    cloudflare_dns_record.api_gatool_org_cname
  ]
}

# Request managed certificate using Azure CLI
resource "null_resource" "request_managed_certificate" {
  provisioner "local-exec" {
    command = <<-EOT
      # Wait for DNS propagation
      echo "Waiting for DNS propagation..."
      sleep 30
      # Request managed certificate for the custom domain
      echo "Requesting managed certificate for api.gatool.org..."
      az containerapp hostname bind \
        --resource-group ${var.resource_group_name} \
        --name ${var.containerapp_name} \
        --hostname api.gatool.org \
        --environment ${azurerm_container_app_environment.main.name} \
        --validation-method HTTP
      echo "Managed certificate request completed!"
    EOT
  }

  triggers = {
    # Use a stable trigger that won't change unless we actually want to recreate the certificate
    hostname = "api.gatool.org"
    container_app = var.containerapp_name
  }

  depends_on = [
    azurerm_container_app_custom_domain.api_gatool_org,
    cloudflare_dns_record.asuid_api,
    cloudflare_dns_record.api_gatool_org_cname
  ]
}
