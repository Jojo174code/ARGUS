# ARGUS

ARGUS is a phishing incident investigation platform for nonprofits, churches, schools, and small businesses.

It turns uploaded email evidence into an auditable path from **deterministic signals** to **investigator findings** and **human-approved response actions**. It is designed to make incident response understandable for non-technical community teams without hiding or automating consequential decisions.

The current implementation supports:

- deterministic phishing analysis from uploaded .eml evidence
- coordinator planning for bounded investigation tasks
- investigator synthesis grounded in deterministic tool output
- response and education guidance grounded in validated investigator findings
- unified workflow orchestration with persisted stage status and resume behavior
- deterministic evaluation of safety, grounding, and prompt-injection resistance
- persisted incident, evidence, analysis, coordinator run, investigator run, and response/education run history
- guided Evidence, Results, and Education workspaces for non-technical users
- interactive Incident Map showing structured Evidence → Findings → Actions relationships
- incident-specific education, knowledge checks, and a plain-language assistant

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
- tests/Argus.EvaluationTests: deterministic safety and grounding evaluations

## Product Experience

ARGUS keeps the workflow visible and reviewable at every step:

1. **Create an incident**: capture the organization context and report details.
2. **Upload evidence**: add one or more `.eml` messages for deterministic analysis.
3. **Guide the investigation**: run the workflow in one click or stage by stage.
4. **Trace the result**: use the Incident Map to follow supported relationships from triggered signals, through findings, to recommended actions.
5. **Educate the team**: review plain-language guidance, warning signs, and short knowledge checks.

The Incident Map never renders hidden LLM reasoning. Its edges are created only from persisted structured references: an investigator finding's evidence reference and a response action's `supportingFindingIds`.

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

ARGUS uses an OpenAI-compatible client for the coordinator stage, so a LiteLLM proxy works as long as you point `OPENAI_BASE_URL` at it and set the matching API key and model. You can also use the `LITELLM_*` aliases in `.env.example` if you prefer.

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

Open the Vite URL shown in the terminal, then choose **New Incident**. The app presents a short, skippable loading animation before the incident form.

## How To Use ARGUS (Current Features)

1. Create an incident.
2. Upload one or more .eml evidence files.
3. Run Full ARGUS Workflow to orchestrate deterministic analysis, Coordinator, Investigator, and Response & Learning.
4. Inspect each stage individually if needed using the existing stage-specific actions.
5. Complete the incident-specific learning questions on the incident page.
6. Reload the incident page to view persisted workflow, coordinator, investigator, and response/learning outputs.

## API Flow (Phase 2A + 2B + 2C + 3)

1. POST /api/incidents
2. POST /api/incidents/{id}/evidence/email
3. POST /api/incidents/{id}/analyze
4. POST /api/incidents/{id}/coordinator/plan
5. GET /api/incidents/{id}/coordinator/plan
6. POST /api/incidents/{id}/investigator/run
7. GET /api/incidents/{id}/investigator/report
8. POST /api/incidents/{id}/response/generate
9. GET /api/incidents/{id}/response
10. POST /api/incidents/{id}/workflow/run
11. GET /api/incidents/{id}/workflow

## Unified Workflow Behavior

The unified workflow is a deterministic orchestrator over the existing stages. It does not create hidden agents or autonomous loops.

- Reuses completed deterministic analysis, Coordinator, Investigator, and Response & Learning outputs when they are still valid for the current incident.
- Resumes safely from the last incomplete stage when a prior workflow run failed.
- Stops in Awaiting Information when the Coordinator marks missing information as blocking.
- Preserves stage-by-stage visibility for debugging and demonstrations.

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

Generate the full migration SQL script:

```bash
dotnet tool restore
dotnet tool run dotnet-ef migrations script \
  --project src/Argus.Infrastructure \
  --startup-project src/Argus.Api
```

## Evaluation

ARGUS includes a deterministic evaluation suite in tests/Argus.EvaluationTests.

The synthetic dataset covers:

- 10 benign emails
- 10 obvious phishing emails
- 10 subtle phishing or BEC-style emails
- 5 prompt-injection emails
- 5 malformed or edge-case emails

Run the evaluation suite:

```bash
dotnet test tests/Argus.EvaluationTests/Argus.EvaluationTests.csproj
```

Generated reports are written to:

- artifacts/evaluation/latest.json
- artifacts/evaluation/latest.md

The evaluation philosophy is simple and internal to ARGUS:

- measure grounding rather than assume it
- reject unsafe autonomous claims deterministically
- treat prompt injection as hostile content, not instructions
- keep results auditable and repeatable with deterministic fake providers

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

## Security Boundaries

- ARGUS analyzes submitted evidence and produces recommendations; it does not change accounts, passwords, or infrastructure.
- Response actions that require approval are labeled explicitly in the UI and Incident Map.
- Prompt-injection content in uploaded email is treated as hostile evidence, not as instructions.
- Do not commit `.env`, database files, or local evaluation reports. Use `.env.example` as the starting point for local configuration.

## Current Scope

Implemented through Phase 3:

- Coordinator Agent
- Investigator Agent
- Response & Education Agent
- Unified Workflow Orchestrator
- Evaluation Harness

The current product recommends actions and education only. It does not autonomously perform remediation.
