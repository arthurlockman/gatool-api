#!/usr/bin/env python3
"""Migrates all data from Azure Blob Storage to AWS S3.

Blobs are skipped if the S3 copy is already up-to-date (same or newer
timestamp). Re-run before cutover to sync any changes. Use MIGRATE_FORCE=true
to overwrite everything regardless of timestamps.

Prerequisites:
  - Azure CLI logged in (az login) — used to fetch storage account keys
  - AWS CLI profile 'gatool' configured

Setup:
  python3 -m venv .venv
  .venv/bin/pip install azure-storage-blob boto3

Usage:
  AZURE_STORAGE_ACCOUNT=<account> .venv/bin/python scripts/migrate-data.py

Environment variables:
  AZURE_STORAGE_ACCOUNT  (required)  Azure storage account name
  AWS_REGION             (optional)  AWS region, default: us-east-2
  MIGRATE_PARALLEL       (optional)  Number of parallel transfers, default: 20
  MIGRATE_FORCE          (optional)  Set to "true" to re-upload blobs already in S3
"""

import json
import os
import subprocess
import sys
import threading
from concurrent.futures import ThreadPoolExecutor, as_completed

import boto3
from azure.storage.blob import BlobServiceClient
from botocore.exceptions import ClientError

AZURE_ACCOUNT = os.environ.get("AZURE_STORAGE_ACCOUNT")
REGION = os.environ.get("AWS_REGION", "us-east-2")
AWS_PROFILE = "gatool"
PARALLEL = int(os.environ.get("MIGRATE_PARALLEL", "20"))
FORCE = os.environ.get("MIGRATE_FORCE", "false").lower() == "true"

CONTAINERS = [
    "gatool-team-updates",
    "gatool-team-updates-history",
    "gatool-user-preferences",
]

BAR_WIDTH = 40

# Thread-local S3 clients (boto3 clients are not thread-safe)
_thread_local = threading.local()


def get_s3_client():
    if not hasattr(_thread_local, "s3"):
        session = boto3.Session(profile_name=AWS_PROFILE, region_name=REGION)
        _thread_local.s3 = session.client("s3")
    return _thread_local.s3


def get_azure_client() -> BlobServiceClient:
    # Fetch the account key via az CLI (same as `az storage blob list` does internally)
    result = subprocess.run(
        ["az", "storage", "account", "keys", "list",
         "--account-name", AZURE_ACCOUNT, "--output", "json"],
        capture_output=True, text=True,
    )
    if result.returncode != 0:
        print(f"Error: Failed to get Azure storage keys: {result.stderr}", file=sys.stderr)
        sys.exit(1)
    account_key = json.loads(result.stdout)[0]["value"]
    return BlobServiceClient(
        account_url=f"https://{AZURE_ACCOUNT}.blob.core.windows.net",
        credential=account_key,
    )


def list_blobs(azure_client: BlobServiceClient, container: str) -> list:
    """Returns list of BlobProperties (with .name and .last_modified)."""
    container_client = azure_client.get_container_client(container)
    return list(container_client.list_blobs())


def s3_last_modified(bucket: str, key: str):
    """Returns the S3 object's LastModified datetime, or None if missing."""
    try:
        resp = get_s3_client().head_object(Bucket=bucket, Key=key)
        return resp["LastModified"]
    except ClientError:
        return None


def migrate_blob(
    azure_client: BlobServiceClient, container: str, blob_props
) -> str:
    """Returns 'ok', 'skip', or 'err'."""
    blob = blob_props.name
    try:
        if not FORCE:
            s3_mtime = s3_last_modified(container, blob)
            if s3_mtime and s3_mtime >= blob_props.last_modified:
                return "skip"

        blob_client = azure_client.get_container_client(container).get_blob_client(blob)
        data = blob_client.download_blob().readall()

        get_s3_client().put_object(
            Bucket=container,
            Key=blob,
            Body=data,
            ContentType="application/json",
        )
        return "ok"
    except Exception as e:
        print(f"  ✗ {blob}: {e}", file=sys.stderr)
        return "err"


def print_bar(done: int, total: int):
    pct = done * 100 // total if total else 0
    filled = done * BAR_WIDTH // total if total else 0
    bar = "█" * filled + "░" * (BAR_WIDTH - filled)
    print(f"\r  [{bar}] {done}/{total} ({pct}%)", end="", file=sys.stderr, flush=True)


def migrate_container(
    azure_client: BlobServiceClient, container: str
) -> tuple[int, int, int]:
    print(f"=== Migrating: {container} ===")

    blobs = list_blobs(azure_client, container)
    if not blobs:
        print("  (empty container, skipping)")
        return 0, 0, 0

    total = len(blobs)
    print(f"  Found {total} blobs — {PARALLEL} parallel workers")

    ok = skip = err = 0
    done = 0
    print_bar(0, total)

    with ThreadPoolExecutor(max_workers=PARALLEL) as pool:
        futures = {
            pool.submit(migrate_blob, azure_client, container, b): b for b in blobs
        }
        for future in as_completed(futures):
            result = future.result()
            if result == "ok":
                ok += 1
            elif result == "skip":
                skip += 1
            else:
                err += 1
            done += 1
            print_bar(done, total)

    print(file=sys.stderr)
    print(f"  ✓ Migrated: {ok}  Skipped: {skip}  Errors: {err}")
    return ok, skip, err


def main():
    if not AZURE_ACCOUNT:
        print("Error: Set AZURE_STORAGE_ACCOUNT environment variable", file=sys.stderr)
        sys.exit(1)

    azure_client = get_azure_client()

    total_ok = total_skip = total_err = 0

    for container in CONTAINERS:
        ok, skip, err = migrate_container(azure_client, container)
        total_ok += ok
        total_skip += skip
        total_err += err

    print()
    print("=== Migration Complete ===")
    print(f"Migrated: {total_ok}  Skipped: {total_skip}  Errors: {total_err}")

    if total_err > 0:
        print("⚠ Some blobs failed. Re-run to retry (migrated blobs are skipped).")
        sys.exit(1)


if __name__ == "__main__":
    main()
