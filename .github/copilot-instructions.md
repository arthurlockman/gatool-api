# GATool API Coding Instructions

## Project Overview
GATool API is a .NET API service that provides game announcer data for FIRST Robotics competitions. It aggregates data from multiple sources (FIRST API, The Blue Alliance, statbotics.io) and serves it through a unified REST API.

## Architecture & Components

### Core Services Structure
- API Controllers (`Controllers/`): REST endpoints organized by data source/function
- Services (`Services/`): Data access and business logic layer
  - External API clients (FRC, TBA, Statbotics, etc.) inherit from `IApiService`
  - `TeamDataService`: Core service for aggregating team data
  - `ScheduleService`: Handles event scheduling logic
  - `UserStorageService`: Manages user data and permissions

### Key Infrastructure Components
- Redis caching (`RedisCacheAttribute.cs`): Attribute-based response caching
- Azure Container Apps: Main deployment target with auto-scaling
- Scheduled Jobs (`Jobs/`): Background tasks for data sync
  - High scores update (15-minute intervals)
  - User sync (daily at 2 AM UTC)

## Development Workflow

### Environment Setup
```bash
# Required environment variables (stored in Azure Key Vault in production)
KeyVaultUrl=<url>
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

### Jobs
Run background jobs using:
```bash
dotnet run --job [JobName]  # Available: UpdateGlobalHighScores, SyncUsers
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
- API Configuration: `Program.cs`
- Infrastructure Setup: `infra/container_app.tf`
- External API Models: `Models/{FRCApiModels,TBAModels,StatboticsModels}.cs`
- Rate Limiting/Caching: `Attributes/RedisCacheAttribute.cs`

## Integration Points
- FIRST APIs (FRC & FTC)
- The Blue Alliance API
- Statbotics.io
- FTC Scout
- Auth0 (authentication)
- Azure Key Vault (secrets)
- Redis (caching)
- New Relic (monitoring)