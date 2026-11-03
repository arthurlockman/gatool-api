# GATool API

![gatool-api deploy status](https://github.com/arthurlockman/gatool-api/actions/workflows/main_gatool-api.yml/badge.svg)

gatool is a tool to provide **_FIRST®_** Game Announcers with up to date information while announcing events during the
**_FIRST_** Robotics season. As a web-based tool, it uses up-to-date information about the event to provide a
comprehensive set of useful data to Game Announcers. It is designed to work on desktops, laptops and tablet devices. In
a pinch, it can be used on a mobile phone.

gatool relies on data from APIs provided by [**_FIRST_**](https://frc-api-docs.firstinspires.org), [**
_FIRST_** Global](https://api.first.global/v1),[The Blue Alliance](https://www.thebluealliance.com/apidocs/v3), [statbotics.io](https://www.statbotics.io/docs/rest),
and [FTCScout](https://ftcscout.org/api) as well as its own data sources. We created the gatool APIs to provide a single
access point for the gatool and for other tools that want to use its data. We ask that if you build a tool that uses
gatool APIs, that you include "Data provided by the **_FIRST_** Game Announcer Tool API" and a link to this page in your
application/service.

[Watch an overview of gatool on YouTube to learn more!](https://youtu.be/-n96KgtgYF0)

## Contributions

We welcome any and all contributions! Please feel free to fork the repository and contribute back to our
development. [Issues can be filed in the GitHub issue tracker](https://github.com/arthurlockman/gatool-api/issues/new).

## Development

### Quick Start

The fastest way to run the API locally:

```bash
./run-local.sh
```

This script will automatically:

- Start Redis in Docker
- Check AWS authentication
- Build and run the API

The API will be available at http://localhost:8080/swagger

### Detailed Setup

For detailed local development instructions, see:

- **[QUICKSTART.md](QUICKSTART.md)** - Get up and running in minutes
- **[LOCAL_DEVELOPMENT.md](LOCAL_DEVELOPMENT.md)** - Complete development guide with troubleshooting

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (for Redis)
- AWS CLI: `brew install awscli` (macOS)

## API Documentation

All endpoints are served from:

```
https://api.gatool.org/
```

### 🚀 NEW: All API documentation (including types!) is auto-generated and available at https://api.gatool.org/swagger/

Event team lists use a query parameter on the season's team-list endpoint:

```text
GET /v3/2026/teams?eventCode=mawor1
GET /ftc/v2/2025/teams?eventCode=USMAWAL
```

For FTC, the year is the season's starting year. Both endpoints combine all upstream pages.
The Casters Tool matchup operation (`GET /v3/{year}/matchups/{eventCode}/{teamNumbers}`)
is deprecated in OpenAPI but remains available for existing clients.

Validate documentation locally with the .NET 10 SDK:

```bash
dotnet run --project scripts/swagger-coverage/SwaggerCoverage.csproj
```

This check maps the API assembly's actual controller endpoints, compares them with ApiExplorer and
the generated OpenAPI document using the same Swagger configuration as the service, and verifies
operation summaries, parameters, request bodies, response descriptions, and Casters Tool deprecation.
It also checks the event-team query contract, raw JSON and seasonal score schemas, PNG media type,
and Mailchimp webhook form metadata. No AWS credentials, Redis, or external API calls are needed:
the API startup and endpoint handlers are not executed. To save the generated document, append
`-- --output /absolute/path/swagger.json`.
