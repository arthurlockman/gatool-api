# Local Development Setup

This guide will help you run the GATool API locally for testing and development.

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (for Redis)
- Azure CLI (for Key Vault access) - `brew install azure-cli` on macOS

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

### 2. Azure Authentication

The API uses Azure Key Vault to retrieve secrets. You need to authenticate with Azure:

```bash
# Login to Azure
az login

# Set your Azure subscription (if you have multiple)
az account set --subscription "your-subscription-id"
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
- **HTTP**: http://localhost:5000
- **HTTPS**: https://localhost:5001
- **Swagger UI**: http://localhost:5000/swagger

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

### appsettings.Local.json (Optional)
Created for you with placeholder values. This file contains:
- Local Redis configuration
- Placeholders for API keys (if you want to bypass Key Vault in the future)

**Note**: `appsettings.Local.json` is not currently used by the application. The app requires Azure Key Vault access.

## Required Secrets in Azure Key Vault

The application retrieves these secrets from Azure Key Vault (`https://GAToolApiKeys.vault.azure.net`):

| Secret Name | Description | Required For |
|------------|-------------|--------------|
| `Auth0Issuer` | Auth0 domain URL | Authentication |
| `Auth0Audience` | Auth0 API audience | Authentication |
| `UserStorageConnectionString` | Azure Blob Storage | User data endpoints |
| `FRCApiKey` | FIRST API key | FRC data endpoints |
| `TBAApiKey` | The Blue Alliance API key | TBA/offseason endpoints |
| `StatboticsApiKey` | Statbotics API key | Statistical data |
| `FTCApiKey` | FTC API key | FTC data endpoints |
| `TOAApiKey` | The Orange Alliance API key | FTC data endpoints |

## Testing Without Authentication

Some endpoints require authentication. To test without setting up Auth0:

### Option 1: Test Public Endpoints
Many endpoints don't require authentication. Try these:

```bash
# Get offseason events
curl http://localhost:5000/v3/2025/offseason/events

# Get team data  
curl http://localhost:5000/v3/2025/offseason/teams/cc

# Get match schedule
curl http://localhost:5000/v3/2025/offseason/schedule/hybrid/cc
```

### Option 2: Use Swagger UI
1. Navigate to http://localhost:5000/swagger
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

# Run the application
dotnet run
```

## Troubleshooting

### "Key Vault URL required to start up"
**Problem**: The application can't find the Key Vault configuration.

**Solution**: Ensure you're authenticated with Azure CLI:
```bash
az login
az account show
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

### "Access denied to Azure Key Vault"
**Problem**: Your Azure account doesn't have permission to read secrets from the Key Vault.

**Solution**: Contact the Azure administrator to grant you "Key Vault Secrets User" role on the Key Vault.

### "The refresh token has expired due to inactivity" (AADSTS700082)
**Problem**: Your Azure CLI token has expired (typically after 90 days of inactivity).

**Solution**: 
```bash
az logout
az login --tenant "c8c6d255-9e1a-4560-aa0e-8e61501ab304" --scope "https://vault.azure.net/.default"
```

Or simply:
```bash
az logout
az login --scope "https://vault.azure.net/.default"
```

### Port already in use
**Problem**: Port 5000 or 5001 is already in use.

**Solution**: Stop the other application or specify different ports:
```bash
dotnet run --urls "http://localhost:5050;https://localhost:5051"
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
├── Properties/          # Launch settings
└── appsettings*.json    # Configuration files
```

## Next Steps

1. **Review the Swagger UI** at http://localhost:5000/swagger to see all available endpoints
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
4. Store it in Azure Key Vault as `TBAApiKey`

### FIRST API (Required for Official Events)
1. Go to https://frc-events.firstinspires.org/services/API
2. Request API access
3. Store the credentials in Azure Key Vault

## Contact

If you have issues accessing Azure resources or need permissions, contact the project administrator.

