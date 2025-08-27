# Issue 2 — [Gateway] Implement **HTTP Mode** forwarding (Local runtime)

## Summary

Add **HTTP Mode** forwarding while keeping the runtime **Local**. For validated LINE `message` events, construct **ChatRequest v0.1** and call the external **Chat Layer** HTTP endpoint, then map its response to LINE replies (abiding by character/ batching limits). On provider errors/timeouts, send fallback text as specified. (Derived from `docs/specs/gateway-line.spec.md` sections: *Forwarding to the Chat Layer Service (v0.1)*, *HTTP Forwarding*, *Response Handling*, *Batching rule*.)

## Platform & Language

- **Service:** `gateway-line` — C# / .NET 8 (ASP.NET Core) — see Microsoft Official documentation via MCP tools called `microsoft_docs_search` and `microsoft_docs_fetch`
- **Runtime platform:** **Local** (developer machine)
- **Chat layer target:** external **HTTP** service. For local testing, the monorepo includes `services/chat-layer` (TypeScript/Node.js) Echo service described in the README.

## Specs & README

- **Gateway spec:** `docs/specs/gateway-line.spec.md` — see *Forwarding to the Chat Layer Service (v0.1)*, *HTTP Forwarding* settings, *Response Handling*, and *Batching rule*.
- **Repository README(Current version)::** `README.md` — see *chat-layer (Echo v0.1) minimal API* and *Status / Roadmap* sections. you can update it.

## Scope of Work

- **Config surface**
  - `Chat:Mode=Http`
  - `Chat:ApiVersion=0.1`
  - `Chat:Http:*` — `BaseUrl`, `EndpointTemplate` (e.g., `/chat/{version}/generate-replies`) or `Endpoint` override, optional `ApiKey`, `VersionHeaderName` (default `X-Chat-Api-Version`), and `AdditionalHeaders` map.
  - **URL resolution rules**: Absolute wins; else resolve relative path against `Chat:Http:BaseUrl`; else fall back to `App:BaseUrl`; error if none.
- **Wire protocol (v0.1)**
  - **Request** (JSON):
    ```jsonc
    {
      "request_id": "<uuid>",
      "event_id": "<LINE webhookEventId>",
      "origin": { "platform": "line" },
      "conversation": { "id": "<room|group|user key>" },
      "author": { "user_id": "<source.userId>" },
      "message": { "text": "<user text>", "language": "ja-JP" },
      "limits": { "timeout_seconds": 20, "max_chars_per_message": 1000 }
    }
    ```
  - **Headers**: `Content-Type: application/json` + optional `X-Chat-Api-Version: 0.1` + optional `Authorization: Bearer <Chat:Http:ApiKey>`.
  - **Response** (success):
    ```jsonc
    {
      "request_id": "<same if echoed>",
      "status": "ok",
      "messages": ["text 1", "text 2"],
      "fallback_used": false
    }
    ```
  - **Response** (provider error):
    ```jsonc
    { "status": "provider_error", "error": { "code": "timeout", "message": "…" }, "fallback_used": true }
    ```
- **Mapping rules**
  - See table in spec (gateway → chat request). Build strings using validated inbound values.
- **Behavior**
  - On `status=ok`: send each text via LINE **Reply API**; enforce max **5** messages per reply.
  - On `status=provider_error` or `content_filtered`: reply with the chat layer’s fallback text; set telemetry `fallback_used=true`.
  - Honor `Chat:RequestTimeoutSeconds` as the end‑to‑end HTTP call budget.
- **Observability**
  - Log outbound HTTP: method, URL path (no secrets), status, latency.
  - Preserve `x-line-request-id` from LINE replies.
  - Structured logs for `messages_over_batch_limit`, `reply_token_invalid_or_expired`.

## Non‑Goals (for this issue)

- Azure Service Bus / Cosmos DB adapters.
- Azure Container Apps deployment manifests.

## Test Plan

- **Unit**
  - URL resolution matrix (absolute, relative + BaseUrl, fallback to App\:BaseUrl, error if none).
  - Mapping table fidelity (all fields present, types correct).
  - Timeout honored; fallback text is sent on provider timeouts.
- **Integration (Local)**
  - Stub Chat Layer (e.g., WireMock.Net) that returns: (a) `ok` with 1–5 messages; (b) `ok` with >5 messages (gateway must log & fail send); (c) `provider_error` with fallback; (d) slow response → timeout path.

## Configuration Keys (env)

- `CHAT_MODE=Http`, `CHAT_API_VERSION=0.1`, `CHAT_MAX_CHARS_PER_MESSAGE=1000`, `CHAT_REQUEST_TIMEOUT_SECONDS=20`
- `CHAT_HTTP_BASEURL`, `CHAT_HTTP_ENDPOINT_TEMPLATE` or `CHAT_HTTP_ENDPOINT`
- `CHAT_HTTP_API_KEY` (optional), `CHAT_HTTP_VERSION_HEADER` (default `X-Chat-Api-Version`)
- `CHAT_HTTP_ADDITIONAL_HEADERS` (JSON map)

## Definition of Done

- HTTP mode works end‑to‑end against a stubbed Chat Layer.
- All mapping/timeout/batching rules covered by tests above.
- README updated with a working example command to run the stub and the gateway locally.

## References (Official docs)

- **Gateway contract** (request/response v0.1, mapping, batching rule) — internal spec in repo.
- Azure — Service Bus (for upcoming cloud runtime):
  - Message counters & backlog: [https://learn.microsoft.com/azure/service-bus-messaging/message-counters](https://learn.microsoft.com/azure/service-bus-messaging/message-counters)
  - .NET `QueueRuntimeProperties.ActiveMessageCount`: [https://learn.microsoft.com/dotnet/api/azure.messaging.servicebus.administration.queueruntimeproperties.activemessagecount](https://learn.microsoft.com/dotnet/api/azure.messaging.servicebus.administration.queueruntimeproperties.activemessagecount)
  - Quotas: [https://learn.microsoft.com/azure/service-bus-messaging/service-bus-quotas](https://learn.microsoft.com/azure/service-bus-messaging/service-bus-quotas)
  - Premium messaging: [https://learn.microsoft.com/azure/service-bus-messaging/service-bus-premium-messaging](https://learn.microsoft.com/azure/service-bus-messaging/service-bus-premium-messaging)
  - Message sessions: [https://learn.microsoft.com/azure/service-bus-messaging/message-sessions](https://learn.microsoft.com/azure/service-bus-messaging/message-sessions) , enabling sessions: [https://learn.microsoft.com/azure/service-bus-messaging/enable-message-sessions](https://learn.microsoft.com/azure/service-bus-messaging/enable-message-sessions)
- LINE — Send reply message: [https://developers.line.biz/en/reference/messaging-api/#send-reply-message](https://developers.line.biz/en/reference/messaging-api/#send-reply-message)
- LINE — Common specs → response headers (`x-line-request-id`): [https://developers.line.biz/en/docs/messaging-api/common-specifications/#response-headers](https://developers.line.biz/en/docs/messaging-api/common-specifications/#response-headers)

> **Next milestone (Cloud runtime, separate issues):** swap local queue/locks for **Azure Service Bus** and **Cosmos DB** (or **Service Bus sessions**) to achieve multi‑instance correctness, plus Azure Container Apps deploy files.

