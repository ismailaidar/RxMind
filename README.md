# RxMind — AI Specialty Pharmacy Assistant

RxMind is an internal tool for specialty pharmacy staff. When a prescription comes in — via phone, fax, or walk-in — a pharmacist or admin enters the details and RxMind runs an automated analysis: clinical safety check, insurance and PA requirements, delivery timeline, and financial assistance. The result is a structured report the staff can act on immediately.

**The problem it solves:** processing a specialty prescription manually involves multiple people, multiple phone calls, and can take hours. RxMind runs the full pipeline in seconds.

**Who uses it:** pharmacists and admins (Entra ID roles: `Pharmacist`, `Admin`). Patients do not interact with this system.

---

## Workflow

```
                        ┌─────────────────────────────────────┐
                        │         Pharmacy Staff              │
                        │  (Pharmacist / Admin — Entra ID)    │
                        └──────────────┬──────────────────────┘
                                       │ enters prescription details
                                       ▼
                        ┌─────────────────────────────────────┐
                        │           RxMind Web                │
                        │     ASP.NET Core Razor Pages        │
                        └──────────────┬──────────────────────┘
                                       │ POST /process  (Bearer token)
                                       ▼
                        ┌─────────────────────────────────────┐
                        │           RxMind API                │
                        │       ASP.NET Core Minimal API      │
                        └──────────────┬──────────────────────┘
                                       │
                    ┌──────────────────▼──────────────────────┐
                    │           Sequential Workflow           │
                    │       (Microsoft Agents AI)             │
                    │                                         │
                    │  ┌─────────────────────────────────┐    │
                    │  │  1. Intake Agent                 │   │
                    │  │     Extracts: patient, meds,     │   │
                    │  │     dosage, insurance, prescriber│   │
                    │  └──────────────┬──────────────────┘    │
                    │                 │                       │
                    │  ┌──────────────▼──────────────────┐    │
                    │  │  2. Clinical Agent               │   │
                    │  │     Drug interactions, dosage    │◄──┼──── Azure AI Search
                    │  │     safety, clinical warnings    │   │     (rxmind-knowledge)
                    │  └──────────────┬──────────────────┘    │          ▲
                    │                 │                       │          │
                    │  ┌──────────────▼──────────────────┐    │   ┌──────┴──────────┐
                    │  │  3. Operations Agent             │   │   │  Knowledge Base │
                    │  │     PA requirements, delivery    │◄──┼───│  RxMind_        │
                    │  │     timeline, financial assist.  │   │   │  Formulary.pdf  │
                    │  └──────────────┬──────────────────┘    │   │  RxMind_        │
                    │                 │                       │   │  Policies.pdf   │
                    │  ┌──────────────▼──────────────────┐    │   └─────────────────┘
                    │  │  4. Orchestrator Agent           │   │
                    │  │     Compiles final staff-facing  │   │
                    │  │     analysis report              │   │
                    │  └──────────────┬──────────────────┘    │
                    │                 │                       │
                    └─────────────────┼───────────────────────┘
                                      │
                                      ▼ all agents share one LLM
                        ┌─────────────────────────────────────┐
                        │         Azure OpenAI                │
                        │         gpt-4.1-mini                │
                        └─────────────────────────────────────┘
```

### Startup — knowledge base indexing

Runs once on first startup, skipped on every restart after that:

```
RxMind_Formulary.pdf ──► Azure Content Understanding ──► markdown text
RxMind_Policies.pdf  ──►                              ──► split into 500-word chunks
                                                           ──► Azure AI Search index
```

### Security

```
Browser ──► Entra ID login ──► OIDC cookie (Web)
Web     ──► Entra ID token ──► Bearer JWT  (API validates)
API     ──► DefaultAzureCredential ──► Azure OpenAI / Search / Content Understanding
```

---

## Tech stack

| What | Tool |
|---|---|
| AI agents & workflow | Microsoft Agents AI (sequential workflow) |
| Language model | Azure OpenAI (gpt-4.1-mini) |
| PDF text extraction | Azure Content Understanding |
| Knowledge base search | Azure AI Search |
| Content safety | Azure AI Content Safety |
| Authentication | Microsoft Entra ID (OIDC + JWT bearer) |
| Observability | Application Insights + OpenTelemetry |
| Backend API | ASP.NET Core Minimal API |
| Frontend | ASP.NET Core Razor Pages |
| Retrieval eval | xUnit + custom metrics (Hit@k, MRR, P@k) |

---

## Setup

### 1. Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- Azure CLI — `az login` for local development
- Azure services:
  - Azure OpenAI (gpt-4.1-mini deployment)
  - Azure AI Search
  - Azure AI Content Safety
  - Azure Content Understanding (Azure AI Services)
  - Microsoft Entra ID app registration
  - Application Insights

### 2. Configure your environment

Create a `.env` file in the project root:

```
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com
AZURE_MODEL_DEPLOYMENT=gpt-4.1-mini
AZURE_SEARCH_ENDPOINT=https://your-search.search.windows.net
CONTENT_UNDERSTANDING_ENDPOINT=https://your-resource.services.ai.azure.com/
CONTENT_SAFETY_ENDPOINT=https://your-resource.cognitiveservices.azure.com/
ENTRA_TENANT_ID=your-tenant-id
ENTRA_CLIENT_ID=your-client-id
ENTRA_CLIENT_SECRET=your-client-secret
APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=...
```

### 3. Log in to Azure

```bash
az login
```

### 4. Run the API

```bash
cd src/RxMind.Api
dotnet run
```

Starts at `http://localhost:5033`. On first run, extracts PDFs and builds the search index (~30–60 seconds).

### 5. Run the web app

```bash
cd src/RxMind.Web
dotnet run
```

Navigate to `https://localhost:7000`. Sign in with your Entra ID account (must have `Pharmacist` or `Admin` role assigned).

---

## Retrieval eval

The `tests/RxMind.Eval` project measures whether Azure AI Search returns useful chunks for real pharmacy queries. It runs on every CI pass and fails the build if retrieval regresses.

### How it works

```
golden_dataset.json  ──►  fire 10 queries against live index
                     ──►  check each chunk for relevant keywords
                     ──►  compute Hit@3 / MRR / P@3
                     ──►  assert thresholds  ──►  write eval_report.md
```

### Metrics and thresholds

| Metric | Threshold | What it catches |
|---|---|---|
| Hit@3 | ≥ 70% | Retrieval returns nothing useful for most queries |
| MRR | ≥ 0.50 | Relevant chunk consistently ranks 3rd or lower |
| P@3 | ≥ 0.40 | More than 60% of returned slots are irrelevant noise |
| No empty results | 0 | Index is down or empty |

### Run it

```bash
# requires AZURE_SEARCH_ENDPOINT in .env — skips cleanly if not set
dotnet test tests/RxMind.Eval/
```

After a run, `eval_report.md` is written next to the test DLL with a per-query breakdown showing which chunks were retrieved, their relevance scores, and which keyword matched (or didn't).

---

## Example input

A pharmacist enters details received from a patient call:

| Field | Value |
|---|---|
| Patient | Sarah Chen |
| Prescriber | Dr. Patel |
| Medication | Humira |
| Dosage | 40mg |
| Diagnosis | Crohn's disease |
| Insurance | BlueCross |

RxMind returns a structured report covering clinical warnings, PA requirements, delivery timeline, and financial assistance programs.

