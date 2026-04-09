#!/bin/bash
# Migrates all data from Azure Blob Storage to AWS S3.
#
# Prerequisites:
#   - Azure CLI logged in (az login)
#   - AWS CLI configured with appropriate credentials
#   - Both Azure and AWS have network access
#
# Usage:
#   AZURE_STORAGE_ACCOUNT=<account> AWS_REGION=us-east-1 ./scripts/migrate-data.sh
#
# The script copies all blobs from 4 Azure containers to matching S3 buckets,
# preserving key names exactly.

set -euo pipefail

AZURE_ACCOUNT="${AZURE_STORAGE_ACCOUNT:?Set AZURE_STORAGE_ACCOUNT}"
REGION="${AWS_REGION:-us-east-2}"

CONTAINERS=(
  "gatool-high-scores"
  "gatool-team-updates"
  "gatool-team-updates-history"
  "gatool-user-preferences"
)

TOTAL_BLOBS=0
TOTAL_ERRORS=0

for container in "${CONTAINERS[@]}"; do
  echo "=== Migrating: $container ==="

  # List all blobs in the Azure container
  blobs=$(az storage blob list \
    --account-name "$AZURE_ACCOUNT" \
    --container-name "$container" \
    --query "[].name" \
    --output tsv 2>/dev/null)

  if [ -z "$blobs" ]; then
    echo "  (empty container, skipping)"
    continue
  fi

  count=0
  errors=0

  while IFS= read -r blob; do
    # Download from Azure to temp file
    tmpfile=$(mktemp)
    if az storage blob download \
      --account-name "$AZURE_ACCOUNT" \
      --container-name "$container" \
      --name "$blob" \
      --file "$tmpfile" \
      --no-progress \
      --output none 2>/dev/null; then

      # Upload to S3
      if aws s3 cp "$tmpfile" "s3://${container}/${blob}" \
        --region "$REGION" \
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
  TOTAL_BLOBS=$((TOTAL_BLOBS + count))
  TOTAL_ERRORS=$((TOTAL_ERRORS + errors))
done

echo ""
echo "=== Migration Complete ==="
echo "Total blobs migrated: $TOTAL_BLOBS"
echo "Total errors: $TOTAL_ERRORS"

if [ "$TOTAL_ERRORS" -gt 0 ]; then
  echo "⚠ Some blobs failed to migrate. Re-run the script to retry."
  exit 1
fi
