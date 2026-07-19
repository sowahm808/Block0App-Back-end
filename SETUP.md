# SETUP

## Local API URLs

The backend Development profile listens on:

- `http://localhost:5000`
- `https://localhost:5001`

If an Angular app is using a base URL of `https://localhost:5000/api`, the browser will fail before the request reaches ASP.NET Core with `net::ERR_SSL_PROTOCOL_ERROR`, because port `5000` is the HTTP endpoint. Use one of these base URLs instead:

- `http://localhost:5000/api`
- `https://localhost:5001/api`

The API also keeps the canonical versioned route under `/api/v1`, so `/api/auth/register` and `/api/v1/auth/register` are both accepted for auth endpoints during local development.

If you choose HTTPS, trust the ASP.NET Core development certificate on your machine before calling the API from the browser.
