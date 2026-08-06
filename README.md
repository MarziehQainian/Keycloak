# Keycloak Demo API

A small .NET 10 Web API secured by Keycloak. Every credential in this repository is for local development only.

## Prerequisites

- .NET 10 SDK
- Docker with Docker Compose

## Run locally

Start Keycloak:

```powershell
docker compose up -d
docker compose ps
```

The realm is imported automatically. Keycloak is ready when the `keycloak` service is healthy.

Start the API in another terminal:

```powershell
dotnet run --project KeycloakDemo.Api
```

Local URLs:

- Keycloak: http://localhost:8080
- Keycloak admin console: http://localhost:8080/admin
- Swagger UI: http://localhost:5099/swagger
- API base URL: http://localhost:5099

The default Keycloak bootstrap administrator is `localadmin` / `localadmin`. Override it by copying `.env.example` to `.env` and changing the values. `.env` is ignored by Git.

## Get an access token

The imported public Swagger client allows the password grant strictly to make local command-line testing easy. Do not use this flow or these credentials in production.

Normal development user (`demo-user` / `user-password`):

```powershell
$userToken = (Invoke-RestMethod -Method Post `
  -Uri http://localhost:8080/realms/demo/protocol/openid-connect/token `
  -ContentType 'application/x-www-form-urlencoded' `
  -Body @{ client_id = 'keycloak-demo-swagger'; grant_type = 'password'; username = 'demo-user'; password = 'user-password' }).access_token
```

Development administrator (`demo-admin` / `admin-password`):

```powershell
$adminToken = (Invoke-RestMethod -Method Post `
  -Uri http://localhost:8080/realms/demo/protocol/openid-connect/token `
  -ContentType 'application/x-www-form-urlencoded' `
  -Body @{ client_id = 'keycloak-demo-swagger'; grant_type = 'password'; username = 'demo-admin'; password = 'admin-password' }).access_token
```

In Swagger, select **Authorize** and paste either access token. Swagger sends it as a Bearer token.

## Test the endpoints

```powershell
# Public: 200 without a token
Invoke-RestMethod http://localhost:5099/api/public

# Profile: 401 without a token
Invoke-WebRequest http://localhost:5099/api/profile -SkipHttpErrorCheck

# Profile: 200 for either development user
Invoke-RestMethod http://localhost:5099/api/profile -Headers @{ Authorization = "Bearer $userToken" }

# Admin: 403 for the normal user
Invoke-WebRequest http://localhost:5099/api/admin -Headers @{ Authorization = "Bearer $userToken" } -SkipHttpErrorCheck

# Admin: 200 for the administrator
Invoke-RestMethod http://localhost:5099/api/admin -Headers @{ Authorization = "Bearer $adminToken" }
```

Stop Keycloak when finished:

```powershell
docker compose down
```

For non-local environments, use HTTPS, disable direct access grants, and provide secure credentials and configuration through an appropriate secret store.
