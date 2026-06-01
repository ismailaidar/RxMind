# RxMind — AI Specialty Pharmacy Assistant

RxMind is an internal tool for specialty pharmacy staff. When a prescription comes in — via phone, fax, or walk-in — a pharmacist or admin enters the details and RxMind runs an automated analysis: clinical safety check, insurance and PA requirements, delivery timeline, and financial assistance. The result is a structured report the staff can act on immediately.

**The problem it solves:** processing a specialty prescription manually involves multiple people, multiple phone calls, and can take hours. RxMind runs the full pipeline in seconds.

**Who uses it:** pharmacists and admins (Entra ID roles: `Pharmacist`, `Admin`). Patients do not interact with this system.

---

## How it works

A pharmacist enters prescription details received from a patient or prescriber. The input passes through 4 agents in order:

```
Staff enters prescription details
    ↓
1. Intake Agent       — extracts patient name, medication, dosage, insurance, prescriber
    ↓
2. Clinical Agent     — checks drug interactions, dosage safety, clinical warnings
    ↓                   (searches the knowledge base for formulary info)
3. Operations Agent   — identifies PA requirements, delivery timeline, financial assistance
    ↓                   (searches the knowledge base for policy info)
4. Orchestrator Agent — compiles a professional summary report for staff to act on
    ↓
Staff-facing analysis report
```

Each agent sees what the previous one wrote, adds its own section, and passes it along.

---

## The knowledge base (RAG)

The clinical and operations agents don't just rely on the model's memory — they can search a real knowledge base built from two PDF documents:

- `RxMind_Formulary.pdf` — drug coverage and formulary info
- `RxMind_Policies.pdf` — PA requirements, delivery timelines, financial assistance

On first startup, the app:
1. Extracts text from both PDFs using **Azure Content Understanding**
2. Splits the text into 500-word chunks
3. Uploads every chunk to **Azure AI Search**

On every restart after that, it skips this step — the index already exists.

When an agent needs information, it calls the `SearchKnowledgeBase` tool, which searches the index and returns the 3 most relevant chunks.

---

## Tech stack

| What | Tool |
|---|---|
| AI agents & workflow | Microsoft Agents AI (sequential workflow) |
| Language model | Azure OpenAI (gpt-4.1-mini) |
| PDF text extraction | Azure Content Understanding |
| Knowledge base search | Azure AI Search |
| Backend API | ASP.NET Core (C#) |
| Frontend | ASP.NET Core Razor Pages |

---

## Project structure

```
RxMind/
├── index.html                        ← open this in your browser
├── .env                              ← your Azure keys go here
└── src/
    ├── RxMind.Api/
    │   └── Program.cs                ← startup + API endpoint
    └── RxMind.Agents/
        ├── RxMindWorkflow.cs         ← the 4 agents and sequential workflow
        ├── DocumentExtractor.cs      ← PDF → markdown text
        ├── SearchIndexService.cs     ← creates the index + uploads chunks
        ├── KnowledgeBaseService.cs   ← searches the index
        ├── Config.cs                 ← reads environment variables
        └── files/
            ├── RxMind_Formulary.pdf
            └── RxMind_Policies.pdf
```

---

## Setup

### 1. Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- An Azure account with these services set up:
  - Azure OpenAI
  - Azure AI Search
  - Azure Content Understanding (Azure AI Services)

### 2. Configure your environment

Open `.env` in the project root and fill in your values:

```
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com
AZURE_MODEL_DEPLOYMENT=gpt-4.1-mini
AZURE_SEARCH_ENDPOINT=https://your-search.search.windows.net
CONTENT_UNDERSTANDING_ENDPOINT=https://your-resource.services.ai.azure.com/
```

### 3. Log in to Azure

The app uses your Azure login instead of API keys (DefaultAzureCredential):

```bash
az login
```

### 4. Run the API

```bash
cd src/RxMind.Api
dotnet run
```

The API starts at `http://localhost:5033`. On first run it will extract the PDFs and build the search index — this takes about 30–60 seconds.

### 5. Open the web app

```bash
cd src/RxMind.Web
dotnet run
```

Navigate to `https://localhost:7000`. Sign in with your Entra ID account (must have `Pharmacist` or `Admin` role assigned). Enter prescription details received from a patient or prescriber and click **Run Analysis**.

---

## Example input

A pharmacist enters details received from a patient call:

```
Patient: Sarah Chen
Prescriber: Dr. Patel
Medication: Humira 40mg — Crohn's disease
Insurance: BlueCross
```

RxMind returns a structured report covering the intake summary, clinical warnings, PA requirements, delivery timeline, and any financial assistance programs available.

---

## API

`POST http://localhost:5033/process`

```json
// Request
{ "input": "your prescription request here" }

// Response
{ "response": "the full pharmacy team report" }
```
