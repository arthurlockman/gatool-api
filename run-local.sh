#!/bin/bash

# GATool API - Local Development Runner
# This script helps you quickly start the API for local testing
# NOTE: It has only been tested on macOS.

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
    echo "❌ .NET SDK is not installed. Please install .NET 10.0 SDK first."
    echo "   Download from: https://dotnet.microsoft.com/download/dotnet/10.0"
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
echo "🔍 Checking AWS authentication..."

# Check if AWS CLI is installed
if ! command -v aws &> /dev/null; then
    echo "⚠️  AWS CLI not installed. Install with: brew install awscli"
    echo "   The API will fail to start without AWS authentication."
    read -p "Continue anyway? (y/n) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 1
    fi
else
    # Check if logged into AWS
    if ! aws sts get-caller-identity &> /dev/null; then
        echo "⚠️  Not authenticated with AWS. Run 'aws configure' to set up credentials."
        read -p "Continue anyway? (y/n) " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            exit 1
        fi
    else
        echo "✅ Authenticated with AWS"
        ACCOUNT=$(aws sts get-caller-identity --query Account --output text)
        REGION=$(aws configure get region)
        echo "   Account: $ACCOUNT, Region: $REGION"
    fi
fi

echo ""
echo "🏗️  Building the application..."
dotnet build --configuration Debug

echo ""
echo "🚀 Starting the API..."
echo ""
echo "📋 The API will be available at:"
echo "   - Swagger UI: http://localhost:8080/swagger"
echo "   - HTTP:       http://localhost:8080"
echo ""
echo "Press Ctrl+C to stop the server"
echo ""

# Run the application
ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build

