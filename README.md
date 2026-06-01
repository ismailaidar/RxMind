# RxMind — AI Specialty Pharmacy Assistant

RxMind is an AI-powered pharmacy assistant. You describe your prescription request, and a team of AI agents processes it — checking clinical safety, insurance requirements, and delivery timelines — then gives you a clear summary.

---

## How it works

When you submit a request, it passes through 4 agents in order:

```
Patient input
    ↓
1. Intake Agent       — pulls out the key info (name, medication, insurance, prescriber)
    ↓
2. Clinical Agent     — checks drug interactions, dosage safety, clinical warnings
    ↓                   (searches the knowledge base for formulary info)
3. Operations Agent   — checks if Prior Authorization is needed, estimates delivery time, finds financial assistance
    ↓                   (searches the knowledge base for policy info)
4. Orchestrator Agent — compiles everything into a clean, patient-friendly summary
    ↓
Final response
```

Each agent sees what the previous one wrote, adds its own section, and passes it along. The last agent turns it all into something readable.

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
| Frontend | Plain HTML/CSS/JS |

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

### 5. Open the UI

Open `index.html` in your browser. Type a prescription request and hit Submit.

---

## Example request

```
My name is Sarah Chen. My doctor Dr. Patel prescribed Humira 40mg 
for Crohn's disease. I have BlueCross insurance.
```

The app will come back with an intake summary, clinical notes, PA/delivery info, and a patient-friendly final summary.

---

## API

`POST http://localhost:5033/process`

```json
// Request
{ "input": "your prescription request here" }

// Response
{ "response": "the full pharmacy team report" }
```
