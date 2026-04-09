# GATool API Coding Instructions

## Project Overview
GATool API is a .NET 10 API service that provides game announcer data for FIRST Robotics competitions. It aggregates data from multiple sources (FIRST API, The Blue Alliance, statbotics.io) and serves it through a unified REST API.

## Architecture & Components

### Core Services Structure
- API Controllers (`Controllers/`): REST endpoints organized by data source/function
- Services (`Services/`): Data access and business logic layer
  - External API clients (FRC, TBA, Statbotics, etc.) inherit from `IApiService`
  - `TeamDataService`: Core service for aggregating team data
  - `ScheduleService`: Handles event scheduling logic
  - `UserStorageService`: Manages user data and permissions via S3

### Key Infrastructure Components
- Redis caching (`RedisCacheAttribute.cs`): Attribute-based response caching; Redis runs as an ECS sidecar
- AWS ECS Fargate on ARM64 (Graviton): Main deployment target with auto-scaling
- CDK Infrastructure (`infra-cdk/`): C# CDK stack defining all AWS resources
- Scheduled Jobs (`Jobs/`): Background ECS tasks triggered by EventBridge rules
  - `UpdateGlobalHighScores`: Runs every 15 minutes
  - `SyncUsers`: Runs twice daily at 2 AM and 2 PM UTC

## AWS Infrastructure

### Deployment Architecture
- **Account**: 069176179806, **Region**: us-east-2
- **VPC**: Public-only subnets (no NAT Gateway, saves ~$32/month)
- **ECS Cluster**: `gatool`, ARM64 Graviton Fargate tasks
- **API Service**: `gatool-api` — ALB with HTTPS (ACM cert for `*.gatool.org`), auto-scaling 1–5 tasks
- **API Task**: 0.5 vCPU / 1 GB — app container + Redis sidecar
- **Job Task**: 0.25 vCPU / 512 MB — app container + Redis sidecar (no ALB)
- **S3 Buckets**: `gatool-high-scores`, `gatool-team-updates`, `gatool-team-updates-history`, `gatool-user-preferences`
- **Container Image**: `ghcr.io/arthurlockman/gatool-api:aws` (ARM64)

### CDK Stack (`infra-cdk/GatoolStack.cs`)
- Written in C# to match the rest of the codebase
- S3 buckets are imported via `Bucket.FromBucketName()` (they have `RemovalPolicy.RETAIN` and survive stack deletion)
- ACM certificate is imported via `Certificate.FromCertificateArn()` (pre-created wildcard cert)
- Build and deploy:
  ```bash
  cd infra-cdk && npx cdk deploy --require-approval never --profile gatool
  ```
- Synth only (check for errors without deploying):
  ```bash
  cd infra-cdk && npx cdk synth --quiet --profile gatool
  ```

### Secrets Management
- Secrets are stored in **AWS Secrets Manager** with plain names (no prefix): `Auth0Issuer`, `FRCApiKey`, `TBAApiKey`, etc.
- `Program.cs` preloads all secrets at startup via `AwsSecretProvider.PreloadSecretsAsync()`
- `GetSecret()` and `GetSecretAsync()` calls throughout the codebase use these plain names
- `NEW_RELIC_LICENSE_KEY` is injected as an ECS container secret (from Secrets Manager → env var), not loaded by the app
- Helper scripts in `scripts/`: `create-secrets.sh` (initial setup), `rename-secrets.sh` (migration utility)

### Scheduled Jobs & ECS Task Overrides
- Jobs share the same Docker image as the API but are launched with a command override
- The Dockerfile uses `ENTRYPOINT ["dotnet", "gatool-api.dll"]`
- **IMPORTANT**: ECS `ContainerOverride.Command` sets Docker's `CMD`, which becomes *arguments* to the `ENTRYPOINT`. The override must be just the args: `["--job", "JobName"]`, NOT `["dotnet", "gatool-api.dll", "--job", "JobName"]` — the latter would double the entrypoint and start a web server instead of running the job
- `Program.cs` checks `args[0] == "--job"` and `args[1]` for the job name (line ~136)
- Jobs run as one-off ECS Fargate tasks triggered by EventBridge rules

