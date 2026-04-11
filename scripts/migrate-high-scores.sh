#!/bin/bash
# Copies high scores from Azure Blob Storage to AWS S3.
#
# Prerequisites:
#   - Azure CLI logged in (az login)
#   - AWS CLI configured (e.g. --profile gatool)
#
# Usage:
#   AZURE_STORAGE_ACCOUNT=<account> ./scripts/migrate-high-scores.sh

set -euo pipefail

AZURE_ACCOUNT="${AZURE_STORAGE_ACCOUNT:?Set AZURE_STORAGE_ACCOUNT}"
REGION="${AWS_REGION:-us-east-2}"
AWS_PROFILE="${AWS_PROFILE:-gatool}"
CONTAINER="gatool-high-scores"

echo "=== Migrating: $CONTAINER ==="

blobs=$(az storage blob list \
  --account-name "$AZURE_ACCOUNT" \
  --container-name "$CONTAINER" \
  --query "[].name" \
  --output tsv 2>/dev/null)

if [ -z "$blobs" ]; then
  echo "  (empty container, nothing to migrate)"
  exit 0
fi

count=0
errors=0

while IFS= read -r blob; do
  tmpfile=$(mktemp)
  if az storage blob download \
    --account-name "$AZURE_ACCOUNT" \
    --container-name "$CONTAINER" \
    --name "$blob" \
    --file "$tmpfile" \
    --no-progress \
    --output none 2>/dev/null; then

    if aws s3 cp "$tmpfile" "s3://${CONTAINER}/${blob}" \
      --region "$REGION" \
      --profile "$AWS_PROFILE" \
      --content-type "application/json" \
      --quiet 2>/dev/null; then
      count=$((count + 1))
    else
      echo "  ✗ Failed to upload: $blob"
      errors=$((errors + 1))
    fi
  else
    echo "  ✗ Failed to download: $blob"
    errors=$((errors + 1))
  fi

  rm -f "$tmpfile"
done <<< "$blobs"

echo "  ✓ Migrated $count blobs ($errors errors)"

if [ "$errors" -gt 0 ]; then
  echo "⚠ Some blobs failed. Re-run to retry."
  exit 1
fi
