# RCCP v0.2 Direct Chat Specification

- Status: Proposed
- Specification version: v0.2
- Profile: Direct Chat
- Last updated: 2026-09-08
- Author: Akihiro Fujimoto
- Project: Roidoya Character Chat Platform (RCCP)
- License: MIT

## 1. Purpose

This document specifies the RCCP v0.2 Direct Chat contract. It defines the responsibility boundaries, Channel Adapter-to-Orchestration HTTP API, identifiers, validation, failure behavior, Model boundary, and diagnostics requirements for a direct Character conversation.

Implementation and support status are reported separately by the repository and its releases. They are not implied by publication of this specification.

In this document, **MUST**, **MUST NOT**, **SHOULD**, and **SHOULD NOT** are normative requirements.

## 2. Direct Chat profile

The v0.2 Direct Chat profile handles one input message from one Actor in one conversation and produces one or more ordered Character messages, subject to the request limit. The initial Channel profile is a one-to-one LINE text conversation.

This profile does not define:

- multi-party or multi-Character conversation semantics;
- cross-channel identity linking or continuity;
- Binding or Publication resources;
- raw Channel payload forwarding to Orchestration;
- tool execution, image output, streaming, or proactive delivery; or
- durable processing, exactly-once delivery, History, Memory, or State storage.

These capabilities may be defined by later profiles or versions. Their absence from this contract does not prohibit an implementation from using storage or distributed infrastructure behind the specified boundaries.

## 3. Responsibility boundaries

### 3.1 Contract boundaries

| Boundary | Data or operation | Responsibility |
| --- | --- | --- |
| Channel to Channel Adapter | Webhook, signature, Channel event, external identifiers | Authenticate the event, select supported input, and normalize Channel data |
| Channel Adapter to Orchestration | `POST /chat/v0.2/generate-replies` | Exchange the normalized Direct Chat request and response defined here |
| Orchestration to Core Model capability | Character instruction, conversation input, Model settings, deadline | Generate and validate a Provider-neutral Model result |
| Channel Adapter to Channel | Channel-specific reply operation | Validate, convert, and deliver the accepted output |

Unless stated otherwise, “request” and “response” mean the synchronous HTTP boundary between the Channel Adapter and Orchestration. They do not mean a Channel Webhook or a Model Provider request.

This specification defines logical responsibilities rather than programming languages, class layouts, process placement, or deployment topology.

### 3.2 Component responsibilities

| Component | Responsibilities | Outside its responsibility |
| --- | --- | --- |
| Channel Adapter | Channel authentication and event checks, authorization admission, external-to-RCCP ID resolution, request construction, Channel reply-token custody, Orchestration invocation, response validation, Channel conversion and delivery, fixed system failure message | Character personality and conversation flow; Provider-specific Model processing |
| Orchestration | Request and target validation, assembly of Character instruction and input, Model invocation, reply validation, output ID generation, v0.2 response construction | Channel delivery and reply-token management; Provider credentials and Provider-specific public contract types |
| Core Model capability | Provider API conversion, generation, stop-reason and Structured Output validation, common internal success or failure result | Character-specific flow, Channel delivery, fixed system failure-message generation |

### 3.3 Admission and authorization

The Channel Adapter MUST verify the Channel request, parse the event, select supported events, and admit only authorized users before ordinary conversation processing.

Unsupported or unauthorized events MUST NOT be sent to the ordinary queue, Model, or History path and MUST NOT receive a Character reply. An empty Orchestration response is not the representation of an unauthorized input.

Webhook receipt and later generation or delivery results are separate. A later result does not change an already returned Webhook HTTP response.

An RCCP ID is not authorization. User admission at the Adapter and authentication or access control for the Orchestration endpoint are separate boundaries.

## 4. Orchestration request

### 4.1 Endpoint

```text
POST /chat/v0.2/generate-replies
Content-Type: application/json
```

The path selects the contract version. The body does not contain another version field.

### 4.2 Request example

All values in this example are illustrative.

```json
{
  "interaction_id": "interaction:sample-input-001",
  "scope": {
    "tenant_id": "tenant:sample",
    "service_id": "service:sample-chat"
  },
  "origin": {
    "channel_type": "line",
    "channel_instance_id": "channel:sample-line"
  },
  "conversation": { "id": "conversation:sample-001" },
  "actor": { "id": "actor:sample-001" },
  "character": { "id": "character:sample" },
  "message": {
    "id": "message:sample-input-001",
    "content": [ { "type": "text", "text": "Hello" } ]
  },
  "limits": {
    "deadline_at": "2026-09-08T00:00:20Z",
    "max_output_messages": 1
  }
}
```

