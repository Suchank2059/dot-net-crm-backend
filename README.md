# Backend

The backend is an ASP.NET Core project targeting .NET 10. For local development, Docker Compose runs it at <http://localhost:5002>.

## Run locally

From the repository root, with `.env` configured:

```bash
docker compose -f docker-compose.local.yml up -d --build backend
docker compose -f docker-compose.local.yml logs -f backend
docker compose -f docker-compose.local.yml down
```

The container uses `dotnet watch`, so ordinary source changes are picked up automatically.

## Add a NuGet package

From this directory:

```bash
dotnet add package Package.Name
```

After changing package references, rebuild/recreate the backend container so the new dependency is restored:

```bash
docker compose -f ../docker-compose.local.yml up -d --build backend
```

## Build

With the .NET 10 SDK installed:

```bash
dotnet restore backend.csproj
dotnet build backend.csproj
```
