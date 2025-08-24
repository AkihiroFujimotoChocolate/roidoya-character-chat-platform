# Roidoya Character Platform

A small, channel-agnostic chat stack designed for multi-channel bots and assistants.  
This monorepo currently includes:

- **gateway-line** (C#/.NET 8): minimal LINE webhook endpoint that verifies `X-Line-Signature` and returns **200 OK** for LINE’s “Webhook URL verification”.
- **chat-layer** (TypeScript/Node.js): minimal **Echo** service that returns the same text it receives. No channel-specific logic.

MIT-licensed.

---

## Status

- ✅ LINE gateway: Webhook URL verification (HMAC, base64 signature)
- ✅ Chat layer: Echo (no limits, no splitting, channel-agnostic)
- ⏳ Next: gateway → chat wiring, HTTP mode, reply sending from gateway
- ⏳ Future: Discord/X/Slack gateways, multi-cloud adapters (AWS/GCP), observability

---

## Repository layout

```
roidoya-character-platform/
  /services
    /gateway-line          # C# / .NET 8 (verify-only)
      /src/LineBot.Api
    /chat-layer            # TypeScript / Node.js (Echo-only)
      /src
  /docs
    /specs                 # gateway-line spec document(s)
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

### 1) Run the LINE gateway (verify-only)

Set your LINE **Channel Secret** (choose one of the two methods):

**A. appsettings**  
Edit `services/gateway-line/src/LineBot.Api/appsettings.json`:
```json
{
  "Line": { "ChannelSecret": "REPLACE_WITH_YOUR_CHANNEL_SECRET" }
}
```

**B. Environment variable**  
```bash
# Linux/macOS
export Line__ChannelSecret='REPLACE_WITH_YOUR_CHANNEL_SECRET'
# Windows (PowerShell)
$env:Line__ChannelSecret='REPLACE_WITH_YOUR_CHANNEL_SECRET'
```

Start the service:
```bash
dotnet run --project services/gateway-line/src/LineBot.Api
```

Test the verification flow:
```bash
BODY='{"destination":"Uxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx","events":[]}'
SECRET='REPLACE_WITH_YOUR_CHANNEL_SECRET'
SIG=$(printf '%s' "$BODY" | openssl dgst -sha256 -hmac "$SECRET" -binary | openssl base64)

curl -i -X POST http://localhost:5000/line/webhook   -H "Content-Type: application/json"   -H "X-Line-Signature: $SIG"   --data "$BODY"
# Expect: HTTP/1.1 200 OK
```

> Note: This service only validates the signature and returns 200 OK for valid requests. It does not parse events or send replies yet.

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

### gateway-line (verify-only)

- **POST** `/line/webhook`  
  **Headers**: `X-Line-Signature: <base64 HMAC-SHA256(raw_body, channel_secret)>`  
  **Body**: raw JSON from LINE (e.g., `{"destination":"...","events":[]}`)  
  **200 OK** if signature is valid; **400** if missing/invalid; **500** if not configured.

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

- Wire gateway → chat (HTTP call) for Echo mode
- Add **HTTP mode** (chat-layer calls an external provider)
- Implement LINE Reply API sending in gateway (respect channel limits there)
- Add adapters for AWS (SQS/Dynamo), GCP (Pub/Sub/Firestore)
- Observability (structured logs, health checks, metrics)

---

## Contributing

PRs and issues are welcome. Keep chat-layer channel-agnostic and put channel rules in the respective gateway. Please include small, focused changes with clear commit messages.

---

## License

MIT © 2025 Your Name

(Replace `Your Name` with your preferred display name in `LICENSE`.)
