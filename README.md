# Voice AI Demo — Atlas Airways Voice Assistant

A browser-based voice assistant for a fictional airline (Atlas Airways), built with **Azure AI Voice Live API**, **.NET 10**, and **Vite**. Clone the repo, deploy one Azure resource, and start talking.

```
Browser (Vite/TS)  ←—WebSocket—→  .NET 10 API  ←—WebSocket—→  Azure Voice Live API
                                       │
                                       ├── 6 function tools (in-memory)
                                       ├── 11 knowledge base files (markdown)
                                       └── 3 mock bookings (JSON persistence)
```

---

## Prerequisites

| Tool | Version | Install |
|------|---------|---------|
| Azure Developer CLI (`azd`) | latest | [aka.ms/azd-install](https://aka.ms/azd-install) |
| Azure CLI (`az`) | 2.60+ | [aka.ms/installazurecli](https://aka.ms/installazurecli) |
| .NET SDK | 10.0 | [dotnet.microsoft.com/download/dotnet/10.0](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Node.js | 18+ | [nodejs.org](https://nodejs.org/) |

You also need an **Azure subscription** with access to Azure AI Services.

---

## 1 — Deploy Azure Resources

The app needs a single Azure resource: an **Azure AI Services** account that provides the Voice Live API endpoint. All infrastructure is defined as code (Bicep) and deployed via `azd`.

```bash
# Log in to Azure (both CLIs)
az login
azd auth login

# Provision the Azure resource (auto-detects your user ID for RBAC)
azd provision
```

`azd` will prompt you for:
- **Environment name** — any name (e.g. `voice-ai-dev`)
- **Azure subscription** — select yours
- **Azure location** — pick a region that supports Voice Live API (e.g. `swedencentral`)

The deployment creates:
- **Azure AI Services** account (S0 SKU) with the Voice Live API
- **Cognitive Services User** role assignment for your account (allows `DefaultAzureCredential` to authenticate)

After provisioning, grab the endpoint:

```bash
azd env get-value AZURE_VOICELIVE_ENDPOINT
```

<details>
<summary>Alternative: deploy with Azure CLI only (no azd)</summary>

```bash
az account set --subscription "<your-subscription-id>"
az group create --name rg-voice-ai --location swedencentral

USER_ID=$(az ad signed-in-user show --query id -o tsv)

az deployment group create \
  --resource-group rg-voice-ai \
  --template-file infra/main.bicep \
  --parameters name=voice-ai currentUserObjectId=$USER_ID

# Get the endpoint
az deployment group show \
  --resource-group rg-voice-ai \
  --name main \
  --query properties.outputs.AZURE_VOICELIVE_ENDPOINT.value -o tsv
```

</details>

---

## 2 — Run the Backend

```bash
cd backend/VoiceBot

# Set the endpoint from the deployment output
# Linux/macOS:
export AZURE_VOICELIVE_ENDPOINT="https://<your-resource>.services.ai.azure.com/"
# Windows PowerShell:
$env:AZURE_VOICELIVE_ENDPOINT = "https://<your-resource>.services.ai.azure.com/"

dotnet run
```

The backend starts on **http://localhost:5000**. You should see:

```
Voice Bot backend starting on http://localhost:5000
Endpoint: https://<your-resource>.services.ai.azure.com/
WebSocket: ws://localhost:5000/ws
```

---

## 3 — Run the Frontend

```bash
cd frontend
npm install   # first time only
npm run dev
```

The frontend starts on **http://localhost:5173**.

---

## 4 — Use the Demo

1. Open **http://localhost:5173** in Chrome or Edge (microphone required)
2. Click the mic button
3. Wait for the greeting — the bot will say "Welcome to Atlas Airways!"
4. Start talking

### Reset before a demo

Reset all booking data to factory defaults:

```bash
curl -X POST http://localhost:5000/reset
```

---

## Demo Scenarios

### Scenario 1 — General Information

> **Say:** "Can I bring a cabin bag on my flight?"

Tests knowledge retrieval from airline policy documents. The bot searches 11 markdown knowledge base files and gives grounded answers with source attribution shown in the UI.

### Scenario 2 — Personalized Information

> **Say:** "I haven't received my booking confirmation, is everything ok?"

When asked for a booking reference, use **AA5678** (Anna Lindström, ARN → LHR). The confirmation email is marked as not sent — the bot will acknowledge this, look up the booking details, and offer to resend the email.

### Scenario 3 — Add Baggage (Transaction)

> **Say:** "I want to add baggage to my booking"

Use booking reference **AA1234** (Erik Johansson, CPH → OSL). The bot walks through available options (displayed visually as a card), confirms pricing, and executes the change. The booking panel on the right updates live.

### Scenario 4 — Complex Policy Question

> **Say:** "Does my hockey bag and my sticks go as one piece?"

Use booking reference **AA9012** (Magnus Berg, CPH → JFK) if context is needed. Tests policy interpretation with the sports equipment knowledge base. If the answer is ambiguous, the bot offers to escalate to a human agent.

### Mock Bookings

| Reference | Passenger | Route | Class | Scenario |
|-----------|-----------|-------|-------|----------|
| AA1234 | Erik Johansson | CPH → OSL | Atlas Go | Add baggage |
| AA5678 | Anna Lindström | ARN → LHR | Atlas Plus | Missing confirmation |
| AA9012 | Magnus Berg | CPH → JFK | Atlas Business | Sports equipment |

---

## Configuration

| Environment Variable | Required | Default | Description |
|----------------------|----------|---------|-------------|
| `AZURE_VOICELIVE_ENDPOINT` | ✅ | — | Azure AI Foundry / AI Services endpoint URL |
| `AZURE_VOICELIVE_MODEL` | — | `gpt-realtime` | Voice Live model name |
| `AZURE_VOICELIVE_VOICE` | — | `en-US-Ava:DragonHDLatestNeural` | TTS voice name |

Environment variables take precedence over `appsettings.json`.

---

## Project Structure

```
voice-ai/
├── infra/                        # Infrastructure as Code (Bicep)
│   ├── main.bicep                # Azure AI Services resource + RBAC
│   └── main.bicepparam           # Default parameters
├── backend/VoiceBot/             # .NET 10 backend
│   ├── Program.cs                # Minimal API (WebSocket, health, reset)
│   ├── VoiceLiveHandler.cs       # WebSocket proxy to Azure Voice Live API
│   ├── SystemPrompt.cs           # LLM system instructions
│   ├── MockData.cs               # In-memory bookings + JSON persistence
│   ├── Tools/                    # Function tools called by the LLM
│   │   ├── ToolRegistry.cs       # Tool registration and dispatch
│   │   ├── KnowledgeBaseTool.cs  # Search knowledge base files
│   │   ├── AuthenticationTool.cs # Validate booking references
│   │   ├── BookingTool.cs        # Retrieve booking details
│   │   ├── BaggageTool.cs        # List/add/remove baggage
│   │   ├── ConfirmationEmailTool.cs # Resend confirmation emails
│   │   └── EscalationTool.cs     # Handoff to human agent
│   └── Data/                     # Knowledge base (11 markdown files)
│       ├── atlas-cabin-baggage.md
│       ├── atlas-checked-baggage.md
│       ├── atlas-sports-equipment.md
│       └── ...
├── frontend/                     # Vite + vanilla TypeScript
│   ├── index.html
│   └── src/
│       ├── main.ts               # App entry, mic button, WebSocket lifecycle
│       ├── websocket.ts          # WebSocket client (audio + events)
│       ├── audio.ts              # PCM16 capture + playback
│       ├── ui.ts                 # Chat transcript, baggage options card
│       ├── booking.ts            # Booking details side panel
│       ├── system-events.ts      # Inline tool-call event cards
│       └── style.css             # Full UI styling
└── README.md
```

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Voice Engine | Azure AI Voice Live API |
| LLM | GPT Realtime (speech-to-speech) |
| TTS Voice | en-US-Ava:DragonHDLatestNeural |
| Backend | .NET 10 minimal API |
| SDK | Azure.AI.VoiceLive 1.1.0-beta.3 |
| Auth | Microsoft Entra ID (DefaultAzureCredential) |
| Frontend | Vite 8 + vanilla TypeScript |
| Data | In-memory + JSON file persistence |

## What's Real vs Simulated

| Real | Simulated |
|------|-----------|
| Voice interaction (speech-to-speech) | Booking database (in-memory mock data) |
| LLM reasoning and function calling | Payment processing |
| Knowledge grounding against policies | CRM / customer profile |
| Multi-turn conversation with interruptions | Agent handoff (logged, not connected) |
| Authentication flow | — |
| Transaction confirmation flow | — |

## Troubleshooting

| Problem | Fix |
|---------|-----|
| `Azure AI Foundry endpoint is required` | Set `AZURE_VOICELIVE_ENDPOINT` env var |
| Auth error (401/403) | Run `az login`, verify you have **Cognitive Services User** role on the resource |
| No microphone prompt in browser | Use Chrome/Edge, check `chrome://settings/content/microphone` |
| Stale booking data | Run `curl -X POST http://localhost:5000/reset` |
| Wrong subscription | Run `az account set --subscription "<id>"` |
