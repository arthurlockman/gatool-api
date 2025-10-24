# Quick Start Guide

## Fastest Way to Run Locally

### 1. Prerequisites
- Install [Docker Desktop](https://www.docker.com/products/docker-desktop)
- Install [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Install Azure CLI: `brew install azure-cli` (macOS) or [Download](https://docs.microsoft.com/cli/azure/install-azure-cli)

### 2. Run the Script

```bash
./run-local.sh
```

That's it! The script will:
- ✅ Start Redis in Docker
- ✅ Check Azure authentication
- ✅ Build the project
- ✅ Start the API

### 3. Test the API

Open your browser to **http://localhost:5000/swagger**

Or test from the command line:
```bash
# Get offseason events for 2025
curl http://localhost:5000/v3/2025/offseason/events

# Get teams at Chezy Champs
curl http://localhost:5000/v3/2025/offseason/teams/cc

# Get match schedule
curl http://localhost:5000/v3/2025/offseason/schedule/hybrid/cc
```

## Manual Setup (Alternative)

If you prefer to run commands manually:

```bash
# 1. Start Redis
docker run -d --name redis -p 6379:6379 redis:latest

# 2. Login to Azure
az login

# 3. Run the API
dotnet run
```

## Troubleshooting

### "Permission denied: ./run-local.sh"
Make the script executable:
```bash
chmod +x run-local.sh
```

### "Unable to connect to Redis"
Start Redis manually:
```bash
docker start redis
# or
docker run -d --name redis -p 6379:6379 redis:latest
```

### "Access denied to Key Vault" or "Refresh token expired"
Your Azure token may have expired. Re-authenticate:
```bash
az logout
az login --scope "https://vault.azure.net/.default"
```

Then try running the API again:
```bash
./run-local.sh
```

## Testing Endpoints

### Test Offseason Events
```bash
curl -s http://localhost:5000/v3/2025/offseason/events | jq
```

### Test Rankings
```bash
curl -s http://localhost:5000/v3/2025/offseason/rankings/cc | jq
```

### Test Alliances
```bash
curl -s http://localhost:5000/v3/2025/offseason/alliances/cc | jq
```

### Test Match Scores
```bash
curl -s http://localhost:5000/v3/2025/offseason/schedule/hybrid/cc | jq
```

## Stopping the API

Press `Ctrl+C` in the terminal where the API is running.

To stop Redis:
```bash
docker stop redis
```

---

For more detailed information, see [LOCAL_DEVELOPMENT.md](LOCAL_DEVELOPMENT.md)

