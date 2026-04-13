# UptimeDaddy.API

This is theUptimeDaddy backend service for monitoring website uptime and performance. This repository contains the .NET 10 Web API that stores websites, measurements, and communicates with worker devices via MQTT.

Features
- JWT authentication (token validation configured in Program.cs)
- MQTT publish/subscribe for sending scan requests and receiving measurements
- EF Core with PostgreSQL for production and InMemory provider for tests
- Swagger/OpenAPI with Bearer auth configured

Getting started (local)
1. Set up environment variables (example `.env` or environment):
   - `Jwt__Key` - secret used to validate JWT tokens
   - `ConnectionStrings__DefaultConnection` - PostgreSQL connection string
   - `Mqtt__Host` and `Mqtt__Port` for MQTT broker

2. Run locally:
   - `dotnet build`
   - `dotnet run --project UptimeDaddy.API` or run from Visual Studio

Running tests
- There is a test project `UptimeDaddy.API.Tests` (xUnit). Run:
  - `dotnet test UptimeDaddy.API.Tests\UptimeDaddy.API.Tests.csproj`

API docs
- Swagger UI is available at `/swagger` when running the app locally.

Development notes
- To add a migration: `dotnet ef migrations add NAME --project UptimeDaddy.API --startup-project UptimeDaddy.API`
- To update database: `dotnet ef database update --project UptimeDaddy.API --startup-project UptimeDaddy.API`
