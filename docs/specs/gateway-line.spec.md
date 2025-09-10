# C# API Specification — LINE Webhook Gateway for .NET (Azure Edition)

## Overview

A .NET 8 service that receives LINE webhook events, validates signatures, and forwards each message to a configurable chat component that generates the reply; the service then sends that reply back to the user. The service is designed for **Azure Container Apps** (multi‑instance safe) and supports a **Local** mode for development.

- **Runtime modes**: `Local`, `Azure` (future: `AWS`, etc.)
- **Public API Docs**: Disabled (no Swagger/OpenAPI UI)
- **Auth**: HMAC‑SHA256 validation of `x-line-signature` using `LINE_CHANNEL_SECRET`
- **Scale**: Horizontal via Azure Container Apps (+ KEDA); correctness maintained across replicas
- **Platform**: .NET 8 (LTS). .NET 9 (STS) supported. See Microsoft’s **.NET support policy** and **lifecycle** pages for dates and definitions (LTS vs STS). [https://dotnet.microsoft.com/platform/support/policy/dotnet-core](https://dotnet.microsoft.com/platform/support/policy/dotnet-core) · [https://learn.microsoft.com/lifecycle/products/microsoft-net-and-net-core](https://learn.microsoft.com/lifecycle/products/microsoft-net-and-net-core)

*For an end-to-end primer on the Messaging API, see the [Messaging API overview](https://developers.line.biz/en/docs/messaging-api/overview/).*


## Project Layout (suggested)

```
src/
  LineBot.Api/                   // ASP.NET Core app (controllers, DI, middleware)
  LineBot.Core/                  // Domain abstractions (interfaces, models)
  LineBot.Adapters.Line/         // LINE REST client, signature helper
  LineBot.Adapters.AzureBus/     // Azure Service Bus queue adapter
  LineBot.Adapters.CosmosLock/   // Cosmos DB lock adapter
  LineBot.Chat/                  // Chat service implementation(s): Echo (test) or HTTP (prod)
```

## Endpoint

### POST `/line/webhook`
**Purpose**: Receive LINE webhook events.  
*See also: [Webhooks (Messaging API reference)](https://developers.line.biz/en/docs/messaging-api/receiving-messages/#webhook) · [Verify webhook signature](https://developers.line.biz/en/docs/messaging-api/receiving-messages/#signature-validation)*

**Headers**
- `x-line-signature` (required)
- `Content-Type: application/json`


**Behavior Summary**

1. Validate request signature.
2. **Event Filtering** (Accept only message events):
   - Ignore non-`message` events and events where `mode == "standby"`.
   - For the meaning of webhook `mode` values (`active` / `standby`) and other event fields, see [Webhook event objects](https://developers.line.biz/en/docs/messaging-api/receiving-messages/#webhook-event-objects).

3. **User-Level Locking** (per-user mutex):
   - If another message from the same user/conversation is in flight, **send** one of `Lock:LockedMessages` to the user and **return 200 OK** (do not enqueue).
4. **Queueing & Flow Limitation**:
   - Attempt to enqueue to **Azure Service Bus**.
   - If the **global backlog** (ActiveMessageCount) is ≥ `Reply:QueueMaxSize`, or enqueue fails due to quotas/throttling, **send** one of `Reply:QueueFullMessages` and **return 200 OK** (do not enqueue).
   - Otherwise, enqueue successfully and **return 200 OK**.
5. A background worker consumes from Service Bus, applies the reply QPS limit, invokes the chat service, and sends replies via the LINE API.

### Webhook Response Codes (explicit policy)
- **200 OK** — Event accepted (enqueued) or intentionally handled with **busy**/**locked** user messaging.
- **400 Bad Request** — Invalid signature or malformed JSON.
- **415 Unsupported Media Type** — Non-JSON content.
- **500 Internal Server Error** — Unexpected fault (note: LINE may redeliver on non-2xx).
*Note:* Non-2xx responses may trigger **webhook redelivery**. See [Redelivered webhooks](https://developers.line.biz/en/docs/messaging-api/receiving-messages/#redelivered-webhooks).

**429 policy:** Do **not** use `429 Too Many Requests` for normal flow control of `/line/webhook`. Returning non‑2xx can trigger LINE redelivery and operational noise. Use 200 with user‑visible busy/locked messages instead. Only use 429 if you explicitly want redelivery during exceptional, operator‑initiated shedding (default: **disabled**).

### Reply Token Timing & Error Handling (LINE)
- **Reply first, no push fallback.** The worker attempts a Reply API call using the `replyToken`. If invalid/expired/already-used, drop and **log** the error.
- **Time sensitivity.** Reply tokens are short-lived and single-use; minimize latency from enqueue → reply.
- **Redelivery behavior.** If `deliveryContext.isRedelivery = true`, check idempotency by `webhookEventId`. If not handled, attempt a single Reply. No push fallback.
- **API references:**
  - [Send reply message (API reference)](https://developers.line.biz/en/reference/messaging-api/#send-reply-message)
  - [Redelivered webhooks](https://developers.line.biz/en/docs/messaging-api/receiving-messages/#redelivered-webhooks)


### Forwarding to the Chat Layer Service (v0.1)

**Purpose:** After a message is accepted (queued or handled as locked/busy), a background worker calls the chat layer to generate user-visible replies.

**Invocation Modes**

- **Echo (Test-only)** — Returns the user’s text unchanged. Used for Local development and test deployments; no external provider calls are made. Respects `Chat:MaxCharsPerMessage` for splitting.
- **HTTP (Production)** — The worker makes an HTTP call to the external chat layer service using `Chat:Http:*` settings.

### Runtime rules for `Chat:Mode`
| Runtime:Platform | Allowed `Chat:Mode` | Default | Notes |
|---|---|---|---|
| `Azure` | `Http` (production), `Echo` (test) | `Http` | `Echo` is permitted for multi-instance test deployments; not recommended for production. |
| `Local` | `Echo`, `Http` | `Echo` | `Http` is available for end-to-end testing against a running chat service. |

**Same behavior across modes:** All other gateway behavior (signature check, locking, queueing/flow control, worker rate limit, 5-message/logging rules) is identical. Only the **chat generation step** differs.

**HTTP Request Format (v0.1)**

- **Method/Path**: `POST` to `{Chat:Http:BaseUrl}{Chat:Http:Endpoint}`. When using a template, resolve `{version}` with `Chat:ApiVersion` (e.g., `/chat/v0.1/generate-replies`).
- **Headers**: `Content-Type: application/json`; optionally include `Chat:ApiVersion` via `Chat:Http:VersionHeaderName`.
- **Body**: **ChatRequest v0.1** (no `operation` or `version` fields in the body).

**Field Mapping (gateway → chat request v0.1)**

| Gateway Source         | ChatRequest Field              | Notes                                    |
| ---------------------- | ------------------------------ | ---------------------------------------- |
| Validated inbound text | `message.text`                 | Raw user message text.                   |
| Platform label         | `origin.platform`              | e.g., `line`, `discord`, `slack`, `web`. |
| Conversation key       | `conversation.id`              | Channel/room/thread id as available.     |
| Sender id              | `author.user_id`               | Originating user id.                     |
| Time budget            | `limits.timeout_seconds`       | From `Chat:RequestTimeoutSeconds`.       |
| Per-message limit      | `limits.max_chars_per_message` | From `Chat:MaxCharsPerMessage`.          |

**Response Handling (v0.1)**
- On `status = ok`: send each text in `messages[]` to the user via the LINE Messaging API.
- On `status = provider_error` or `content_filtered`: send the chat layer’s fallback text and record `fallback_used`.
- **Batching rule:** LINE allows **up to 5** message objects per single Reply API call. Attempts beyond 5 should be treated as an application **error** and logged (see Logging & Telemetry). See [Send reply message](https://developers.line.biz/en/reference/messaging-api/#send-reply-message).


**Polymorphism / Future Versions**

- The gateway selects the wire version via `Chat:ApiVersion`.
- For HTTP mode, the version can also be conveyed via header (`Chat:Http:VersionHeaderName`).
- New versions (e.g., `0.2`) extend the request/response while keeping the endpoint path **/generate-replies**. The gateway must ignore unknown fields and bind to the version it requested.

## Non‑Functional Requirements

- **Multi‑instance correctness (Azure Container Apps)**: Queue saturation and lock decisions are made against **shared Azure services** (Service Bus / Cosmos DB), not per‑instance memory.
- **Idempotency**: Duplicate/redelivered events must not produce duplicated replies.
- **Observability**: Structured logs; Azure Monitor / Application Insights integration.
- **Security**: Secrets in Azure Key Vault; private networking (where possible); no secrets in logs.

## Configuration

Configurations are provided via `appsettings.*` and/or environment variables. Keys use `Section:Subsection` form; env overrides use `__` (double underscore).

### Platform & Mode

| Setting            | Env Var            | Type                  | Required | Description                                                                                                |
| ------------------ | ------------------ | --------------------- | -------- | ---------------------------------------------------------------------------------------------------------- |
| `Runtime:Platform` | `RUNTIME_PLATFORM` | Enum(`Local`,`Azure`) | Yes      | Selects adapter set and behaviors.                                                                         |
| `App:BaseUrl`      | `APP_BASE_URL`     | String                | No       | Base URL of this application; used to resolve relative chat endpoints when `Chat:Http:BaseUrl` is not set. |

### LINE

| Setting                      | Env Var                         | Type    | Required              | Description                                                                                                                                                   |
| ---------------------------- | ------------------------------- | ------- | --------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Line:ChannelSecret`         | `LINE_CHANNEL_SECRET`           | String  | Yes                   | Secret used for HMAC validation of `x-line-signature`.                                                                                                        |
| `Line:ChannelAccessToken`    | `LINE_CHANNEL_ACCESS_TOKEN`     | String  | Yes                   | Access token for LINE Messaging API (reply/push/profile).                                                                                                     |
| `Line:GetUserProfile`        | `GET_LINE_USER_PROFILE`         | Boolean | No (default: `false`) | Fetch user profile on demand.                                                                                                                                 |
| `Line:MaxIncomingTextLength` | `LINE_MAX_INCOMING_TEXT_LENGTH` | Integer | No (default: `0`)     | Maximum acceptable length for inbound LINE text (characters). `0` disables the check; over-limit messages should be logged (e.g., `incoming_text_truncated`). |

### Chat Layer (Forwarding & Behavior)

| Setting                          | Env Var                          | Type                | Required              | Description                                                                                                                                                    |
| -------------------------------- | -------------------------------- | ------------------- | --------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Chat:Mode` | `CHAT_MODE` | Enum(`Echo`,`Http`) | Yes | Chat invocation mode. `Echo` (test-only) returns the same text without calling an external provider; `Http` (production) forwards to an external chat service. |
| `Chat:ApiVersion`                | `CHAT_API_VERSION`               | String              | No (default: `"0.1"`) | Version of the chat layer request/response layout to use when forwarding.                                                                                      |
| `Chat:MaxCharsPerMessage` | `CHAT_MAX_CHARS_PER_MESSAGE` | Number | No (default: `1000`) | Used when preparing v0.1 replies and validating splits. *Character counting follows LINE’s rules (UTF-16 code units).* See [Character counting in a text](https://developers.line.biz/en/docs/messaging-api/character-counting/). |
| `Chat:RequestTimeoutSeconds`     | `CHAT_REQUEST_TIMEOUT_SECONDS`   | Number              | No (default: `20`)    | End‑to‑end timeout for a single chat generation call.                                                                                                          |
| `Chat:Fallbacks:Default`         | `CHAT_FALLBACK_DEFAULT`          | String              | Yes                   | Generic fallback text returned by the chat layer on errors/refusals.                                                                                           |
| `Chat:Fallbacks:ContentFiltered` | `CHAT_FALLBACK_CONTENT_FILTERED` | String              | No                    | Optional override when content is refused by safety policy.                                                                                                    |
| `Chat:Fallbacks:Timeout`         | `CHAT_FALLBACK_TIMEOUT`          | String              | No                    | Optional override used on provider timeout.                                                                                                                    |
| `Chat:AuthorUserIdPrefix`   | `CHAT_AUTHOR_USERID_PREFIX` | String | No       | `line-` | Prefix to prepend to `author.user_id` when forwarding to Chat Layer.        |

#### HTTP Forwarding (when `Chat:Mode = Http`)

| Setting                       | Env Var                        | Type   | Required                           | Description                                                                                                             |
| ----------------------------- | ------------------------------ | ------ | ---------------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| `Chat:Http:BaseUrl`           | `CHAT_HTTP_BASEURL`            | String | Yes (Http)                         | Base URL of the external chat layer service.                                                                            |
| `Chat:Http:EndpointTemplate`  | `CHAT_HTTP_ENDPOINT_TEMPLATE`  | String | No                                 | Path template with `{version}`, e.g., `/chat/{version}/generate-replies`. Used with `Chat:ApiVersion` to build the URL. |
| `Chat:Http:Endpoint`          | `CHAT_HTTP_ENDPOINT`           | String | No                                 | Explicit path override (e.g., `/chat/v0.1/generate-replies`). If set, it takes precedence over the template.            |
| `Chat:Http:ApiKey`            | `CHAT_HTTP_API_KEY`            | String | No                                 | API key or bearer token for the external service.                                                                       |
| `Chat:Http:VersionHeaderName` | `CHAT_HTTP_VERSION_HEADER`     | String | No (default: `X-Chat-Api-Version`) | Header name to convey `Chat:ApiVersion` when required by the service.                                                   |
| `Chat:Http:AdditionalHeaders` | `CHAT_HTTP_ADDITIONAL_HEADERS` | Object | No                                 | Key/value map of extra headers to include on every request.                                                             |

**URL Resolution Rules**

1. If `Chat:Http:Endpoint` or `Chat:Http:EndpointTemplate` is an **absolute URL**, use it as-is.
2. If it is a **relative path** and `Chat:Http:BaseUrl` is set, resolve against `Chat:Http:BaseUrl`.
3. Otherwise, if `App:BaseUrl` is set, resolve against `App:BaseUrl`.
4. If none are set, treat as configuration error.

### Flow Limitation & Replies

| Setting                           | Env Var                              | Type          | Required            | Description                                                                                                                            |
| --------------------------------- | ------------------------------------ | ------------- | ------------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| `Reply:Qps`                       | `REPLY_QPS`                          | Number        | No (default: `5.0`) | **Per‑replica** reply rate cap (messages/sec). Overall throughput ≈ replicas × Qps.                                                    |
| `Reply:QueueMaxSize`              | `REPLY_QUEUE_MAXSIZE`                | Integer       | No (default: `15`)  | **Soft global backlog limit**. If Service Bus `ActiveMessageCount` ≥ this value, send `Reply:QueueFullMessages` instead of enqueueing. |
| `Reply:QueueFullMessages`         | `REPLY_QUEUE_FULL_MESSAGES`          | Array[String] | Yes                 | Candidate texts to send when the system is busy. One is chosen at random.                                                              |
| `Flow:BacklogSamplePeriodSeconds` | `FLOW_BACKLOG_SAMPLE_PERIOD_SECONDS` | Number        | No (default: `5`)   | Sampling interval for reading Service Bus `ActiveMessageCount`. Use cached value between samples.                                      |
| `Flow:BacklogSampleJitterSeconds` | `FLOW_BACKLOG_SAMPLE_JITTER_SECONDS` | Number        | No (default: `2`)   | Randomized jitter added to sampling interval to avoid thundering herds.                                                                |

**Backlog Sampling Guidance**

- The gateway samples `ActiveMessageCount` via `ServiceBusAdministrationClient.GetQueueRuntimePropertiesAsync` on the configured interval and uses the cached value to decide when to send **busy** messages, avoiding per-request admin calls. See **QueueRuntimeProperties.ActiveMessageCount** and **Message counters** in Azure docs. [https://learn.microsoft.com/dotnet/api/azure.messaging.servicebus.administration.queueruntimeproperties.activemessagecount](https://learn.microsoft.com/dotnet/api/azure.messaging.servicebus.administration.queueruntimeproperties.activemessagecount) · [https://learn.microsoft.com/azure/service-bus-messaging/message-counters](https://learn.microsoft.com/azure/service-bus-messaging/message-counters)

### User‑Level Locking

| Setting                   | Env Var                     | Type          | Required              | Description                                                        |
| ------------------------- | --------------------------- | ------------- | --------------------- | ------------------------------------------------------------------ |
| `Lock:UserTimeoutSeconds` | `LOCK_USER_TIMEOUT_SECONDS` | Integer       | No (default: `300`)   | Lock TTL; protects against worker crashes.                         |
| `Lock:EnableLockedReply`  | `ENABLE_LOCKED_REPLY`       | Boolean       | Yes (default: `true`) | When `true`, send `Lock:LockedMessages` if a lock is already held. |
| `Lock:LockedMessages`     | `LOCKED_MESSAGES`           | Array[String] | Yes                   | Texts to send when a user is locked (in‑flight).                   |

### Azure Service Bus

| Setting                               | Env Var                                 | Type    | Required              | Description                                                                  |
| ------------------------------------- | --------------------------------------- | ------- | --------------------- | ---------------------------------------------------------------------------- |
| `Azure:ServiceBus:ConnectionString`   | `AZURE_SERVICEBUS_CONNECTION_STRING`    | String  | Yes (Azure)           | Namespace connection string.                                                 |
| `Azure:ServiceBus:QueueName`          | `AZURE_SERVICEBUS_QUEUE_NAME`           | String  | Yes (Azure)           | Target queue for work items.                                                 |
| `Azure:ServiceBus:UseSessions`        | `AZURE_SERVICEBUS_USE_SESSIONS`         | Boolean | No (default: `false`) | If `true`, set `SessionId` = user key for per‑user FIFO without DB locks.    |
| `Azure:ServiceBus:MaxConcurrentCalls` | `AZURE_SERVICEBUS_MAX_CONCURRENT_CALLS` | Integer | No (default: `1`)     | Worker concurrency per replica. Keeps bursts under control with `Reply:Qps`. |

**Sessions option (alternative to DB locks)**

- Azure Service Bus **message sessions** provide **ordered, exclusive** processing for related messages across consumers. Enable sessions on the queue and use `SessionId = session key (e.g., user:{userId})` to get per-user FIFO across replicas. (Basic tier doesn’t support sessions.) See **Message sessions** and **Enable sessions** docs. [https://learn.microsoft.com/azure/service-bus-messaging/message-sessions](https://learn.microsoft.com/azure/service-bus-messaging/message-sessions) · [https://learn.microsoft.com/azure/service-bus-messaging/enable-message-sessions](https://learn.microsoft.com/azure/service-bus-messaging/enable-message-sessions)

**Quotas (overview)**

- **Queue size**: Specify **1–80 GB** at creation; when full, new sends are rejected. [Service Bus quotas](https://learn.microsoft.com/azure/service-bus-messaging/service-bus-quotas)
- **Message size**: Standard/Basic up to **256 KB** per message; Premium supports **large messages up to 100 MB** (AMQP; performance trade‑offs). See **Premium messaging** and **Quotas**. [https://learn.microsoft.com/azure/service-bus-messaging/service-bus-premium-messaging](https://learn.microsoft.com/azure/service-bus-messaging/service-bus-premium-messaging) · [https://learn.microsoft.com/azure/service-bus-messaging/service-bus-quotas](https://learn.microsoft.com/azure/service-bus-messaging/service-bus-quotas)

### Azure Cosmos DB (for locks / idempotency)

| Setting                  | Env Var                  | Type   | Required                           | Description                                 |
| ------------------------ | ------------------------ | ------ | ---------------------------------- | ------------------------------------------- |
| `Azure:Cosmos:Endpoint`  | `AZURE_COSMOS_ENDPOINT`  | String | Yes (Azure, if not using sessions) | Account endpoint.                           |
| `Azure:Cosmos:Key`       | `AZURE_COSMOS_KEY`       | String | Yes (Azure, if not using sessions) | Key or use Managed Identity with RBAC.      |
| `Azure:Cosmos:Database`  | `AZURE_COSMOS_DATABASE`  | String | Yes (Azure, if not using sessions) | Database for locks/state.                   |
| `Azure:Cosmos:Container` | `AZURE_COSMOS_CONTAINER` | String | Yes (Azure, if not using sessions) | Container for per‑user locks (TTL enabled). |

### Local Development

| Setting               | Env Var               | Type    | Required             | Description                                                |
| --------------------- | --------------------- | ------- | -------------------- | ---------------------------------------------------------- |
| `Local:Queue:MaxSize` | `LOCAL_QUEUE_MAXSIZE` | Integer | No (default: `15`)   | Bounded in‑memory queue capacity. Not multi‑instance safe. |
| `Local:Locks:Enabled` | `LOCAL_LOCKS_ENABLED` | Boolean | No (default: `true`) | In‑memory user locks. Not multi‑instance safe.             |

### Logging & Telemetry

| Setting                     | Env Var               | Type    | Required                    | Description                           |
| --------------------------- | --------------------- | ------- | --------------------------- | ------------------------------------- |
| `Logging:LogLevel:Default`  | `LOG_LEVEL`           | String  | No (default: `Information`) | Logging level.                        |
| `Azure:AppInsights:Enabled` | `APPINSIGHTS_ENABLED` | Boolean | No (default: `true`)        | Enable Application Insights exporter. |

#### LINE Logging Recommendations (per official guidelines)
Follow LINE’s official **development guidelines → Save logs** for both webhooks received and Messaging API requests you send. See: [Save logs (development guidelines)](https://developers.line.biz/en/docs/messaging-api/development-guidelines/#save-logs).

**For Messaging API requests (outbound to LINE):**
- `x-line-request-id` (from the response header). See [Common specifications → Response headers](https://developers.line.biz/en/docs/messaging-api/common-specifications/#response-headers).
- Time the API request was made
- HTTP method
- API endpoint called
- HTTP status code returned by the LINE Platform
- *(Optional, if policy permits)* request body parameters and response body

**For webhooks received (from LINE to your server):**
- Sender IP address
- Time the webhook was received
- HTTP method
- Request path
- HTTP status code your server returned
- *(Optional)*: `x-line-signature` header value and the webhook event object (avoid sensitive payloads in lower log levels)

**Privacy & safety notes**

- Do not log access tokens, channel secrets, or full raw bodies at normal levels.
- Apply retention per your compliance policy; ensure logs are searchable and correlated via `webhookEventId` where available.

#### Additional error logs (gateway behavior)

- **Error: `messages_over_batch_limit`** — log when attempting a Reply request with more than 5 message objects; include `webhookEventId`, `isRedelivery`, `attempted_message_count`, `limit=5`, and request path.
- **Error: `reply_token_invalid_or_expired`** — log when the Reply API fails due to token invalid/expired; include `webhookEventId`, `isRedelivery`, truncated token hash/suffix, HTTP status, and provider error details.

## Data Contracts (minimal)

> The raw JSON must be preserved for signature validation. The service only relies on a subset of LINE fields for routing and flow control.

### Webhook Envelope

| Field         | Type         | Required | Description             |
| ------------- | ------------ | -------- | ----------------------- |
| `destination` | String       | Yes      | LINE destination ID.    |
| `events`      | Array[Event] | Yes      | List of webhook events. |

### Event (subset used by the service)

| Field            | Type   | Required    | Description                                          |
| ---------------- | ------ | ----------- | ---------------------------------------------------- |
| `type`           | String | Yes         | Event type; must be `message` to process.            |
| `mode`           | String | Yes         | `active` or `standby`; `standby` events are ignored. |
| `timestamp`      | Number | Yes         | Epoch millis.                                        |
| `webhookEventId` | String | Yes         | Unique id for idempotency.                           |
| `source.type`    | String | Yes         | `user` / `group` / `room`.                           |
| `source.userId`  | String | Conditional | Present for 1:1 chats; used for lock key.            |
| `source.groupId` | String | Conditional | Present for groups; may be part of lock key.         |
| `source.roomId`  | String | Conditional | Present for rooms; may be part of lock key.          |
| `replyToken`     | String | Conditional | Required to reply.                                   |
| `message`        | Object | Conditional | Present for `message` events (text and others).      |

---

# Chat Layer Service — v0.1 Minimal Interface (Text Only)

## Operation & Versioning (v0.1)

- **Operation**: *generate-replies* (implied by the endpoint path). No `operation` field exists in the request body.
- **Version**: Carried in the URL path (e.g., `/chat/v0.1/generate-replies`), optionally duplicated in a header. No `version` field exists in the request body.

## Request (`ChatRequest v0.1`)

| Field                          | Type   | Required | Key Type    | Default             | Description                                                                 |
| ------------------------------ | ------ | -------- | ----------- | ------------------- | --------------------------------------------------------------------------- |
| `request_id`                   | String | No       | Primary Key | Auto-generated UUID | Unique id for tracing; echoed if provided.                                  |
| `event_id`                     | String | No       | N/A         | —                   | Upstream event/message id (idempotency/debug).                              |
| `origin.platform`              | String | No       | N/A         | `"unknown"`         | Source platform label (e.g., `line`, `discord`, `twitter`, `slack`, `web`). |
| `conversation.id`              | String | No       | N/A         | —                   | Conversation/channel/thread identifier (transport-neutral).                 |
| `author.user_id`               | String | No       | N/A         | —                   | Sender’s user id on the origin platform.                                    |
| `message.text`                 | String | **Yes**  | N/A         | —                   | Raw user text (UTF-8).                                                      |
| `message.language`             | String | No       | N/A         | Unspecified         | BCP-47 tag (e.g., `ja-JP`, `en-US`).                                        |
| `limits.timeout_seconds`       | Number | No       | N/A         | `20`                | Hard time budget inside chat layer.                                         |
| `limits.max_chars_per_message` | Number | No       | N/A         | `1000`              | Per-message character ceiling used for splitting.                           |

## Response (`ChatResponse v0.1`)

| Field           | Type          | Required    | Key Type    | Default         | Description                                                                     |
| --------------- | ------------- | ----------- | ----------- | --------------- | ------------------------------------------------------------------------------- |
| `request_id`    | String        | No          | Foreign Key | Echo if present | Mirrors the request id when provided.                                           |
| `status`        | String        | **Yes**     | N/A         | —               | `ok` \| `provider_error` \| `content_filtered`.                                 |
| `messages`      | Array[String] | Conditional | N/A         | —               | Present when `status = ok`. Each entry respects `limits.max_chars_per_message`. |
| `fallback_used` | Boolean       | No          | N/A         | `false`         | `true` when the chat layer returned its configured fallback text.               |
| `error.code`    | String        | Conditional | N/A         | —               | On failure (e.g., `timeout`, `quota_exceeded`, `network`).                      |
| `error.message` | String        | Conditional | N/A         | —               | Short diagnostic string (not user-facing).                                      |

### Echo mode (test-only) behavior

- **Purpose:** wiring/CI smoke tests; no external provider calls.
- **Output:** returns the **same text** received in `message.text`.
- **Splitting:** if the text exceeds `limits.max_chars_per_message`, split into multiple reply strings (order preserved).
- **Response:** `status = ok`, `messages = [ one or more echoed strings ]`, `fallback_used = false`.
- **Multi-instance:** safe on `Runtime:Platform = Azure` (ordering/back-pressure handled by Service Bus and locks/sessions).
- **Production:** use `Chat:Mode = Http` in production environments.

---

## Versioning & Polymorphism Strategy

To evolve the chat-layer contract without breaking callers, the gateway and chat service use versioned URLs and tolerant parsing.

| Aspect                      | Specification                                                                                                                                                                                          |
| --------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Version location**        | The **URL path** carries the version (e.g., `/chat/v0.1/generate-replies`). The body has **no** `version` field. An optional header may also carry the version (`Chat:Http:VersionHeaderName`).        |
| **Operation selection**     | The operation is implied by the endpoint path (`generate-replies`). No `operation` field exists in the body.                                                                                           |
| **Negotiation**             | The worker selects the version via configuration (`Chat:ApiVersion`) and composes the URL using `Chat:Http:EndpointTemplate` or `Chat:Http:Endpoint`. In HTTP mode it may also set the version header. |
| **Request/Response shapes** | Bodies are versioned by name (`ChatRequest v0.1`, `ChatResponse v0.1`). Future versions add optional fields; major versions may change semantics.                                                      |
| **Compatibility policy**    | The gateway must ignore unknown fields in responses and send only fields defined for the chosen version. The chat service should remain backward compatible for supported minor versions.              |
| **Deprecation**             | Support multiple versions concurrently during migration; announce a retirement window before removing an older version.                                                                                |

---

**End of specification.**