### Health Checks
- **API**: ALB target group health check on `/livecheck` (no container-level health check needed)
- **Redis sidecar**: Container health check using `redis-cli ping`
- **Jobs**: No health check (they run and exit)

### Monitoring & Logging
- Application logs go to **New Relic** via Serilog `NewRelicLogs` sink (configured in `appsettings.json`)
- CloudWatch log streams exist but are typically empty (all meaningful logs are in New Relic)
- New Relic account ID: 3727261
- The New Relic .NET agent is installed in the Docker image and requires `NEW_RELIC_LICENSE_KEY` env var — without it, the app crashes at startup with `Either LicenseKey or InsertKey must be supplied`
- New Relic CLI profile: `gatool`

### CloudFormation Gotchas
- `CREATE_IN_PROGRESS` cannot be cancelled — only `UPDATE_IN_PROGRESS` can (via `aws cloudformation cancel-update-stack`)
- Failed CREATE triggers full rollback (deletes everything)
- ECS service stabilization timeout is ~30 minutes and blocks the whole stack
- `cdk deploy` re-enables any disabled EventBridge rules
- To temporarily disable a job: `aws events disable-rule --name <rule-name> --profile gatool`

## Development Workflow

### Environment Setup
```bash
# Required environment variables
Redis__Host=localhost
Redis__Port=6379
Redis__UseTls=false
Redis__Password=""
```

### Running Locally
1. Start Redis (required for local development)
2. Build and run API:
   ```bash
   dotnet run
   ```
   API will be available at http://localhost:8080

### Running Jobs Locally
```bash
dotnet run -- --job UpdateGlobalHighScores
dotnet run -- --job SyncUsers
```

### Building the Docker Image
```bash
docker build --platform linux/arm64 -t gatool-api:aws .
```

### AWS CLI Profile
The AWS CLI profile `gatool` is used for all operations:
```bash
aws ecs list-services --cluster gatool --profile gatool
aws ecs describe-services --cluster gatool --services gatool-api --profile gatool
```

## Common Patterns

### Authentication/Authorization
- JWT-based auth using Auth0
- Role-based access control via policies:
  - "user": Basic access
  - "admin": Administrative functions

### Caching Strategy
```csharp
// Cache responses with the [RedisCache] attribute
[RedisCache(Duration = 300)]  // 5 minutes
[HttpGet("example")]
public async Task<IActionResult> GetExample()
```

### Error Handling
- Use `ExternalApiException` for third-party API failures
- Global exception handling via `ExceptionHandlingMiddleware`

## Key Files for Common Tasks
- API Configuration & Job Dispatch: `Program.cs`
- AWS Infrastructure (CDK): `infra-cdk/GatoolStack.cs`
- CDK Entry Point: `infra-cdk/Program.cs`
- Docker Image: `Dockerfile` (ENTRYPOINT-based, ARM64)
- External API Models: `Models/{FRCApiModels,TBAModels,StatboticsModels}.cs`
- Rate Limiting/Caching: `Attributes/RedisCacheAttribute.cs`
- Serilog & S3 Config: `appsettings.json`
- AWS Helper Scripts: `scripts/create-secrets.sh`, `scripts/rename-secrets.sh`

## Integration Points
- FIRST APIs (FRC & FTC)
- The Blue Alliance API
- Statbotics.io
- FTC Scout
- Auth0 (authentication)
- AWS Secrets Manager (secrets)
- AWS S3 (storage for user data, high scores, team updates)
- Redis (caching, runs as ECS sidecar)
- New Relic (monitoring & logging)
- GitHub Container Registry (container images)