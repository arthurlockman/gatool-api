# syntax=docker/dockerfile:1
# check=skip=SecretsUsedInArgOrEnv

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY . .
RUN dotnet publish gatool-api.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

# Install the Newrelic agent
RUN apt-get update && apt-get install -y wget ca-certificates gnupg \
&& echo 'deb [signed-by=/usr/share/keyrings/newrelic-apt.gpg] http://apt.newrelic.com/debian/ newrelic non-free' | tee /etc/apt/sources.list.d/newrelic.list \
&& wget -O- https://download.newrelic.com/NEWRELIC_APT_2DAD550E.public | gpg --import --batch --no-default-keyring --keyring /usr/share/keyrings/newrelic-apt.gpg \
&& apt-get update \
&& apt-get install -y newrelic-dotnet-agent

# Enable the agent
ENV CORECLR_ENABLE_PROFILING=1 \
CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A} \
CORECLR_NEWRELIC_HOME=/usr/local/newrelic-dotnet-agent \
CORECLR_PROFILER_PATH=/usr/local/newrelic-dotnet-agent/libNewRelicProfiler.so

# Disable automatic log forwarding to prevent duplicates (we use Serilog instead)
ENV NEW_RELIC_APPLICATION_LOGGING_ENABLED=false \
NEW_RELIC_APPLICATION_LOGGING_FORWARDING_ENABLED=false \
NEW_RELIC_APPLICATION_LOGGING_METRICS_ENABLED=true \
NEW_RELIC_APPLICATION_LOGGING_LOCAL_DECORATING_ENABLED=false

# Will be filled in by deployment
ENV NEW_RELIC_LICENSE_KEY=""
ENV NEW_RELIC_APP_NAME=""

WORKDIR /app

# Create non-root user for security
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

# Copy the published app from build stage
COPY --from=build /app/publish .

# Environment variables for Redis configuration
ENV Redis__Host=localhost
ENV Redis__Port=6379
ENV Redis__UseTls=false
ENV Redis__Password=""

# Expose port
EXPOSE 8080

# Health check

# Set the entry point
ENTRYPOINT ["dotnet", "gatool-api.dll"]
