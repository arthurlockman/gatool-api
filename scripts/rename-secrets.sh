#!/bin/bash
# Renames secrets from gatool/<name> to <name> in AWS Secrets Manager.
# Copies the value, creates the new secret, then deletes the old one.
#
# Usage: ./scripts/rename-secrets.sh

set -euo pipefail

REGION="${AWS_REGION:-us-east-2}"
PROFILE="${AWS_PROFILE:-gatool}"

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

echo "Renaming ${#SECRETS[@]} secrets: gatool/<name> → <name>"
echo "Region: $REGION | Profile: $PROFILE"
echo "---"

for name in "${SECRETS[@]}"; do
  old_name="gatool/$name"

  # Get the value from the old secret
  value=$(aws secretsmanager get-secret-value \
    --secret-id "$old_name" \
    --region "$REGION" \
    --profile "$PROFILE" \
    --query "SecretString" \
    --output text 2>/dev/null) || true

  if [ -z "$value" ]; then
    echo "  ⚠ $old_name not found, skipping"
    continue
  fi

  # Create new secret without prefix
  if aws secretsmanager describe-secret --secret-id "$name" --region "$REGION" --profile "$PROFILE" &>/dev/null; then
    echo "  ⟳ $name already exists, updating value..."
    aws secretsmanager put-secret-value \
      --secret-id "$name" \
      --secret-string "$value" \
      --region "$REGION" \
      --profile "$PROFILE" \
      --no-cli-pager > /dev/null
  else
    echo "  ✓ Creating $name..."
    aws secretsmanager create-secret \
      --name "$name" \
      --secret-string "$value" \
      --region "$REGION" \
      --profile "$PROFILE" \
      --no-cli-pager > /dev/null
  fi

  # Delete old secret
  echo "  🗑 Deleting $old_name..."
  aws secretsmanager delete-secret \
    --secret-id "$old_name" \
    --force-delete-without-recovery \
    --region "$REGION" \
    --profile "$PROFILE" \
    --no-cli-pager > /dev/null

done

echo "---"
echo "✅ Done. Verify with:"
echo "  aws secretsmanager list-secrets --region $REGION --profile $PROFILE --no-cli-pager"
