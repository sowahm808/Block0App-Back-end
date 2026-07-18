# Mind Unlocking Academy — WhisperWrap Block Zero Backend

Production-oriented modular monolith for the 21-day medical exam preparation challenge.

## Current architecture

```mermaid
flowchart LR
  Client-->Api[ASP.NET Core API]
  Api-->Application
  Application-->Domain
  Api-->Infrastructure
  Infrastructure-->Sql[(Azure SQL)]
  Infrastructure-->Redis[(Azure Cache for Redis)]
  Infrastructure-->Blob[(Azure Blob Storage)]
  Infrastructure-->Bus[(Azure Service Bus)]
  Workers-->Infrastructure
```

The repository now contains a .NET 8 solution skeleton with API, Application, Domain, Infrastructure, Contracts, Workers, and test projects. Phase 1 establishes configuration validation, Identity-ready persistence, policy authorization, ProblemDetails, rate limiting, health checks, Serilog, OpenAPI, and an outbox worker foundation.

## Production status

This is not yet production complete. See [TODO.md](TODO.md) for phased requirements, risks, and next actions.
