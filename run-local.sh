#!/bin/bash

# GATool API - Local Development Runner
# This script helps you quickly start the API for local testing

set -e

echo "🚀 GATool API - Local Development Setup"
echo "========================================"
echo ""

# Check if Docker is installed
if ! command -v docker &> /dev/null; then
    echo "❌ Docker is not installed. Please install Docker Desktop first."
    echo "   Download from: https://www.docker.com/products/docker-desktop"
    exit 1
fi

# Check if .NET is installed
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET SDK is not installed. Please install .NET 9.0 SDK first."
    echo "   Download from: https://dotnet.microsoft.com/download/dotnet/9.0"
    exit 1
fi

# Check if Redis container exists
if docker ps -a --format '{{.Names}}' | grep -q '^redis$'; then
    echo "✅ Redis container found"
    
    # Check if Redis is running
    if ! docker ps --format '{{.Names}}' | grep -q '^redis$'; then
        echo "⚡ Starting Redis container..."
        docker start redis
    else
        echo "✅ Redis is already running"
    fi
else
    echo "📦 Creating and starting Redis container..."
    docker run -d --name redis -p 6379:6379 redis:latest
fi

echo ""
echo "🔍 Checking Azure authentication..."

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    echo "⚠️  Azure CLI not installed. Install with: brew install azure-cli"
    echo "   The API will fail to start without Azure authentication."
    read -p "Continue anyway? (y/n) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 1
    fi
else
    # Check if logged into Azure
    if ! az account show &> /dev/null; then
        echo "⚠️  Not logged into Azure. Logging in..."
        az login --scope "https://vault.azure.net/.default"
    else
        echo "✅ Authenticated with Azure"
        SUBSCRIPTION=$(az account show --query name -o tsv)
        echo "   Using subscription: $SUBSCRIPTION"
        echo ""
        echo "💡 If you get authentication errors, your token may have expired."
        echo "   Run: az logout && az login --scope \"https://vault.azure.net/.default\""
    fi
fi

echo ""
echo "🏗️  Building the application..."
dotnet build --configuration Debug

echo ""
echo "🚀 Starting the API..."
echo ""
echo "📋 The API will be available at:"
echo "   - Swagger UI: http://localhost:5000/swagger"
echo "   - HTTP:       http://localhost:5000"
echo "   - HTTPS:      https://localhost:5001"
echo ""
echo "Press Ctrl+C to stop the server"
echo ""

# Run the application
ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build

