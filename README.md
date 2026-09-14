# ARGUS

ARGUS is an AI-assisted cyber incident clinic that analyzes suspicious email evidence, coordinates a bounded investigation, generates evidence-grounded findings, recommends response actions, and teaches users how to recognize similar threats.

It is designed for small organizations, nonprofits, churches, schools, small businesses, cyber clinics, and teams without a dedicated security operations center. ARGUS supports investigation and learning; it does not replace professional incident response.

## Overview

Small organizations often need to investigate phishing reports without dedicated security analysts. ARGUS provides a guided workflow that combines deterministic email analysis with bounded AI agents, keeping evidence, findings, and recommendations reviewable. It does not autonomously perform destructive remediation.

## Key Features

- Secure `.eml` evidence upload with SHA-256 integrity fingerprints
- Deterministic phishing analysis, explainable risk scoring, email authentication checks, and URL inspection
- MITRE ATT&CK mapping and structured Evidence -> Findings -> Actions visualization
- Coordinator Agent, Investigator Agent, and Response & Education Agent
- Evidence-grounded findings and human-in-the-loop response recommendations
- Incident-specific cybersecurity education, knowledge checks, and plain-language assistance
- Guided workflow UI, persisted workflow progress, and visual results dashboard
- Prompt-injection defenses, structured model outputs, and persisted/auditable investigation state

## How ARGUS Works

```text
Email Evidence (.eml)
	|
	v
Deterministic Analysis
	|
	v
Coordinator Agent
	|
	v
Investigator Agent
	|
	v
Response & Education Agent
	|
	v
Findings + Actions + Education
```

Deterministic analysis happens before AI. Each AI agent has a bounded responsibility and works from structured evidence and prior persisted output.

## Safety Model

- ARGUS does not execute email attachments or automatically visit malicious URLs.
- ARGUS does not change passwords, block accounts, or autonomously remediate production systems.
- Evidence references are validated and unknown references are rejected.
- Security-changing recommendations require human approval.
- Prompt injection in email content is treated as hostile data, not instructions.
- Structured model outputs and deterministic analysis precede AI-assisted conclusions.

## Tech Stack

| Area | Technology |
| --- | --- |
| Backend | .NET 9, ASP.NET Core Web API, Entity Framework Core, MimeKit |
| Storage | PostgreSQL support for relational deployments; InMemory provider for Development and tests |
| Frontend | React 19, TypeScript, Vite |
| AI | OpenRouter; default model `deepseek/deepseek-v4-pro-0813` |
| Testing | .NET unit, integration, and evaluation tests; Vitest and Testing Library |

## Project Structure

```text
ARGUS/
├── src/
│   ├── Argus.Api/            # API host, controllers, startup configuration
│   ├── Argus.Application/    # use cases, validation, agents, investigation tools
│   ├── Argus.Domain/         # domain entities and core models
│   ├── Argus.Infrastructure/ # persistence, email parsing, OpenRouter client, migrations
│   └── argus-web/            # React frontend
├── tests/                    # unit, integration, and deterministic evaluation projects
├── .env.example              # safe environment-variable template
└── README.md
```

## Prerequisites

- Git
- .NET SDK 9.x
- Node.js 20+ and npm 10+
- An OpenRouter API key
- PostgreSQL only when running with the PostgreSQL configuration; Development uses InMemory by default

## Quick Start

### 1. Clone the repository

```bash
git clone https://github.com/Jojo174code/ARGUS.git
cd ARGUS
```

### 2. Create local environment settings

macOS/Linux:

```bash
cp .env.example .env
```

Windows PowerShell:

```powershell
Copy-Item .env.example .env
```

Edit `.env` with your OpenRouter credential. Do not commit this file.

```dotenv
OPENROUTER_API_KEY=your-openrouter-key
OPENROUTER_MODEL=deepseek/deepseek-v4-pro-0813
OPENROUTER_BASE_URL=https://openrouter.ai/api/v1
OPENROUTER_SITE_URL=http://localhost
OPENROUTER_APP_NAME=ARGUS
OPENROUTER_TIMEOUT_SECONDS=120
OPENROUTER_MAX_RETRIES=2
```

### 3. Run the backend

```bash
dotnet restore
dotnet run --project src/Argus.Api
```

The Development API listens at `http://localhost:5056`. Development defaults to an InMemory database, so no PostgreSQL setup is required for a first run.

### 4. Run the frontend

In a second terminal:

```bash
cd src/argus-web
npm install
npm run dev -- --host localhost --port 5173
```

Open `http://localhost:5173`.

### 5. Follow the workflow

**New Incident** -> **Evidence** -> upload `.eml` -> **Guide** -> run the workflow -> **Results** -> **Learn**.

## Verify OpenRouter

When the API runs in Development, verify configuration and authentication without exposing credentials:

```bash
curl -sS http://localhost:5056/api/development/openrouter/status
```

Expected shape:

```json
{
  "provider": "OpenRouter",
  "model": "deepseek/deepseek-v4-pro-0813",
  "authenticationSucceeded": true,
  "httpStatus": 200,
  "error": null
}
```

