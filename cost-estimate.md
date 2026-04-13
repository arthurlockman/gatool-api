# GATool API — AWS Cost Estimate

> Generated from AWS Price List API (us-east-2, April 2026).
> Architecture: ECS Fargate on **Graviton (ARM64)**, public subnets (no NAT Gateway), Redis sidecar, ALB with HTTPS.

## Monthly Cost Summary

| Service | Unit Pricing | Usage | Monthly Cost |
|---------|-------------|-------|-------------|
| **ECS Fargate — API** (0.5 vCPU, 1 GB) | $0.03238/vCPU-hr, $0.00356/GB-hr | 1 task × 730 hrs | **$14.42** |
| **ECS Fargate — HighScores Job** (0.25 vCPU, 0.5 GB) | same ARM rates | 2,880 runs × 2 min = 96 hrs | **$0.95** |
| **ECS Fargate — SyncUsers Job** (0.25 vCPU, 0.5 GB) | same ARM rates | 60 runs × 10 min = 10 hrs | **$0.10** |
| **Application Load Balancer** | $0.0225/hr + $0.008/LCU-hr | 730 hrs, ~0.5 LCU avg | **$19.35** |
| **Secrets Manager** (15 secrets) | $0.40/secret/mo | 15 secrets | **$6.00** |
| **CloudWatch Logs** | $0.50/GB ingested | ~2 GB/month | **$1.00** |
| **S3 Standard** (4 buckets) | $0.023/GB-mo | <1 GB total | **$0.03** |
| **ACM Certificate** | Free with ALB | 1 cert | **$0.00** |
| | | **Total** | **~$41.85/month** |

### Calculation Details

**Fargate ARM (Graviton) — API Service:**
- vCPU: 0.5 × $0.03238 × 730 = $11.82
- Memory: 1 × $0.00356 × 730 = $2.60
- Total: $14.42/month

**Fargate ARM — UpdateGlobalHighScores:**
- vCPU: 0.25 × $0.03238 × 96 = $0.78
- Memory: 0.5 × $0.00356 × 96 = $0.17
- Total: $0.95/month

**Fargate ARM — SyncUsers:**
- vCPU: 0.25 × $0.03238 × 10 = $0.08
- Memory: 0.5 × $0.00356 × 10 = $0.02
- Total: $0.10/month

**ALB:**
- Hourly: $0.0225 × 730 = $16.43
- LCU (low traffic): 0.5 × $0.008 × 730 = $2.92
- Total: $19.35/month

## Assumptions

- API runs with min=1 task 24/7 (can scale to 0 off-season for ~$14 savings)
- HighScores job: every 15 min, ~2 min runtime per invocation
- SyncUsers job: 2x daily, ~10 min runtime per invocation
- Low traffic: ~0.5 LCU average on ALB
- S3 data volume under 1 GB across all 4 buckets
- CloudWatch Logs ingestion ~2 GB/month
- Secrets Manager API calls negligible (preloaded at startup)

## Excluded

- Data transfer out to internet (minimal for API responses)
- Route 53 (using Cloudflare for DNS)
- ECR (using GHCR instead)
- AWS Support plan costs

## Savings vs Previous Architecture

| | Azure (actual) | AWS (x86 + NAT) | AWS (ARM, no NAT) |
|--|---------------|-----------------|-------------------|
| Monthly cost | **$250** | ~$79 | **~$42** |
| vs Azure | — | -68% | **-83%** |

## Further Optimization Ideas

- **Scale to zero off-season**: saves ~$14/month in Fargate costs
- **Reduce HighScores frequency off-season**: 30-min or hourly intervals
- **CloudWatch Logs**: if >5 GB/month, switch to Infrequent Access class ($0.25/GB)
