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

## Render database setup

The backend currently uses Entity Framework Core SQL Server via `UseSqlServer`, so it needs a SQL Server-compatible database. Render managed databases are PostgreSQL, which cannot be used by this codebase unless the persistence layer is migrated from SQL Server to PostgreSQL/Npgsql.

### Recommended: Azure SQL or another managed SQL Server

1. Create a SQL Server-compatible database, for example Azure SQL Database.
2. Create the `MindUnlocking` database and a SQL login/user with permission to read, write, and run migrations for that database.
3. Allow network access from Render to the database. For Azure SQL, configure firewall/networking according to your security requirements.
4. In Render, set `Sql__ConnectionString` to a production SQL Server connection string:

   ```text
   Server=tcp:YOUR-AZURE-SQL-SERVER.database.windows.net,1433;Initial Catalog=MindUnlocking;Persist Security Info=False;User ID=YOUR-SQL-USER;Password=YOUR-SQL-PASSWORD;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
   ```

5. Apply the EF Core migrations from a machine that has network access to the database:

   ```bash
   dotnet ef database update --project src/MindUnlocking.Infrastructure --startup-project src/MindUnlocking.Api
   ```

6. Deploy the Render web service and verify `/health/ready`. That endpoint includes the SQL health check.

### Render-only option: private SQL Server service

If you must keep the database inside Render, create a separate private Docker service that runs a SQL Server Linux container and attach a persistent disk for database files. This is less managed than Azure SQL and is not the preferred production setup.

Use these database service environment variables as a starting point:

```text
ACCEPT_EULA=Y
MSSQL_PID=Express
MSSQL_SA_PASSWORD=YOUR_STRONG_SA_PASSWORD
```

Then set the API service connection string to the private service host name:

```text
Sql__ConnectionString=Server=YOUR-PRIVATE-SQL-SERVICE-NAME,1433;Database=MindUnlocking;User Id=sa;Password=YOUR_STRONG_SA_PASSWORD;Encrypt=True;TrustServerCertificate=True;Connection Timeout=30;
```

Apply migrations against that private database before relying on the API in production.
