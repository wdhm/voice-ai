# Atlas Airways Voice AI Demo – Implementation Plan

## Problem Statement
Atlas Airways has issued an RFI requiring a live voice bot demo covering 4 scenarios: general info retrieval, personalized booking lookup, deterministic transactions, and complex policy interpretation. The demo must be voice-driven, handle variations/interruptions, and clearly state what is real vs simulated.

## Proposed Approach
- **Voice Engine**: Azure AI Voice Live API (via Microsoft Foundry) — provides end-to-end speech-to-speech with built-in STT, LLM, and TTS. No manual orchestration needed.
- **Backend**: .NET 10 minimal API — acts as the middle-tier WebSocket proxy between the browser and Voice Live API, hosts function-calling handlers (tool implementations), and serves mock data.
- **Frontend**: Vite (vanilla TypeScript) — minimal UI for microphone capture, audio playback, and conversation transcript display.
- **Model**: `gpt-realtime` via Voice Live API — native speech-to-speech with lowest latency. Audio goes directly through the model without separate STT/TTS pipeline hops.
- **Voice**: Azure standard HD voice (e.g. `en-US-Ava:DragonHDLatestNeural`) for natural output.
- **SDK**: `Azure.AI.VoiceLive` NuGet package for .NET WebSocket integration.

## Latency Optimization Strategy
Every layer is tuned to minimize end-to-end response time:

| Layer | Optimization |
|-------|-------------|
| **Model** | `gpt-realtime` — native audio-in/audio-out, no STT→LLM→TTS chain. Single round-trip. |
| **Region** | `swedencentral` — co-locate backend and Foundry resource in the same region to eliminate cross-region network hops. |
| **Backend** | .NET 10 minimal API with Kestrel — raw WebSocket passthrough, no middleware overhead. Binary audio frames forwarded without serialization. |
| **Data** | All knowledge base and mock data loaded into memory at startup. Function tools return in <1ms — no DB queries, no HTTP calls. |
| **WebSocket** | Single persistent connection per session (browser↔backend↔Voice Live). No HTTP request/response overhead. |
| **Turn detection** | `azure_semantic_vad` with 500ms silence threshold — responds as soon as the user finishes speaking. Filler word removal prevents false barge-ins. |
| **Audio format** | PCM16 at 24kHz — native format for Voice Live, no transcoding needed. |
| **Frontend** | Stream audio chunks directly to WebSocket as they arrive from the mic — no buffering. Play response audio incrementally as chunks arrive. |
| **Echo cancellation** | Server-side (`server_echo_cancellation`) — avoids client-side processing delay. |
| **Function calls** | All tool implementations are synchronous in-memory lookups — no async I/O or external service calls during the conversation. |

## Architecture

```
┌─────────────┐   WebSocket    ┌─────────────────┐   WebSocket    ┌──────────────────┐
│  Vite UI     │ ◄────────────► │  .NET 10 API     │ ◄────────────► │  Voice Live API  │
│  (browser)   │   audio/text   │  (middle-tier)   │   audio/events │  (Azure Foundry) │
└─────────────┘                 └─────────────────┘                 └──────────────────┘
                                       │
                                       │ function calls
                                       ▼
                                ┌─────────────────┐
                                │  Mock Services   │
                                │  - Atlas KB (RAG)  │
                                │  - Booking DB    │
                                │  - Baggage rules │
                                └─────────────────┘
```

## Project Folder Structure