The body is one normalized Direct Chat interaction envelope. It does not require an additional `interaction` or `input` wrapper and does not imply that every admitted input produces a reply.

### 4.3 Required fields

| Field | Requirement |
| --- | --- |
| `interaction_id` | Non-empty string identifying input processing and response correlation |
| `scope.tenant_id` | Non-empty string identifying the tenant |
| `scope.service_id` | Non-empty string identifying the RCCP-based service within the tenant |
| `origin.channel_type` | Channel type; `line` for the LINE Direct Chat profile |
| `origin.channel_instance_id` | Non-empty string identifying the configured Channel connection |
| `conversation.id` | Non-empty string identifying the conversation |
| `actor.id` | Non-empty string identifying the input Actor within RCCP |
| `character.id` | Non-empty string identifying the responding Character |
| `message.id` | Non-empty string identifying the input message |
| `message.content` | Array containing exactly one text part in this profile |
| `message.content[0].type` | Exactly `text` |
| `message.content[0].text` | Input text |
| `limits.deadline_at` | Absolute UTC timestamp in RFC 3339 format; see Section 6 |
| `limits.max_output_messages` | Positive integer limiting the number of output messages |

Every listed field and parent object is required. A missing value, `null`, type mismatch, or empty or whitespace-only ID is invalid. Input text that is empty or whitespace-only is invalid. Leading or trailing whitespace alone does not authorize automatic rewriting of the text.

Orchestration MUST NOT invent missing IDs or limits. The request MUST NOT contain a raw LINE User ID, reply token, raw Channel payload, separate `request_id` or `execution_id`, Binding, Publication, revision, or undefined metadata field.

## 5. IDs and scope

### 5.1 Uniqueness and assignment

| ID | Uniqueness scope | Assigned or resolved by |
| --- | --- | --- |
| Tenant | RCCP operating environment | Adapter from operator configuration |
| Service | Tenant | Adapter from operator configuration |
| Channel instance and Character | Tenant and service | Adapter from corresponding configuration |
| Actor and conversation | Tenant and service | Adapter from external-user and conversation mapping |
| Interaction | Tenant and service | Adapter for a new input; preserved for redelivery |
| Input message | All input and output messages within tenant and service | Adapter; preserved for redelivery |
| Output message | All input and output messages within tenant and service | Orchestration |

IDs are opaque strings. Example prefixes, UUIDs, and string concatenation are not required syntaxes. Different ID kinds have distinct namespaces, while input and output message IDs share the message-ID namespace. Globally unique values do not remove the requirement to validate tenant and service scope.

### 5.2 Stability rules

- Redelivery of the same external input MUST preserve `interaction_id` and the input `message.id`. A new input with identical text MUST receive new IDs. The two IDs MUST NOT be assumed to be equal.
- The same tenant, service, Channel instance, Actor, and Character combination MUST resolve to the same `conversation.id`. Time, process restart, Character-setting changes, or Orchestration implementation changes alone MUST NOT change it.
- A different Actor, Character, or Channel instance creates a different conversation. This contract does not define an explicit reset operation.
- `channel_instance_id` identifies a configured Channel connection, not a process, server, replica, or credential. Restart, relocation, or credential rotation for the same connection MUST preserve it; a distinct bot account or Channel connection receives another ID.

### 5.3 Guarantees not provided

Stable input IDs do not by themselves guarantee exactly-once processing, generation, or delivery. A conforming deployment may provide stronger durability through separately specified infrastructure.

If a retained output is redelivered, its output ID is preserved. If a request is executed again and regenerated, identical text and output IDs are not guaranteed. An implementation MUST NOT derive an invariant output ID only from `interaction_id` and array position when a different generated message could receive the same ID.

The response does not repeat scope. The synchronous caller retains request scope and call correlation and verifies `interaction_id`. Bare IDs MUST NOT be correlated across services.

## 6. Limits and deadlines

### 6.1 Output-message limit

`limits.max_output_messages` is the maximum number of elements in a successful `messages` array. The Adapter derives the effective hard ceiling from Channel constraints, Channel-connection configuration, and service or operator policy. Orchestration may return fewer messages, but a successful response contains at least one and no more than the limit.

The request does not define `max_chars_per_message`. This does not remove application-level output limits: generation MUST be bounded and validated, and the Adapter MUST apply final Channel-specific validation. A prompt instruction is not sufficient enforcement.

### 6.2 Deadline semantics