This endpoint is Development-only. Never paste an API key into issue reports, logs, or source files.

## Using ARGUS

### 1. Create an Incident

Create a case with organization and reporter context.

### 2. Upload Evidence

Upload the suspicious message in `.eml` format. The maximum file size is 2 MB.

### 3. Use Guide

Run individual stages or the Full Workflow. The workflow starts with deterministic analysis, then runs the Coordinator Agent, Investigator Agent, and Response & Education Agent as applicable.

### 4. Review Results

Use **What ARGUS Found** to review risk, confidence, findings, and recommended actions. Review recommendations before taking security-changing action.

### 5. Learn

Review the incident-specific education content, warning signs, knowledge checks, and plain-language assistant.

## AI Workflow

### Coordinator Agent

Creates a bounded investigation plan and identifies missing information.

### Investigator Agent

Executes supported deterministic investigation tools and synthesizes evidence-grounded findings.

### Response & Education Agent

Produces prioritized, human-approved response recommendations and incident-specific learning material.

All agents use the OpenRouter provider boundary but keep distinct responsibilities.

## Investigation Tools

The Investigator Agent can use these registered deterministic tools:

- Email Metadata
- Email Authentication
- URL Inspection
- MITRE Mapping

## Testing

Run all .NET tests from the repository root:

```bash
dotnet test
```

Run frontend tests and a production build:

```bash
cd src/argus-web
npm test
npm run build
```

Run deterministic evaluations:

```bash
dotnet test tests/Argus.EvaluationTests/Argus.EvaluationTests.csproj
```

Evaluation output is generated under `artifacts/evaluation/` and is intentionally ignored. Evaluation results use deterministic fake providers and are not live-AI performance claims.

## Demo Evidence

Use only safe, synthetic email evidence included in the test projects. Do not test with active malware, credentials, or URLs you do not trust.

## Configuration

| Variable | Required | Default | Purpose |
| --- | --- | --- | --- |
| `OPENROUTER_API_KEY` | Yes | None | OpenRouter API credential |
| `OPENROUTER_MODEL` | No | `deepseek/deepseek-v4-pro-0813` | OpenRouter model identifier |
| `OPENROUTER_BASE_URL` | No | `https://openrouter.ai/api/v1` | OpenRouter API base URL |
| `OPENROUTER_SITE_URL` | No | None | Optional OpenRouter HTTP-Referer value |
| `OPENROUTER_APP_NAME` | No | `ARGUS` | OpenRouter application title |
| `OPENROUTER_TIMEOUT_SECONDS` | No | `120` | HTTP request timeout, 10-300 seconds |
| `OPENROUTER_MAX_RETRIES` | No | `2` | Transient request retry count, 0-3 |
| `VITE_API_BASE_URL` | No | `http://localhost:5056` | Frontend API base URL |

Environment variables take precedence over application configuration. Restart the backend after changing `.env`.

## Troubleshooting

### OpenRouter 401 or authentication failure

- Verify `OPENROUTER_API_KEY` in `.env`.
- Restart the backend after updating `.env`.
- Use the Development-only status endpoint above to check authentication.
- Check for stale shell environment variables, which take precedence over `.env` values.

### Address already in use

On macOS/Linux, identify the process using the API port:

```bash
lsof -i :5056
```

Stop the stale process, then run the API again.

### Frontend cannot reach the backend

- Confirm the API is running at `http://localhost:5056`.
- Confirm the frontend is running at `http://localhost:5173`.
- Check `VITE_API_BASE_URL` if you changed the API address.
- The default CORS configuration permits `http://localhost:5173`.

### Workflow unavailable

First check the Development OpenRouter status endpoint. Then confirm that an `.eml` evidence file has been uploaded and the backend is still running.

## API Overview

| Method | Route | Purpose |
| --- | --- | --- |
| `POST` | `/api/incidents` | Create an incident |
| `GET` | `/api/incidents/{id}` | Get an incident |
| `POST` | `/api/incidents/{id}/evidence/email` | Upload email evidence |
| `POST` | `/api/incidents/{id}/analyze` | Run deterministic analysis |
| `POST` | `/api/incidents/{id}/coordinator/plan` | Run the Coordinator Agent |
| `POST` | `/api/incidents/{id}/investigator/run` | Run the Investigator Agent |
| `POST` | `/api/incidents/{id}/response/generate` | Run Response & Education |
| `POST` | `/api/incidents/{id}/workflow/run` | Run the unified workflow |
| `GET` | `/api/incidents/{id}/workflow` | Get persisted workflow progress |

Development Swagger is available when the API runs in Development.

## Development Principles

- Deterministic analysis before agentic processing
- Evidence before conclusions
- Bounded agents with explicit responsibilities
- Human approval for consequential action
- Transparent, structured findings
- No autonomous destructive remediation

## License

No license file is currently included in this repository. Do not assume reuse rights without permission from the repository owner.

## Project Status

ARGUS is an actively developed cybersecurity research and competition project. Treat it as experimental software, not as a production replacement for professional incident response.

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
