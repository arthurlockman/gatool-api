# Local Development Setup

This guide will help you run the GATool API locally for testing and development.

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (for Redis)
- AWS CLI (for Secrets Manager access) - `brew install awscli` on macOS

## Quick Start

### 1. Install Redis (using Docker)

The easiest way to run Redis locally is with Docker:

```bash
docker run -d --name redis -p 6379:6379 redis:latest
```

To verify Redis is running:

```bash
docker ps | grep redis
```

To stop Redis when done:

```bash
docker stop redis
```

To start it again later:

```bash
docker start redis
```

### 2. AWS Authentication

The API uses AWS Secrets Manager to retrieve secrets. You must have AWS credentials configured with access to the Secrets Manager secrets.

```bash
# Configure AWS CLI with your credentials
aws configure

# Or set environment variables
export AWS_ACCESS_KEY_ID=your-access-key
export AWS_SECRET_ACCESS_KEY=your-secret-key
export AWS_REGION=us-east-2
```

Verify your credentials are working:

```bash
aws sts get-caller-identity
```

### 3. Run the API

From the project root directory:

```bash
# Restore dependencies
dotnet restore

# Run the API
dotnet run
```

The API will start at:

- **HTTP**: http://localhost:8080
- **Swagger UI**: http://localhost:8080/swagger

## Alternative: Using Visual Studio / Rider

If you're using an IDE:

1. Open the solution file: `gatool-api.sln`
2. Ensure Redis is running (see step 1 above)
3. Press F5 or click the Run button
4. The browser should automatically open to the Swagger UI

## Configuration Files

### appsettings.Development.json

Used when `ASPNETCORE_ENVIRONMENT=Development`. This is automatically loaded and contains:

- Console logging configuration
- Local Redis settings (localhost:6379)

## Required Secrets in AWS Secrets Manager

The application retrieves these secrets from AWS Secrets Manager (prefix: `gatool/`):

| Secret Name                   | Description                 | Required For            |
|-------------------------------|-----------------------------|-------------------------|
| `gatool/Auth0Issuer`          | Auth0 domain URL            | Authentication          |
| `gatool/Auth0Audience`        | Auth0 API audience          | Authentication          |
| `gatool/FRCApiKey`            | FIRST API key               | FRC data endpoints      |
| `gatool/TBAApiKey`            | The Blue Alliance API key   | TBA/offseason endpoints |
| `gatool/FTCApiKey`            | FTC API key                 | FTC data endpoints      |
| `gatool/TOAApiKey`            | The Orange Alliance API key | FTC data endpoints      |
| `gatool/CasterstoolApiKey`    | Casterstool API key         | Matchup connections     |
| `gatool/FRCCurrentSeason`     | Current FRC season year     | Season filtering        |
| `gatool/FTCCurrentSeason`     | Current FTC season year     | Season filtering        |
| `gatool/MailChimpAPIKey`      | MailChimp API key           | User sync               |
| `gatool/MailchimpAPIURL`      | MailChimp API URL           | User sync               |
| `gatool/MailchimpListID`      | MailChimp list ID           | User sync               |
| `gatool/Auth0AdminClientId`   | Auth0 Management client ID  | User sync               |
| `gatool/Auth0AdminClientSecret` | Auth0 Management secret   | User sync               |
| `gatool/NewRelicLicenseKey`   | New Relic license key       | Monitoring              |

To create all secrets at once, use the provided script:

```bash
AWS_REGION=us-east-2 ./scripts/create-secrets.sh
```

## Testing Without Authentication

Some endpoints require authentication. To test without setting up Auth0:

### Option 1: Test Public Endpoints

Many endpoints don't require authentication. Try these:

```bash
# Get offseason events
curl http://localhost:8080/v3/2025/offseason/events

# Get team data  
curl http://localhost:8080/v3/2025/offseason/teams/cc

# Get match schedule
curl http://localhost:8080/v3/2025/offseason/schedule/hybrid/cc
```

### Option 2: Use Swagger UI

1. Navigate to http://localhost:8080/swagger
2. Expand any endpoint
3. Click "Try it out"
4. Fill in parameters and click "Execute"

## Environment Variables

You can override configuration values using environment variables:

```bash
# Override Redis host
export Redis__Host=your-redis-host

# Override Redis port
export Redis__Port=6380

# Override AWS region
export AWS__Region=us-east-2

# Run the application
dotnet run
```

## Troubleshooting

### "Unable to retrieve secrets from AWS"

**Problem**: The application can't access AWS Secrets Manager.

**Solution**: Ensure you're authenticated with the AWS CLI:

```bash
aws sts get-caller-identity
aws secretsmanager list-secrets --filter Key=name,Values=gatool/
```

### "Unable to connect to Redis"

**Problem**: Redis is not running or not accessible.

**Solution**:

```bash
# Check if Redis is running
docker ps | grep redis

# If not running, start it
docker start redis

# Or create a new container
docker run -d --name redis -p 6379:6379 redis:latest
```

### Port already in use

**Problem**: Port 8080 is already in use.

**Solution**: Stop the other application or specify different ports:

```bash
dotnet run --urls "http://localhost:5050"
```

## API Keys for Testing

### The Blue Alliance (TBA) API Key

Required for offseason endpoints. Get a free key at:
https://www.thebluealliance.com/account

### FIRST API Key

Required for official event data. Request at:
https://frc-events.firstinspires.org/services/API

## Useful Commands

```bash
# Build the project
dotnet build

# Run with specific environment
ASPNETCORE_ENVIRONMENT=Development dotnet run

# Run with verbose logging
dotnet run --verbosity detailed

# Clean build artifacts
dotnet clean

# Run a specific job (instead of web API)
dotnet run --job UpdateGlobalHighScores
```

## Project Structure

```
gatool-api/
├── Controllers/          # API endpoints
│   ├── FRCApiController.cs     # FRC/offseason endpoints
│   ├── FTCApiController.cs     # FTC endpoints
│   └── ...
├── Services/            # Business logic and external API clients
├── Models/              # Data models
├── Middleware/          # Request/response middleware
├── Jobs/                # Background jobs
├── infra-cdk/           # AWS CDK infrastructure (C#)
├── scripts/             # Migration and setup scripts
├── Properties/          # Launch settings
└── appsettings*.json    # Configuration files
```

## Next Steps

1. **Review the Swagger UI** at http://localhost:8080/swagger to see all available endpoints
2. **Test the offseason endpoints** you've been working on:
    - `/v3/{year}/offseason/events`
    - `/v3/{year}/offseason/teams/{eventCode}`
    - `/v3/{year}/offseason/schedule/hybrid/{eventCode}`
    - `/v3/{year}/offseason/rankings/{eventCode}`
    - `/v3/{year}/offseason/alliances/{eventCode}`
3. **Check the logs** in the console for any errors or warnings

## Getting API Keys

If you need to test with real data and don't have API keys:

### The Blue Alliance (Required for Offseason Endpoints)

1. Go to https://www.thebluealliance.com/account
2. Sign in or create an account
3. Generate an API key
4. Store it in AWS Secrets Manager as `gatool/TBAApiKey`

### FIRST API (Required for Official Events)

1. Go to https://frc-events.firstinspires.org/services/API
2. Request API access
3. Store the credentials in AWS Secrets Manager as `gatool/FRCApiKey`

## Contact

If you have issues accessing AWS resources or need permissions, contact the project administrator.