`limits.deadline_at` is the deadline by which the Adapter MUST have received the entire HTTP response body and completed contract validation. It is neither a header-arrival deadline nor a Channel-delivery deadline.

- The Adapter reserves delivery time before it sets the deadline.
- A valid response accepted by the deadline may be validated against Channel constraints and sent within the reserved delivery time. The same deadline is not mechanically reapplied immediately before sending.
- Queue waiting, redelivery, or another execution of the same input MUST NOT extend the deadline.
- After timeout failure behavior has been selected, a late successful response MUST NOT replace it or cause an additional delivery.
- The deadline MUST NOT be extended to wait for an error response.
- A caller-side timeout does not prove that an upstream Provider has stopped processing.

For the LINE Direct Chat profile:

```text
deadline_at = original LINE event timestamp + fixed processing budget
```

The processing budget excludes the reserved LINE-delivery margin. The Adapter MUST NOT recalculate the deadline from Webhook receipt or queue-dequeue time. The same event and configuration reproduce the same deadline across redelivery. If a previously built request is reused, its existing deadline is retained.

LINE transport delay consumes the processing budget. If the deadline has already passed, Orchestration MUST NOT call the Model.

## 7. Orchestration responses

### 7.1 Successful response

```json
{
  "interaction_id": "interaction:sample-input-001",
  "status": "ok",
  "messages": [
    {
      "id": "message:sample-output-001",
      "content": [ { "type": "text", "text": "Hello!" } ]
    }
  ]
}
```

- `interaction_id` MUST equal the request value.
- `status: "ok"` means that valid output was generated; it does not mean that Channel delivery succeeded.
- `messages` is ordered and contains at least one element and no more than the requested limit.
- Every output `id` is a non-empty opaque string, differs from the input message ID, and is unique within the output array.
- Every `content` contains exactly one non-empty, non-whitespace text part in this profile.
- Array order defines output order. The response does not add `sequence`, repeated request context, effects, or undefined metadata.

### 7.2 Error response

```json
{
  "interaction_id": "interaction:sample-input-001",
  "status": "error",
  "error": { "code": "processing_failed" }
}
```

An `ok` response contains `messages` and no `error`. An `error` response contains `error` and no `messages`. The response does not expose retry advice, exception details, Provider names, internal service names, or generated Model content.

| Error code | Meaning |
| --- | --- |
| `invalid_request` | The input cannot be processed as a valid supported request |
| `deadline_exceeded` | The specified deadline was exceeded |
| `temporarily_unavailable` | Processing capacity or a dependency is temporarily unavailable |
| `processing_failed` | Processing ended without a valid reply for another reason |

A correlated error response includes `interaction_id` only when a valid value can be interpreted from the request. The server MUST NOT invent one for malformed JSON or a request with no valid interaction ID. Transport-level rejection need not use this application error shape.

## 8. HTTP and validation

### 8.1 Version selection

| Condition | Result |
| --- | --- |
| Optional `X-Chat-Api-Version` header is sent | Value MUST be `0.2` |
| Header is omitted | Accepted |
| Header and path disagree | HTTP 400; no reinterpretation as another version |
| Path version is unsupported | HTTP 404; no forwarding to another version |

When correlation is available, a header mismatch may use `invalid_request`.

### 8.2 Application result mapping

| Condition | HTTP | Body |
| --- | ---: | --- |
| Valid output generated | 200 | `ok` with `messages` |
| Invalid field, type, or request structure | 400 | `error` with `invalid_request` |
| Unsupported tenant, service, or Character | 400 | `error` with `invalid_request` |
| Deadline expired when Orchestration receives the request | 400 | `error` with `deadline_exceeded` |
| Deadline expires during processing | 500 | `error` with `deadline_exceeded` |
| Temporary unavailability | 503 | `error` with `temporarily_unavailable` |
| Other generation or processing failure | 500 | `error` with `processing_failed` |

Authentication failures, unsupported paths, proxies, and other transport infrastructure are not all defined by this table. A caller MUST tolerate a non-RCCP response such as a proxy error or non-JSON body.

### 8.3 Validation rules

- HTTP and JSON validity, the v0.2 contract, the selected content profile, and Channel pre-send constraints are distinct validations.
- HTTP/body inconsistency, correlation mismatch, missing required fields, type mismatch, and duplicate properties in one JSON object MUST be rejected.
- An unknown `status` or content type MUST NOT be interpreted as success.
- An unknown optional property that does not change existing meaning may be ignored when safe.
- Senders emit only the four error codes in Section 7.2. A receiver treats an unknown string code as a generic failure but MUST NOT use it as a reason to retry. A missing code or non-string code is an invalid response.
- An empty `messages` array, invalid or duplicate output ID, blank text, incorrect text-part count, message-count excess, or output-limit violation MUST NOT be accepted as success.
- The Adapter MUST NOT turn invalid output into success by truncating, splitting, dropping extra messages, or partially sending it.

