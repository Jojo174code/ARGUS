# ARGUS

ARGUS is a phishing incident investigation platform for nonprofits, churches, schools, and small businesses.

The current implementation supports:

- deterministic phishing analysis from uploaded .eml evidence
- coordinator planning for bounded investigation tasks
- investigator synthesis grounded in deterministic tool output
- persisted incident, evidence, analysis, coordinator run, and investigator run history

## Current Architecture

- Backend: ASP.NET Core 9, EF Core 9, PostgreSQL provider, InMemory test provider
- Frontend: React 19 + Vite + TypeScript
- AI integration: provider abstraction via ILlmClient with OpenAI-compatible implementation

## Repository Layout

- src/Argus.Api: API host and controllers
- src/Argus.Application: use-case services, DTOs, validation
- src/Argus.Domain: domain entities and models
- src/Argus.Infrastructure: persistence, repositories, AI provider, migrations
- src/argus-web: React frontend
- tests/Argus.UnitTests: unit tests
- tests/Argus.IntegrationTests: API integration tests

## Prerequisites

- .NET SDK 9.x
- Node.js 20+
- npm 10+
- PostgreSQL 14+ (for relational local runs)

## Environment Setup

Create a local environment file from .env.example and set values:

```bash
OPENAI_API_KEY=
OPENAI_MODEL=
OPENAI_BASE_URL=https://api.openai.com/v1
```

Backend defaults are in src/Argus.Api/appsettings.json, including:

- ConnectionStrings:ArgusDatabase
- Database:Provider (Postgres or InMemory)

## Running ARGUS

### 1. Restore and build

```bash
dotnet restore
dotnet build
```

### 2. Apply database migrations

```bash
dotnet tool restore
dotnet tool run dotnet-ef database update \
	--project src/Argus.Infrastructure \
	--startup-project src/Argus.Api
```

### 3. Run backend API

```bash
dotnet run --project src/Argus.Api
```

Default local API URL from launch settings:

- http://localhost:5056

### 4. Run frontend

```bash
cd src/argus-web
npm install
npm run dev
```

Set VITE_API_BASE_URL if needed (default client fallback points to http://localhost:5056).

## How To Use ARGUS (Current Features)

1. Create an incident.
2. Upload one or more .eml evidence files.
3. Run deterministic analysis.
4. Generate a coordinator investigation plan.
5. Run investigator for evidence-grounded findings.
6. Reload incident page to view persisted coordinator and investigator outputs.

## API Flow (Phase 2A + 2B)

1. POST /api/incidents
2. POST /api/incidents/{id}/evidence/email
3. POST /api/incidents/{id}/analyze
4. POST /api/incidents/{id}/coordinator/plan
5. GET /api/incidents/{id}/coordinator/plan
6. POST /api/incidents/{id}/investigator/run
7. GET /api/incidents/{id}/investigator/report

## EF Core Migration Workflow

Migrations are stored in src/Argus.Infrastructure/Migrations and use src/Argus.Api as startup.

Add a migration:

```bash
dotnet tool run dotnet-ef migrations add <Name> \
	--project src/Argus.Infrastructure \
	--startup-project src/Argus.Api
```

Apply migrations:

```bash
dotnet tool run dotnet-ef database update \
	--project src/Argus.Infrastructure \
	--startup-project src/Argus.Api
```

Generate SQL script:

```bash
dotnet tool run dotnet-ef migrations script \
	--project src/Argus.Infrastructure \
	--startup-project src/Argus.Api
```

List migrations:

```bash
dotnet tool run dotnet-ef migrations list \
	--project src/Argus.Infrastructure \
	--startup-project src/Argus.Api
```

## Validation Commands

Backend:

```bash
dotnet build
dotnet test
```

Frontend:

```bash
cd src/argus-web
npm run build
npm run test
```

## Current Scope

Implemented through Phase 2B (Coordinator + Investigator).

Phase 2C (Response and Education Agent) is not implemented in this README scope.
