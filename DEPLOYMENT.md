# DEPLOYMENT

See TODO.md for current implementation status. This document is part of the production documentation set and must be expanded as each phase is completed.

## Render backend environment

Use `.env.render.example` as the Render Dashboard environment-variable checklist for the `MindUnlocking.Api` web service. Render automatically provides `PORT`; the API binds to `http://0.0.0.0:$PORT` when `PORT` exists and `ASPNETCORE_URLS` is not already set.

Required production values:

- `ASPNETCORE_ENVIRONMENT=Production`
- `Jwt__Issuer`: the public backend URL, for example `https://YOUR-RENDER-SERVICE.onrender.com`
- `Jwt__Audience`: the API audience expected by clients, normally `mind-unlocking-api`
- `Jwt__SigningKey`: a strong random secret with at least 32 characters
- `Sql__ConnectionString`: the SQL Server or Azure SQL connection string
- `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, etc.: each frontend origin allowed to call the API

Do not upload a real `.env` file or commit production secrets. Keep secrets in Render environment variables.
