# Solution Architecture & Integration

---

## Slide 1 — High-Level Architecture

```mermaid
graph TB
    subgraph Customer
        C["🎙️ Customer<br/>Browser / Phone"]
    end

    subgraph Azure["☁️ Microsoft Azure"]
        subgraph App["Voice AI Web App (.NET)"]
            WS["WebSocket Proxy"]
            TOOLS["Tool Execution<br/><i>Auth · Knowledge Base<br/>Business Logic · Guardrails</i>"]
        end
        subgraph AI["Azure AI Foundry"]
            VL["Voice Live API<br/><i>Speech-to-Speech · GPT Realtime<br/>Function Calling</i>"]
        end
    end

    subgraph External["External Systems"]
        SF["Salesforce CRM<br/><i>Bookings · Profiles · Cases</i>"]
    end

    C <-->|"WebSocket<br/>Audio + Events"| WS
    WS <-->|"WebSocket<br/>Audio + AI Events"| VL
    VL -->|"Tool Calls"| TOOLS
    TOOLS -->|"Results"| VL
    TOOLS <-->|"API Gateway"| SF

    style Azure fill:#e8f4fd,stroke:#0078d4,stroke-width:2px
    style App fill:#d0e8ff,stroke:#0078d4
    style AI fill:#d0e8ff,stroke:#0078d4
    style Customer fill:#fff3e0,stroke:#f59e0b
    style External fill:#fce4ec,stroke:#e63946
```

**Key points:**

- **Fully hosted on Microsoft Azure** — AI Foundry, App Services, Entra ID
- **Speech-to-speech** — no separate STT/TTS pipeline, lowest latency
- **Server-side function calling** — LLM decides when to call tools, backend executes securely
- **All customer data stays within Azure tenant** — no third-party processing

---

## Slide 2 — Integration Points

```mermaid
graph TB
    subgraph Azure["☁️ Azure"]
        subgraph App["Voice AI Web App"]
            CORE["🖥️ .NET Orchestration<br/><i>WebSocket Proxy · Tool Execution<br/>Guardrails · Logging</i>"]
        end
        AIF["🧠 Azure AI Foundry<br/><i>Voice Live API · GPT Realtime<br/>Content Safety</i>"]
        APIG["🔀 Azure API Gateway<br/><i>Rate Limiting · Routing<br/>Monitoring</i>"]
        EID["🔐 Azure Entra ID<br/><i>SSO / OAuth · Token Auth<br/>RBAC</i>"]
    end

    subgraph Integrations["Atlas Airways Systems"]
        IVR["📞 IVR / Amazon Connect<br/><i>Call Routing · Phone Channel<br/>Agent Transfer</i>"]
        SFDC["💼 Salesforce CRM<br/><i>Bookings · Customer Profiles<br/>Case Management</i>"]
        SAS["✈️ Atlas Airways Backend Systems<br/><i>Inventory · Payments<br/>Loyalty (SkyPoints)</i>"]
    end

    AIF <--> CORE
    EID --> CORE
    CORE <-->|"via"| APIG
    APIG --> IVR
    APIG --> SFDC
    APIG --> SAS

    style Azure fill:#e8f4fd,stroke:#0078d4,stroke-width:2px
    style App fill:#d0e8ff,stroke:#0078d4
    style Integrations fill:#f5f5f5,stroke:#666
```

**Key points:**

- **Salesforce CRM** — Read/write bookings, customer profiles, case creation via REST API
- **Azure API Gateway** — Single entry point for all backend integrations; auth, throttling, observability
- **IVR / Amazon Connect** — Voice bot deployed as a contact flow; seamless handoff to human agents
- **Azure Entra ID** — Unified identity across all systems, no API keys in production

---

## Slide 3 — Data Flow & Orchestration

```mermaid
graph LR
    A["🎙️ <b>1. Customer</b><br/><i>Voice input via<br/>browser or phone</i>"]
    B["🖥️ <b>2. Voice AI Web App</b><br/><i>WebSocket proxy<br/>+ tool execution</i>"]
    C["🧠 <b>3. Azure AI Foundry</b><br/><i>GPT Realtime understands<br/>intent + reasons</i>"]
    D["🔊 <b>4. Voice Reply</b><br/><i>Speech streamed<br/>back in real time</i>"]

    A -->|"Audio stream"| B
    B <-->|"Audio + events"| C
    C -->|"Tool calls"| B
    B -->|"Streamed speech"| D

    B --> KB["📚 Knowledge Base<br/><i>Atlas Airways travel policies</i>"]
    B --> INT["🔗 Integrations<br/><i>Salesforce · Bookings<br/>Payments</i>"]

    style A fill:#fff3e0,stroke:#f59e0b
    style B fill:#d0e8ff,stroke:#0078d4
    style C fill:#e8f4fd,stroke:#0078d4
    style D fill:#fff3e0,stroke:#f59e0b
    style KB fill:#f3e5f5,stroke:#9c27b0
    style INT fill:#fce4ec,stroke:#e63946
```

**Orchestration flow:**

1. **Customer** — Voice audio streams to our web app over WebSocket
2. **Voice AI Web App** — Proxies audio to Azure AI Foundry; intercepts and executes tool calls server-side
3. **Azure AI Foundry** — Processes speech natively via GPT Realtime; reasons over context and decides which tools to call
4. **Voice Reply** — Response generated as speech by the model, streamed back through the web app with sub-second latency

**All steps happen in a single streaming round-trip — no handoffs between separate STT → LLM → TTS services.**
