# GATool API — AWS Cost Estimate

> Generated from AWS Price List API (us-east-2, April 2026).
> Architecture: ECS Fargate on **Graviton (ARM64)**, public subnets (no NAT Gateway), shared ElastiCache (Redis) for FusionCache L2 + backplane, ALB with HTTPS, Fargate Spot mix.

## Monthly Cost Summary

| Service | Unit Pricing | Usage | Monthly Cost |
|---------|-------------|-------|-------------|
| **ECS Fargate — API On-Demand** (1 vCPU, 2 GB) | $0.03238/vCPU-hr, $0.00356/GB-hr | 1 task × 730 hrs | **$28.84** |
| **ECS Fargate — API Spot** (1 vCPU, 2 GB) | ~70% discount on ARM rates | 1 task × 730 hrs | **$8.65** |
| **ECS Fargate — HighScores Job** (0.25 vCPU, 0.5 GB) | ARM On-Demand rates | 2,880 runs × 2 min = 96 hrs | **$0.95** |
| **ElastiCache** (cache.t4g.micro, Redis OSS) | $0.016/node-hr | 1 node × 730 hrs | **$11.68** |
| **Application Load Balancer** | $0.0225/hr + $0.008/LCU-hr | 730 hrs, ~0.5 LCU avg | **$19.35** |
| **DynamoDB** (on-demand) | $0.125/M RRU, $0.625/M WRU | Light traffic, <1 GB storage | **~$0.50** |
| **Secrets Manager** (15 secrets) | $0.40/secret/mo | 15 secrets | **$6.00** |
| **CloudWatch Logs** | $0.50/GB ingested | ~3 GB/month | **$1.50** |
| **S3 Standard** (4 buckets) | $0.023/GB-mo | <1 GB total | **$0.03** |
| **ACM Certificate** | Free with ALB | 1 cert | **$0.00** |
| | | **Total** | **~$77.50/month** |

### What Changed

| Change | Before | After | Cost Impact |
|--------|--------|-------|-------------|
| API task size | 0.5 vCPU / 1 GB | 1 vCPU / 2 GB | +$14.42/task |
| Minimum tasks | 1 | 2 | +1 task |
| Capacity strategy | FARGATE only | FARGATE (base 1) + FARGATE_SPOT (4:1) | -$20.19 (Spot savings) |
| DynamoDB table | — | `gatool-high-scores` (PAY_PER_REQUEST) | +$0.50 |
| SyncUsers job | 2x daily | Removed | -$0.10 |
| Auto-scale max | 5 | 8 | $0 at min capacity |
| Container health check | None | curl /livecheck | $0 |
| Image tag source | Hardcoded | SSM Parameter | $0 |
| Redis topology | Per-task sidecar (free, isolated) | Shared ElastiCache `cache.t4g.micro` | **+$11.68** |
| FusionCache (L1+L2+backplane) | None | Service-layer caching, stampede protection | $0 (code only) |
| **Net change** | **$41.85/mo** | **$77.50/mo** | **+$35.65/mo** *(cumulative; ElastiCache itself = +$11.68)* |

### Calculation Details

**Fargate ARM (Graviton) — API Service (On-Demand, base task):**
- vCPU: 1 × $0.03238 × 730 = $23.64
- Memory: 2 × $0.00356 × 730 = $5.20
- Total: $28.84/month

**Fargate ARM (Graviton) — API Service (Spot task):**
- Spot pricing is ~70% off On-Demand for ARM Fargate
- $28.84 × 0.30 = $8.65/month

**Fargate ARM — UpdateGlobalHighScores:**
- vCPU: 0.25 × $0.03238 × 96 = $0.78
- Memory: 0.5 × $0.00356 × 96 = $0.17
- Total: $0.95/month

**ElastiCache (cache.t4g.micro, Redis OSS):**
- 1 × $0.016/hr × 730 = $11.68/month
- Provides shared L2 + backplane so all API tasks and job tasks share a single coherent FusionCache, enabling cross-task stampede protection against downstream APIs (FRC, TBA, Statbotics, etc.)
- No data-transfer charge (same-AZ traffic from ECS tasks); no backup/snapshot storage configured
- A self-hosted Fargate Redis (~$10/mo) was considered and rejected — operational overhead not worth the $1.68/mo savings

**ALB:**
- Hourly: $0.0225 × 730 = $16.43
- LCU (low traffic): 0.5 × $0.008 × 730 = $2.92
- Total: $19.35/month

**DynamoDB (on-demand):**
- Storage: <1 GB, within 25 GB free tier = $0.00
- Read/Write: light traffic, ~$0.50/month

## Assumptions

- API runs with min=2 tasks 24/7 (capacity provider: 1 On-Demand base + 1 Spot)
- Fargate Spot ARM discount assumed at ~70% off On-Demand
- HighScores job: every 15 min, ~2 min runtime per invocation
- Low traffic: ~0.5 LCU average on ALB
- DynamoDB: light read/write load, well under free tier storage
- S3 data volume under 1 GB across all 4 buckets
- CloudWatch Logs ingestion ~3 GB/month (higher with 2 API tasks)
- Secrets Manager API calls negligible (preloaded at startup)
- ElastiCache: single-node, no Multi-AZ, no automatic backups (cache loss is non-fatal — FusionCache absorbs L2 outages via L1 + fail-safe + factory)

## Excluded

- Data transfer out to internet (minimal for API responses)
- Route 53 (using Cloudflare for DNS)
- ECR (using GHCR instead)
- AWS Support plan costs
- Fargate Spot interruption recovery (rare, minimal impact)

## Savings vs Previous Architecture

| | Azure (actual) | AWS (original CDK) | AWS (pre-FusionCache) | AWS (current CDK) |
|--|---------------|--------------------|------------------------|--------------------|
| Monthly cost | **$250** | ~$42 | ~$66 | **~$77.50** |
| vs Azure | — | -83% | -74% | **-69%** |

## Further Optimization Ideas

- **Scale back to 1 task off-season**: saves ~$8.65–28.84/month depending on Spot vs OD
- **Reduce task size off-season**: drop to 0.5 vCPU / 1 GB when load is low (also viable now that the Redis sidecar is gone — sidecar previously ate ~256 MB of the task budget)
- **Reduce HighScores frequency off-season**: 30-min or hourly intervals
- **CloudWatch Logs**: if >5 GB/month, switch to Infrequent Access class ($0.25/GB)
- **Move DynamoDB to provisioned**: if traffic is predictable, free tier covers 25 RCU/25 WCU
- **Stop ElastiCache off-season**: `cache.t4g.micro` can be deleted between events to save $11.68/mo; FusionCache will run L1-only and downstream APIs absorb the modest extra load
