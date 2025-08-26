# Roidoya Character Platform

A small, channel-agnostic chat stack designed for multi-channel bots and assistants.  
This monorepo currently includes:

- **gateway-line** (C#/.NET 8): LINE webhook endpoint with **Echo Mode** on **Local** runtime. Validates `X-Line-Signature`, processes message events, implements user locking and queue management, and replies via LINE Reply API.
- **chat-layer** (TypeScript/Node.js): minimal **Echo** service that returns the same text it receives. No channel-specific logic.

MIT-licensed.

---

## Debugging .NET Applications in Visual Studio Code

You can debug .NET applications in Visual Studio Code by following the official Microsoft documentation:

**Reference:** [Debug a .NET console application using Visual Studio Code](https://learn.microsoft.com/en-us/dotnet/core/tutorials/debugging-with-visual-studio-code)

### Steps to Debug

1. **Open your project folder in Visual Studio Code.**
2. **Set a breakpoint:**
  - Open the file you want to debug (e.g., `Program.cs`).
  - Click in the left margin next to the line number, or press `F9`.
3. **Start debugging:**
  - Open the Debug view by clicking the Debug icon on the left sidebar.
  - Click "Run and Debug" and select the appropriate configuration (usually C#).
  - Alternatively, press `F5` to start debugging.
4. **Use the Debug Console:**
  - Interact with your application and inspect/change variable values in the Debug Console tab.
5. **Step through your code:**
  - Use the toolbar or keyboard shortcuts (`F10` for Step Over, `F11` for Step Into, `Shift+F11` for Step Out) to step through your program.
6. **Set conditional breakpoints:**
  - Right-click a breakpoint and select "Edit Breakpoint" to add conditions.
7. **Stop debugging:**
  - Press `Shift+F5` or click the Stop button.

For more details and screenshots, see the [official tutorial](https://learn.microsoft.com/en-us/dotnet/core/tutorials/debugging-with-visual-studio-code).

## Status

- ✅ LINE gateway: **Echo Mode** implementation (Local runtime) with signature validation, event filtering, user locking, queue management, and LINE Reply API integration
- ✅ Chat layer: Echo (no limits, no splitting, channel-agnostic)  
- ✅ Complete: gateway → chat wiring, Echo mode, reply sending from gateway
- ⏳ Future: HTTP mode, Discord/X/Slack gateways, multi-cloud adapters (AWS/GCP), observability

---

## Repository layout

```
roidoya-character-platform/
  /services
    /gateway-line          # C# / .NET 8 (Echo Mode - Local runtime)
      /src/LineBot.Api
    /chat-layer            # TypeScript / Node.js (Echo-only)
      /src
  /docs
    /specs                 # gateway-line spec document(s)
    /issues                # issues for Copilot Coding Agent
  LICENSE
  README.md
```

---

## Design principles

- **Channel-agnostic chat layer** — no LINE/Discord/X rules here.
- **Channel-specific logic stays in the gateway** — signature verification, batching, limits, reply API behavior, etc., live in the respective gateway service.
- **Small, composable services** — easy to extend to new channels and clouds.

---

## Quickstart (local dev)

### Prereqs
- .NET 8 SDK
- Node.js 20+

### 1) Run the LINE gateway (Echo Mode - Local runtime)

**Configure LINE credentials** (choose one of the two methods):

**A. appsettings.Development.json**  
Edit `services/gateway-line/src/LineBot.Api/appsettings.Development.json`:
```json
{
  "Line": { 
    "ChannelSecret": "YOUR_CHANNEL_SECRET_FROM_LINE_CONSOLE",
    "ChannelAccessToken": "YOUR_CHANNEL_ACCESS_TOKEN_FROM_LINE_CONSOLE" 
  }
}
```

**B. Environment variables**  
```bash
# Linux/macOS
export LINE_CHANNEL_SECRET='YOUR_CHANNEL_SECRET'
export LINE_CHANNEL_ACCESS_TOKEN='YOUR_CHANNEL_ACCESS_TOKEN'

# Windows (PowerShell)
$env:LINE_CHANNEL_SECRET='YOUR_CHANNEL_SECRET'
$env:LINE_CHANNEL_ACCESS_TOKEN='YOUR_CHANNEL_ACCESS_TOKEN'
```

**Start the service:**
```bash
cd services/gateway-line/src/LineBot.Api
dotnet run
```

The service will start on `http://localhost:5286` (or another port - check the console output).

**Set up webhook URL (for real LINE integration):**

1. **Development tunnel**: Use ngrok, devtunnel, or similar:
   ```bash
   # Option A: ngrok
   ngrok http 5286
   # Copy the https URL (e.g., https://abc123.ngrok.io)
   
   # Option B: devtunnel (Visual Studio / Azure)
   devtunnel host -p 5286 --allow-anonymous
   # Copy the https URL
   ```

2. **LINE Developers Console**: Set your webhook URL to `{tunnel_url}/line/webhook`
   - Example: `https://abc123.ngrok.io/line/webhook`

**Test the Echo functionality:**

Health check:
```bash
curl http://localhost:5286/
# → "LINE Webhook Gateway - Echo Mode (Local)"
```

Webhook simulation (replace `YOUR_CHANNEL_SECRET` with your actual secret):
```bash
BODY='{"destination":"Uxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx","events":[{"type":"message","mode":"active","timestamp":1234567890123,"webhookEventId":"test-event-123","source":{"type":"user","userId":"U1234567890abcdef1234567890abcdef"},"replyToken":"test-reply-token","message":{"id":"1234567890123","type":"text","text":"Hello Echo!"}}]}'
SECRET='YOUR_CHANNEL_SECRET'
SIG=$(printf '%s' "$BODY" | openssl dgst -sha256 -hmac "$SECRET" -binary | openssl base64)

curl -i -X POST http://localhost:5286/line/webhook \
  -H "Content-Type: application/json" \
  -H "X-Line-Signature: $SIG" \
  -H "X-Line-Request-Id: test-123" \
  --data "$BODY"
# Expect: HTTP/1.1 200 OK
# Check logs for message processing
```

**What the gateway does:**
- ✅ Validates LINE webhook signatures (HMAC-SHA256)  
- ✅ Filters events (only processes text `message` events in `active` mode)
- ✅ Implements user-level locking (prevents concurrent processing per user)
- ✅ Manages local queue with capacity limits (default: 15 messages)
- ✅ Background worker processes messages with QPS throttling (default: 5 QPS)
- ✅ Echo chat: returns the same text, splitting long messages if needed (default: 1000 chars/message)
- ✅ Sends replies via LINE Reply API (up to 5 messages per reply)
- ✅ Handles redelivery with idempotency (avoids duplicate replies)
- ✅ Comprehensive logging with X-Line-Request-Id support

---

### 2) Run the chat layer (Echo-only)

Install, build, and start:
```bash
cd services/chat-layer
npm install
npm run build
npm start
```

Call the Echo endpoint:
```bash
curl -s http://localhost:8080/chat/v0.1/generate-replies   -H "Content-Type: application/json"   -d '{"request_id":"r1","message":{"text":"hello"}}'
# → {"request_id":"r1","status":"ok","messages":["hello"],"fallback_used":false}
```

---

## Minimal API reference

### gateway-line (Echo Mode - Local)

- **GET** `/`  
  **Response**: `"LINE Webhook Gateway - Echo Mode (Local)"`  
  Health check endpoint.

- **POST** `/line/webhook`  
  **Headers**: 
  - `X-Line-Signature: <base64 HMAC-SHA256(raw_body, channel_secret)>` (required)
  - `X-Line-Request-Id: <request_id>` (optional, for logging)
  - `Content-Type: application/json`
  
  **Body**: LINE webhook JSON payload
  ```json
  {
    "destination": "Uxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
    "events": [
      {
        "type": "message",
        "mode": "active",
        "timestamp": 1234567890123,
        "webhookEventId": "event-id-123",
        "source": { "type": "user", "userId": "U123..." },
        "replyToken": "reply-token-123",
        "message": { "type": "text", "text": "Hello!" },
        "deliveryContext": { "isRedelivery": false }
      }
    ]
  }
  ```
  
  **Responses:**
  - **200 OK** — Event accepted and processed (or intentionally handled with busy/locked messaging)
  - **400 Bad Request** — Invalid signature or malformed JSON  
  - **500 Internal Server Error** — Configuration error

  **Event Processing Logic:**
  1. **Signature validation** — Verifies HMAC-SHA256 with `Line:ChannelSecret`
  2. **Event filtering** — Only processes `message` events with `mode != "standby"` and `message.type == "text"`
  3. **User locking** — Prevents concurrent processing; sends locked message if user is busy
  4. **Queue management** — Enqueues for background processing; sends busy message if queue is full
  5. **Background worker** — Processes with QPS throttling, calls Echo chat, sends reply via LINE API

**Configuration Options (Environment Variables):**
```bash
# Required
LINE_CHANNEL_SECRET="your-channel-secret"
LINE_CHANNEL_ACCESS_TOKEN="your-access-token"

# Optional (with defaults)
RUNTIME_PLATFORM="Local"
CHAT_MODE="Echo"
CHAT_MAX_CHARS_PER_MESSAGE=1000
LOCAL_QUEUE_MAXSIZE=15
LOCAL_LOCKS_ENABLED=true
REPLY_QPS=5.0
LOCK_USER_TIMEOUT_SECONDS=300
LOG_LEVEL="Information"
```

### chat-layer (Echo v0.1)

- **POST** `/chat/v0.1/generate-replies`  
  **Request**
  ```json
  {
    "request_id": "r1",
    "message": { "text": "hello" }
  }
  ```
  **Response**
  ```json
  {
    "request_id": "r1",
    "status": "ok",
    "messages": ["hello"],
    "fallback_used": false
  }
  ```

> Versioning is via URL path (`/chat/v0.1/...`). The chat layer remains channel-agnostic.

---

## Roadmap (short)

- ✅ **Echo Mode (Local)** — Complete LINE webhook processing with local queue, user locks, and Echo replies
- ⏳ **HTTP Mode** — Gateway calls external chat service via HTTP (gateway-line already supports this)
- ⏳ **Cloud runtimes** — Azure Service Bus + Cosmos DB for multi-instance deployments  
- ⏳ **Additional channels** — Discord/X/Slack gateways
- ⏳ **Multi-cloud adapters** — AWS (SQS/DynamoDB), GCP (Pub/Sub/Firestore)
- ⏳ **Observability** — Structured logs, health checks, metrics

---

## Contributing

PRs and issues are welcome. Keep chat-layer channel-agnostic and put channel rules in the respective gateway. Please include small, focused changes with clear commit messages.

---

## License

MIT © 2025 Your Name

(Replace `Your Name` with your preferred display name in `LICENSE`.)