## 9. Failure, retry, and delivery

### 9.1 Adapter-to-Orchestration retry

The Adapter performs no automatic Orchestration retry for a Direct Chat request. This includes connection failure, timeout, invalid response, regular error response, and HTTP 503. A temporary-error label alone does not prove that re-execution is safe.

Webhook or queue redelivery is a separate responsibility. No automatic outer retry does not guarantee a single interaction execution or exactly-once behavior.

### 9.2 Delivery-stage behavior

| Failure point | Required behavior |
| --- | --- |
| Orchestration call or response validation fails before Channel send | If sending remains possible, attempt the fixed system failure message |
| Channel format or size validation fails before send | Do not send invalid Character output; if possible, attempt the fixed system failure message |
| Channel API returns error or times out after send begins | Record delivery failure or unknown result; do not automatically add another system message |
| Fixed system failure-message delivery fails | Record and stop; do not repeat the fixed message |

The fixed system failure message is a prevalidated, Character-independent message owned by the Adapter. It is subject to Channel pre-send validation. A generation failure MUST NOT be converted into a Character-specific successful fallback.

The LINE Direct Chat profile defines no automatic push fallback after reply delivery fails. External conditions may prevent even the fixed message from arriving; delivery is not guaranteed.

## 10. Orchestration and Model capability

### 10.1 Logical boundary

| Orchestration input to Model capability | Meaning |
| --- | --- |
| Character instruction | Personality and voice, kept distinct from user input |
| Conversation input | Input selected by Orchestration for this interaction |
| Model settings | Model and bounded-output settings |
| Processing deadline | Original request deadline reflected in the available call time |

The Model capability is Provider-neutral at its Orchestration boundary and converts to a Provider-specific API within Core. Credentials remain with the connection implementation. Channel reply tokens and delivery operations are not Model input.

Provider HTTP success, Model generation success, Orchestration success, and Channel delivery success are separate states. Refusal, truncation, or unavailable reply text is not a valid text result. Provider failures are converted to common internal categories, which Orchestration maps to Sections 7 and 8.

### 10.2 Structured Output

The Model connection uses a fixed schema containing the final utterance. The following is illustrative; the exact schema is defined by the corresponding implementation profile.

```json
{ "text": "Hello!" }
```

The Model capability validates the Provider stop reason and schema, then returns validated final-utterance text to Orchestration. Orchestration validates it as a reply and constructs the v0.2 response. Other Provider content blocks MUST NOT be mistaken for the final utterance.

Refusal, truncation, schema failure, or missing reply text is failure. Free-form text MUST NOT be recovered as a successful result, and no automatic regeneration occurs. The Model does not generate interaction or message IDs. Provider Structured Output and the v0.2 Adapter response are separate structures.

### 10.3 Model retry and timeout

One Orchestration execution normally makes one Model call. There is no automatic Model retry or regeneration. SDK and HTTP-library automatic retries MUST be disabled.

The Model timeout is the shorter of a configured upper bound and the remaining request time minus a post-processing margin. The margin covers result validation, response construction and return, and Adapter receipt and contract validation. The delivery margin was already excluded when `deadline_at` was calculated and MUST NOT be deducted twice.

If there is no usable time before the Model call, the Model MUST NOT be called. A result arriving after timeout does not trigger an additional delivery. Caller timeout does not guarantee Provider-side cancellation.

## 11. Configuration and target mismatch

- A tenant, service, or Character not supported by Orchestration configuration maps to `invalid_request` and HTTP 400. There is no implicit default-Character fallback.
- Missing server configuration for an otherwise correct target is a server fault, not a client request error.
- Statically detectable required-configuration faults MUST prevent the affected component from accepting processing.
- If a model-selection error is discovered at runtime, it is `processing_failed` with HTTP 500. There is no implicit switch to another Model.
- Model selection MUST be explicit and observable without logging credentials.

Configuration keys and formats, fixed values, ID algorithms, internal types, and class structure are implementation details.

## 12. Diagnostics and sensitive data

Diagnostics are not conversation History or durable processed-event records.

Ordinary diagnostics may include time, component and stage, tenant/service/interaction correlation, outcome and internal cause category, duration, Model selection, and Channel delivery outcome. If no valid interaction ID is available, diagnostics indicate that it is unavailable rather than inventing one. Provider error bodies MUST NOT be copied without sanitization.