```
voice-ai/
├── plan.md                              # This plan
├── README.md                            # How to run the demo
├── backend/VoiceBot/
│   ├── VoiceBot.csproj                  # .NET 10 project with NuGet refs
│   ├── Program.cs                       # App entry, WebSocket endpoint, health + reset endpoints
│   ├── VoiceLiveHandler.cs              # Voice Live connection, event loop, browser↔API bridge
│   ├── SystemPrompt.cs                  # System prompt with scope guardrails
│   ├── MockData.cs                      # Mock bookings + baggage pricing (JSON file persistence)
│   ├── Tools/
│   │   ├── ToolRegistry.cs              # Tool registration and dispatch
│   │   ├── KnowledgeBaseTool.cs         # Searches Atlas Airways markdown content (in-memory keyword matching)
│   │   ├── AuthenticationTool.cs        # Validates booking references
│   │   ├── BookingTool.cs               # Returns booking details
│   │   ├── BaggageTool.cs               # Get options + add baggage (persists changes)
│   │   └── EscalationTool.cs            # Logs escalation, returns handoff message
│   └── Data/
│       ├── atlas-cabin-baggage.md          # Cabin bag rules, dimensions, weight limits
│       ├── atlas-checked-baggage.md        # Checked baggage allowances, fees, size limits
│       ├── atlas-sports-equipment.md       # Sports/special items (hockey, golf, ski, bikes)
│       ├── atlas-travel-rules.md           # Ticket types, rebooking/cancellation policies
│       ├── atlas-faq.md                    # 15 common Q&As
│       ├── atlas-contact-info.md           # Phone numbers, chat, social media, hours
│       ├── atlas-skypoints.md              # Loyalty tiers, points, benefits
│       ├── atlas-check-in.md               # Online/airport check-in, deadlines
│       ├── atlas-travel-extras.md          # Seats, meals, Wi-Fi, lounges, upgrades
│       ├── atlas-delays-cancellations.md   # EU261 rights, compensation, claims
│       ├── atlas-special-assistance.md     # Wheelchair, children, pets, medical
│       └── bookings-state.json           # Auto-generated: persisted booking state
├── frontend/
│   ├── index.html                       # Layout: mic button + transcript + booking panel
│   ├── package.json
│   ├── tsconfig.json
│   ├── vite.config.ts
│   └── src/
│       ├── main.ts                      # Entry: mic toggle, state machine, wiring
│       ├── audio.ts                     # PCM16 24kHz mic capture + audio playback
│       ├── websocket.ts                 # WebSocket client with typed callbacks
│       ├── ui.ts                        # Chat bubbles, status text, baggage options card
│       ├── booking.ts                   # Booking details panel (slides in on auth)
│       ├── system-events.ts             # System event cards (knowledge retrieval, auth, etc.)
│       └── style.css                    # Airline theme, animations, booking panel, options card
└── SAS_RFI_Voicebot_Demo_Scenarios_For_Vendors.docx  # Original RFI document
```

## Atlas Airways Demo Scenarios → Implementation Mapping

### Scenario 1: General Information (Knowledge Retrieval)
> "Can I bring a cabin bag on my flight?"
- **Capability**: Knowledge retrieval from trusted source, grounded response, follow-ups
- **Implementation**: Function tool `search_knowledge_base` searches 11 markdown files with keyword matching, returns top 3 results with source attribution.
- **UI visualization**: 🔍 System event card shows "Searching policy documents..." → "Found 2 source(s): cabin baggage, checked baggage"
- **What's real**: Voice interaction, LLM reasoning, grounding against policy text
- **What's simulated**: Knowledge base is local markdown (sourced from public Atlas Airways content)

### Scenario 2: Personalized Information (Auth + Backend Lookup)
> "I haven't received my booking confirmation, is everything ok?"
- **Capability**: Authentication, backend data retrieval, handling missing data
- **Implementation**:
  1. `authenticate_customer` — asks for booking ref, validates against mock DB
  2. `get_booking_details` — returns booking status, passenger info, email status
- **UI visualization**: 🔐 Auth card → 📋 Data retrieval card → Booking panel slides in showing flight details
- **Mock booking**: AA5678 (Anna Lindström, ARN→LHR, confirmation email NOT sent)
- **What's real**: Voice interaction, multi-turn auth flow, data retrieval logic
- **What's simulated**: Booking database (in-memory with JSON persistence)

### Scenario 3: Deterministic Transaction (Modify Booking)
> "I want to add baggage to my booking"
- **Capability**: Authentication, booking retrieval, transaction execution, confirmation
- **Implementation**:
  1. Reuse `authenticate_customer` + `get_booking_details`
  2. `get_baggage_options` — returns options with pricing, displayed visually on screen
  3. `add_baggage_to_booking` — executes transaction, persists to disk, updates booking panel
- **UI visualization**: Options card with 5 baggage choices + prices → ✅ Transaction confirmed card → Booking panel updates with green "+" items
- **System prompt optimization**: Bot summarizes options briefly and refers to screen instead of reading all 5 aloud
- **Mock booking**: AA1234 (Erik Johansson, CPH→OSL, no extra baggage yet)
- **What's real**: Voice interaction, multi-step transaction with confirmation
- **What's simulated**: Booking system, payment processing

