#!/bin/bash
# Migrates all secrets from Azure Key Vault to AWS Secrets Manager.
# Run this once before deploying the ECS service.
#
# Prerequisites:
#   - Azure CLI logged in: az login
#   - AWS CLI configured with credentials for the target account
#
# Usage: ./scripts/create-secrets.sh

set -euo pipefail

VAULT_NAME="GAToolApiKeys"
REGION="${AWS_REGION:-us-east-2}"

SECRETS=(
  "Auth0Issuer"
  "Auth0Audience"
  "FRCApiKey"
  "TBAApiKey"
  "FTCApiKey"
  "CasterstoolApiKey"
  "TOAApiKey"
  "FRCCurrentSeason"
  "FTCCurrentSeason"
  "MailChimpAPIKey"
  "MailchimpAPIURL"
  "MailchimpListID"
  "Auth0AdminClientId"
  "Auth0AdminClientSecret"
  "NewRelicLicenseKey"
)

echo "Migrating ${#SECRETS[@]} secrets: Azure Key Vault ($VAULT_NAME) → AWS Secrets Manager (us-east-2)"
echo "---"

# Verify Azure access
if ! az keyvault secret list --vault-name "$VAULT_NAME" --query "[0].name" -o tsv &>/dev/null; then
  echo "❌ Cannot access Azure Key Vault '$VAULT_NAME'. Run: az login"
  exit 1
fi

for name in "${SECRETS[@]}"; do
  # Fetch from Azure Key Vault
  value=$(az keyvault secret show --vault-name "$VAULT_NAME" --name "$name" --query "value" -o tsv 2>/dev/null)
  if [ -z "$value" ]; then
    echo "  ⚠ $name not found in Key Vault, skipping"
    continue
  fi

  # Push to AWS Secrets Manager
  if aws secretsmanager describe-secret --secret-id "$name" --region "$REGION" &>/dev/null; then
    echo "  ⟳ $name already exists, updating..."
    aws secretsmanager put-secret-value \
      --secret-id "$name" \
      --secret-string "$value" \
      --region "$REGION" \
      --no-cli-pager
  else
    echo "  ✓ Creating $name..."
    aws secretsmanager create-secret \
      --name "$name" \
      --secret-string "$value" \
      --region "$REGION" \
      --no-cli-pager
  fi
done

echo "---"
echo "✅ All secrets migrated. Verify with:"
echo "  aws secretsmanager list-secrets --region $REGION --no-cli-pager"
