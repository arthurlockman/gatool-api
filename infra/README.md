# GATool API Infrastructure

This directory contains the Terraform configuration for the GATool API infrastructure on Azure.

## Structure

The Terraform configuration has been organized into multiple files following best practices:

### Core Files

- **`versions.tf`** - Terraform version constraints and required providers
- **`providers.tf`** - Provider configurations (Azure, Cloudflare, 1Password)
- **`variables.tf`** - Input variable definitions with descriptions and defaults
- **`data.tf`** - Data sources (Key Vault, secrets)

### Resource Files

- **`infrastructure.tf`** - Core Azure infrastructure (Log Analytics, Virtual Network, Subnet)
- **`redis.tf`** - Azure Managed Redis cluster and database configuration
- **`container_app.tf`** - Container App environment and main application
- **`container_jobs.tf`** - Scheduled Container Apps jobs (high scores, user sync)
- **`dns_certificates.tf`** - DNS records and SSL certificate management

## Resources Deployed

### Core Infrastructure

- **Resource Group**: `gatool` (pre-existing)
- **Log Analytics Workspace**: For monitoring and logging
- **Virtual Network**: Isolated network for Container Apps
- **Subnet**: Dedicated subnet for Container Apps

### Redis Cache

- **Azure Managed Redis**: Balanced_B0 SKU (cost-optimized)
- **Redis Database**: TLS-encrypted with access key authentication

### Container Apps

- **Container App Environment**: Managed environment with scale-to-zero capability
- **Main Application**: API service with auto-scaling (min: 0, max: auto)
- **High Scores Job**: Scheduled every 15 minutes
- **User Sync Job**: Scheduled daily at 2 AM UTC

### DNS & Certificates

- **Custom Domain**: api.gatool.org
- **Cloudflare DNS**: Automated DNS record management
- **Azure Managed Certificate**: Automated SSL certificate provisioning

## Configuration

### Prerequisites

1. **1Password CLI**: For secure credential management
2. **Azure CLI**: For managed certificate provisioning
3. **Terraform**: Version >= 1.3.0

### Required Secrets (1Password)

The configuration expects these secrets in 1Password vault "Infrastructure", item "gatool-terraform-keys":

- `az-subscription-id`: Azure subscription ID
- `cf-api-token`: Cloudflare API token
- `cf-zone-id`: Cloudflare zone ID for gatool.org

### Azure Key Vault Secrets

- `NewRelicLicenseKey`: New Relic license key for monitoring

## Environment Variables

The Container Apps are configured with these environment variables:

- `NEW_RELIC_LICENSE_KEY`: From Azure Key Vault
- `NEW_RELIC_APP_NAME`: Application name for monitoring
- `NODE_ENV`: Set to "production"
- `REDIS_HOST`: Azure Managed Redis hostname
- `REDIS_PORT`: Redis port (10000 for TLS)
- `REDIS_PASSWORD`: From Redis access keys
- `REDIS_TLS`: Enabled for secure connections