### Scenario 4: Complex Interaction (Policy Interpretation + Escalation)
> "Does my hockey bag and my club go as one colli?"
- **Capability**: Policy interpretation, handling ambiguity, clarification questions, escalation
- **Implementation**:
  1. `search_knowledge_base` — returns sports equipment policy content
  2. System prompt instructs bot to ask clarifying questions when ambiguous
  3. `escalate_to_agent` — triggered when bot cannot confidently answer
- **UI visualization**: 🔍 Knowledge retrieval card → 🔄 Escalation card (if needed)
- **Mock booking**: AA9012 (Magnus Berg, CPH→JFK, sports equipment flagged)
- **What's real**: Voice interaction, ambiguity handling, clarification dialogue, escalation decision
- **What's simulated**: Agent handoff (logs escalation intent)

## Voice Live API Configuration

Session configured via `VoiceLiveSessionOptions` in `VoiceLiveHandler.cs`:
- **Model**: `gpt-realtime` (env: `AZURE_VOICELIVE_MODEL`)
- **Voice**: `en-US-Ava:DragonHDLatestNeural` (env: `AZURE_VOICELIVE_VOICE`)
- **Audio**: PCM16 input/output, 24kHz
- **VAD**: Azure semantic VAD, 500ms silence, filler word removal
- **Noise**: Azure deep noise suppression (server-side)
- **Echo**: Server-side echo cancellation
- **Tool choice**: Auto
- **Modalities**: Text + Audio
- **Initial greeting**: Triggered via `SystemMessageItem`

## Implementation Status

### ✅ Completed
- [x] Azure AI Foundry resource created
- [x] .NET 10 backend scaffolded with Voice Live SDK + Azure.Identity
- [x] WebSocket proxy: browser ↔ .NET ↔ Voice Live API
- [x] Session configuration with all optimizations
- [x] Initial greeting via SystemMessageItem
- [x] 6 function tools implemented and registered
- [x] 11 Atlas Airways knowledge base markdown files (sourced from public Atlas Airways content)
- [x] Mock booking data with JSON file persistence across restarts
- [x] Reset endpoint (`POST /reset`) to restore defaults before demos
- [x] Vite frontend with airline-themed UI
- [x] PCM16 24kHz audio capture and playback
- [x] Chat-style transcript with "You" / "Assistant" labels and paragraph formatting
- [x] System event cards in transcript (🔍 knowledge retrieval, 🔐 auth, 📋 data, ✅ transaction, 🔄 escalation)
- [x] Booking details panel (slides in on authentication, updates live on changes)
- [x] Baggage options card (visual display of options with pricing)
- [x] System prompt with scope guardrails + concise option presentation instruction
- [x] Console logging for session lifecycle and function calls
- [x] End-to-end tested with live Voice Live API

### 📋 Remaining (Demo Polish)
- [ ] System prompt tuning based on demo rehearsals
- [ ] Prepare "what's real vs simulated" disclosure notes
- [ ] Test all 4 scenarios with variations, interruptions, incomplete inputs

## Key Decisions & Assumptions
1. **No avatar** — keeping it minimal; can add later if needed
2. **English language primarily** — demo in English, but system prompt allows responding in customer's language. VAD supports `["en", "sv", "no", "da"]` if needed.
3. **Real Atlas Airways public content** — baggage/travel policies sourced from Atlas Airways website, stored locally as markdown
4. **Mocked backend systems** — bookings, CRM, transactions simulated with JSON persistence; clearly disclosed
5. **No telephony** — browser-based demo only (Voice Live supports ACS telephony if needed later)
6. **Single region** — `swedencentral` (closest to Atlas Airways, supports HD voices)
7. **Microsoft Entra ID auth** via `DefaultAzureCredential` — no API keys
8. **Multimodal UX** — baggage options shown visually instead of read aloud for speed
9. **Demo transparency** — system event cards make each step visible to the audience

## Tech Stack Summary
| Component | Technology |
|-----------|-----------|
| Voice Engine | Azure AI Voice Live API |
| LLM | GPT Realtime (via Voice Live, fully managed) |
| TTS Voice | en-US-Ava:DragonHDLatestNeural |
| Backend | .NET 10 minimal API |
| Voice SDK | Azure.AI.VoiceLive 1.1.0-beta.3 (NuGet) |
| Frontend | Vite 8 + vanilla TypeScript |
| Mock Data | In-memory + JSON file persistence |
| Auth | Microsoft Entra ID via Azure.Identity (DefaultAzureCredential) |
| Hosting | Local dev (demo day: Azure App Service or local) |