| Sensitive value | Permitted diagnostic representation |
| --- | --- |
| External user identifier, including LINE User ID | HMAC only; never the raw value |
| Input or generated text | HMAC only, labeled by processing stage; never the raw text |
| API key, reply token, or HMAC key | Never recorded, including as HMAC |

HMAC applies only to values written to diagnostics. It does not replace Model input, Channel output text, RCCP IDs, or request and response fields. The Adapter applies HMAC to an external user ID and does not send the raw value to Orchestration.

The HMAC key is dedicated and separate from API credentials. Comparison scope is separated by tenant, service, and purpose. An HMAC value is not authorization, Idempotency, or an RCCP interaction or message ID. HMAC diagnostics are not anonymous or automatically public. This specification does not authorize full request/response or Character-instruction logging.

| Failure | Required behavior |
| --- | --- |
| Required HMAC configuration is missing or invalid at startup | Do not begin affected processing |
| HMAC generation fails at runtime | Omit the affected value; do not fall back to raw data or a plain hash |
| Diagnostic sink write fails | Do not regenerate or redeliver the conversation, and do not select the fixed failure message solely because diagnostics failed |

An implementation MUST NOT enter an unbounded diagnostic loop. SDK, HTTP, debug, and exception paths also MUST NOT expose raw text, external user identifiers, or secrets. HMAC-key rotation MUST NOT change RCCP Actor, conversation, interaction, or message IDs.

## 13. Conformance requirements

A conforming implementation verifies at least the following behavior:

| ID | Requirement |
| --- | --- |
| C-01 | Unsupported or unauthorized Channel input does not enter ordinary conversation processing |
| C-02 | Valid Direct Chat input crosses the Adapter, Orchestration, and Model boundaries and produces a validated Channel reply |
| C-03 | Invalid request structure, scope, targets, limits, identifiers, and text are rejected without invented defaults |
| C-04 | Invalid response correlation, message count, IDs, content, and output limits are not accepted as success |
| C-05 | Version headers, unsupported paths, HTTP/body mismatch, malformed JSON, and duplicate properties are handled as specified |
| C-06 | Deadline calculation and validation do not extend the deadline on queueing, redelivery, or re-execution |
| C-07 | Adapter and Model automatic retries are disabled, and a late result does not create an additional delivery |
| C-08 | Refusal, truncation, invalid Structured Output, and missing output are failures rather than partial successes |
| C-09 | Pre-send and post-send failures remain distinct, and fixed failure-message delivery does not loop |
| C-10 | Redelivery preserves input IDs while a new identical-text input receives new IDs |
| C-11 | Actor, conversation, and Channel-connection IDs obey their scope and stability rules |
| C-12 | Static configuration faults prevent affected admission; runtime Model-selection faults do not trigger an implicit fallback |
| C-13 | Channel, Orchestration, and Provider responsibilities remain separated and common contracts contain no secrets or Provider-specific types |
| C-14 | Diagnostics contain no raw external identifier or conversation text and do not alter RCCP data or behavior |
| C-15 | HMAC or diagnostic-sink failure does not expose raw values, repeat conversation processing, or enter an unbounded loop |

Conformance with this contract alone does not establish Production readiness, durable processing, exactly-once behavior, or a supported RCCP deployment profile.

## 14. Versioning and compatibility

- The v0.1-to-v0.2 request and response change may be breaking.
- Adapter and Orchestration require matching contracts, but need not be built from the same source commit. Contract tests establish the supported pairing.
- After v0.2 is finalized, adding required fields, changing field names, meanings, or types, and deleting fields are compatibility-affecting changes.
- Safe rejection by an older consumer is not backward compatibility. A sender cannot unilaterally treat a new unknown content type as a compatible addition.
- v0.1 MUST NOT be silently removed merely because v0.2 work begins. Its coexistence and deprecation are separate decisions.

## 15. External references

- [LINE Messaging API: Webhook event objects](https://developers.line.biz/en/reference/messaging-api/nojs/#webhook-event-objects)
- [LINE Messaging API: Send reply message](https://developers.line.biz/en/reference/messaging-api/#send-reply-message)
- [Anthropic TypeScript SDK](https://platform.claude.com/docs/en/cli-sdks-libraries/sdks/typescript)
- [Anthropic Structured Outputs](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)
- [Anthropic stop reasons](https://platform.claude.com/docs/en/build-with-claude/handling-stop-reasons)

## 16. License

This specification is distributed under the repository's MIT License.
