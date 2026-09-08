# Roidoya Character Chat Platform — Architecture and Technical Disclosure

**Status:** Fifth Public Edition\
**Edition:** 2026-09-08.2\
**Publication date:** 2026-09-08 (Asia/Tokyo)\
**Author:** Akihiro Fujimoto\
**Project:** Roidoya Character Chat Platform (RCCP)\
**Canonical repository:** `AkihiroFujimotoChocolate/roidoya-character-chat-platform`\
**Canonical path:** `docs/publications/rccp-architecture-and-technical-disclosure.md`\
**License:** MIT License, consistent with the repository license unless otherwise stated

## Abstract

Roidoya Character Chat Platform (RCCP) is an engineering architecture for building and operating production character-chat services across multiple interaction channels while keeping channel transport, character behavior, durable continuity, orchestration, model access, tools, linguistic processing, policy evaluation, affect state, expression rendering, and operational controls replaceable and independently evolvable. This document specifies implementable responsibility boundaries, stable interaction contracts, creator-editable workflow publication, persistent memory and relationship state, ontology and knowledge-graph-backed continuity, multi-instance ordering and idempotency, recoverable external effects, replay and migration, provider-neutral model/tool mediation, consent-scoped cross-channel identity, multi-party and character-to-character conversation, public-room continuity, layered input/output rule evaluation, language detection and language policy, prompt-injection-resistant execution boundaries, target-specific character emotion, capability-aware structured expression, cyclic world observation and action resolution, and authorized creator/operator intervention without depending on unconstrained model autonomy. It includes complete processing embodiments, alternative implementations, state machines, technical combinations, and functional figures using application code, workflow engines, managed cloud services, messaging platforms, queues, knowledge formats, graph or non-graph stores, language-processing libraries, rule engines, classifiers, and model APIs.

**Keywords:** character chat platform; conversational agent; channel adapter; durable workflow; creator-editable workflow; immutable publication revision; character memory; relationship state; ontology; knowledge graph; Open Knowledge Format; multi-party conversation; many-to-many conversation; character-to-character conversation; idempotent webhook processing; conversation ordering; effect journal; cross-channel identity; topic-scoped consent; provider-neutral model service; tool mediation; language identification; fastText language identification; morphological analysis; MeCab; pattern matching; policy rule engine; prohibited-term detection; safe-span exception; prompt injection; structured model output; affect state; target-specific emotion; facial expression; gesture; animation cue; channel capability mapping; authoritative world state; observer-scoped projection; character belief; action intent; action resolution; world event; creator intervention; operator intervention; bounded autonomy; live-chat character system; derived-state migration; execution provenance

> This document describes implementable technical architectures and embodiments for building and operating character-chat services with RCCP. It includes both implemented and not-yet-implemented arrangements. A described embodiment does not imply that it is currently implemented, selected as the only supported architecture, or claimed to be novel.

## 1. Purpose and scope

Roidoya Character Chat Platform (RCCP) is an engineering platform for building, operating, validating, and continuously improving production-quality character-chat services while reusing common technical foundations.

The current focus is text-based character interaction. The architecture is intentionally designed so that additional interaction forms, content parts, channels, tools, state models, memory models, and model providers can be added without requiring unrelated parts of a service to be redesigned.

This disclosure focuses on technical arrangements for:

- separating external-channel concerns from channel-independent character behavior;
- composing character experiences through orchestration;
- exposing reusable character-related capabilities as replaceable services or modules;
- maintaining continuity across interactions through memory, relationship state, workflow state, event state, or combinations of them;
- representing world facts, character beliefs, actor memories, and relationships through ontologies, knowledge graphs, portable knowledge bundles, or equivalent structured records;
- coordinating direct, group, room, broadcast, many-to-many, and character-to-character interactions without collapsing their participant, speaker, audience, or privacy scopes;
- linking channel-local identities while authorizing cross-channel reuse separately by topic, relationship, character, destination, audience, purpose, and time;
- evaluating user input, retrieved content, tool results, model output, and rendered output through versioned pattern, rule, dictionary, morphological, and classifier policies with explicit precedence and safe exceptions;
- identifying message-level or span-level language through replaceable existing libraries, then applying creator-, operator-, or developer-controlled allowed-language behavior without treating language identification itself as an RCCP-specific invention;
- separating untrusted content from instructions and enforcing authorization, tool, memory-write, and output boundaries even when prompt-injection detection is uncertain or fails;
- maintaining character-specific global mood, target-directed emotion, relationship state, expression intent, and channel-rendered behavior as distinct but related records;
- producing validated structured character output containing text, emotion, facial expression, gesture, pose, animation cues, or other semantic intents and transforming it according to Channel capabilities;
- separating authoritative world events, observer-scoped projections, and Character subjective state while connecting them through a validated action feedback loop;
- allowing Creators and authorized Operators to introduce scheduled events, correct drift, direct Character development, change perception, or pause autonomy through versioned and auditable interventions;
- preserving stable contracts while allowing implementations and infrastructure to change;
- maintaining correctness in horizontally scaled or multi-instance deployments;
- controlling duplicate processing, concurrency, backlog, rate limits, and external-provider failures;
- allowing content creators to modify character-specific experience flows without requiring infrastructure changes;
- defining production execution against supported cloud infrastructure while localizing cloud-specific dependencies behind stable application boundaries;
- providing concrete and alternative implementation forms rather than requiring a single deployment topology, storage product, workflow engine, or model provider.

RCCP does not require every deployment to use every component described here. Components may be combined, omitted, replaced, embedded in another process, or deployed independently when the resulting arrangement preserves the required responsibility and contract boundaries.

## 2. Technical problems addressed

Production character-chat systems repeatedly encounter technical problems that are not solved merely by connecting a user interface to a language model.

Representative problems include:

1. **Channel-specific behavior leaks into character logic.** LINE, Web, Discord, Slack, and other channels differ in authentication, event delivery, identifiers, message limits, retries, reply semantics, batching, rate limits, and transport behavior.
2. **Character behavior becomes tightly coupled to one model provider or prompt call.** Replacing a model, adding retrieval, introducing tools, or changing memory behavior then requires large application changes.
3. **Long-lived character experiences require several kinds of continuity.** Conversation history alone may be insufficient for durable memory, relationship progression, story state, scheduled events, quests, or character-side state.
4. **Content changes and infrastructure changes have different owners and cadences.** Character creators may need to adjust experience logic while developers and operators retain control of deployment, security, resource boundaries, and shared services.
5. **Distributed execution introduces correctness failures.** Duplicate webhooks, retries, concurrent messages, multiple worker replicas, expired reply tokens, queue backlogs, provider throttling, and partial failures can produce duplicate or contradictory character behavior.
6. **Interfaces outlive implementations.** Services, providers, databases, workflow engines, and deployment environments may change while existing channels and content should continue operating.
7. **Local-only substitutes can hide production failures.** An application that uses in-memory queueing, locking, or state in development may exercise materially different execution paths from its production deployment.
8. **Character services need operational boundaries.** Cost, tool access, model access, data access, timeouts, concurrency, and resource use need explicit limits that can be enforced without embedding every limit into character content.
9. **Flat history cannot express qualified or conflicting knowledge safely.** A world fact, a character's mistaken belief, an actor-provided statement, and a private inference can refer to the same subject while differing in perspective, source, validity, confidence, and permitted audience.
10. **An ingress event is not necessarily a conversational turn.** Group, room, broadcast, and many-to-many interaction may require multiple actor events, multiple eligible character speakers, deterministic turn sealing, causal ordering, and speaker-identified delivery.
11. **Shared scenes can leak or corrupt private state.** Multiple actors and characters can participate in one interaction while each character, actor, relationship, and audience has a different authorized view and a different state-transition scope.
12. **Identity correspondence does not itself authorize data reuse.** Proving that two channel-local identities belong to the same actor must remain separate from deciding which memories, relationships, topics, purposes, destinations, and audiences may cross the channel boundary.
13. **Naive pattern matching causes both missed detections and false positives.** Raw, normalized, tokenized, lemmatized, and classified forms can disagree; unrestricted substring matching can reject permitted compound words; and an exception can become an unintended bypass if its scope and precedence are not explicit.
14. **Language identification and language permission are different decisions.** Short messages, mixed-language text, names, code, emoji, and uncertain detector output require a normalized result and a separately versioned policy that can allow, translate, clarify, refuse, route, or return a fixed response.
15. **Prompt injection cannot be reduced to a prohibited-term list.** Direct user instructions and indirect instructions embedded in retrieved documents, tool results, files, images, or prior memory can attempt to cross trust boundaries even when no known attack phrase matches.
16. **A character's felt emotion is not the same as its displayed expression.** The character can feel one emotion toward one entity, discuss another entity, address a third participant, and intentionally display a different expression to an audience.
17. **Structured expressive output requires semantic validation and capability negotiation.** A model-generated facial expression, pose, gesture, or animation identifier cannot be passed directly to every Channel or renderer, and malformed or unsupported output requires deterministic fallback.
18. **World fact, observable result, and Character belief are not interchangeable.** One event can have hidden causes, limited witnesses, delayed reports, conflicting interpretations, and Character-specific consequences.
19. **Generated narration must not become authoritative state by implication.** A User, Model, Tool, or Character can propose an action or assert a fact without having permission to commit a World Event, memory, relationship, or belief transition.
20. **Autonomous behavior cannot be the only continuity-control mechanism.** A production experience needs explicit ways for Creators and Operators to schedule story events, correct drift, direct growth or perception change, pause effects, and activate safe revisions.
21. **Human intervention still requires technical boundaries.** A privileged correction or event can conflict with concurrent state, exceed its delegated scope, duplicate a scheduled occurrence, hide historical causes, or partially update multiple Characters unless authority, revision, idempotency, transaction, and provenance are explicit.

The disclosed architecture addresses these problems through explicit responsibility boundaries, stable contracts, replaceable implementations, orchestration, shared reliability mechanisms, and concrete failure-handling rules.

## 3. Terminology

### 3.1 Channel

An external interaction surface or transport through which an end user or external system exchanges events with a character service. Examples include LINE, a Web application, Discord, Slack, another messaging platform, or an application-specific protocol.

### 3.2 Channel Adapter

A component responsible for channel-specific ingress and egress behavior. It converts external events into a channel-neutral internal representation and converts internal outputs into channel-specific responses.

### 3.3 Orchestration

The execution layer that composes reusable capabilities into a character-specific interaction flow. Orchestration may include sequential execution, branching, parallel work, retries, waiting, asynchronous work, schedules, event handling, and durable long-running workflows.

### 3.4 Core Service

A reusable channel-independent capability invoked by orchestration or by another core service. Examples include Character, Memory, State/Relationship, Model, Search, and Tool services.

A core service is a logical responsibility. It does not have to be a separately deployed network service.

### 3.5 Character definition

Versioned configuration and content that define character-specific behavior or references to such behavior, such as identity, behavioral instructions, speaking style, model policy, memory policy, workflow reference, tool permissions, safety constraints, or content references.

### 3.6 Continuity state

Persistent information that can affect later interactions. Continuity state may include conversation history, durable memory, relationship state, workflow state, event state, story or quest progression, character state, or other service-defined state.

### 3.7 Stable contract

A message, API, event, or data interface intended to survive implementation changes. Stable contracts are versioned or evolved using compatibility rules so callers do not need to change whenever an implementation is replaced.

### 3.8 Reference embodiment

A concrete, implementable arrangement that demonstrates how the architecture can be realized. A reference embodiment is not the only permitted arrangement.

### 3.9 Actor

A person, account holder, external agent, or other originator that participates through one or more channel-local identities. An Actor identifier can be local to a channel, pairwise across selected channels, or canonical within an authorized RCCP scope.

### 3.10 Qualified Knowledge Statement

A representation of a subject-predicate-object assertion together with qualifiers such as perspective holder, truth mode, source, confidence, validity interval, publication revision, visibility, and consent. The physical representation can be a graph statement, row, document, event, or equivalent record.

### 3.11 Participant and speaker

A Participant is an Actor, character, system role, or permitted external agent included in an interaction scope. A speaker is the Participant selected to emit a particular output. Participation does not automatically confer speaking eligibility, access to every context item, or permission to receive every output.

### 3.12 Multi-party turn

An immutable or versioned unit formed from one or more admitted source interactions and processed under a participant snapshot, audience scope, formation policy, causal identity, and state revision. A Multi-party Turn can select zero, one, or multiple character speakers.

### 3.13 Identity Link

A verified correspondence between two or more channel-local identities or between a channel-local identity and a canonical Actor identifier. An Identity Link establishes identity correspondence only; it does not by itself authorize memory or relationship transfer.

### 3.14 Continuity Sharing Grant

A revisioned authorization that permits selected continuity data to be reused across specified identity, topic, relation, character, destination, audience, purpose, and time scopes. The grant can be narrowed, superseded, revoked, expired, or deleted independently of an Identity Link.

### 3.15 Text representation

A versioned view derived from immutable raw text for a specific evaluation purpose. Examples include Unicode-normalized text, case-folded text, script-normalized text, token sequences, dictionary forms, readings, and classifier feature input. A Text Representation retains a mapping to raw offsets so a result can be explained, rendered, or re-evaluated after a policy or analyzer revision.

### 3.16 Policy rule and policy decision

A Policy Rule declares a condition, scope, priority, provenance, and action. A Policy Decision is the immutable result of evaluating one or more rules and model-based signals against a versioned input. A match is evidence; it is not automatically the final action.

### 3.17 Language Detection Result

A normalized record produced by a replaceable language-detection adapter. It can contain message-level and span-level language candidates, script, detector-specific raw scores, calibrated confidence, reliability, model and library revisions, and the evaluated Text Representation. It does not itself allow or prohibit a language.

### 3.18 Affect state

A versioned record of emotion or mood experienced by a character. An Affect State can identify an experiencer, target, cause, category or dimensional value, intensity, confidence, validity interval, decay, privacy, provenance, and revision. It is separate from long-lived Relationship State and externally rendered Expression Intent.

### 3.19 Expression intent

A validated, channel-neutral semantic description of what a character intends to communicate or display, such as text, emotion label, facial expression, gesture, pose, voice style, or animation cue. A Channel Adapter or renderer maps the intent to supported capabilities and assets.

## 4. Logical architecture

The backend responsibilities are organized around three principal logical areas:

```text
External Channels
        |
        v
+------------------+
| Channel Adapters |
+------------------+
        |
        v
+------------------+
|  Orchestration   |
+------------------+
        |
        v
+------------------+
|  Core Services   |
+------------------+
        |
        v
Persistent State / External Providers / Infrastructure
```

This is a logical responsibility model, not a mandatory physical deployment model.

A deployment may place multiple areas in one process for simplicity, or may deploy individual components independently for scaling, security, failure isolation, technology choice, or release independence.

### 4.1 Responsibility rule

Channel-specific concerns should terminate at a Channel Adapter unless a downstream capability genuinely needs channel metadata.

Character behavior, memory policy, relationship progression, model selection, retrieval policy, and reusable tools should not depend directly on LINE reply tokens, Discord interaction IDs, HTTP cookies, or equivalent transport-specific mechanisms.

When downstream logic needs limited channel context, the adapter conveys normalized metadata rather than exposing the complete transport API as a platform-wide dependency.

## 5. Channel Adapters

A Channel Adapter may perform the following functions:

- receive webhook requests, socket messages, HTTP requests, or other channel events;
- validate signatures, tokens, authentication, or origin-specific security properties;
- reject malformed input before it enters character processing;
- filter unsupported event types;
- derive normalized conversation and actor identifiers;
- retain channel-specific correlation data required for later replies;
- translate external message payloads into a common interaction contract;
- apply channel-specific size, timing, batch, or reply constraints;
- serialize outputs into channel-specific response objects;
- call the channel's reply or send API;
- handle channel retry or redelivery behavior;
- implement channel-specific rate or quota controls;
- expose channel-specific telemetry while propagating platform-wide correlation identifiers.

### 5.1 Normalization boundary

A channel event can be normalized into an internal interaction with at least:

- a request or correlation identifier;
- an optional upstream event identifier;
- an origin or channel label;
- a conversation identifier;
- an actor identifier;
- a target character identifier when applicable;
- one or more content parts;
- selected normalized metadata;
- processing limits or deadlines when applicable.

The adapter may retain opaque channel-specific reply data outside the normalized interaction, for example by storing it in a work item or correlation record that is consumed by the same adapter's outbound path.

### 5.2 Alternative adapter placements

The following are all valid embodiments:

- one independently deployed adapter per channel;
- multiple channel adapters in one process with separate endpoints;
- a shared ingress process with channel-specific adapter modules;
- serverless functions for individual channels;
- an embedded Web adapter inside a larger Web application;
- an adapter that publishes normalized interactions to a durable queue;
- an adapter that invokes orchestration synchronously when the channel's timing constraints allow it.

The key requirement is preservation of the channel-specific responsibility boundary, not a specific process topology.

Section 50 gives concrete mappings for several external communication services. Naming an external service in those embodiments identifies an integration target and its public API constraints; it does not make that service a platform-wide dependency.

## 6. Orchestration

Orchestration composes character-specific experiences using reusable capabilities.

A single interaction may execute a flow such as:

```text
Receive normalized interaction
        |
        v
Load character revision
        |
        v
Load selected continuity state
        |
        v
Evaluate workflow conditions
        |
        +--> optional retrieval/search
        |
        +--> optional tool calls
        |
        +--> optional state transition
        |
        v
Build model input
        |
        v
Generate candidate output
        |
        v
Validate/transform output
        |
        +--> write memory
        +--> update relationship/state
        +--> emit event/schedule continuation
        |
        v
Return channel-neutral output
```

This flow is an example. Steps may be skipped, reordered where semantics permit, executed in parallel, retried, delegated, or resumed later.

### 6.1 Workflow capabilities

An orchestration implementation may support:

- sequential steps;
- conditional branches;
- parallel branches;
- fan-out and fan-in;
- retries with bounded policies;
- compensation or rollback actions;
- asynchronous activities;
- event-driven continuation;
- timer or scheduled continuation;
- wait and resume;
- durable long-running workflows;
- human or creator approval steps;
- sub-workflows;
- reusable workflow templates;
- per-character workflow customization.

### 6.2 Existing workflow engines

RCCP does not require a proprietary workflow runtime or RCCP-specific workflow language.

One embodiment uses an existing workflow engine or durable-workflow system and integrates RCCP capabilities as activities, tasks, workers, HTTP services, functions, or plugins.

Another embodiment uses ordinary application code for short synchronous flows and introduces a workflow engine only when waiting, retry persistence, long-running execution, schedules, or creator editing justify it.

A third embodiment combines both: application code implements low-latency steps while a durable engine coordinates long-lived state transitions.

Section 49 gives concrete RCCP mappings for several existing orchestration runtimes. Those mappings are alternative embodiments, not a requirement that RCCP select one universal engine.

### 6.3 Content creator boundary

Orchestration is a principal boundary at which character-specific experiences can be designed or changed by content creators.

A system can expose different editing surfaces over the same execution model:

- forms for common settings;
- reusable templates for common interaction patterns;
- a graphical workflow editor;
- declarative YAML/JSON workflow definitions;
- code-based workflows;
- an advanced editor exposing the workflow engine's native representation;
- a restricted creator-facing view with developer-controlled capabilities.

A production system may require review, validation, preview, approval, versioning, staged rollout, or rollback before a creator-edited workflow becomes active.

## 7. Core Services

Core Services provide reusable channel-independent capabilities. The following list is illustrative rather than exhaustive.

### 7.1 Character Service

A Character Service can:

- resolve a character by identifier;
- resolve an immutable or versioned revision;
- provide behavioral instructions and style constraints;
- provide references to workflows, memories, knowledge, tools, assets, or policies;
- support staged or published revisions;
- expose metadata required by other services without exposing creator-only editing data to runtime callers.

A concrete character definition may use a structure such as:

```json
{
  "character_id": "char:alice",
  "revision": "2026-08-09T01:00:00Z",
  "display_name": "Alice",
  "behavior": {
    "instructions_ref": "content://characters/alice/instructions",
    "style_ref": "content://characters/alice/style"
  },
  "workflow_ref": "workflow://character-chat/default",
  "model_policy_ref": "policy://models/default-chat",
  "memory_policy_ref": "policy://memory/long-term-v2",
  "tool_policy_ref": "policy://tools/alice-default"
}
```

The references may instead be embedded documents, database keys, Git revisions, object-storage URIs, package references, or service-specific identifiers.

### 7.2 Memory Service

A Memory Service persists and retrieves information intended to affect later interactions.

Possible memory classes include:

- raw interaction history;
- rolling summaries;
- extracted facts;
- user preferences;
- character observations;
- episodic events;
- semantic memories stored with embeddings;
- manually curated memories;
- memories scoped to a character, actor, conversation, group, account, or service.

A memory record can include:

```json
{
  "memory_id": "mem:01J...",
  "scope": {
    "character_id": "char:alice",
    "actor_id": "user:123"
  },
  "kind": "episodic",
  "content": "The user said they plan to visit Kyoto next month.",
  "source_event_id": "evt:abc",
  "created_at": "2026-08-09T01:23:45Z",
  "importance": 0.62,
  "expires_at": null,
  "metadata": {}
}
```

A different implementation may avoid normalized memory records entirely and instead use conversation summaries, vector retrieval, an event log, a graph representation, or provider-managed memory. The orchestration contract should allow the memory implementation to be replaced without forcing a channel adapter rewrite.

### 7.3 State / Relationship Service

A State or Relationship Service stores structured state that is better represented as explicit fields or transitions than as natural-language memory.

Examples include:

- relationship level;
- trust or affinity values;
- quest or story progression;
- unlocked topics;
- event completion flags;
- character mood or mode;
- cooldowns;
- counters;
- feature eligibility;
- per-user or per-conversation state machines.

A state record may be versioned for optimistic concurrency:

```json
{
  "state_key": "relationship:char:alice:user:123",
  "version": 42,
  "values": {
    "affinity": 18,
    "chapter": "chapter-3",
    "flags": ["met_at_station", "received_key"]
  },
  "updated_at": "2026-08-09T01:25:00Z"
}
```

Relationship state may be a separate service, a namespace within a general State Service, part of a Character Runtime, or encoded as workflow state. These are alternative embodiments.

### 7.4 Model Service

A Model Service isolates model-provider selection and invocation from character workflows.

It may provide:

- provider routing;
- model selection by policy;
- prompt or message transformation;
- timeout and retry policy;
- token or cost budgets;
- streaming or non-streaming generation;
- structured output validation;
- fallback providers or models;
- safety-policy integration;
- provider-specific telemetry;
- caching when semantically appropriate.

A workflow can request a capability such as `generate character response` without embedding a provider-specific HTTP request in the workflow definition.

An alternative embodiment allows the workflow to invoke providers directly but localizes provider-specific details in reusable workflow activities or libraries.

### 7.5 Search Service

A Search Service may expose retrieval over:

- character lore;
- service documentation;
- user-authorized data;
- conversation archives;
- structured databases;
- vector indexes;
- full-text indexes;
- knowledge graphs;
- external search providers.

Search can be called explicitly by a workflow, selected by a model/tool loop, or triggered by a policy based on message classification.

### 7.6 Tool Service

A Tool Service exposes controlled external capabilities such as:

- application APIs;
- game actions;
- calendar or scheduling actions;
- content lookup;
- purchases or account actions where authorized;
- device control;
- internal business operations;
- arbitrary service-specific functions.

The tool boundary can enforce authentication, authorization, allow-lists, parameter validation, rate limits, cost limits, audit logging, and side-effect confirmation independently of model-provider behavior.

## 8. Stable interaction contract

Service-to-service messages should be treated as long-lived contracts.

Where existing message concepts are sufficient, implementations should prefer conventional concepts such as `message`, `role`, and `content` rather than inventing unnecessary RCCP-specific vocabulary.

The internal contract should not be fixed to one model provider's API.

### 8.1 Reference interaction envelope

One implementable contract is:

```json
{
  "request_id": "req:01J...",
  "event_id": "line:event:abc",
  "origin": {
    "platform": "line",
    "channel_instance": "official-account:primary"
  },
  "conversation": {
    "id": "conversation:xyz"
  },
  "author": {
    "id": "actor:123"
  },
  "character": {
    "id": "char:alice"
  },
  "message": {
    "role": "user",
    "content": [
      {
        "type": "text",
        "text": "Hello"
      }
    ]
  },
  "limits": {
    "deadline_ms": 20000,
    "max_output_messages": 5,
    "max_chars_per_message": 1000
  },
  "metadata": {}
}
```

The initial text-focused implementation can simplify `content` to a string. A later compatible version can introduce multiple content parts such as text and images.

### 8.2 Reference output envelope

A corresponding output may be:

```json
{
  "request_id": "req:01J...",
  "status": "ok",
  "messages": [
    {
      "role": "assistant",
      "content": [
        {
          "type": "text",
          "text": "Hello. Good to see you again."
        }
      ]
    }
  ],
  "effects": {
    "memory_writes": [],
    "state_updates": [],
    "events": []
  },
  "metadata": {}
}
```

Another embodiment keeps memory/state/event effects internal to orchestration and returns only user-visible messages. Both arrangements are disclosed.

### 8.3 Correlation identifiers

A distributed implementation should preserve correlation across:

- inbound channel event;
- normalized interaction;
- queued work item;
- workflow execution;
- model call;
- tool call;
- persistence updates;
- outbound channel request.

The same identifier need not be used everywhere. A trace can link request ID, event ID, workflow execution ID, and provider request IDs.

## 9. End-to-end processing flow

A robust processing flow can use the following stages.

### Stage A — Ingress

1. Receive a channel event.
2. Validate channel-specific authenticity.
3. Parse the event while preserving any raw representation required by signature validation.
4. Filter unsupported or non-actionable events.
5. Derive normalized actor and conversation keys.
6. Check event idempotency when the channel can redeliver.
7. Enforce ingress-level concurrency or backlog policy when required by the channel.
8. Create a normalized work item.

### Stage B — Admission and durable handoff

1. Persist or enqueue the work item before acknowledging an event when the channel protocol permits or requires it.
2. Alternatively, acknowledge first when the channel requires a very short response window and use a channel-supported later-response mechanism.
3. Record enough correlation data to prevent duplicate replies.
4. Apply bounded backlog and overload policy.

### Stage C — Orchestration

1. Resolve the character and revision.
2. Load required continuity state.
3. Evaluate workflow conditions.
4. Invoke search, tools, or other services as needed.
5. Invoke one or more model operations.
6. Validate or transform generated output.
7. Compute memory writes and state transitions.
8. Persist effects with concurrency control appropriate to the state model.
9. Return or publish channel-neutral output.

### Stage D — Channel egress

1. Enforce channel-specific message count, size, formatting, and timing constraints.
2. Serialize channel-neutral outputs.
3. Attempt channel reply/send operation.
4. Record provider request IDs and result status.
5. Do not repeat non-idempotent replies solely because a lower-level send attempt has an ambiguous outcome unless the channel provides a safe deduplication mechanism.

## 10. Continuity across interactions

RCCP supports character experiences that continue beyond one independent request/response pair.

Continuity may be created through one or more of the following mechanisms.

### 10.1 Conversation context

Recent messages or summaries are included in model input.

### 10.2 Durable memory

Information from earlier interactions is persisted and selectively retrieved later.

### 10.3 Relationship state

A structured relationship model changes based on interactions and affects later behavior.

### 10.4 Workflow state

A durable workflow remembers its execution position and resumes after an external event, time delay, or later user message.

### 10.5 Event or story state

Events are recorded and later transitions depend on which events occurred.

### 10.6 Combined continuity

A character interaction may simultaneously use:

- recent message context for immediate coherence;
- durable memory for recalled facts;
- relationship state for long-term progression;
- workflow state for an active quest;
- scheduled events for future interaction.

These mechanisms are complementary and need not be forced into a single universal memory abstraction.

## 11. Creator-editable character experiences

A production system can separate **capability definition** from **experience composition**.

Developers define and operate reusable capabilities such as model invocation, retrieval, state storage, tool access, and channel integrations.

Content creators compose or configure those capabilities through character definitions and workflows.

### 11.1 Example workflow

```text
on user_message:
    character = load_character(active_revision)
    state = load_relationship_state(character, actor)
    memory = retrieve_memory(character, actor, user_message)

    if state.chapter == "prologue":
        context = search(character.prologue_knowledge, user_message)
    else:
        context = search(character.default_knowledge, user_message)

    result = model.generate(
        character = character,
        message = user_message,
        memory = memory,
        state = state,
        context = context
    )

    if result.contains_state_transition:
        update_state(result.state_transition)

    write_selected_memory(result.memory_candidates)
    return result.messages
```

The workflow may be expressed as source code, a workflow-engine definition, a graphical graph, or another executable representation.

### 11.2 Publishing workflow changes

One embodiment separates draft and active workflow revisions:

1. Creator edits a draft.
2. Static validation checks missing references, invalid tool permissions, cycles where prohibited, or schema incompatibility.
3. Preview executes the draft against test interactions.
4. Automated regression cases compare expected properties.
5. An authorized reviewer approves the revision.
6. The revision receives an immutable identifier.
7. A deployment or configuration pointer changes the active revision.
8. Runtime interactions resolve the active revision.
9. Rollback changes the pointer to a previous revision without reverting unrelated infrastructure.

Alternative embodiments omit approval or preview for low-risk environments while preserving versioned activation.

## 12. Reliability in multi-instance deployments

A horizontally scaled service must not rely on process-local state for decisions that require cross-replica correctness.

Examples include:

- duplicate-event suppression;
- exclusive processing for the same actor or conversation;
- shared backlog limits;
- global or partitioned rate limits;
- durable workflow ownership;
- state transition concurrency;
- scheduled continuation.

### 12.1 Idempotency

An event source identifier, when available, can be recorded in shared storage with a processing state such as:

```text
received -> admitted -> processing -> effects_committed -> reply_attempted -> completed
```

A simpler implementation may only record `processed` after durable acceptance or after successful effect commitment.

The correct boundary depends on whether processing has externally visible side effects.

Alternative idempotency mechanisms include:

- a database uniqueness constraint on event ID;
- a compare-and-set insert;
- a queue system with duplicate detection;
- a workflow engine with deterministic workflow identifiers;
- an idempotency key propagated to downstream services;
- combinations of the above.

### 12.2 Per-actor or per-conversation concurrency

Some character experiences require ordered or exclusive processing for a user or conversation.

Implementations include:

- distributed lock with TTL/lease;
- queue partitioning by conversation key;
- message sessions with exclusive session receivers;
- durable workflow instance per conversation;
- optimistic concurrency on state combined with retry;
- application-level sequence numbers.

A lock must expire or be recoverable after worker failure. A session or partition mechanism can avoid a separate lock when it already guarantees the required ordering and exclusivity.

### 12.3 Backlog control

Admission can consider a shared backlog rather than only each replica's local queue.

Possible controls include:

- queue active-message count;
- queue depth plus age of oldest message;
- estimated processing delay;
- model-provider saturation;
- worker concurrency;
- per-tenant or per-character quotas;
- channel reply-token deadlines.

When the expected processing delay exceeds the channel's usable reply window, a system may reject, degrade, or switch response mode rather than accepting work that is likely to fail later.

### 12.4 Rate control

Rate limits may exist at multiple levels:

- per channel;
- per model provider;
- per tool provider;
- per tenant/service;
- per character;
- per actor;
- per worker replica;
- global shared limits.

A token bucket, leaky bucket, fixed/sliding window, provider quota service, queue-based pacing, or workflow concurrency limit can implement these policies.

The rate-control mechanism should be chosen according to whether the required limit is local, partitioned, or global.

## 13. Failure handling

### 13.1 Model provider failure

A model operation can fail because of timeout, throttling, quota, network failure, provider error, invalid output, or safety filtering.

Possible responses include:

- bounded retry when the operation is safe to repeat;
- alternate model within the same provider;
- alternate provider;
- configured fallback text;
- degraded workflow that omits an optional capability;
- deferred processing when the channel supports it;
- explicit failure surfaced to the channel adapter.

The workflow should distinguish failures that can be retried from failures that should not be repeated.

### 13.2 Tool failure

A tool call can be classified as read-only/idempotent or side-effecting.

Read-only calls may be retried under a bounded policy.

Side-effecting calls should use idempotency keys, transactional APIs, provider operation IDs, or explicit confirmation/reconciliation so retries do not duplicate effects.

### 13.3 Persistence conflict

When two executions attempt to update the same state, an implementation can:

- serialize the executions;
- use optimistic concurrency and recompute the transition;
- reject the later update;
- merge commutative fields;
- represent state changes as append-only events and derive current state.

### 13.4 Partial completion

If a model response was generated but state persistence failed, the system should define whether the reply may be sent.

A strong-consistency embodiment commits required state effects before exposing the reply.

A lower-latency embodiment sends the reply first and records best-effort secondary memories afterward, but only for effects whose loss does not violate user-visible invariants.

The distinction between required and optional effects should be explicit in workflow design.

## 14. Infrastructure responsibilities

Production RCCP deployments use supported cloud infrastructure for generic infrastructure responsibilities rather than relying on RCCP-specific reimplementations of those capabilities.

Required properties can include:

- durable queueing;
- shared persistence;
- distributed coordination;
- idempotency storage;
- secrets management;
- identity and access control;
- rate control;
- observability;
- hosting and autoscaling;
- backup and disaster recovery.

RCCP should rely on security and operational capabilities provided by the selected supported cloud infrastructure where those capabilities are not specific to RCCP. It should avoid unnecessarily duplicating, bypassing, or weakening those mechanisms.

Developers and System Operators remain responsible for selecting, configuring, and operating the supported infrastructure profile for their service.

The architecture should specify required properties before binding a logical responsibility to a specific cloud product.

### 14.1 Infrastructure adapter/library

A first-party implementation may use a common C#/.NET infrastructure library for shared concerns such as configuration, telemetry, identity integration, persistence helpers, and queue clients.

This library need not hide every difference among cloud platforms.

Cloud-specific features may be used when they provide clear value, provided the dependency is localized so unrelated character workflows and channel-neutral contracts do not become cloud-specific.

## 15. Production execution requirements

Production RCCP deployments require supported cloud infrastructure that provides the generic infrastructure properties required by the selected deployment profile.

Development-only components or substitutes do not establish production readiness. Production behavior that depends on shared persistence, identity, secrets, networking, failure handling, observability, backup/recovery, or other infrastructure properties must satisfy the corresponding production requirements.

### 15.1 Small production deployments

A small number of users does not make a development-only component production-grade.

A personal or small-team character service can use a smaller production deployment while still providing the persistence, secrets handling, backup, authentication, and failure behavior required for production use.

## 16. Deployment embodiments

The following are logical deployment embodiments. When used for Production RCCP, they are realized within a supported cloud infrastructure profile that satisfies the required production properties.

### 16.1 Compact deployment

Suitable for a small service while preserving logical boundaries:

```text
+---------------------------------------------------+
| One application process                           |
|                                                   |
| Channel Adapter modules                           |
| Orchestration module                              |
| Core Service modules                              |
+---------------------------------------------------+
          |             |              |
          v             v              v
      Database      Durable Queue   Model Provider
```

The modules use internal interfaces matching stable contracts. They can later be split into network services.

### 16.2 Service-oriented deployment

```text
Channel Adapter(s)
        |
        v
Interaction Queue / API
        |
        v
Orchestration Workers
   |       |       |
   v       v       v
Character Memory  Model ... Core Services
        |
        v
Shared/owned persistence
```

Services can be scaled independently.

### 16.3 Durable-workflow deployment

```text
Channel Adapter
      |
      v
Workflow Engine <---- timers/events
      |
      +--> Character activity
      +--> Memory activity
      +--> State activity
      +--> Search activity
      +--> Tool activity
      +--> Model activity
      |
      v
Channel-neutral response
```

The workflow engine persists execution state and resumes work after failures or waits.

### 16.4 Event-oriented deployment

Components publish domain or integration events to an event bus. Consumers update projections, memories, analytics, or schedules asynchronously.

Critical user-visible transitions may still use synchronous or transactional paths while non-critical effects are event-driven.

## 17. Reference embodiment: LINE channel with queued processing

The existing RCCP repository contains a LINE gateway and an early chat-layer contract. This section describes a concrete embodiment consistent with that design while generalizing it into the architecture above.

### 17.1 Ingress

1. LINE sends a webhook request.
2. The LINE Channel Adapter validates the `X-Line-Signature` over the raw request body.
3. The adapter accepts relevant message events and ignores unsupported events according to configured policy.
4. A `webhookEventId` is used as an idempotency identifier.
5. The adapter derives a user/conversation processing key.
6. The adapter checks whether the event can be admitted under per-user concurrency and shared backlog policy.
7. Accepted work is enqueued for background processing.
8. The webhook endpoint returns the channel-appropriate HTTP status.

### 17.2 Background processing

1. A worker consumes the work item.
2. A rate-control policy limits outbound reply activity or provider activity.
3. The worker invokes a channel-neutral character-processing contract.
4. The character-processing path may be a simple HTTP service, an orchestration workflow, or an in-process implementation.
5. The result contains channel-neutral reply messages.
6. The adapter validates LINE-specific limits, including the number and size of message objects.
7. The adapter calls the LINE Reply API using the original reply token.
8. Provider request identifiers and result status are logged.

### 17.3 Reply-token failure

If the reply token has expired, was already used, or is otherwise invalid, one embodiment records the failure and does not automatically fall back to a push message.

A different service policy may permit push fallback if permitted by the channel, user-consent model, cost policy, and service requirements. That alternative is separate from the core architecture.

### 17.4 Distributed locking alternatives

The LINE embodiment can use either:

- a shared database record or lease keyed by user/conversation; or
- a queue/session feature that guarantees exclusive ordered processing for the same session key.

The second embodiment can eliminate a separate distributed lock if its delivery semantics satisfy the required behavior.

### 17.5 Shared backlog alternatives

The adapter can sample shared queue depth periodically rather than querying administrative queue state for every webhook.

A cached queue-depth estimate with jittered refresh reduces management-plane calls and avoids synchronized polling across replicas.

Another embodiment uses queue age or application metrics rather than queue count.

## 18. Reference embodiment: versioned HTTP character-processing contract

An existing public implementation uses an endpoint shaped like:

```text
POST /chat/v0.1/generate-replies
```

with a text-focused request containing fields such as request ID, origin, conversation, author, message text, and limits.

This is a concrete existing contract, not the required version number for RCCP as a whole and not the version number of this document.

### 18.1 Compatibility strategy

A versioned HTTP embodiment can:

- place a contract version in the URL path;
- optionally duplicate the version in a header;
- omit a redundant body version field;
- ignore unknown response fields where safe;
- add optional fields compatibly;
- run multiple incompatible major or breaking versions concurrently during migration.

Alternative embodiments include media-type versioning, schema identifiers, gRPC package versions, event-type versions, or versioned queue topics.

The architectural requirement is controlled compatibility and migration, not one universal version-placement rule.

## 19. Multimodal extension

Although the current RCCP focus is text, a text-first message contract can be extended without redesigning channel, orchestration, and core-service boundaries.

For example:

```json
{
  "message": {
    "role": "user",
    "content": [
      {"type": "text", "text": "What do you think of this?"},
      {"type": "image_ref", "uri": "blob://uploads/01J...", "media_type": "image/jpeg"}
    ]
  }
}
```

The Channel Adapter is responsible for retrieving, validating, or safely referencing channel-provided media as required.

Orchestration decides whether to invoke image understanding, storage, moderation, extraction, or a multimodal model.

A model provider that accepts native multimodal messages can receive a transformed representation. Another provider can receive text produced by a separate image-analysis service.

Thus multimodal support does not require model-provider-specific objects to become the RCCP-wide contract.

## 20. Scheduled and proactive character behavior

A character experience may include actions not triggered directly by the current user message.

Examples include:

- scheduled reminders or events;
- story events that become available later;
- cooldown completion;
- periodic world-state changes;
- tool results arriving asynchronously;
- external application events;
- character-initiated messages where the channel and service policy permit them.

A durable orchestration embodiment stores a workflow instance and resumes it on timer or event.

An event-driven embodiment stores explicit state and starts a new workflow when a timer/event message arrives.

Both approaches can reuse the same Character, Memory, State, Tool, and Model services.

## 21. Security and trust boundaries

Security responsibilities should follow component boundaries.

### 21.1 Channel boundary

The Channel Adapter validates external authenticity and limits untrusted input before normalized processing.

### 21.2 Tool boundary

Tool execution enforces authorization independently of generated model text.

A model request cannot grant itself additional tool permissions merely by emitting a tool name.

### 21.3 Content creator boundary

Creator-editable workflows execute within operator/developer-defined capabilities, permissions, quotas, and deployment policies.

A workflow editor can expose only approved activities rather than arbitrary infrastructure credentials.

### 21.4 Secrets

Secrets are referenced through a secrets-management mechanism or runtime identity and are not embedded in character content, workflow definitions intended for creators, normal logs, or public configuration examples.

### 21.5 Data scopes

Memory, state, search, and tools can enforce explicit actor, character, tenant/service, and environment scopes so one character or conversation cannot retrieve another scope's data without authorization.

## 22. Observability

A production implementation should provide structured observability across the entire interaction path.

Useful signals include:

- inbound event count and validation failures;
- admitted/rejected/backlogged interactions;
- queue depth and queue age;
- per-conversation contention;
- workflow duration and failure step;
- model/provider latency, errors, throttling, and cost-related metrics;
- search and tool latency/failures;
- state concurrency conflicts;
- reply/send success and provider request IDs;
- duplicate/redelivery events;
- fallback usage;
- active character/workflow revision;
- resource saturation.

Logs should use correlation identifiers and avoid secrets or unrestricted raw private payloads by default.

## 23. Compatibility and migration

Stable contracts should evolve independently from internal implementation replacements.

### 23.1 Compatible additions

Examples include:

- adding optional metadata;
- adding a new content-part type that older consumers can ignore or reject explicitly;
- adding a new optional effect list;
- adding an enum value when consumers have defined unknown-value behavior.

### 23.2 Breaking changes

When a breaking change is required, possible migration techniques include:

- parallel API versions;
- versioned event types;
- dual publishing;
- adapter translation between old and new contracts;
- shadow execution;
- per-character or per-channel migration;
- read-old/write-new state migration;
- workflow-revision pinning for in-flight durable workflows.

### 23.3 Long-running workflow compatibility

A durable workflow may outlive a deployment.

An implementation can preserve compatibility by:

- pinning a workflow instance to a revision;
- keeping old worker/activity versions available until old instances finish;
- migrating workflow state through an explicit migration step;
- terminating/restarting only workflows whose product semantics permit it.

## 24. Explicit alternative embodiments

The following alternatives are specifically disclosed as implementable choices. They may be used independently or in combinations consistent with their semantics.

### 24.1 Communication

- synchronous HTTP;
- gRPC;
- durable queue messaging;
- publish/subscribe events;
- in-process interface calls;
- workflow-engine task/activity protocols;
- hybrid synchronous and asynchronous communication.

### 24.2 Service topology

- modular monolith;
- independently deployed services;
- serverless functions;
- containerized workers;
- mixed topology where high-latency or independently scaled capabilities are separated and low-latency capabilities remain in-process.

### 24.3 Workflow execution

- ordinary application code;
- existing workflow engine;
- durable workflow engine;
- state machine;
- event-sourced process manager;
- hybrid code plus durable coordinator.

### 24.4 Conversation ordering

- distributed lock;
- queue partition/session;
- workflow instance per conversation;
- optimistic concurrency with retry;
- sequence numbers;
- no ordering where the experience does not require it.

### 24.5 Persistence

- relational database;
- document database;
- key-value store;
- event store;
- vector database for selected memory/search functions;
- object storage for large content;
- multiple stores owned by different capabilities.

### 24.6 Memory

- complete history;
- bounded recent history;
- summarization;
- structured fact memory;
- semantic/vector memory;
- episodic memory;
- graph memory;
- provider-managed memory;
- manually curated memory;
- combinations with selection policy.

### 24.7 State/relationship

- dedicated Relationship Service;
- general State Service;
- workflow state;
- event-sourced aggregate;
- fields embedded in a user-character profile;
- external game/application state accessed through a tool.

### 24.8 Model routing

- fixed provider/model;
- policy-selected model;
- per-character model policy;
- per-request routing;
- fallback chain;
- ensemble/multiple model calls;
- local/self-hosted model;
- external managed provider;
- provider-specific optimization hidden behind a stable capability contract.

### 24.9 Creator customization

- configuration-only;
- template parameters;
- visual workflow editor;
- declarative workflow files;
- code-based extensions;
- approved tool/activity catalog;
- full native workflow-engine editing for advanced creators;
- separate draft/review/publish lifecycle.

### 24.10 Infrastructure

- supported cloud infrastructure profiles;
- cloud-managed or cloud-hosted infrastructure capabilities that satisfy required properties;
- cloud-specific adapters localized behind application boundaries;
- multiple supported cloud profiles sharing platform-level contracts;
- infrastructure-as-code and deployment automation for supported profiles.

## 25. Explicit combination disclosures

The following combinations are explicitly disclosed to remove ambiguity about whether individual components may be used together.

### Combination A — Multi-channel, shared orchestration

Multiple Channel Adapters normalize LINE, Web, Discord, or other channel events into one stable interaction contract. All channels invoke the same orchestration and Core Services. Channel-specific reply constraints are enforced only on egress.

### Combination B — Creator-editable workflow with stable Core Services

Content creators modify a versioned workflow while Character, Memory, State, Search, Tool, and Model services expose stable contracts. Workflow revisions can be activated or rolled back without redeploying those services.

### Combination C — Durable continuity

A durable workflow instance is keyed by character and conversation, retrieves durable memory, updates explicit relationship state, waits for timers/events, and later resumes while preserving the same character revision or an explicitly migrated revision.

### Combination D — Multi-instance channel processing

A Channel Adapter runs multiple replicas, stores idempotency in shared persistence, serializes per-conversation work using distributed locks or queue sessions, admits work through a shared durable queue, and applies shared or partitioned rate/backlog controls.

### Combination E — Provider-independent character generation

Orchestration builds a channel-neutral/model-neutral interaction context and invokes a Model Service. The Model Service converts it to OpenAI-, Anthropic-, Gemini-, local-model-, or other provider-specific requests and converts responses back to a stable output contract.

### Combination F — Tool-enabled character with controlled side effects

A workflow allows a model or deterministic logic to request a tool. Tool Service validates the requested operation against character/service policy, validates arguments, obtains service credentials independently of the model, executes the operation with idempotency where required, logs an audit event, and returns a normalized result to orchestration.

### Combination G — Search plus memory plus relationship state

Before generation, orchestration retrieves recent conversation context, selected durable memories, explicit relationship state, and relevant external knowledge. Each source remains independently replaceable and is combined only for the current interaction context.

### Combination H — Stable RCCP contracts with supported-cloud bindings

The same Channel Adapter, orchestration code, Core Service interfaces, and application-level contracts can be used across supported cloud profiles while cloud-specific infrastructure adapters bind each production deployment to the selected profile. Identity, secrets, messaging, persistence, networking, observability, and related generic infrastructure capabilities are supplied by that profile without making cloud-specific details part of character workflows or channel-neutral contracts.

### Combination I — Versioned content and versioned interfaces

A character interaction is processed against an immutable character/workflow revision while services communicate through independently versioned stable contracts. Content rollback therefore does not require protocol rollback, and protocol migration does not require rewriting historical character revisions.

### Combination J — Overload-aware channel behavior

A Channel Adapter combines shared queue/backlog measurements, estimated processing time, channel reply deadlines, and provider saturation to decide whether to admit, degrade, or reject work. The overload response is channel-specific while the capacity signals are shared.

### Combination K — Asynchronous secondary effects

Required state transitions are committed before a reply is exposed, while non-critical analytics, low-importance memory extraction, indexing, or telemetry enrichment are emitted as asynchronous events and may complete later.

### Combination L — Multimodal input with text-first output

A Channel Adapter stores or references an uploaded image, emits a message containing text and an image reference, orchestration invokes either a multimodal model or a separate image-analysis capability, and the character returns text through a channel that may not support rich output.

### Combination M — Immutable publication revision bound to runtime execution

A content creator edits character and workflow drafts, automated validation and preview execute against the draft, an authorized publication action creates immutable character/workflow revision identifiers, and each runtime interaction records the revision identifiers it used. Rollback changes the active pointer to a previous immutable revision without rewriting historical executions.

### Combination N — Model migration with shadow or canary evaluation

A Model Service exposes a stable generation capability while operators introduce a new provider or model behind that contract. Selected interactions are evaluated through shadow execution, offline replay, canary routing, or a combination of them. The active character/workflow definition does not need to change unless the model policy itself is intentionally changed.

### Combination O — Operator-controlled graceful degradation

Observability detects saturation or failure of a model, search system, tool provider, or channel API. An operator policy disables optional workflow branches, selects a fallback model/capability, reduces concurrency, applies stricter admission control, or serves a configured fallback response while preserving required state and idempotency behavior.

### Combination P — Isolated multi-service or multi-tenant execution

A shared RCCP deployment carries an explicit service or tenant scope through normalized interactions, persistence keys, memory/state access, tool authorization, quotas, telemetry, and configuration. Isolation may be logical within shared infrastructure, physical through separate databases/queues/accounts, or hybrid by sensitivity and workload.

### Combination Q — Streaming generation with channel-specific buffering

Orchestration requests a streaming model response through a provider-neutral Model Service. A Web Channel Adapter forwards partial text as a stream, while a messaging Channel Adapter that cannot safely expose partial output buffers the same stream until a complete message is available. Cancellation, deadline, and correlation identifiers are propagated through both paths, and required state effects are committed according to the same workflow policy regardless of whether output was streamed or buffered.

### Combination R — Transactional state transition with asynchronous side effects

A workflow computes a required state transition and one or more secondary effects. The required state update and an outbox record are committed atomically in shared persistence. A separate dispatcher later publishes memory-indexing, analytics, notification, or other secondary work from the outbox. External side-effecting tools use idempotency keys or an effect journal so dispatcher retries do not duplicate the effect.

### Combination S — Cross-channel identity with shared continuity

LINE, Web, Discord, or other Channel Adapters retain their channel-local actor identifiers but may map them, after service-defined linking or authentication, to a canonical actor identifier. Memory, relationship state, and other continuity data can be scoped to either the local actor or canonical actor. Unlinked channels remain isolated, and unlinking or re-linking does not require changing the channel-neutral orchestration contract.

### Combination T — Budgeted context assembly with provider-independent generation

Orchestration assembles character instructions, current workflow objective, recent conversation context, selected long-term memories, structured state, retrieved knowledge, and tool results under explicit context and cost budgets. A context-assembly policy selects, truncates, summarizes, or omits inputs while retaining provenance. The Model Service then converts the assembled representation to a provider-specific request without making provider tokenization or message objects part of the platform-wide contract.

### Combination U — Scoped deletion across source and derived stores

A service receives a deletion or retention-expiry request for an actor, conversation, character scope, or content item. Authoritative memory/state records are deleted or tombstoned according to policy, and derived vector indexes, caches, search projections, summaries, and analytics identifiers are asynchronously removed or rebuilt from remaining authorized source data. Deletion progress is observable and retryable without reintroducing deleted data from stale projections.

### Combination V — Dead-letter recovery pinned to historical revisions

A failed interaction or workflow instance is moved to a dead-letter or quarantine store with its original request identifiers, publication revision, workflow revision, and contract version. An operator can inspect, replay, or re-drive the work against the original compatible revision or an explicitly selected migration path. Replay suppresses already-committed non-idempotent effects unless an operator intentionally authorizes reconciliation.

### Combination W — Layered policy enforcement around model-directed tools

A character workflow exposes an approved tool catalog. A model can propose a tool call, but a separate policy boundary validates character permissions, actor authorization, service/tenant scope, parameter schema, cost/rate limits, and whether confirmation is required. A tool result is normalized before it re-enters orchestration, and the model cannot expand its own permissions by generating additional tool names or credentials.

### Combination X — Multiple characters sharing one world state

A coordinator workflow manages an interaction involving multiple character definitions. Each character can retain private memory and relationship state, while a separate shared-world State Service stores events or facts visible to multiple characters. Speaker selection can be deterministic, rule-based, workflow-driven, or model-assisted. Context assembly exposes only the private and shared state authorized for the character currently acting.

### Combination Y — Portable immutable character package with environment binding

A published character can be represented as an immutable package or manifest containing character definition, workflow revision, policy references, schema versions, and content hashes. Environment-specific bindings resolve logical references to concrete model providers, stores, tools, or credentials without embedding secrets in the package. The same package can therefore be validated in one environment and activated in another subject to compatibility and policy checks.

### Combination Z — Budget-aware graceful model degradation

A Model Service receives a request carrying latency, cost, quality, or token budgets. Normal operation uses a preferred model policy; provider saturation, deadline pressure, or budget exhaustion can select a lower-cost model, omit optional retrieval or secondary model calls, use a cached/static response where semantically valid, or return a configured fallback. The selected degradation path is recorded with execution provenance.

### Combination AA — Online state or index migration without global downtime

A service introduces a new memory representation, state schema, embedding model, or search index while old interactions continue. New writes can be dual-written, version-tagged, or written only to the new representation while a background process backfills historical data. Reads can prefer the new representation and fall back to the old until coverage is sufficient. After validation, traffic switches to the new representation and the old projection is retired without changing Channel Adapter contracts.

## 26. Example implementation decomposition

A first-party/reference implementation can use C#/.NET for Channel Adapters and Core Services while keeping contracts language-neutral.

One possible repository shape is:

```text
services/
  gateway-line/
  gateway-web/
  character-service/
  memory-service/
  state-service/
  model-service/
  tool-service/

orchestration/
  workflows/
  workers/
  templates/

shared/
  contracts/
  infrastructure-dotnet/
  telemetry/

docs/
  architecture/
  specifications/
  publications/
```

Another implementation can use a modular monolith and preserve the same logical package boundaries.

## 27. Relationship to the current public implementation

At the time this draft was prepared, the public repository contains an early implementation centered on:

- a C#/.NET LINE gateway;
- a minimal channel-agnostic chat layer;
- a text-focused versioned HTTP contract;
- local echo and HTTP forwarding behavior;
- specification material for queueing, locking, idempotency, rate control, and Azure-oriented deployment.

The current physical `chat-layer` service is not intended to permanently define the future Orchestration/Core Services boundary.

The current LINE gateway is a useful concrete Channel Adapter reference, but future Channel Adapters may use different infrastructure and deployment arrangements while preserving the responsibility boundary described here.

## 28. Non-requirements

This disclosure does not require:

- one specific cloud provider;
- one specific workflow engine;
- one specific model provider;
- one universal memory model;
- one universal relationship model;
- one mandatory microservice topology;
- a graphical editor;
- a no-code production deployment;
- all Core Services to be present in every service;
- all character content to be stored in the RCCP repository;
- character-specific production secrets or proprietary content to be distributed with RCCP.

## 29. Further implementable extensions

The same architecture can be extended to:

- voice input/output by adding audio content parts and speech capabilities;
- avatar or motion output by adding structured character-action outputs;
- live-streaming characters by combining scheduled/event-driven orchestration with streaming channel adapters;
- digital signage by combining sensor/event channels with character output adapters;
- games by exposing game state and actions through State/Tool services;
- ontology- or knowledge-graph-backed world knowledge, character belief, memory, and relationship retrieval;
- external business systems through controlled tools and domain-specific core services;
- multi-party conversations that coordinate several actors, several characters, or both;
- multiple characters through per-character definitions and workflows, or workflows coordinating several character identities;
- shared world state through an independently scoped State Service;
- human moderation or operator intervention as workflow steps.

These extensions do not require the platform-wide message contract to adopt any single model provider's native request format.

## 30. Public reference material

For implementation-specific details already present in the repository, see the public project documentation, including:

- `README.md`
- the source code under `services/gateway-line/`
- the source code under `services/chat-layer/`

Official external platform documentation should be consulted for the current behavior and limits of each integrated channel, cloud service, model provider, workflow engine, database, or other dependency.

## 31. Document evolution

This document is intentionally publishable before every RCCP design decision is final.

Later editions may:

- add additional concrete embodiments;
- add diagrams or reference implementations;
- refine data contracts;
- describe additional Core Services;
- describe selected workflow engines or deployment profiles;
- correct errors;
- document newer alternatives.

A later edition may revise RCCP's current preferred or supported implementation while earlier published editions remain part of the historical technical disclosure. An earlier embodiment remains a disclosed historical alternative even when a later edition no longer presents it as a current supported deployment option.

Published editions should remain recoverable through repository history and fixed publication artifacts rather than rewriting historical publication records.


## 32. Authoring, validation, and publication surfaces

RCCP may expose creator-facing authoring capabilities without placing production infrastructure credentials or unrestricted runtime control in the creator interface.

A creator-facing surface can manage:

- character definitions;
- workflow definitions;
- model and memory policies selected from approved choices;
- knowledge/content references;
- tool permissions selected from an approved catalog;
- test conversations and preview sessions;
- draft, review, published, and retired revisions.

### 32.1 Draft and published representations

One embodiment maintains mutable drafts and immutable published revisions. A publication record identifies the exact character definition, workflow definition, content references, and policy references activated together.

```json
{
  "publication_id": "pub:01J...",
  "character_id": "char:alice",
  "character_revision": "rev:character:42",
  "workflow_revision": "rev:workflow:17",
  "model_policy_revision": "rev:model-policy:8",
  "memory_policy_revision": "rev:memory-policy:5",
  "tool_policy_revision": "rev:tool-policy:3",
  "published_at": "2026-08-09T02:00:00Z"
}
```

Runtime execution resolves one publication record or equivalent revision set and records it with the interaction.

Another embodiment publishes a single immutable character package containing these elements. Both forms are disclosed.

### 32.2 Validation

Before activation, validation can include:

- schema validation;
- unresolved-reference checks;
- tool-permission checks;
- model-policy compatibility;
- cycle/deadlock checks where applicable;
- required fallback behavior;
- content-size limits;
- synthetic conversation tests;
- regression assertions on character behavior;
- cost/time budget checks.

Validation may reject publication, warn, or require additional review depending on policy.

### 32.3 Preview isolation

Preview execution can use the production-equivalent orchestration path while isolating side effects. For example, preview may:

- use a separate state/memory namespace;
- replace side-effecting tools with simulators;
- require explicit confirmation before external actions;
- use test model credentials or quotas;
- mark telemetry as preview traffic.

This allows creators to observe realistic behavior without modifying active user state.

## 33. Operator control and runtime management

System Operators may require a control surface distinct from creator authoring.

An operator-facing control plane or administrative API can expose:

- deployment health;
- queue depth and processing delay;
- model/provider health;
- error and fallback rates;
- active publication revisions;
- resource and cost usage;
- tool/API failure rates;
- concurrency and rate-limit status;
- configuration and feature flags;
- controlled pause, drain, resume, or degradation actions.

The operator surface does not need to be a custom RCCP Web UI. It may be implemented through an existing observability platform, workflow-engine UI, cloud console, GitOps workflow, administrative API, or a composed interface.

### 33.1 Safe operational changes

Operational changes can be separated from creator content changes. Examples include:

- change worker concurrency without editing character content;
- disable an unavailable tool globally while leaving workflows intact;
- reroute a model policy to a fallback provider;
- reduce maximum processing time during overload;
- drain a queue before deployment;
- suspend proactive/scheduled workflows while allowing direct replies;
- roll back an infrastructure deployment without rolling back character content.

This separation prevents lower-level operational changes from unnecessarily propagating into creator-facing artifacts.

## 34. Service and tenant isolation embodiments

RCCP does not require multi-tenancy, but a deployment can support multiple services, customers, teams, characters, or environments by carrying an explicit scope through the architecture.

A normalized interaction may include:

```json
{
  "scope": {
    "service_id": "service:example",
    "tenant_id": "tenant:optional",
    "environment": "production"
  }
}
```

Isolation can be implemented as:

- shared application and shared database with partitioned keys and authorization;
- shared application with separate databases or schemas;
- separate queues for workload isolation;
- separate model/tool credentials;
- separate cloud accounts/subscriptions/projects;
- fully separate deployments;
- a hybrid arrangement based on sensitivity or scale.

Quotas, rate limits, memory/state access, search indexes, tools, secrets, and telemetry can all be scoped consistently.

An implementation that does not need multi-tenancy can omit `tenant_id` and use one service scope per deployment.

## 35. Evaluation and continuous improvement

A character service can use the same versioned artifacts and stable contracts for systematic evaluation.

### 35.1 Regression cases

A regression case can specify:

- initial character/workflow revision;
- initial memory/state fixtures;
- one or more user interactions;
- permitted or prohibited output properties;
- expected state transitions;
- expected tool usage constraints;
- latency or cost budgets.

Assertions do not need to require exact generated text. They may check style, required facts, prohibited behavior, state transitions, tool calls, structured output, or evaluator scores.

### 35.2 Revision comparison

Candidate changes can be compared with the active revision through:

- deterministic test cases;
- recorded and appropriately governed interaction replays;
- synthetic interaction generation;
- side-by-side human review;
- automated evaluators;
- shadow model/workflow execution;
- canary traffic.

The evaluation system records which character, workflow, model policy, and service versions produced each result.

### 35.3 Production feedback

Operational and experience signals can be joined by correlation identifiers and revision identifiers. This allows developers, operators, and content creators to distinguish:

- infrastructure regressions;
- model-provider regressions;
- workflow/content regressions;
- state/memory problems;
- channel-specific failures.

The same information can support rollback or a new corrected publication revision.

## 36. Execution provenance and reproducibility

For debugging, audit, regression analysis, and controlled rollback, an implementation can record an execution manifest for each interaction or workflow run.

A manifest may include:

```json
{
  "request_id": "req:01J...",
  "character_revision": "rev:character:42",
  "workflow_revision": "rev:workflow:17",
  "contract_version": "interaction-contract:1",
  "model_policy_revision": "rev:model-policy:8",
  "resolved_model": "provider/model-id",
  "memory_policy_revision": "rev:memory-policy:5",
  "tool_policy_revision": "rev:tool-policy:3",
  "state_versions": {
    "relationship": 42
  }
}
```

A reproducible test does not require retaining every private prompt or user payload indefinitely. Retention can be governed separately. The important architectural property is that the system can identify the configuration and implementation revisions that participated in an execution.

This enables a later test environment to reconstruct the relevant configuration, substitute governed fixtures for private data, and determine whether a behavioral change came from content, orchestration, provider routing, tools, state, or infrastructure.

## 37. Streaming, cancellation, and interaction supersession

Some channels can expose partial output while others require complete messages. RCCP can support both without making streaming a platform-wide requirement.

A channel-neutral generation operation may produce a sequence of events such as:

```text
started -> text_delta* -> tool_request? -> text_delta* -> completed
```

A Web Channel Adapter may forward text deltas to a client using WebSocket, Server-Sent Events, HTTP streaming, or another transport. A messaging Channel Adapter may buffer deltas and emit only complete channel messages. Another adapter may expose typing/progress indicators but withhold generated text until completion.

### 37.1 Cancellation and deadlines

A request can carry a deadline or cancellation identifier through Channel Adapter, orchestration, Model Service, Search, and Tool calls. Cancellation may be triggered by:

- an end user explicitly cancelling an operation;
- a newer message superseding an older generation according to service policy;
- channel reply-window expiration;
- an operator draining or pausing work;
- a workflow timeout;
- provider or resource budget exhaustion.

Cancellation is cooperative where an underlying provider cannot guarantee immediate termination. Required state transitions and external side effects must define whether they occur before, after, or independently of cancellation.

### 37.2 Supersession policy

For rapid successive messages, alternative policies include:

- serialize all messages in arrival order;
- cancel an in-progress generation and start from the latest user message;
- combine a burst of messages before generation;
- allow parallel generation when state semantics permit it;
- queue later messages while preserving an explicit conversation sequence number.

The policy can be selected per service, character, channel, or workflow.

## 38. Atomicity, outbox/inbox, and external side effects

Distributed character workflows may need to update internal state and trigger external work without relying on a distributed transaction across all systems.

### 38.1 Transactional outbox embodiment

One embodiment commits required application state and an outbox record in the same local database transaction. A dispatcher later publishes the outbox entry to a queue, event bus, indexer, or other downstream system. The outbox record is marked delivered only after the downstream handoff reaches the configured durability boundary.

This pattern can be used for:

- memory extraction or indexing;
- analytics events;
- scheduled follow-up creation;
- search-index updates;
- notification work;
- workflow continuation;
- replication into derived stores.

An inbound consumer can use an inbox/idempotency record so repeated queue delivery does not repeat committed effects.

### 38.2 Effect journal for non-idempotent tools

A side-effecting tool operation can use an effect record such as:

```json
{
  "effect_id": "effect:01J...",
  "request_id": "req:01J...",
  "tool": "calendar.create_event",
  "idempotency_key": "tool-op:...",
  "status": "prepared",
  "provider_operation_id": null
}
```

The status may progress through `prepared`, `submitted`, `confirmed`, `failed`, or `compensated`. Reconciliation uses the provider operation ID or idempotency key before deciding whether a retry is safe.

Exactly-once behavior therefore does not depend on an exactly-once transport guarantee; it can be approximated at the application effect boundary through durable identity and reconciliation.

## 39. Context assembly and budget management

Model input for a character interaction can be assembled from independently governed sources rather than from one monolithic prompt string.

Possible sources include:

- character identity and behavioral instructions;
- current workflow objective;
- service or operator policy;
- recent conversation history;
- conversation summary;
- selected long-term memories;
- relationship or other structured state;
- retrieved knowledge;
- tool descriptions and prior tool results;
- channel capabilities;
- current time/event context.

A context-assembly component or workflow step can assign budgets by token count, character count, cost estimate, latency estimate, priority, or content class.

When the complete context exceeds a budget, the system can:

- drop low-priority optional items;
- select fewer memories or search results;
- summarize older conversation history;
- compress structured data;
- use a smaller or larger model context window according to policy;
- split work across multiple model calls;
- reject the interaction when required context cannot be represented safely.

The assembled context can record provenance linking each inserted fragment to its source revision or record identifier. Static character content can be precompiled or cached separately from dynamic actor-specific context.

## 40. Cross-channel identity and continuity

A channel-local actor identifier is not necessarily a globally stable identity. RCCP can keep channel identity separate from canonical service identity.

One embodiment stores mappings such as:

```json
{
  "canonical_actor_id": "actor:123",
  "identities": [
    {"platform": "line", "local_id": "U..."},
    {"platform": "web", "local_id": "account:456"}
  ]
}
```

Linking can require authenticated account ownership, explicit consent, an operator action, or another service-defined verification mechanism.

Memory and state scopes may then be:

- channel-local;
- conversation-local;
- canonical-actor-wide;
- character-and-actor scoped;
- service-specific;
- explicitly shared among selected scopes.

A deployment can omit canonical identity entirely when cross-channel continuity is undesirable. Linking and unlinking are therefore identity-policy operations rather than a requirement of the Channel Adapter contract.

## 41. Channel capability negotiation and output adaptation

Channels differ not only in transport but in what they can render and when they can respond.

A Channel Adapter can expose a normalized capability description for the current interaction, for example:

```json
{
  "capabilities": {
    "streaming_text": false,
    "images": true,
    "buttons": true,
    "message_edit": false,
    "proactive_send": true,
    "reply_deadline_ms": 45000,
    "max_messages": 5
  }
}
```

Orchestration may ignore these capabilities and generate a generic response, leaving all adaptation to the adapter. Alternatively, a workflow may select among semantically equivalent output plans according to capability.

For example, a rich response can define a primary text representation plus optional image, button, animation, or structured-action representations. A text-only channel emits the primary text while a richer channel emits additional parts.

This preserves one character experience model without requiring every channel to implement the same presentation features.

## 42. Data lifecycle, retention, and derived-data cleanup

Continuity systems create data that may need expiration, correction, or deletion independently of application deployment.

A memory or state record can carry retention metadata such as:

- creation time;
- expiration time;
- source interaction;
- scope;
- sensitivity classification;
- deletion/tombstone state;
- governing policy revision.

Deletion or expiry may need to propagate to derived data such as:

- vector embeddings;
- full-text indexes;
- summaries;
- caches;
- materialized views;
- evaluation datasets;
- analytics records where policy requires removal or de-identification.

One embodiment treats authoritative records as the source of truth and rebuilds derived stores from authorized source data. Another emits deletion events consumed by each derived store. A third periodically reconciles derived indexes against authoritative tombstones.

Retention policy for operational audit records can differ from retention policy for conversation content, provided the remaining records do not unintentionally reconstruct content that was required to be removed.

## 43. Poison work, dead-letter handling, and controlled replay

Retries should not cause one malformed or permanently failing interaction to block a queue or workflow partition indefinitely.

A processing component can classify failures into:

- transient and retryable;
- provider-throttled and retry-after;
- user/content error;
- incompatible contract or revision;
- permanent external failure;
- unknown/poison work.

After a bounded retry policy, work can enter a dead-letter or quarantine store containing correlation identifiers, failure metadata, and the publication/workflow/contract revisions required to interpret the payload.

An operator replay path can:

1. inspect the failure;
2. select the original compatible runtime or an explicit migration;
3. run a dry execution with side effects disabled;
4. verify which effects were already committed;
5. re-drive only the safe remaining work;
6. record the replay as a new correlated execution.

This makes recovery an explicit operation rather than an uncontrolled repeat of the original message.

## 44. Layered safety and policy enforcement

A character service can apply service-defined safety and policy at multiple boundaries without making one model provider's moderation mechanism the platform-wide authority.

Possible policy points include:

- channel ingress;
- normalized interaction acceptance;
- retrieval/search authorization;
- memory write eligibility;
- tool exposure and tool invocation;
- model request construction;
- generated-output validation;
- channel egress;
- proactive/scheduled behavior.

Policies can be deterministic rules, allow/deny lists, authorization checks, schema validators, classifiers, moderation models, human approval, provider-native controls, or combinations.

Developer/operator hard limits can remain non-overridable by creator-edited content. Creator-selectable policies can operate inside those hard limits. The policy revision used for an execution can be recorded with provenance.

Different failure modes may intentionally use different defaults: for example, a side-effecting tool can fail closed while an optional style classifier can fail open or be skipped according to service requirements.

## 45. Multiple characters and shared-world state

A single interaction can involve more than one character while preserving separate character identities and private continuity.

A coordinator workflow may:

1. resolve multiple character revisions;
2. load shared world or scene state;
3. load only the private memory/state visible to each character;
4. select which character acts next;
5. invoke one or more character/model operations;
6. update shared and private state under separate authorization rules;
7. emit one or more channel-neutral outputs with speaker identity.

Speaker selection may be round-robin, rule-based, event-driven, workflow-defined, priority-based, or model-assisted.

Shared world state may contain locations, objects, global events, quest state, or facts visible to multiple characters. Private memory remains separately scoped so a shared coordinator does not imply that every character can read every other character's memory.

## 46. Portable character packages and environment bindings

A published character can be represented by an immutable package or manifest so the same logical content can be validated, transferred, archived, or activated without embedding environment secrets.

A package may identify:

- character definition revision;
- workflow revision;
- schema versions;
- model/memory/tool policy references;
- knowledge or asset references;
- required capability names;
- dependency versions;
- content hashes or signatures.

Environment bindings resolve logical capabilities such as `model:primary-chat`, `memory:long-term`, or `tool:calendar` to deployment-specific implementations and credentials.

This allows:

- preview and production to use the same immutable character package with different bindings;
- import/export between RCCP-compatible deployments;
- archival of the exact published artifact;
- compatibility checks before activation;
- migration when a required capability is replaced.

The package can be a directory, archive, OCI artifact, Git revision, object-store object, database record set, or another immutable artifact format.

## 47. Derived-state and online migration

Many stores used by character systems are derived projections rather than authoritative state. Examples include embeddings, search indexes, summaries, analytics projections, and denormalized relationship views.

When an embedding model, memory schema, index mapping, or state representation changes, migration techniques can include:

- background backfill;
- dual read;
- dual write;
- version-tagged records;
- read-new/fallback-old;
- shadow index population;
- canary reads;
- projection rebuild from source events or authoritative records.

A migration can proceed while Channel Adapters and stable interaction contracts remain unchanged. Once validation shows adequate coverage, reads switch to the new representation and the old projection can be retired according to rollback and retention policy.

## 48. Relationship to established techniques

Many individual mechanisms described in this document are established software-engineering or distributed-systems techniques, including durable workflows, queues, idempotency, optimistic concurrency, outbox/inbox processing, retrieval, long-term memory, graph/state-machine execution, versioned content, and operational observability. Character platforms and general-purpose agent/workflow frameworks also provide subsets of these capabilities.

This document does not assert that each individual mechanism is novel. Its purpose is to describe implementable RCCP arrangements, responsibility boundaries, processing rules, and explicit combinations sufficiently concretely that those arrangements can be built and evaluated without depending on unstated implementation choices.

Where a named open-source project, cloud platform, communication service, database, or other product is useful for a concrete embodiment, this document may identify it by name and describe how RCCP can integrate with its publicly documented interfaces and behavior. Such a named embodiment is illustrative unless explicitly stated otherwise; another implementation providing equivalent semantics can be substituted.

## 49. Concrete orchestration-engine embodiments

RCCP can be implemented over different existing orchestration runtimes without changing the platform-wide responsibility split between Channel Adapters, Orchestration, and Core Services.

The following embodiments deliberately map RCCP concepts onto concrete publicly documented workflow systems. They are alternatives rather than a requirement that every RCCP installation support every engine.

### 49.1 Kestra embodiment

Kestra is a concrete fit for an embodiment in which creator-visible orchestration is represented declaratively. Kestra flows define tasks, ordering, inputs, outputs, triggers, retries, concurrency, error handling, and related execution behavior, and can be edited as YAML or through a no-code editor.

One RCCP implementation can map concepts as follows:

| RCCP concept | Kestra mapping |
| --- | --- |
| character-experience workflow revision | a version-controlled Kestra Flow definition or generated Flow |
| workflow input | normalized interaction data plus immutable character/workflow revision identifiers |
| Core Service invocation | HTTP task, script/task worker, custom plugin, or another task that calls a stable RCCP service contract |
| branching/parallelism | Flow control tasks and declarative orchestration |
| scheduled/proactive behavior | Kestra trigger that starts a Flow with RCCP identifiers as input |
| execution correlation | Kestra execution identifier stored with the RCCP correlation/work item |
| failure/retry | Flow/task retry and error handling, combined with RCCP idempotency for externally visible effects |
| creator editing | restricted no-code/YAML editing over an approved RCCP task vocabulary |
| publication | validation produces or approves an immutable Flow revision associated with an RCCP publication revision |

A request-oriented embodiment creates one Kestra execution per accepted interaction. The Channel Adapter durably admits the interaction and starts the Flow with fields such as `interaction_id`, `conversation_id`, `actor_id`, `character_id`, `character_revision`, and a reference to any channel-specific reply context. The Flow then invokes Character, Memory, State/Relationship, Search, Tool, and Model capabilities through stable RCCP contracts.

A longer-lived embodiment associates a Kestra execution or a sequence of correlated executions with an RCCP story/event process. A paused execution can represent manual validation or a bounded wait. Where an external event does not naturally resume the same Flow, a new event-triggered execution can load durable RCCP state and continue from an application-level continuation token. The RCCP state model therefore does not depend on every wait primitive being represented inside one engine execution.

For creator-facing use, RCCP can expose only an approved set of task types. A creator-visible node such as `GenerateCharacterReply` can compile into several lower-level Kestra tasks while hiding infrastructure credentials and unrestricted plugins. The publication step can validate that:

- referenced Core Service operations exist;
- character and workflow revisions are immutable or resolvable;
- tool calls are in the allowed catalog;
- retryable tasks are idempotent or guarded by an effect journal;
- channel-specific credentials are not embedded in the Flow;
- unsupported task/plugin types are rejected;
- production Flow revisions are associated with the RCCP publication revision used by runtime execution.

This preserves Kestra's declarative editing model while keeping RCCP-specific contracts, safety boundaries, and content revisioning outside the workflow engine.

Public documentation basis: Kestra documents Flow inputs/outputs/tasks, sequential/parallel/conditional orchestration, triggers, retries, concurrency, revisions, execution states, YAML authoring, and no-code editing at <https://kestra.io/docs/workflow-components/flow> and <https://kestra.io/docs/workflow-components/execution>.

### 49.2 Temporal embodiment

Temporal is a concrete fit for a code-first durable-execution embodiment. RCCP workflow code runs in Temporal Workers while failure-prone or externally visible operations are represented as Activities.

One RCCP implementation can map concepts as follows:

| RCCP concept | Temporal mapping |
| --- | --- |
| durable conversation/story process | Workflow Execution |
| character-processing step with I/O | Activity |
| worker capability partition | Task Queue |
| external interaction arriving during a long-running process | workflow message such as a Signal or Update |
| scheduled continuation | durable Timer |
| reusable nested process | Child Workflow or callable workflow-level abstraction |
| workflow execution history | Temporal Event History plus RCCP execution provenance |
| creator workflow revision | immutable RCCP workflow-definition data interpreted by stable Workflow code, or separately versioned deployed Workflow code |

In a conversation-scoped embodiment, the Workflow ID is deterministically derived from an RCCP scope such as service, character, and conversation. The first accepted interaction starts the Workflow. Later interactions can be delivered to the running Workflow as messages. The Workflow decides which RCCP activities to schedule, while actual calls to model providers, databases, search systems, tools, or external channels occur in Activities rather than deterministic Workflow code.

A single turn can be expressed as:

```text
Signal/Update: normalized interaction
        |
        v
Workflow validates active publication revision
        |
        +--> Activity: load character
        +--> Activity: load memory/state
        +--> Activity: search/retrieve
        |
        v
Workflow selects next transition
        |
        +--> Activity: model generation
        +--> Activity: guarded tool effect
        |
        v
Workflow records logical transition
        |
        +--> Activity: persist memory/state
        +--> Activity: emit channel-neutral output
```

The Channel Adapter need not wait synchronously for the complete durable Workflow. One embodiment acknowledges/adopts the channel request quickly, then obtains output from a completion event or application-side result store. Another embodiment uses a request/response path for short turns while Temporal is used only for long-running story state, scheduled events, approval waits, or effects requiring durable retry.

Creator editing can remain data-driven even though Temporal itself is code-first. RCCP can store an immutable creator-authored graph/DSL as character content, pin its revision when a Workflow starts, and execute that data through a stable Temporal interpreter Workflow. A second embodiment generates or deploys code from an approved workflow definition, but a production publication still pins the generated code/version and compatible worker deployment.

Because Workflow histories are replayed, RCCP integrations must avoid nondeterministic external work directly in Workflow code. Model calls, database access, wall-clock-dependent external operations, and side effects belong in Activities or other replay-safe abstractions. Tool Activities with non-idempotent effects can use the effect-journal pattern from Section 38 so retries do not duplicate an external action.

Public documentation basis: Temporal describes durable Workflow Execution, Activities, workers/task queues, persisted history and replay at <https://docs.temporal.io/> and in its public architecture documentation. The Temporal service API also exposes lifecycle operations for workflows and activities at <https://api-docs.temporal.io/>.

### 49.3 Dapr Workflow embodiment

Dapr Workflow is a concrete fit when RCCP also uses Dapr building blocks for service invocation, pub/sub, state management, bindings, secrets, configuration, resiliency, or related distributed-application concerns.

One RCCP implementation can map concepts as follows:

| RCCP concept | Dapr mapping |
| --- | --- |
| durable orchestration instance | Dapr Workflow instance |
| Core Service operation | Workflow Activity calling a service directly or through Dapr service invocation |
| external continuation | workflow external event |
| scheduled continuation | durable workflow timer |
| asynchronous domain/integration event | Dapr pub/sub |
| persistent application state outside execution history | RCCP state store accessed through an RCCP service, optionally backed by Dapr state management |
| external integration binding | Dapr input/output binding where appropriate |
| operational management | Dapr Workflow HTTP/gRPC management APIs plus RCCP operator controls |

In one deployment, Channel Adapters and Core Services are ordinary applications using Dapr sidecars. The Channel Adapter validates the external channel request and publishes a normalized interaction or starts/raises an event to a Dapr Workflow. Activities invoke RCCP Core Services through stable HTTP/gRPC contracts, potentially using Dapr service invocation. Non-user-visible secondary effects can be emitted through pub/sub.

The workflow may keep only execution-control state while durable character memory and relationship/world state remain in RCCP-owned persistence. This avoids treating a workflow runtime's event history as the sole long-term character data model. A different embodiment deliberately stores compact conversational state in workflow state while large memories, embeddings, assets, and auditable source records remain external.

Creator-authored workflows can be compiled into parameters consumed by a stable code-authored Dapr Workflow. Because Dapr Workflow is code-authored, RCCP may expose a visual or declarative creator representation that is interpreted by the workflow application rather than exposing application source code. Publication binds the creator representation revision to a compatible workflow application revision.

Dapr's management APIs can be wrapped by RCCP operator controls for pause/resume, termination, purge, or external-event delivery. Direct creator access to those management APIs is not required.

Public documentation basis: Dapr documents stateful long-running workflows, activities, HTTP/gRPC workflow management, service invocation, pub/sub, state management, bindings, workflow versioning and other workflow features at <https://docs.dapr.io/developing-applications/building-blocks/workflow/> and <https://docs.dapr.io/developing-applications/building-blocks/workflow/workflow-overview/>.

### 49.4 Conductor OSS embodiment

Conductor OSS is a concrete fit for a durable, definition-oriented worker/task architecture. Conductor workflows can be defined as JSON or code, and workers can be implemented in multiple languages.

One RCCP implementation can map concepts as follows:

| RCCP concept | Conductor mapping |
| --- | --- |
| creator experience definition | generated or validated Conductor workflow definition |
| Core Service adapter | worker task or built-in HTTP/system task |
| model/tool/search operation | dedicated worker task, or system task when the required policy can be enforced |
| waiting/approval | Wait/Human/event-oriented task |
| reusable experience | sub-workflow |
| conditional/parallel flow | Conductor operators/system tasks |
| execution state | Conductor workflow/task state plus RCCP provenance |
| worker isolation | task-type-specific worker pools |

RCCP can register a restricted workflow definition generated from creator content. The definition references RCCP-owned task names such as:

```text
rccp.load_character
rccp.load_continuity
rccp.search
rccp.generate
rccp.call_tool
rccp.persist_transition
rccp.emit_output
```

Workers implementing those tasks call stable RCCP Core Service contracts. The Conductor server coordinates scheduling, retries, waits, timeouts, and flow control, while workers retain policy enforcement at the RCCP boundary.

Built-in HTTP or AI-oriented tasks can be used in another embodiment, but direct model-provider or tool credentials should still be mediated by publication policy and operator-controlled configuration when RCCP requires provider independence or restricted creator capabilities. A workflow definition published by a creator can therefore be transformed before registration, replacing generic HTTP/model nodes with approved RCCP task references.

Conductor's retry/restart/rerun facilities do not by themselves make externally visible effects safe to repeat. RCCP task workers still apply idempotency keys, transaction/outbox rules, or the effect journal described elsewhere in this document.

Public documentation basis: Conductor OSS documents JSON/code workflow definitions, worker/task queues, retries, waits, events, sub-workflows, HTTP tasks and durable execution at <https://docs.conductor-oss.org/devguide/concepts/index.html>, <https://docs.conductor-oss.org/devguide/concepts/tasks.html>, and <https://docs.conductor-oss.org/devguide/architecture/index.html>.

### 49.5 LangGraph as an embedded conversational graph runtime

LangGraph is a distinct embodiment from a general infrastructure workflow engine. It can be used inside RCCP's Orchestration implementation for LLM-oriented graph execution while a separate queue, scheduler, or durable workflow system handles broader service-level durability.

One RCCP mapping is:

- a LangGraph `thread_id` corresponds to an RCCP conversation/workflow scope;
- graph state contains transient turn-processing state and references to durable RCCP continuity;
- graph nodes call RCCP Character, Memory, Search, Model, State, and Tool capabilities;
- checkpoints provide resumable graph state;
- interrupts provide approval or other external-input boundaries;
- subgraphs represent reusable experience fragments or specialist processing;
- streaming output is converted into RCCP channel-neutral stream events before Channel Adapter adaptation.

A production implementation can use a durable checkpointer and can keep authoritative character memory outside the graph checkpoint. This allows graph state to be rebuilt, migrated, or discarded without deleting the authoritative memory/state model.

For non-idempotent actions, a node that can be replayed after an interrupt or failure must not perform an unguarded external effect. The node instead calls an RCCP Tool Service that owns effect-journal/idempotency rules, or delegates the operation to another durable workflow/activity system.

A hybrid embodiment uses LangGraph for the low-latency conversational graph and Temporal, Dapr Workflow, Kestra, Conductor OSS, or another durable system for scheduled events, multi-hour waits, cross-service sagas, or operationally significant long-running processes.

Public documentation basis: LangGraph documents checkpoint persistence, threads, interrupts, subgraphs, fault-tolerant restart, memory and streaming at <https://docs.langchain.com/oss/python/langgraph/persistence>, <https://docs.langchain.com/oss/python/langgraph/interrupts>, and <https://docs.langchain.com/oss/python/langgraph/streaming>.

### 49.6 Engine-independent RCCP contract

The preceding mappings are intentionally different, but RCCP can keep the following contract stable across them:

1. Channel Adapters emit a normalized interaction.
2. An orchestration adapter starts, signals, invokes, or otherwise advances the selected execution runtime.
3. The runtime invokes RCCP capabilities through stable Core Service contracts or approved in-process equivalents.
4. Character/workflow publication revisions are explicit execution inputs rather than implicit mutable global configuration.
5. Externally visible side effects are guarded independently of engine retry behavior.
6. Engine-native execution identifiers are correlated with RCCP request/conversation/publication identifiers.
7. Channel-neutral output returns through an RCCP output contract before channel-specific rendering.
8. Memory, relationship state, world state, and other long-lived character data have explicitly defined ownership rather than being accidentally coupled to one workflow engine's internal history format.

This permits an RCCP installation to change orchestration engines without requiring every Channel Adapter or Core Service contract to change simultaneously.

## 50. Concrete external-service Channel Adapter embodiments

RCCP Channel Adapters may name and directly integrate with external services when those services expose supported APIs and the integration is permitted by the applicable service configuration, permissions, policies, and terms.

The following examples show how service-specific transport semantics can be contained in the Channel Adapter while the character-processing path remains channel-neutral.

### 50.1 LINE Messaging API

Section 17 describes the existing LINE-oriented reference path in detail. A production LINE adapter can additionally treat current LINE Messaging API semantics as explicit adapter constraints:

- receive webhook events over HTTP;
- verify the LINE request signature against the raw request body before character processing;
- use `webhookEventId` for duplicate detection;
- tolerate webhook redelivery and possible event reordering;
- acknowledge ingress independently from long-running model processing;
- retain `replyToken` only in channel-specific correlation data;
- use the Reply API when a valid one-use reply token is available;
- use Push or other send APIs only under service policy that permits proactive sending;
- adapt RCCP outputs to LINE-supported message object types and per-request limits;
- handle unsend/delete-related events according to the service's data-lifecycle policy;
- obtain user-sent media through the channel API only when the event and permissions permit it.

The normalized RCCP interaction need not expose LINE-specific objects to Core Services. It can carry stable fields such as content parts, actor/conversation identifiers, quote/reply relationships, and selected metadata while the adapter retains LINE-specific tokens and destination data.

Official LINE documentation describes webhook delivery, signature verification, asynchronous processing recommendations, redelivery/duplicate behavior, reply tokens and send APIs at <https://developers.line.biz/en/docs/messaging-api/receiving-messages/> and <https://developers.line.biz/en/docs/messaging-api/sending-messages/>.

### 50.2 Discord

A Discord adapter can support message events, application interactions, or both.

For Gateway-based message handling:

1. A Discord Gateway client maintains the WebSocket session, heartbeat, sequence number, intents, and reconnect/resume behavior.
2. Accepted `MESSAGE_CREATE` or other selected events are normalized into RCCP interactions.
3. `guild_id`, `channel_id`, thread/channel context, author/user identity, message ID, mentions, and reply relationships are mapped into normalized identifiers/metadata according to RCCP policy.
4. The Discord event/message identifier becomes an idempotency input.
5. RCCP Orchestration produces a channel-neutral output.
6. The adapter sends or edits the corresponding Discord message through the supported Discord API and applies rate-limit feedback from the API rather than hard-coding a fixed limit.

For Discord Interactions, an adapter can receive `INTERACTION_CREATE` through the Gateway or configure an outgoing interaction webhook. The adapter validates the inbound interaction according to Discord's documented mechanism and, when character processing may exceed the initial response window, sends a deferred interaction response. Durable RCCP processing continues after that acknowledgement, then edits the original response or sends a follow-up using the interaction token while it remains valid.

This creates a useful separation:

```text
Discord interaction
    |
    +--> immediate validation + ACK/defer
    |
    +--> durable RCCP work item
            |
            v
       Orchestration
            |
            v
       channel-neutral output
            |
            v
       Discord edit/follow-up
```

A Discord-specific capability descriptor can indicate whether the current context supports ephemeral responses, components, attachments, thread replies, or other channel features. Section 41's capability-negotiation mechanism then selects a supported rendering without changing character logic.

Official Discord documentation describes Gateway events and resume/heartbeat behavior, interaction receipt via Gateway or webhook, deferred responses, the initial response window and follow-up token lifetime, and dynamic HTTP rate-limit handling at <https://docs.discord.com/developers/events/gateway-events>, <https://docs.discord.com/developers/interactions/receiving-and-responding>, and <https://docs.discord.com/developers/topics/rate-limits>.

### 50.3 Slack

A Slack adapter can receive events through the Events API using either a public HTTP endpoint or Socket Mode.

For an HTTP Events API embodiment:

1. Slack sends an event envelope to the Channel Adapter.
2. The adapter verifies Slack's request signature using the raw request body and signing secret.
3. The adapter extracts a stable event identifier, workspace/team identifier, channel/conversation identifier, actor/user identifier, and the subscribed event payload.
4. The adapter durably admits the normalized work item and returns an HTTP 2xx promptly.
5. RCCP Orchestration processes the event asynchronously.
6. The adapter sends the result through an approved Slack Web API operation such as `chat.postMessage`, reply/update methods, or another capability appropriate to the interaction.

Slack documents a three-second acknowledgement expectation for Events API deliveries and recommends queueing events rather than performing the full reaction synchronously. That behavior maps directly to the RCCP admission-and-background-processing pattern.

Slack retries failed event deliveries. Therefore the adapter keeps Slack's event identifier and retry metadata outside the model prompt and uses them for duplicate suppression, telemetry, and retry diagnosis. OAuth scopes and app installation context are treated as adapter authorization, not as character-level policy.

A Socket Mode embodiment replaces the public HTTP ingress but keeps the same normalization, durable admission, idempotency, orchestration, and outbound Web API path.

Official Slack documentation describes Events API HTTP/Socket Mode delivery, acknowledgement/retry behavior, request signing, OAuth-scoped events, and `chat.postMessage` at <https://docs.slack.dev/apis/events-api/>, <https://docs.slack.dev/authentication/verifying-requests-from-slack>, and <https://docs.slack.dev/reference/methods/chat.postmessage>.

### 50.4 Microsoft Teams

A Microsoft Teams Channel Adapter can treat Teams activities as transport-specific envelopes and convert supported message or interaction activities into RCCP interactions.

The adapter distinguishes conversation scope because Teams supports personal, group-chat, and channel contexts with different interaction behavior. A normalized conversation key can therefore be derived from tenant/application scope plus the Teams conversation/channel identifiers rather than from the user ID alone.

One embodiment is:

1. the Teams-facing bot/adapter receives a message activity;
2. Teams-specific activity routing and authentication remain in the adapter/SDK boundary;
3. the adapter derives normalized actor, conversation, tenant/service, mention, and message metadata;
4. the activity ID or another stable delivery identifier is used for idempotency;
5. the adapter admits the work and returns/acknowledges according to the Teams delivery requirements;
6. RCCP Orchestration runs independently of the Teams SDK;
7. output is converted back into Teams text, cards, or other supported activities;
8. proactive output uses the stored Teams conversation reference/identity only where the application has the required permissions and the service policy permits it.

Current Teams documentation notes that long processing can result in retried requests, so the adapter must be safe against duplicate delivery. It also distinguishes personal, group-chat, and channel scopes and supports proactive bot messages. Those are Channel Adapter concerns rather than reasons to fork Core Services.

Official Microsoft documentation describes Teams bot activity handling, conversation scopes, replies/proactive messages and retry behavior at <https://learn.microsoft.com/en-us/microsoftteams/platform/bots/bot-concepts> and related Teams bot documentation.

### 50.5 Service-specific extensions without platform-wide coupling

An integration may expose richer features than RCCP's minimum common contract. Examples include:

- Discord components or ephemeral responses;
- LINE Flex Messages, stickers, rich menus, or quote tokens;
- Slack blocks, threads, shortcuts, or interactive components;
- Teams Adaptive Cards or app-specific actions.

RCCP can support such features through typed optional capabilities rather than adding every vendor field to the platform-wide message schema.

One embodiment uses:

```json
{
  "content": [
    {"type": "text", "text": "Choose an option."}
  ],
  "presentation_hints": {
    "choices": [
      {"id": "a", "label": "Option A"},
      {"id": "b", "label": "Option B"}
    ]
  }
}
```

The Discord adapter can render the choices as components, the LINE adapter as a supported template/Flex representation, Slack as blocks/actions, Teams as an Adaptive Card, and a plain Web/text adapter as numbered text choices. The creator workflow expresses the semantic choice interaction once; the Channel Adapter chooses the concrete vendor representation.

A second embodiment allows an explicitly channel-specific workflow branch when the experience intentionally depends on a vendor capability. The branch condition reads a normalized capability descriptor such as `channel.capabilities.interactive_choices` rather than importing the vendor SDK into Core Services.

## 51. Managed cloud orchestration embodiments

RCCP does not require the orchestration runtime itself to be open source. A deployment can use a managed cloud workflow service when that service provides the execution, waiting, retry, correlation, and operational properties required by the selected RCCP experience.

The engine-neutral RCCP boundary remains the same:

```text
Channel Adapter
      |
      v
normalized interaction / event
      |
      v
RCCP orchestration binding
      |
      +--> managed workflow runtime
      |        |
      |        +--> Core Service / Tool / Model activities
      |        +--> durable waits / callbacks
      |        +--> scheduled transitions
      |
      v
channel-neutral output / durable side effect
```

The managed runtime owns workflow execution mechanics. RCCP continues to own character definitions, publication revisions, durable memory/state semantics, stable service contracts, channel normalization, tool authorization, policy, and the mapping between creator-authored definitions and executable workflows.

### 51.1 Azure Durable Functions embodiment

Azure Durable Functions is a concrete code-first managed embodiment, particularly natural for a C#/.NET RCCP deployment.

One mapping is:

| RCCP concept | Azure Durable Functions mapping |
| --- | --- |
| durable conversation/story process | orchestration instance |
| externally visible or nondeterministic operation | activity function |
| compact serially updated coordination state | durable entity where appropriate |
| delayed event/story transition | durable timer |
| inbound continuation or approval | external event |
| immutable creator publication revision | orchestration input plus RCCP publication record |
| execution identity | orchestration instance ID plus RCCP run/correlation ID |
| model/tool/database call | activity, not nondeterministic orchestrator code |

A conversation-scoped implementation can derive the orchestration instance ID from `service_id + conversation_id`, or can create a separate turn/story orchestration and keep the long-lived conversation state in RCCP persistence. The former gives a single durable locus for sequential interaction. The latter limits workflow-history growth and makes conversation state less dependent on one execution runtime.

Because Durable Functions replays orchestrator code, RCCP keeps model calls, database access, random values, wall-clock-sensitive external decisions, and network I/O outside the orchestrator. Those operations are invoked through Activities or other documented replay-safe mechanisms. Activity effects still use RCCP idempotency/effect-journal rules because an activity can be executed more than once under failure conditions.

An external Channel Adapter can acknowledge a webhook first and then raise an external event to an existing orchestration. The event contains the RCCP interaction identifier in addition to its payload so duplicate external events can be suppressed at the RCCP layer. If no long-lived orchestration exists, the adapter can start one using a deterministic instance identifier and then submit the interaction.

Creator editing can be implemented in either of two ways:

1. deploy versioned orchestrator code generated from an approved RCCP workflow definition; or
2. run a stable interpreter orchestrator that receives an immutable RCCP workflow graph/DSL revision and schedules generic RCCP activities according to that definition.

The interpreter embodiment is useful when Content Creators edit data rather than application code. A publication binds `character_revision`, `workflow_revision`, and the compatible interpreter/application version.

For in-flight execution upgrades, RCCP can map its publication revision to Durable Functions orchestration versioning or use side-by-side orchestration/application deployments. Older runs remain pinned to a compatible version while new publications start against the new version.

Official documentation basis: Microsoft documents stateful orchestrator/activity/entity programming, checkpoints, retries and recovery, deterministic orchestrator constraints, external events, and orchestration versioning at <https://learn.microsoft.com/azure/azure-functions/durable/> and related Durable Task documentation.

### 51.2 AWS Step Functions embodiment

AWS Step Functions provides a concrete state-machine embodiment in which an RCCP workflow revision compiles to or selects an Amazon States Language state machine.

One mapping is:

| RCCP concept | AWS Step Functions mapping |
| --- | --- |
| published experience workflow | versioned state machine definition or alias-selected definition |
| Core Service call | Lambda, HTTP/API integration, AWS SDK integration, or activity/service task |
| parallel context retrieval | `Parallel` state or equivalent branches |
| bounded loop / conditional routing | `Choice`, `Map`, loop through state transitions |
| retryable transient operation | `Retry` / `Catch` policy |
| delayed transition | `Wait` state |
| external completion or human/tool callback | callback pattern with task token in Standard Workflows |
| short high-volume idempotent process | Express Workflow where its delivery/execution semantics are acceptable |
| long-running/non-idempotent coordination | Standard Workflow |

A turn-oriented state machine can load Character, Memory, State, and Search data in parallel, call the Model Service, optionally execute guarded Tool Service actions, persist the transition, and write a channel-neutral output record.

For a callback-style tool or external approval:

```text
Step Functions task issues RCCP operation + opaque callback reference
        |
        v
external operation proceeds
        |
        v
RCCP callback ingress validates identity/result
        |
        v
task-token completion resumes state machine
```

The task token remains infrastructure/correlation data and is not exposed to the model or character content. RCCP stores the mapping between its own `effect_id` / `run_id` and the task token in an operational store with appropriate expiry and access control.

Standard and Express workflow semantics are not treated as interchangeable. An RCCP publication or deployment profile declares which execution class is allowed. A workflow containing non-idempotent external effects can use Standard execution plus the RCCP effect journal; a high-volume stateless transformation can use Express only when duplicated execution is harmless or independently deduplicated.

Creator-facing workflow editing can compile an RCCP graph into ASL rather than exposing the raw AWS definition. The compiler validates allowed services, retry policies, maximum fan-out, timeout limits, and tool permissions before deployment.

Official documentation basis: AWS documents Standard and Express workflow execution models, service integrations, `Retry`/`Catch`, request-response, `.sync`, and `.waitForTaskToken` callback patterns at <https://docs.aws.amazon.com/step-functions/latest/dg/welcome.html> and <https://docs.aws.amazon.com/step-functions/latest/dg/choosing-workflow-type.html>.

### 51.3 Google Cloud Workflows embodiment

Google Cloud Workflows provides a concrete managed declarative embodiment. RCCP can compile a creator-approved workflow revision into Workflows YAML/JSON or invoke a stable generic workflow that interprets RCCP definition data.

One mapping is:

| RCCP concept | Google Cloud Workflows mapping |
| --- | --- |
| published orchestration | deployed workflow revision |
| Core Service or external API call | HTTP call or Google Cloud connector |
| concurrent retrieval | parallel branches |
| reusable subflow | subworkflow |
| retryable call | workflow retry policy |
| scheduled entry | Cloud Scheduler or event-triggered execution |
| wait for external result | callback endpoint |
| Google Cloud domain event | Eventarc/Pub/Sub-triggered start or callback bridge |
| RCCP execution identity | workflow execution ID plus RCCP run/correlation ID |

For a long-running RCCP tool operation, the workflow can create a callback endpoint, pass only a protected callback reference to an RCCP Tool Service or bridge, suspend without polling, and resume when the Tool Service submits the validated result. The callback result is then converted into a stable RCCP tool-result record before subsequent model or workflow processing.

Parallel branches are suitable for Character, Memory, State, and Search retrieval, but branch outputs are normalized into RCCP-owned context structures rather than leaking connector-specific response schemas to Model Service or character content.

Google Cloud connectors can invoke managed Google services directly; a portable RCCP deployment can instead route through stable RCCP Core Services. The choice is deployment-specific and can coexist: direct connectors for infrastructure-local operations, RCCP service contracts for domain capabilities that should remain replaceable.

Official documentation basis: Google documents Workflows as a managed orchestration platform with HTTP calls/connectors, state, retries, parallel steps, callbacks, subworkflows and long waits at <https://cloud.google.com/workflows/docs/overview>, <https://cloud.google.com/workflows/docs/creating-callback-endpoints>, and the Workflows connector reference.

### 51.4 Managed-runtime portability

A portable RCCP workflow definition does not have to be the native definition of any one managed engine.

An engine-neutral definition can express operations such as:

```yaml
steps:
  - parallel:
      - call: character.load
      - call: memory.retrieve
      - call: state.load
  - call: model.generate
  - when: output.requires_tool
    call: tool.execute
  - call: state.persist_transition
  - emit: channel.output
```

An RCCP compiler/interpreter maps that definition to Temporal, Kestra, Dapr Workflow, Conductor, Durable Functions, Step Functions, Google Cloud Workflows, or another runtime. The supported subset may differ by runtime. Publication validation reports unsupported semantics before deployment rather than silently changing them.

A deployment can also intentionally use native workflow definitions when portability is not desired. The stable boundary is then the RCCP input/output, Core Service contracts, publication identity, provenance, and durable state model rather than the workflow language itself.

## 52. Additional process and batch-orchestration embodiments

Not every RCCP workflow must run on the same engine. Low-latency user interaction, long-running story state, batch memory processing, content build pipelines, and operator approval processes can use different runtimes while sharing RCCP contracts and publication identities.

### 52.1 Apache Airflow for offline and scheduled RCCP work

Apache Airflow is a useful embodiment for scheduled or batch-oriented RCCP work such as:

- nightly memory summarization;
- embedding or retrieval-index rebuilds;
- offline evaluation;
- asset/content transformation;
- analytics aggregation;
- periodic relationship/state maintenance;
- export/import;
- publication validation across many characters;
- large-scale backfill after a schema or derived-state change.

A deployment can keep synchronous user turns on another orchestrator while emitting an RCCP domain event or durable job record consumed by an Airflow DAG.

Airflow deferrable operators can release worker capacity while waiting for an external condition and resume from trigger events. RCCP can use that mechanism for long-running batch dependencies, but the authoritative progress/result remains associated with an RCCP `job_id`, `publication_revision`, and provenance record.

This produces a split orchestration:

```text
interactive turn engine
       |
       +--> immediate response path
       |
       +--> durable RCCP batch request
                    |
                    v
              Airflow DAG
                    |
          summarize / evaluate / rebuild
                    |
                    v
            versioned RCCP result
```

The batch result is applied through an idempotent Core Service operation so replaying or retrying a DAG task does not create duplicate memories or state transitions.

Official documentation basis: Apache Airflow documents DAG scheduling and deferrable operators/triggers that suspend tasks without occupying normal worker capacity at <https://airflow.apache.org/docs/apache-airflow/stable/authoring-and-scheduling/deferring.html>.

### 52.2 Argo Workflows for container-native jobs

Argo Workflows provides a Kubernetes-native embodiment for RCCP tasks naturally packaged as containers, including evaluation suites, media preprocessing, large imports, model-adjacent batch jobs, and isolated content-build steps.

An RCCP publication pipeline can create an Argo Workflow containing approved container templates. Each step receives immutable references such as:

- `service_id`;
- `character_id`;
- `character_revision`;
- `workflow_revision`;
- `artifact_manifest`;
- `validation_profile`.

The workflow writes immutable validation or build artifacts, then RCCP records whether that artifact set is accepted for publication. A suspended Argo step can represent an operator approval gate; resumption continues the pipeline without making Argo the authority for character runtime state.

This embodiment is intentionally different from using Argo as the main conversational state machine. Interactive user-turn orchestration can stay in a low-latency/durable engine while Argo handles container-heavy work.

Official documentation basis: Argo Workflows documents workflow steps/templates and explicit suspend/resume behavior at <https://argo-workflows.readthedocs.io/>.

### 52.3 BPMN/message-correlation embodiment with Camunda

A BPMN-oriented RCCP deployment can use Camunda 8 for creator/operator processes, approval workflows, or even selected long-running character experiences.

A message catch event can wait for an RCCP event identified by a message name and correlation key. The correlation key can be a conversation-scoped, story-scoped, publication-scoped, or generated interaction identifier rather than a raw channel user ID.

For example:

```text
character event process
    |
    +--> send channel output
    |
    +--> wait for "user_reply"
             correlation = interaction_wait_id
    |
    +--> normalize reply
    |
    +--> continue state/story transition
```

Using a generated interaction-specific key avoids ambiguity when multiple process instances involving the same user are waiting concurrently.

Human/User Tasks can represent operator review, content approval, escalation, or a creator action without mixing those control-plane responsibilities into the Character/Memory/Model services. RCCP records the corresponding process/task identity in provenance and keeps domain state in RCCP persistence.

Camunda is treated here as a named process-engine embodiment, not as a claim that all editions or components have the same open-source licensing model.

Official documentation basis: Camunda documents BPMN user tasks and message correlation using message names/correlation keys at <https://docs.camunda.io/docs/components/modeler/bpmn/user-tasks/> and <https://docs.camunda.io/docs/components/concepts/messages/>.

## 53. Additional concrete Channel Adapter embodiments

The following adapters add execution shapes that differ materially from LINE, Discord, Slack, and Microsoft Teams.

### 53.1 Telegram Bot API

A Telegram adapter can receive updates either through HTTPS webhooks or through `getUpdates` long polling. Those two ingress mechanisms terminate in the same RCCP normalization path.

`update_id` is retained as channel delivery metadata and used as an idempotency/ordering input. The adapter maps Telegram `chat_id`, user ID, message/thread/topic identity, reply relationships, and selected message content into RCCP actor/conversation/content structures.

For webhook mode:

1. Telegram posts an `Update` to the registered endpoint.
2. The adapter validates the configured secret token/header when used.
3. The adapter durably records/adopts the update using `update_id`.
4. It returns promptly.
5. RCCP orchestration produces output.
6. The adapter uses `sendMessage` or another permitted Bot API method.

For long polling, the poller advances its offset only according to the adapter's admission policy so an accepted update is not silently lost between receipt and durable RCCP admission.

Telegram also exposes message/thread/topic distinctions and can support partial/draft message behavior in current Bot API versions. RCCP can expose streaming only as an optional channel capability; the core model/output contract does not require Telegram-specific streaming semantics.

Official documentation basis: Telegram documents mutually exclusive `getUpdates`/webhook delivery, `update_id`, webhook secret tokens and `sendMessage` at <https://core.telegram.org/bots/api>.

### 53.2 Facebook Messenger Platform

Facebook Messenger was already present in early RCCP planning as a possible channel beyond LINE. A Messenger Channel Adapter can map Page-scoped messaging into RCCP without letting Graph API fields leak into Core Services.

A typical adapter:

1. receives subscribed Messenger webhook events for a configured Facebook Page/application;
2. verifies and authenticates the webhook according to the configured Meta integration;
3. maps Page/application scope, Page-scoped user identity, message ID and message content to RCCP identifiers;
4. durably admits the interaction;
5. processes it through RCCP orchestration;
6. emits text/media/template output through the Messenger Send API when the application's permissions and current messaging policy allow it.

The adapter retains messaging-window, permission, Page-token, template, and Graph API version concerns. Orchestration receives semantic capabilities such as `can_reply_now`, `supports_buttons`, or `supports_media` rather than raw Meta permission objects.

When the external service restricts whether or when a business/page may initiate or continue messaging, RCCP treats that as a delivery capability/policy check. A scheduled character event can still be generated internally while the adapter chooses not to send it, queues it for another permitted channel, or records that delivery is currently unavailable.

Public documentation basis: Meta publishes the Messenger Platform Send API and official Messenger Platform API collection, including Page-scoped recipients, permissions and messaging-window constraints, through its developer documentation and Meta-maintained API collection.

### 53.3 Twitch chat

Twitch chat represents a public, many-user-to-one-channel interaction topology rather than a private one-user conversation.

A Twitch adapter can receive chat messages through EventSub `channel.chat.message` subscriptions using webhook or WebSocket transport, normalize them into RCCP interactions, and send replies through the supported chat-message API.

The normalized key space separates:

- broadcaster/channel identity;
- chatter/user identity;
- concrete Twitch message/event identity;
- RCCP character/service identity;
- optional thread/reply relationship.

A single public channel can fan in many actor interactions. RCCP may choose one of several conversation policies:

1. one conversation per Twitch user and character;
2. one shared room conversation with speaker identity preserved;
3. a shared short-term room context plus per-user long-term memory;
4. a moderation/routing stage that selects only some messages for character response.

The selected policy is creator/service configuration, not hard-coded into the Twitch adapter.

Outbound rate limits, authorization scopes, moderation status, announcements, replies, and service-specific message fragments remain adapter concerns. A channel capability descriptor tells orchestration whether reply threading, announcements, moderation actions, or other features are available.

Official documentation basis: Twitch documents receiving chat messages through EventSub and sending chat messages/announcements at <https://dev.twitch.tv/docs/chat/send-receive-messages/>.

### 53.4 YouTube Live Chat

A YouTube Live Chat adapter is another public fan-in embodiment, with additional event types such as membership and fan-funding events.

The adapter obtains a `liveChatId` for the active broadcast and receives chat activity through `liveChatMessages.streamList` or `list`. Each `liveChatMessage.id`, `authorChannelId`, `publishedAt`, message type, and supported content is mapped into RCCP channel metadata and semantic interaction parts.

Text chat messages can enter the normal character-conversation path. Other events can enter event-specific workflows:

```text
Super Chat / membership / gift event
        |
        v
Channel Adapter normalizes semantic event
        |
        +--> optional policy / anti-spam / eligibility gate
        |
        v
Creator-selected event workflow
        |
        v
character reaction / state update / public response
```

This allows creator-authored experiences to react differently to semantic channel events without embedding YouTube API schemas in Character or Model services.

Outbound text uses the supported Live Chat insert operation when authorized. Moderation operations remain a separately permissioned adapter capability rather than a generic Tool automatically available to every character.

The adapter also handles the lifecycle of the live chat: when the broadcast/chat ends, a long-lived RCCP character or user relationship does not disappear, but the concrete channel destination becomes inactive until another live chat/session is bound.

Official documentation basis: Google documents `liveChatMessage` event types, `list`, low-latency `streamList`, `insert`, moderation-related operations and live-chat lifecycle at <https://developers.google.com/youtube/v3/live/docs/liveChatMessages>.

### 53.5 Matrix

Matrix provides a protocol-oriented and potentially self-hosted/federated Channel Adapter embodiment.

An RCCP Matrix adapter can use the Client-Server API to:

- receive initial and incremental room state through `/sync`;
- map Matrix room IDs to RCCP conversation/room identities;
- map Matrix user IDs to external actor identities;
- preserve event IDs and transaction IDs for idempotency/correlation;
- send room events/messages through the Client-Server API;
- optionally handle device-level or encryption-related concerns in a dedicated transport/security component.

Matrix room state is not automatically treated as RCCP character state. The adapter can expose selected semantic room information to Orchestration while durable character memory, relationship, and story state remain under RCCP ownership.

For a federated deployment, the RCCP character may participate through an account on one homeserver while interacting with users from remote homeservers. Federation mechanics remain outside Core Services.

Official documentation basis: the Matrix Client-Server specification documents room messaging and incremental synchronization through `/sync` at <https://spec.matrix.org/latest/client-server-api/>.

### 53.6 Public-room and direct-message adapters share one core

LINE, Messenger, Telegram private chats, Discord DMs, public Discord channels, Slack channels, Teams channels, Twitch chat, YouTube Live Chat, Matrix rooms, and a first-party Web UI can all use the same Core Services without pretending that their conversation topology is identical.

A normalized interaction can therefore carry explicit topology information:

```json
{
  "conversation": {
    "scope": "direct | group | room | broadcast_chat",
    "conversation_id": "normalized-id",
    "thread_id": "optional-normalized-thread"
  },
  "actor": {
    "actor_id": "normalized-actor"
  },
  "audience": {
    "visibility": "private | group | public"
  }
}
```

Memory and relationship policy can then choose whether state is keyed by actor, room, actor+room, character+actor, character+room, or another explicit scope.

## 54. Managed messaging and per-conversation ordering embodiments

RCCP can implement per-conversation ordering and multi-instance coordination using managed broker primitives rather than a custom distributed lock.

### 54.1 Azure Service Bus sessions

An Azure deployment can set `SessionId` to a stable RCCP ordering scope such as `service_id + conversation_id`.

All interactions for that scope enter a session-enabled queue or subscription. A session processor acquires the session and processes its messages in order while other sessions can execute concurrently on other workers.

The mapping is:

| RCCP concept | Service Bus session mapping |
| --- | --- |
| per-conversation serialization | `SessionId` |
| worker ownership of conversation queue | session lock |
| message-level retry/redelivery | Peek-Lock delivery and settlement |
| compact processing checkpoint | optional session state |
| poison interaction | dead-letter handling |
| duplicate send protection | broker duplicate detection plus RCCP idempotency |

RCCP does not rely on session state as its long-term Memory/Relationship store. Session state can keep an operational cursor or pointer, while authoritative domain data remains in RCCP persistence.

If a session lock is lost, another worker can acquire the session. Therefore external effects remain idempotent and are protected by the effect journal; serialized queue delivery does not by itself make an HTTP/tool side effect exactly once.

Official documentation basis: Microsoft documents Service Bus sessions as ordered processing of related messages, exclusive session ownership and optional session state at <https://learn.microsoft.com/azure/service-bus-messaging/message-sessions>.

### 54.2 Amazon SQS FIFO message groups

An AWS deployment can set `MessageGroupId` to an RCCP ordering scope such as a conversation ID. Messages in the same group are processed in strict order and not concurrently, while different groups allow horizontal concurrency.

`MessageDeduplicationId` can be set from a stable RCCP interaction/event ID for send-side duplicate suppression. RCCP still keeps a persistent application-level idempotency record because broker deduplication is bounded by service semantics and does not cover every duplicate source or every downstream side effect.

A concrete mapping is:

```text
normalized interaction
    |
    +--> MessageGroupId = conversation_scope
    +--> MessageDeduplicationId = interaction_id
    |
    v
SQS FIFO
    |
    v
worker processes one message from group
    |
    +--> idempotency check
    +--> orchestration / model / tool
    +--> persist
    +--> complete/delete message
```

A high-volume public room can deliberately use a coarser or finer grouping key depending on whether strict room-wide order or higher concurrency is more important.

Official documentation basis: AWS documents FIFO queues, `MessageGroupId`, strict in-group processing order and `MessageDeduplicationId` at <https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-fifo-queues.html> and related FIFO documentation.

### 54.3 Google Cloud Pub/Sub ordering keys

A Google Cloud deployment can publish interactions with an ordering key derived from the RCCP conversation or another serialization scope and enable message ordering on the subscription.

Messages with the same ordering key are delivered in order; different keys remain independently concurrent. Pub/Sub can redeliver messages, including subsequent messages for an ordering key after a redelivery condition, so the RCCP consumer remains idempotent.

A worker therefore does not equate ordered delivery with exactly-once domain mutation. It uses:

- ordering key for chronology;
- stable RCCP interaction ID for duplicate detection;
- optimistic concurrency or another persistence guard for authoritative state;
- effect journal/idempotency key for external effects.

Official documentation basis: Google documents ordering keys, within-key ordering, at-least-once redelivery behavior and ordering tradeoffs at <https://cloud.google.com/pubsub/docs/ordering>.

### 54.4 Broker ordering is a replaceable coordination strategy

The three broker embodiments can implement the same RCCP invariant:

> for a selected serialization scope, two state-mutating interactions must not be committed in an order that contradicts the accepted ordering policy.

A deployment can replace broker-native ordering with:

- a distributed lock;
- optimistic concurrency and retry;
- a durable workflow instance;
- actor/entity serialization;
- a database transaction/sequence number;
- another queue/stream partitioning mechanism.

The invariant belongs to RCCP; the broker primitive is one implementation.

## 55. Concrete Model Service provider-adapter embodiments

RCCP's Model Service can name and integrate with supported model-provider APIs while keeping provider-specific request, response, streaming, tool-call and conversation-state formats outside Channel Adapters and creator-authored domain logic.

A stable RCCP model request can contain, for example:

```json
{
  "request_id": "req-...",
  "model_policy": "character-default",
  "messages": [
    {"role": "system", "content": [{"type": "text", "text": "..."}]},
    {"role": "user", "content": [{"type": "text", "text": "..."}]}
  ],
  "tools": [
    {
      "tool_id": "weather.lookup",
      "input_schema": {"type": "object"}
    }
  ],
  "output": {
    "stream": true,
    "structured_schema_ref": null
  }
}
```

The provider adapter translates that request into the selected provider's public API and translates provider output back into RCCP semantic events such as:

- text delta;
- completed text/content part;
- tool request;
- tool arguments complete;
- refusal/policy result;
- structured result;
- usage record;
- provider error;
- completion/end reason.

Provider-specific IDs can be retained in provenance, but they are not required as the canonical RCCP conversation identifier.

### 55.1 OpenAI Responses API embodiment

An OpenAI adapter can map RCCP messages/content parts and tool declarations to the Responses API, receive server-sent streaming events, convert function/tool calls into RCCP Tool Service requests, and submit tool results back according to the API's supported continuation model.

The adapter can use provider-hosted tools only when the RCCP policy explicitly permits that capability. A creator's `tool_id` is resolved through the RCCP tool catalog; it is not automatically equivalent to an arbitrary provider tool or remote server.

When the OpenAI API returns a provider response/conversation identifier, RCCP may store it as an optimization or provenance field. A deployment can still reconstruct a request from RCCP-owned memory/context when provider-side continuation state is unavailable, disabled, migrated, or intentionally not retained.

Streaming response events are converted into RCCP's streaming/output lifecycle from Section 37. The Channel Adapter decides whether those deltas are visible on LINE, Telegram, Web, Discord, or another channel.

Official documentation basis: OpenAI documents the Responses API, input/output items, function tools, tool choice, continuation identifiers and streaming events at <https://platform.openai.com/docs/api-reference/responses>.

### 55.2 Anthropic Claude Messages API embodiment

A Claude adapter can map the RCCP request to the Messages API. RCCP tool declarations become Claude tool definitions with input schemas. When Claude returns a `tool_use` content block, the adapter emits a normalized RCCP tool request. The Tool Service executes the authorized operation, and the adapter supplies the corresponding `tool_result` in the next provider interaction.

This keeps the tool authorization boundary outside the model provider:

```text
Claude tool_use
      |
      v
provider adapter -> RCCP normalized ToolRequest
      |
      v
Tool Service policy + effect journal
      |
      v
ToolResult
      |
      v
provider adapter -> Claude tool_result
```

Parallel provider tool calls can become parallel RCCP ToolRequests only if the workflow/tool policy permits them. Non-idempotent effects can be serialized even when the model requested several tools at once.

Server-hosted provider tools can be exposed as separate capabilities with explicit policy. They are not silently merged into the same trust class as RCCP-controlled tools.

Official documentation basis: Anthropic documents the Messages API tool-use loop, `tool_use` / `tool_result`, parallel tool use, strict schemas and server/client tool distinctions at <https://platform.claude.com/docs/en/agents-and-tools/tool-use/overview>.

### 55.3 Gemini API embodiment

A Gemini adapter can map RCCP messages/content parts and tool declarations to the Gemini API, convert function calls into RCCP ToolRequests, and convert tool results back into provider function results.

Current Gemini APIs support streaming and function calling, including incremental tool-call arguments in streaming interactions. The adapter therefore buffers or incrementally validates provider argument deltas and emits a ToolRequest only when the arguments meet the RCCP tool schema and the call is ready for execution.

Gemini server-side or remote-tool capabilities, including remote MCP where supported, remain explicit deployment capabilities. An RCCP tool policy can choose:

- locally executed RCCP Tool Service only;
- selected provider-hosted tools;
- selected remote MCP tools;
- a combination with separate audit records.

Official documentation basis: Google documents Gemini streaming, function calling, tool-result continuation and remote MCP/tool capabilities at <https://ai.google.dev/gemini-api/docs/function-calling>, <https://ai.google.dev/gemini-api/docs/streaming>, and the Gemini API reference.

### 55.4 Provider-neutral context and state ownership

RCCP can use provider-maintained conversation state where beneficial without making it the only authoritative continuity layer.

Three implementations are possible:

1. **RCCP-owned context** — every model request is assembled from RCCP memory/state and sent as a self-contained provider request.
2. **Provider-assisted continuation** — RCCP stores a provider response/conversation reference and uses it to reduce repeated context transfer, while retaining enough RCCP state to recover or migrate.
3. **Hybrid segmented state** — short-term provider continuation is used within one live turn/session, while long-term character memory and relationship state remain RCCP-owned.

The selected mode is recorded in provenance. Failover from one provider to another never assumes that an opaque provider conversation identifier can be consumed by the other provider.

## 56. Additional concrete combinations

The following combinations explicitly connect the newly described embodiments. They are examples rather than required deployments.

### Combination AB — Azure durable conversation with Service Bus session admission

1. LINE, Teams, or another Channel Adapter normalizes an inbound interaction.
2. The adapter publishes it to Azure Service Bus with `SessionId = conversation_scope`.
3. A session processor admits interactions serially per conversation.
4. It raises an external event to a Durable Functions orchestration or starts the pinned orchestration version.
5. Orchestrator code schedules RCCP Activities for context loading, model generation, tools, persistence, and output.
6. External effects use RCCP idempotency/effect records.
7. The Channel Adapter delivers the final channel-neutral output.

### Combination AC — AWS state machine with SQS FIFO admission

1. A Discord, Slack, Web, or another adapter writes accepted interactions to SQS FIFO.
2. `MessageGroupId` represents the selected RCCP ordering scope.
3. `MessageDeduplicationId` derives from the RCCP interaction ID.
4. A worker starts a Step Functions Standard execution with the pinned character/workflow revision.
5. Parallel states load memory/state/search context.
6. Callback-task-token states wait for approved long-running tool operations where required.
7. The output record is consumed by the appropriate Channel Adapter.

### Combination AD — Google Workflows with Pub/Sub ordered event ingress

1. Channel events are normalized and published to Pub/Sub with an ordering key.
2. A subscriber performs idempotency and starts or signals the appropriate RCCP process.
3. Google Cloud Workflows invokes portable RCCP Core Services through HTTP or connectors.
4. A callback endpoint represents a durable external wait.
5. Model output is translated to an RCCP output envelope and delivered by the source or another permitted Channel Adapter.

### Combination AE — conversational graph nested inside durable orchestration

1. Temporal, Durable Functions, Dapr Workflow, or another durable engine owns the long-lived story/process.
2. One durable activity invokes a LangGraph-based conversational graph for the current reasoning/interaction segment.
3. LangGraph checkpoints are local to that graph execution or short-lived conversation segment.
4. Authoritative RCCP memory/relationship/world state remains in Core Services.
5. The outer durable engine records the segment result and schedules future waits/events.

This separates fine-grained AI conversational branching from long-lived durable process guarantees.

### Combination AF — public livestream character with shared and per-user state

1. Twitch EventSub or YouTube Live Chat supplies many public chat events.
2. The Channel Adapter keeps platform user/message/channel IDs outside Core Services.
3. RCCP uses a shared room-context window plus per-user long-term memory.
4. A routing/moderation workflow selects which messages receive character responses.
5. The character's public response is sent through the channel API.
6. Selected membership/funding/moderation events can trigger separate creator-defined workflows.
7. Cross-session user continuity is retained independently of the concrete live broadcast/chat ID.

### Combination AG — Telegram dual-ingress adapter with one RCCP core

One deployment uses Telegram webhooks while another uses `getUpdates` polling. Both paths produce the same normalized interaction and use `update_id` as channel idempotency/order metadata. No Character, Memory, Orchestration, or Model Service changes when the ingress mode changes.

### Combination AH — offline memory/evaluation engine separated from interactive runtime

1. Interactive turns execute through Kestra, Temporal, Dapr, Conductor, managed workflows, or a compact application orchestrator.
2. Each turn emits durable background job/domain events.
3. Airflow or Argo executes periodic summarization, evaluation, embedding/index rebuild, or publication validation.
4. Results are versioned and applied through idempotent RCCP Core Service operations.
5. Interactive runtime can continue while an older derived-data revision remains active.
6. A migration/publication step atomically switches to the newly validated derived revision.

### Combination AI — provider-neutral model and tool execution

1. RCCP Model Service selects OpenAI, Claude, Gemini, or another provider from policy.
2. A provider adapter converts the stable RCCP request to the provider API.
3. A provider tool/function request becomes a normalized RCCP ToolRequest.
4. Tool Service authorizes the tool and executes it with idempotency/effect-journal protection.
5. The result is translated back to provider format.
6. Provider streaming events become RCCP output events.
7. The Channel Adapter renders those events according to channel capability.
8. Long-term memory/state remain portable even if the next turn uses a different model provider.

### Combination AJ — Messenger/public-channel delivery policy independent of character intent

A character workflow can generate a proactive semantic output without assuming that every bound channel permits immediate delivery. The output is stored with a delivery intent and audience. Each Channel Adapter evaluates current service permissions, messaging windows, destination state and capabilities. An allowed channel sends immediately; a disallowed or inactive channel records a non-delivery outcome or waits according to configured policy. Character state is not rolled back merely because one external channel cannot deliver the output.
## 57. Technical effects and implementation families

The architecture can be understood as several implementation families. Each family combines technical mechanisms to produce an observable system effect. The families overlap and may be used together.

| Family | Technical problem | Mechanisms disclosed in this document | Technical effect |
| --- | --- | --- | --- |
| channel isolation | incompatible channel transport, identity, timing, retry, and rendering behavior | Channel Adapter normalization, stable interaction/output contracts, capability negotiation | Core conversation processing can remain unchanged while channels are added or replaced; channel deadlines and delivery constraints are localized |
| creator/runtime separation | creator changes otherwise require application/infrastructure redeployment | immutable character/workflow revisions, stable Core Services, publication binding, preview/publish/rollback | character-specific execution logic can change independently of shared runtime services while executions remain attributable to exact revisions |
| durable continuity | a conversation requires memory, relationship, workflow, and event state across process restarts | separately scoped authoritative state, durable workflow state, revision pinning, scheduled/event continuation | long-lived character behavior survives worker replacement and can resume with controlled state/version semantics |
| multi-instance correctness | duplicate/reordered input and concurrent workers can commit contradictory state or duplicate effects | durable admission, idempotency, ordering scope, optimistic concurrency/queue sessions/workflow serialization, effect journal | horizontally scaled workers process a selected serialization scope consistently without relying on process-local locks |
| side-effect consistency | a process can fail between state commit and an external effect | transactional outbox/inbox, effect journal, provider idempotency keys, reconciliation | retries do not blindly repeat non-idempotent actions and asynchronous effects remain correlated with committed state |
| controlled replay | poison work or an old payload may be unsafe to replay after content/runtime changes | dead-letter/quarantine, historical revision identity, dry run, committed-effect inspection, explicit migration | failed work can be reproduced or resumed without silently interpreting it under an incompatible configuration |
| context portability | model providers have different state, prompt, tool, and streaming formats | provider-neutral context assembly, budget policy, provenance, provider adapters | a character can change model provider without moving channel logic or treating provider conversation state as the sole continuity store |
| cross-channel continuity | one human/actor may interact through several unrelated platform identifiers | verified identity linking, scope policies, canonical actor identity optionality | continuity can intentionally follow a verified actor across channels without making channel IDs global identities |
| public-room interaction | broadcast chats mix many actors and a shared room context | explicit topology, room/shared state, per-actor private state, routing/moderation workflow | public-channel behavior can combine shared scene context with private continuity without conflating all participants |
| online replacement of derived data | embeddings/indexes/summaries become obsolete while a service remains live | versioned projections, dual read/write, backfill, shadow/canary validation, atomic switch | derived-state implementation can be migrated without globally stopping Channel Adapters or changing stable interaction contracts |
| portable publication | character content otherwise embeds deployment-specific credentials/endpoints | immutable character package plus logical capability bindings | the same reviewed content package can move between preview/production/deployments while secrets and provider bindings remain environment-specific |
| nested orchestration | fine-grained conversational branching and long-lived durable process semantics have different execution requirements | conversational graph runtime inside a durable workflow activity/segment | an AI-oriented graph can evolve independently while an outer runtime owns durable waits, retries, and long-lived process identity |

The technical effect of a family does not depend on one named product unless an embodiment explicitly requires that product's interface. Equivalent primitives can implement the same family if they preserve the stated state ownership, ordering, correlation, and failure semantics.

## 58. Fully worked reference embodiments

The following embodiments are deliberately more concrete than the architecture-wide alternatives. They demonstrate complete processing paths, including identity, state ownership, concurrency, failure recovery, and side effects.

### 58.1 Reference Embodiment R1 — LINE, durable queue admission, durable workflow, PostgreSQL, and provider-neutral generation

#### 58.1.1 Deployment

One implementation uses:

- a C#/.NET LINE Channel Adapter deployed with more than one replica;
- a durable queue with per-conversation ordering support;
- a durable workflow runtime;
- PostgreSQL as an authoritative application store;
- separately replaceable Character, Memory, State, Model, Search, and Tool modules/services;
- an outbox dispatcher;
- an OpenAI, Claude, Gemini, local-model, or other provider adapter selected by Model Service policy.

The durable queue can be Azure Service Bus with sessions, SQS FIFO, another broker with partition/group ordering, or a queue plus an explicit RCCP serialization mechanism. The durable workflow can be Temporal, Durable Functions, Dapr Workflow, Kestra, Conductor, or another runtime with equivalent wait/retry/recovery semantics.

For one concrete deployment, use Azure Service Bus sessions for admission and Temporal for durable conversation/story execution. PostgreSQL stores RCCP domain state, including publication revisions, messages, memory metadata, relationship/state, idempotency records, effect records, outbox records, and execution provenance.

#### 58.1.2 Stable identifiers

The adapter derives or assigns:

```text
channel_event_id     = LINE webhookEventId or a stable adapter event identifier
interaction_id       = RCCP-generated or deterministically derived stable ID
service_id           = RCCP service/tenant scope
character_id         = selected RCCP character
external_actor_id    = LINE user/source identity in channel identity storage
canonical_actor_id   = optional verified RCCP actor identity
conversation_id      = RCCP conversation scope
ordering_key         = service_id + conversation_id
workflow_run_id      = durable execution/correlation identity
publication_revision = immutable character/workflow publication
```

Raw channel IDs are not required in Character, Memory, or Model Service contracts. A Channel Identity component or adapter-owned mapping resolves them to the selected RCCP scope.

#### 58.1.3 Ingress transaction

On webhook receipt:

1. verify the LINE request signature before accepting channel content;
2. parse supported events;
3. compute `channel_event_id` and `interaction_id`;
4. begin a database transaction;
5. insert an inbox/idempotency record keyed by `service_id + channel_event_id`;
6. if that key already exists in an admitted or completed state, do not create a second interaction;
7. insert the normalized interaction record and its immutable raw-payload reference or permitted audit metadata;
8. insert an outbox record whose destination is the durable admission queue;
9. commit the transaction;
10. return the channel acknowledgement according to the current LINE requirements.

A separate dispatcher publishes the outbox entry. If publishing fails after the database commit, the dispatcher retries. If publishing succeeds but acknowledgement of the publish is lost, duplicate queue delivery is harmless because the consumer checks the same `interaction_id`.

A representative inbox state is:

```text
RECEIVED -> ADMITTED -> PROCESSING -> COMMITTED -> DELIVERY_PENDING -> DELIVERED
                                  \-> FAILED_RETRYABLE
                                  \-> QUARANTINED
```

The exact number of stored states can differ; the required property is that admission identity is durable before asynchronous processing can be repeated.

#### 58.1.4 Per-conversation ordering

The queue message carries:

```json
{
  "interaction_id": "int:...",
  "service_id": "svc:...",
  "conversation_id": "conv:...",
  "ordering_key": "svc:.../conv:...",
  "publication_revision": "pub:..."
}
```

With Azure Service Bus, `SessionId` equals the ordering key. A worker acquires one session and therefore serializes accepted interactions for that scope. With SQS FIFO, `MessageGroupId` serves the same role. With another broker, a partition key, actor/entity runtime, workflow instance, distributed lock, or optimistic-concurrency loop can provide the equivalent invariant.

Ordering at the broker does not eliminate application idempotency. The worker loads the interaction record, verifies it is not already committed, and begins or signals the durable workflow.

#### 58.1.5 Publication pinning

At admission, or at the configured workflow-start boundary, the system resolves a publication:

```json
{
  "publication_revision": "pub:2026-08-09:17",
  "character_revision": "char:42",
  "workflow_revision": "flow:17",
  "model_policy_revision": "model-policy:8",
  "memory_policy_revision": "memory-policy:5",
  "tool_policy_revision": "tool-policy:3"
}
```

That mapping is immutable for the admitted interaction unless an explicit migration policy says otherwise.

A rollback changes which publication new interactions resolve. It does not rewrite provenance for already admitted work.

#### 58.1.6 Context retrieval

The durable workflow schedules independent retrieval operations:

```text
Character.load(character_revision)
Memory.retrieve(actor/character scope, memory_policy_revision)
State.load(actor/character/world scopes)
Search.retrieve(query, knowledge_revision)
ChannelCapabilities.resolve(interaction_id)
```

Operations that can run concurrently are executed concurrently. Each result returns a stable RCCP representation plus a revision/source reference.

Context Assembly then applies:

- mandatory system/service policy;
- character instructions;
- current workflow objective;
- recent messages;
- selected memory;
- relationship/world state;
- retrieved knowledge;
- available tool declarations;
- channel capabilities;
- budget policy.

If context exceeds the selected model budget, optional sources are reduced according to configured priority. Mandatory safety/tool policy is not removed merely to fit optional memory.

The assembly result records which source IDs/revisions were included.

#### 58.1.7 Model invocation and streaming

Model Service receives the provider-neutral request and resolves a concrete provider/model from `model_policy_revision`.

A provider adapter translates the request. Streaming provider events become RCCP events such as:

```text
OUTPUT_STARTED
TEXT_DELTA
TOOL_REQUEST_READY
TOOL_RESULT_ACCEPTED
OUTPUT_PART_COMPLETED
OUTPUT_COMPLETED
OUTPUT_FAILED
```

A LINE adapter can buffer deltas until a complete reply is available. A Web or other streaming-capable adapter can expose deltas. The same Model Service execution does not need to know which rendering policy is selected.

#### 58.1.8 Tool call and effect journal

If the model or workflow requests `calendar.create_event`:

1. Model Service returns a normalized ToolRequest, not a direct provider credential or provider-specific executable call;
2. Tool Service resolves the tool catalog entry and checks service/character/user policy;
3. arguments are validated against the tool schema;
4. a durable effect record is inserted before the non-idempotent external call;
5. an idempotency key is supplied to the external provider when supported;
6. the provider result/operation ID is recorded;
7. if the process fails after submission but before confirmation, reconciliation checks provider state before retrying;
8. a normalized ToolResult returns to orchestration.

The model never receives infrastructure credentials.

#### 58.1.9 State transition

After a valid model/tool result, the workflow prepares:

- assistant message;
- relationship/state mutations;
- optional memory candidate events;
- optional scheduled/story events;
- output intent.

Authoritative mutations that share one PostgreSQL database can be committed in one local transaction. That transaction also writes outbox entries for asynchronous derived work.

If separate stores are used, each operation has a stable transition/effect ID and the workflow uses explicit compensation/reconciliation rather than assuming a cross-store distributed transaction.

An optimistic state version can be checked:

```text
UPDATE relationship_state
SET version = 43, ...
WHERE state_id = ? AND version = 42
```

A zero-row update signals a conflict. The workflow reloads the current state and either recomputes under policy or fails the turn safely; it does not overwrite an unknown newer state.

#### 58.1.10 Channel delivery

The workflow produces a channel-neutral output intent. The LINE adapter receives or reads that intent and evaluates current delivery capability.

If the original reply token is still usable, it uses the reply operation. If not, and proactive/push delivery is configured and permitted, it may use that path. Otherwise it records a non-delivery result while preserving the already committed character state according to the workflow's delivery semantics.

Delivery has its own durable effect identity so worker retries do not blindly send the same message again.

#### 58.1.11 Crash recovery examples

**Crash after ingress database commit, before queue publish:** outbox dispatcher publishes later.

**Duplicate webhook:** inbox key prevents a second admitted interaction.

**Worker crash after queue receive:** broker redelivers; interaction state/idempotency determines whether work resumes or is already complete.

**Workflow worker restart:** durable runtime resumes from persisted workflow history/state.

**Model timeout before committed output:** retry/fallback policy chooses another attempt/provider; no message or relationship mutation has yet been committed.

**Crash after external tool submission:** effect record/provider operation ID is reconciled before any retry.

**Crash after domain commit, before channel send:** output-delivery record remains pending and can be retried without regenerating the character turn.

**Old dead-letter replay after workflow changes:** operator selects the historical publication/runtime or performs an explicit migration/dry run before effects are enabled.

### 58.2 Reference Embodiment R2 — Discord interactions with SQS FIFO and AWS Step Functions

This embodiment demonstrates that the same RCCP semantics can be realized through a managed state-machine runtime.

#### 58.2.1 Ingress

A Discord interaction or selected Gateway message event is validated and normalized. The adapter creates `interaction_id` and stores durable idempotency metadata.

For an Interaction that requires a prompt acknowledgement, the adapter issues an allowed initial/deferred response inside the channel deadline, then continues asynchronously.

Accepted work is sent to SQS FIFO with:

```text
MessageGroupId         = service_id + conversation_id
MessageDeduplicationId = interaction_id
```

A worker receiving the message checks persistent RCCP idempotency and starts a Step Functions Standard execution using a name or input correlated with `interaction_id`.

#### 58.2.2 State machine

A generated or selected state machine performs:

```text
ResolvePublication
      |
Parallel [
  LoadCharacter,
  LoadMemory,
  LoadRelationshipState,
  RetrieveKnowledge
]
      |
AssembleContext
      |
Generate
      |
Choice(tool requested?)
   | no --------------------+
   | yes                    |
ExecuteAuthorizedTool       |
   |                        |
GenerateWithToolResult      |
   +------------------------+
      |
CommitTransition
      |
EmitOutputIntent
```

Transient Core Service calls have bounded `Retry` rules. Permanent validation failures go through `Catch` to a non-retryable failure state.

A long-running approved tool can use a callback task token. The token is stored only in an operational correlation record and is not given to the model.

#### 58.2.3 Discord egress

A Discord adapter maps the output intent to an interaction edit/follow-up or to another allowed message operation. Discord-specific embeds/components remain adapter/presentation representations. Character and Model Services receive semantic content and capability information rather than Discord component JSON.

#### 58.2.4 State and execution identity

Step Functions execution state is not the authoritative store for long-term character memory or relationship state. PostgreSQL, DynamoDB, or another RCCP state store owns those records. The execution input carries immutable revision identifiers and stable record keys.

This permits replacing Step Functions with another orchestrator without migrating character memory simply because orchestration technology changed.

### 58.3 Reference Embodiment R3 — Public livestream character with shared room context and private actor continuity

This embodiment uses Twitch or YouTube Live Chat.

The Channel Adapter maps each public chat event to:

- one broadcast/room conversation scope;
- one external actor identity;
- one concrete event/message identity;
- one character/service identity;
- an audience visibility of public.

A routing workflow applies spam/rate/eligibility policy and can ignore most messages when traffic is high.

The context is assembled from two different scopes:

```text
Shared room context:
  recent selected public messages
  current stream/event state
  shared world/scene state

Private actor context:
  verified/per-platform actor memory
  actor-character relationship state
  prior selected interactions
```

Private memory is never inserted into another user's personalized context merely because both users are present in the same public room.

A selected message can produce a public character reply. A membership/funding/event notification can enter a distinct creator-defined workflow. The live broadcast ID is an ephemeral destination; long-term actor/character state is keyed independently so it can continue on a later broadcast or another linked channel.

## 59. Explicit processing state machines and transactional boundaries

### 59.1 Interaction processing state

An implementation can represent one admitted interaction with the following logical states:

```text
              +------------------+
              |     RECEIVED     |
              +--------+---------+
                       |
                       v
              +------------------+
              |     ADMITTED     |
              +--------+---------+
                       |
                       v
              +------------------+
              |    PROCESSING    |
              +---+----------+---+
                  |          |
          retryable|          |permanent/poison
                  v          v
          +-----------+  +-------------+
          | RETRY_WAIT|  | QUARANTINED |
          +-----+-----+  +-------------+
                |
                +----------> PROCESSING
                       |
                       v
              +------------------+
              | DOMAIN_COMMITTED |
              +--------+---------+
                       |
                       v
              +------------------+
              | DELIVERY_PENDING |
              +---+----------+---+
                  |          |
                  v          v
            +-----------+ +----------------+
            | DELIVERED | | NOT_DELIVERABLE|
            +-----------+ +----------------+
```

`DOMAIN_COMMITTED` means the character turn/state transition has crossed the configured authoritative commit boundary. Delivery failure after this state is handled as a delivery problem unless the workflow explicitly defines a compensating domain transition.

### 59.2 Idempotency record

A minimal durable record can include:

```json
{
  "idempotency_scope": "service:.../channel:line",
  "source_event_id": "event:...",
  "interaction_id": "int:...",
  "status": "ADMITTED",
  "first_seen_at": "timestamp",
  "publication_revision": "pub:...",
  "committed_transition_id": null,
  "output_intent_id": null
}
```

A duplicate source event resolves to the existing `interaction_id`.

### 59.3 Domain transition record

A state-mutating turn can use a transition record:

```json
{
  "transition_id": "transition:...",
  "interaction_id": "int:...",
  "conversation_id": "conv:...",
  "base_state_versions": {
    "relationship": 42,
    "world": 105
  },
  "new_state_versions": {
    "relationship": 43,
    "world": 106
  },
  "message_ids": ["msg:..."],
  "publication_revision": "pub:...",
  "status": "COMMITTED"
}
```

The exact schema is optional. The disclosed property is durable correlation between the accepted interaction, the state versions it observed/created, and the publication under which the transition was computed.

### 59.4 Outbox state

```text
PENDING -> CLAIMED -> DELIVERED
              |
              +--> PENDING      (lease expired / retry)
              |
              +--> QUARANTINED  (bounded permanent failure)
```

Claiming an outbox item can use a row lock, lease, compare-and-swap, broker transaction, or other multi-worker-safe mechanism.

### 59.5 Effect state

```text
PREPARED -> SUBMITTED -> CONFIRMED
    |           |
    |           +--> UNKNOWN -> RECONCILING -> CONFIRMED / FAILED
    |
    +--> CANCELLED
```

`UNKNOWN` means the caller cannot determine whether an external provider accepted the side effect. The system does not treat `UNKNOWN` as equivalent to `FAILED`. Reconciliation is attempted using provider operation IDs, idempotency keys, external lookup, or operator review.

### 59.6 Publication lifecycle

```text
DRAFT
  |
  v
VALIDATED
  |
  v
APPROVED
  |
  v
PUBLISHED ------> SUPERSEDED
  |                   |
  +-------------------+
         rollback can make a prior approved revision active for new work
```

Published artifacts are immutable. "Rollback" changes the active binding; it does not mutate the old or new revision.

### 59.7 Derived-state migration

```text
OLD_ACTIVE
    |
    +--> BUILD_NEW
              |
              v
         VALIDATE_NEW
          /       \
       fail       pass
       /             \
 discard/retry      DUAL/SHADOW
                       |
                       v
                  SWITCH_READS
                       |
                       v
                  NEW_ACTIVE
                       |
                       v
                  RETIRE_OLD
```

The authoritative source remains available throughout the migration. A rollback policy can switch reads back while the old projection is retained.

## 60. Explicit technical propositions

The propositions below make specific combinations explicit. They are implementation disclosures, not requirements that every RCCP deployment use the listed combination.

### TP-01 — Channel-neutral processing with capability-aware egress

A computer-implemented character interaction system can:

1. receive an event through a channel-specific adapter;
2. authenticate/validate the channel event;
3. map channel-specific actor, destination, message, event, and content fields into a channel-neutral interaction;
4. execute character processing through channel-independent orchestration and Core Services;
5. produce a channel-neutral output intent;
6. obtain or retain capabilities/constraints of the destination channel;
7. transform the output intent according to those capabilities; and
8. perform the channel-specific delivery operation.

The Channel Adapter, rather than the Character or Memory Service, owns the external protocol and delivery timing/format rules.

Variations include selecting the output plan before generation, after generation, or both; direct response, asynchronous response, edit/follow-up, push/proactive delivery; and direct, group, room, or public/broadcast conversation scopes.

### TP-02 — Immutable creator publication bound to runtime execution

A character service can store an immutable publication that binds at least a character definition revision and an orchestration/workflow revision. An admitted interaction resolves the publication and records its identifier. Runtime execution uses the bound revisions even if a newer publication becomes active before the interaction completes. A rollback changes the active publication for subsequently resolved interactions without rewriting the historical publication or execution provenance.

The publication may additionally bind model, memory, tool, policy, knowledge, asset, schema, or capability revisions.

### TP-03 — Stable Core Services with creator-editable orchestration

A system can expose stable Character, Memory, State/Relationship, Model, Search, and/or Tool contracts while representing character-specific behavior as a separately versioned orchestration definition. A Content Creator changes the orchestration definition without requiring redeployment of the underlying shared Core Services. Validation restricts which operations, tools, resources, and execution limits the creator-authored definition may use.

The orchestration may be native code, a workflow engine definition, a graph, a declarative DSL, a template expansion, or an RCCP definition interpreted/compiled into one of those forms.

### TP-04 — Horizontally scaled ordered character interaction with independent idempotency

A multi-instance ingress/worker system can:

1. assign a stable interaction identity from an external event identity;
2. durably record admission before asynchronous processing;
3. route the interaction using an ordering key associated with a conversation/actor/service scope;
4. serialize state-mutating processing for that scope using broker grouping/session semantics, a workflow/actor instance, lock, sequence, or optimistic concurrency;
5. independently check application-level idempotency before committing the state transition; and
6. record output/effects using stable identities.

Thus broker ordering and duplicate suppression are complementary rather than interchangeable mechanisms.

### TP-05 — Character state transition with effect journal

A character workflow can compute a domain transition and a requested external side effect under the same `interaction_id`/`transition_id`. Before executing a non-idempotent external side effect, the system persists an effect record. The effect record progresses through submitted/confirmed or uncertain/reconciliation states. On retry after an uncertain failure, the system checks the prior effect/provider operation before deciding whether to execute again.

The external effect can be a tool action, message delivery, transaction, reservation, calendar operation, purchase request, device/API operation, or another authorized operation.

### TP-06 — Historical-revision controlled replay

Failed work can be stored with identifiers of the character publication, workflow revision, contract version, model/tool/policy revisions, and known committed effects. A replay procedure can dry-run the failed work with side effects disabled, compare historical and current compatibility, and then either execute under the historical compatible runtime or perform an explicit migration before re-enabling remaining effects.

The replay therefore does not silently reinterpret old work under whatever configuration happens to be current.

### TP-07 — Cross-channel continuity with verified identity linkage

A system can maintain channel-local external identities separately from an optional canonical actor identity. A linking operation requires a service-defined verification/consent mechanism. Memory/state records have explicit scopes. After linkage, selected continuity can be shared across channels while channel-local or service-local data remains isolated according to scope policy.

Unlinking changes future resolution without requiring deletion of unrelated channel-local identifiers unless retention policy requires it.

### TP-08 — Budgeted multi-source model context with provenance

A context assembler can receive character instructions, policy, recent history, summaries, long-term memories, structured state, retrieved knowledge, tool descriptions, channel capabilities, and workflow context as independently identified sources. It allocates a model-input budget by priority/cost/token/latency class, reduces optional sources when necessary, preserves mandatory policy, and emits a context manifest linking included fragments to source IDs/revisions.

A later execution can compare manifests to identify whether changed behavior resulted from content/state/retrieval/model policy rather than from the channel.

### TP-09 — Nested conversational graph and durable process

A long-lived durable workflow owns process identity, durable waits, retries, external events, and scheduled continuation. A workflow activity or segment invokes an AI/conversational graph runtime for a bounded reasoning/conversation segment. The graph may checkpoint within the segment, but authoritative long-term character memory/relationship/world state is accessed through RCCP Core Services. The durable workflow records the graph result and can later resume another segment.

The graph runtime and durable runtime can therefore be upgraded/replaced independently subject to stable boundaries.

### TP-10 — Shared public-room context with private actor continuity

For a public room or livestream, a Channel Adapter maps many actor events into one room/broadcast conversation plus separate actor identities. A workflow selects messages/events for character processing. Context combines shared room/scene state with private actor-character continuity whose access is scoped to the selected actor. Public output is generated without exposing one actor's private memory to another actor merely because both are present in the room.

### TP-11 — Portable immutable character package with environment bindings

A published character artifact can contain immutable references/hashes for character content, workflow, schemas, model/memory/tool policy, knowledge/assets, and required logical capabilities while excluding environment credentials. An environment binding maps those logical capabilities to concrete providers, endpoints, stores, secrets, or adapters. The same package can therefore be validated in preview and activated in production with different bindings while retaining the same content identity.

### TP-12 — Online derived-state replacement behind stable contracts

A live character service can create a new version of a derived store such as embeddings, search index, summary projection, or denormalized state while the prior version remains active. Backfill/shadow/dual-read or dual-write validation compares the new projection. A controlled switch changes reads to the new version without changing the Channel Adapter or stable interaction contract. The old version remains available for rollback until retirement criteria are met.

### TP-13 — Provider-neutral model/tool mediation

A Model Service can translate a stable RCCP model request to a selected provider API and translate provider streaming/content/tool events back to RCCP semantic events. A provider tool/function call is not executed directly by the provider adapter; it becomes a normalized ToolRequest evaluated by an RCCP Tool Service for schema, authorization, credentials, idempotency, and effect policy. The normalized result is converted back to the provider's continuation format.

Provider-hosted tools may be permitted as a separately declared capability; they are not implicitly trusted merely because the model requested them.

### TP-14 — Proactive character intent separated from channel delivery permission

A workflow can generate and persist a semantic proactive output intent independently of whether a bound external channel currently permits delivery. Each Channel Adapter evaluates current destination validity, permission/window, rate, and capability policy. It may deliver, defer, redirect to another permitted channel, or record non-delivery. Character/state progression follows an explicit workflow policy and is not automatically determined by one external platform's temporary delivery permission.

### TP-15 — Multi-character shared world with scoped private state

A coordinator can resolve multiple immutable character revisions, load shared world/scene state, load separately authorized private state for each character, select one or more speakers, and commit shared/private state transitions under distinct scopes. A shared coordinator does not imply that every participating character can access all other characters' private memory.

### TP-16 — Conversation supersession with side-effect boundary

When a newer interaction arrives while an older generation is still in progress, a workflow can classify the older work as cancellable, non-cancellable but suppressible, or already past an irreversible side-effect boundary. For cancellable work it signals provider/workflow cancellation. For suppressible work it allows computation to finish but prevents stale output delivery. For work past a side-effect boundary it records/finishes the effect and applies an explicit reconciliation/compensation policy instead of pretending the operation never occurred.

### TP-17 — Channel-ingress mode replacement without domain changes

A Channel Adapter may support two or more ingress transports for the same external service, such as webhook and polling, gateway and interaction endpoint, or HTTP and socket mode. Each transport produces the same normalized interaction and stable event identity semantics. Switching ingress mode requires no change to Character, Memory, State, Model, or Tool contracts.

### TP-18 — Interactive and offline orchestration separation

An interactive runtime handles latency-sensitive user turns and commits durable job/domain records for expensive derived work. A separate batch/container/process orchestrator consumes those records to perform summarization, evaluation, indexing, media processing, backfill, or publication validation. Results are versioned and applied through idempotent Core Service operations. Interactive processing remains available while the derived revision is being built and validated.

## 61. Figure set

The diagrams in this section are functional figures. They can be redrawn in another graphical format without changing the described relationships.

### Figure 1 — Logical RCCP architecture

```mermaid
flowchart TB
  CH[External Channels]
  CA[Channel Adapters]
  ADM[Durable Admission / Ordering]
  ORCH[Orchestration]
  CHAR[Character Service]
  MEM[Memory Service]
  STATE[State / Relationship Service]
  SEARCH[Search Service]
  MODEL[Model Service]
  TOOL[Tool Service]
  OUT[Channel-neutral Output]
  CH --> CA --> ADM --> ORCH
  ORCH --> CHAR
  ORCH --> MEM
  ORCH --> STATE
  ORCH --> SEARCH
  ORCH --> MODEL
  ORCH --> TOOL
  ORCH --> OUT --> CA --> CH
```

### Figure 2 — Admission, processing, and delivery boundaries

```mermaid
flowchart LR
  W[Webhook/Event] --> V[Validate]
  V --> I[Inbox / Idempotency]
  I --> Q[Durable Queue]
  Q --> P[Ordered Processing]
  P --> C[Domain Commit]
  C --> O[Output Intent]
  O --> D[Delivery Effect]
  C --> X[Outbox / Derived Work]
```

### Figure 3 — Revision-pinned execution

```mermaid
flowchart TB
  PUB[Active Publication]
  IN[Admitted Interaction]
  MAN[Execution Manifest]
  RUN[Workflow Run]
  PUB -->|resolve immutable revisions| MAN
  IN --> MAN
  MAN --> RUN
  RUN --> CR[Character revision]
  RUN --> WR[Workflow revision]
  RUN --> MR[Model policy revision]
  RUN --> TR[Tool policy revision]
  RUN --> MMR[Memory policy revision]
```

### Figure 4 — Effect-journal recovery

```mermaid
stateDiagram-v2
  [*] --> Prepared
  Prepared --> Submitted
  Submitted --> Confirmed
  Submitted --> Unknown
  Unknown --> Reconciling
  Reconciling --> Confirmed
  Reconciling --> Failed
  Prepared --> Cancelled
```

### Figure 5 — Cross-channel identity and scoped continuity

```mermaid
flowchart TB
  L[LINE local identity]
  D[Discord local identity]
  W[Web account]
  C[Optional canonical actor]
  L -->|verified link| C
  D -->|verified link| C
  W -->|authenticated link| C
  C --> M[Shared actor-scoped memory]
  L --> LM[LINE-local state]
  D --> DM[Discord-local state]
```

### Figure 6 — Nested conversational graph

```mermaid
flowchart LR
  E[External Event] --> DW[Durable Workflow]
  DW -->|activity/segment| G[Conversational Graph]
  G --> CS[Core Services]
  CS --> G
  G -->|segment result| DW
  DW --> WAIT[Timer / Signal / Durable Wait]
  WAIT --> DW
```

### Figure 7 — Public room with shared and private continuity

```mermaid
flowchart TB
  ROOM[Public Room Events]
  ROUTE[Routing / Moderation]
  SHARED[Shared Room Context]
  A1[Actor A private state]
  A2[Actor B private state]
  GEN[Selected Character Execution]
  ROOM --> ROUTE
  ROUTE --> SHARED
  ROUTE --> A1
  ROUTE --> A2
  SHARED --> GEN
  A1 --> GEN
  A2 --> GEN
  GEN --> OUT[Public Output]
```

The workflow selects only the private state belonging to the actor whose interaction is being processed; the figure shows both possible scopes, not simultaneous disclosure of both actors' private memory to one generation.

### Figure 8 — Derived-state online migration

```mermaid
flowchart LR
  SRC[Authoritative Source]
  OLD[Old Projection Active]
  NEW[Build New Projection]
  VAL[Validate / Shadow]
  SW[Controlled Read Switch]
  RET[Retire Old]
  SRC --> OLD
  SRC --> NEW --> VAL --> SW --> RET
  OLD --> SW
```

### Figure 9 — Creator publication and environment binding

```mermaid
flowchart TB
  DRAFT[Creator Draft]
  VALID[Validation / Preview]
  PKG[Immutable Character Package]
  PREV[Preview Bindings]
  PROD[Production Bindings]
  DRAFT --> VALID --> PKG
  PKG --> PREV
  PKG --> PROD
  PREV --> PM[Preview model/tools/stores]
  PROD --> XM[Production model/tools/stores]
```

### Figure 10 — Model/tool mediation

```mermaid
sequenceDiagram
  participant O as Orchestration
  participant M as Model Service
  participant P as Provider Adapter
  participant T as Tool Service
  participant X as External Tool
  O->>M: stable model request
  M->>P: provider-specific request
  P-->>M: provider tool/function request
  M->>T: normalized ToolRequest
  T->>T: authorize + validate + effect record
  T->>X: external operation
  X-->>T: result / operation ID
  T-->>M: normalized ToolResult
  M->>P: provider continuation/tool result
  P-->>M: generated output
  M-->>O: channel-neutral model output
```

## 62. Ontology, knowledge graph, and perspective-aware continuity

RCCP can represent character knowledge and continuity as connected concepts rather than only as flat prompt text, chronological messages, or independently retrieved vector chunks.

Sections 62 through 79 disclose multiple complete technical embodiments. Each embodiment is independently implementable, and compatible embodiments can be combined. No selection among them is required for the disclosure to apply; a deployment selects the arrangement that satisfies its content, privacy, latency, scale, and operational requirements.

The represented information can include:

- world entities such as people, characters, organizations, places, objects, events, rules, concepts, and fictional terminology;
- world facts and constraints intended to be authoritative within a particular publication revision;
- character beliefs, including incomplete, mistaken, secret, inferred, or conflicting beliefs;
- actor-provided statements, preferences, experiences, and memories;
- actor-character and character-character relationship state;
- conversation, scene, story, quest, and temporal context;
- provenance, confidence, review status, validity interval, visibility, consent, and retention metadata;
- links from structured concepts to source passages, creator notes, interaction events, assets, or external records.

These categories are not required to share one truth status. For example, the following may coexist without being collapsed into one assertion:

- a published world fact;
- what Character A believes about that fact;
- what Character B is permitted to know;
- what an actor privately told Character A;
- what may be stated in a public room;
- what was true during an earlier story revision.

An implementation can model each item as a node, document, row, statement, triple, edge, event, or immutable record. A relation may carry qualifiers such as:

```json
{
  "statement_id": "stmt:01J...",
  "subject": "actor:123",
  "predicate": "prefers_topic",
  "object": "topic:astronomy",
  "perspective": {"kind": "actor_statement", "holder_id": "actor:123"},
  "source": {"interaction_id": "interaction:abc", "message_id": "message:def"},
  "validity": {"from": "2026-09-01T00:00:00Z", "until": null},
  "confidence": 0.9,
  "visibility_scope": "character-and-actor",
  "consent_grant_id": "consent:789",
  "revision": 3
}
```

The example is illustrative. An implementation may use different fields or may separate statements, provenance, authorization, and temporal data into different records.

### 62.1 Alternative representations

The same logical capability can be implemented with one or more of:

- RDF graphs and RDF-compatible vocabularies;
- OWL ontologies where formal classes, properties, constraints, or inference are useful;
- labeled-property graph databases;
- relational tables containing entities, typed relations, and qualifier tables;
- document databases containing linked records;
- event logs from which current knowledge projections are derived;
- vector, full-text, or hybrid indexes connected to authoritative source records;
- Open Knowledge Format bundles;
- ordinary Markdown, JSON, YAML, or other creator-owned files compiled into a runtime representation;
- provider-managed knowledge or memory behind an RCCP adapter.

RCCP does not require the storage representation, authoring representation, query representation, and model-context representation to be identical.

One embodiment stores creator-authored lore as versioned documents, compiles selected concepts and relations into a graph projection, indexes descriptive text for semantic retrieval, and emits a compact context manifest for a model call. Another embodiment stores authoritative statements in relational tables and creates graph and vector projections asynchronously.

### 62.2 Knowledge responsibility boundary

A Knowledge capability can be a dedicated Core Service, part of Search, part of Memory, part of Character, or a set of replaceable services. Regardless of topology, its contract can distinguish:

- resolving a concept by stable identifier;
- traversing explicitly typed relations;
- searching by text, embedding, metadata, or graph pattern;
- retrieving only statements visible to the current actor, character, channel, audience, and workflow;
- returning source and revision identifiers;
- proposing new statements without immediately making them authoritative;
- validating or rejecting a proposed update;
- invalidating, superseding, expiring, or deleting statements and derived projections;
- preserving the difference between source records and derived inferences.

The Model Service does not become the authority for knowledge merely because a model proposed or summarized a statement. A model-produced candidate can enter validation, creator review, deterministic checks, confidence policy, or another workflow before it is published or used as durable memory.

### 62.3 Retrieval and context assembly

Orchestration can combine graph traversal with vector, full-text, rule-based, or direct-key retrieval.

For example:

1. resolve the active character, actor, conversation, scene, and publication revisions;
2. identify candidate concepts from the current interaction;
3. traverse relations allowed by knowledge, memory, relationship, and consent policies;
4. retrieve source passages or structured attributes for selected nodes;
5. rank or filter results by relevance, authority, recency, visibility, token cost, and latency;
6. construct a context manifest that identifies every included item and its scope;
7. generate or select an output;
8. optionally propose memory or relationship updates with source references.

Traversal depth, result count, cycle handling, inference rules, and context budget are explicit policies. The system does not place an entire graph or knowledge bundle into every model request.

### 62.4 Conflicts, time, and character-specific belief

A graph-backed arrangement can keep contradictory statements when they have different sources, perspectives, validity intervals, or publication revisions. Conflict handling may:

- prefer a designated authoritative world source;
- select the belief held by the currently acting character;
- expose uncertainty or disagreement to the workflow;
- ask the actor to clarify;
- retain both statements while preventing either from becoming a system-wide fact;
- resolve the conflict through a creator or operator review process.

This permits stories containing secrecy, unreliable narrators, misunderstandings, changing facts, alternate timelines, or characters with different knowledge without converting every difference into corrupt data.

## 63. Open Knowledge Format embodiments

Open Knowledge Format (OKF) can be used as a portable authoring or exchange representation for RCCP knowledge. In this embodiment, concepts are represented as Markdown documents with YAML frontmatter and are connected by links or references. The bundle remains readable and reviewable by people while tools can discover concepts and relations.

RCCP can use OKF for:

- world lore, terminology, locations, objects, organizations, and events;
- character-visible and creator-only knowledge partitions;
- character definitions or references to character definitions;
- relationship types, conversation topics, and memory categories;
- mappings between concepts and source material;
- creator guidance, provenance, review status, freshness, and lifecycle metadata;
- import, export, archival, migration, and interchange between compatible tools.

### 63.1 Creator publication to runtime knowledge

One implementation uses the following lifecycle:

1. a creator edits a small OKF bundle or a UI that produces an equivalent bundle;
2. validation checks required metadata, identifiers, links, cycles, broken references, allowed custom fields, and access classifications;
3. review or approval records the accepted bundle revision;
4. publication creates an immutable content hash and binds it to a character or service publication;
5. a compiler or ingestion process creates graph, relational, full-text, and/or vector projections;
6. runtime retrieval returns source concept IDs and the immutable bundle revision;
7. rollback activates an earlier bundle revision and rebuilds or switches the affected projections.

The original bundle remains the creator-owned source representation in this embodiment. Runtime indexes are replaceable derived state.

### 63.2 Static lore and dynamic memory

Static creator-authored lore and dynamic actor memory do not have to be stored in the same bundle.

Alternative embodiments include:

- immutable published lore bundles plus a protected actor-memory store;
- a base world bundle plus per-character belief overlays;
- a base world bundle plus per-actor private overlays;
- append-only memory documents compiled into a private graph;
- dynamic records in a database with controlled export to an OKF bundle;
- no OKF representation for dynamic memory, while OKF remains the interchange format for curated knowledge.

If actor-specific memory is represented as OKF, the bundle is stored behind the same access, encryption, retention, deletion, audit, and consent controls as any other private memory. The use of human-readable Markdown does not make the content public or suitable for a public source repository.

### 63.3 Standard fields and RCCP extensions

An RCCP OKF profile can separate:

- fields defined by the selected OKF specification version;
- RCCP-wide extension fields;
- service- or character-specific fields;
- private deployment metadata;
- runtime-only derived fields.

Consumers preserve unknown fields where required by the selected compatibility policy and do not silently interpret service-specific metadata as an OKF-wide semantic guarantee.

The publication records the OKF specification version and RCCP profile revision used for validation. A later OKF version can be adopted through an explicit migration or compatibility layer rather than changing the meaning of an already published bundle.

### 63.4 Specification references

The current Open Knowledge Format specification is maintained at <https://github.com/GoogleCloudPlatform/open-knowledge-format/blob/main/SPEC.md>, with the repository and examples at <https://github.com/GoogleCloudPlatform/open-knowledge-format>. The version used by an RCCP publication is pinned rather than inferred from the latest repository state.

RDF and OWL are separate possible representations with their own formal semantics. Current RDF specifications are published by W3C at <https://www.w3.org/TR/rdf12-concepts/>, and the OWL 2 document set is introduced at <https://www.w3.org/TR/owl2-overview/>. Naming these alternatives does not require an RCCP deployment to use Semantic Web technology.

## 64. Direct, multi-party, room, and broadcast conversation profiles

Conversation topology is a separate concern from the fact that an external platform delivered one event at a time. A multi-party profile can represent many-to-many (n-to-n) conversation among actors and characters without requiring every individual ingress event to contain several speakers.

RCCP can support profiles including:

- direct actor-to-character conversation;
- several actors interacting with one character;
- one actor interacting with several characters;
- several actors and several characters in one scene;
- character-to-character conversation with no human message for a turn;
- public room or livestream conversation;
- private group conversation;
- threaded, topic-based, spatial, scene-based, or game-session conversation;
- a conversation that moves between channels while retaining selected continuity.

A normalized event may still contain one originating actor and one message. Orchestration can aggregate several normalized events into a later conversational turn without requiring the Channel Adapter to invent a synthetic human speaker.

### 64.1 Participant, audience, and speaker model

A multi-party conversation can resolve a participant set such as:

```json
{
  "conversation_id": "conversation:room-42",
  "topology": "multi_party",
  "participants": [
    {"participant_id": "actor:123", "kind": "actor", "role": "member"},
    {"participant_id": "actor:456", "kind": "actor", "role": "moderator"},
    {"participant_id": "char:alice", "kind": "character", "role": "speaker"},
    {"participant_id": "char:bob", "kind": "character", "role": "speaker"}
  ],
  "audience": {"kind": "conversation_members"},
  "thread_id": "thread:topic-7",
  "scene_id": "scene:station"
}
```

Participant membership, authorization, reply targeting, visibility, and output speaker identity are distinct fields or responsibilities. Presence in one room does not authorize access to every participant's private memory.

### 64.2 Turn formation

Alternative turn-formation strategies include:

- process every admitted event independently;
- wait for a fixed or adaptive collection window;
- collect until an explicit end-of-turn signal;
- collect until a quiet period;
- select one message from a high-volume room;
- group replies, mentions, or thread messages;
- let a workflow or model propose a batch subject to deterministic limits;
- use a game, meeting, or scene controller as the turn authority.

The resulting turn records the source event and message IDs it contains. Redelivery of one source event does not cause the entire turn to be applied twice.

### 64.3 Ordering and concurrency

One deployment can use different ordering scopes for different state:

- per-originating actor for private actor memory;
- per actor-character pair for relationship progression;
- per character for character-private state;
- per thread or scene for shared conversational order;
- per world or quest for shared state transitions;
- per external destination for delivery ordering.

The coordinator acquires or validates only the scopes it mutates. Unrelated conversations can continue concurrently. Optimistic concurrency, ordered broker groups, actor runtimes, workflow instances, leases, or explicit locks are alternative implementations.

### 64.4 Output and reply targeting

A multi-party result can contain zero or more channel-neutral output intents. Each intent identifies:

- speaker character or system role;
- intended audience;
- reply target, mention target, thread, or scene when applicable;
- visibility such as public, group, selected participants, or private;
- content parts;
- ordering and correlation metadata;
- delivery policy and expiration.

The Channel Adapter renders these semantics with the capabilities of the destination platform. A channel that cannot express a private subreply, multiple speakers, or a thread can reject the plan, select an authorized fallback, split delivery, or report non-delivery according to policy.

## 65. Character-to-character conversation

Character-to-character conversation is an orchestration topology, not a requirement that one model impersonate every participant in one unrestricted prompt.

A coordinator can:

1. resolve immutable revisions for all participating characters;
2. load shared scene and world state;
3. load only the private knowledge, memory, goals, and relationships authorized for the character whose turn is being evaluated;
4. select the next speaker or speakers;
5. invoke separate Character and Model operations or one constrained multi-speaker operation;
6. validate proposed speech and state transitions;
7. commit shared and private changes under separate scopes;
8. emit outputs to a user-visible channel, an internal simulation stream, or both.

Alternative speaker-selection strategies include fixed order, initiative, workflow rules, event triggers, priority queues, moderator selection, model-assisted selection, and simultaneous proposals followed by arbitration.

### 65.1 Private knowledge and asymmetric relationships

Character A's memory of Character B may differ from Character B's memory of Character A. A relationship can therefore be directional and perspective-scoped.

The coordinator can distinguish:

- shared facts known to both characters;
- facts visible only to one character;
- each character's private memories;
- directional affinity, trust, obligation, suspicion, or familiarity;
- public statements heard by all participants;
- private messages or internal observations;
- creator-only information that affects behavior but must never be emitted.

No participating character receives another character's private state merely because both are controlled by the same coordinator.

### 65.2 Bounded autonomous dialogue

Character-only dialogue can be initiated by a schedule, story event, tool result, state transition, moderator command, or another character output.

The workflow applies explicit limits such as:

- maximum turns, wall-clock deadline, and model/tool budget;
- allowed participants and destinations;
- termination conditions;
- repetition or loop detection;
- state mutation permissions;
- tool and external-effect permissions;
- human approval gates for selected effects;
- whether unobserved dialogue becomes durable canon, a proposal, or disposable simulation.

Stopping generation and committing state are separate decisions. A cancelled dialogue does not automatically roll back effects that were already committed, and no external side effect is repeated merely to recreate a missing conversational turn.

## 66. Consent-scoped cross-channel memory and relationships

Verified identity linkage can establish that several channel-local identities belong to one canonical actor, but identity linkage alone does not authorize every memory or relationship item to flow to every channel.

A separate consent or continuity-sharing policy can authorize selected information by:

- canonical actor;
- source and destination channel or channel instance;
- service, character, conversation, group, or audience;
- topic or concept;
- memory class or individual memory;
- relationship dimension or relationship summary;
- purpose, such as personalization, continuity, search, or analytics;
- visibility, such as private direct chat or public room;
- validity period, revocation state, and policy revision.

An illustrative grant is:

```json
{
  "consent_grant_id": "consent:789",
  "actor_id": "actor:123",
  "source_scopes": [
    {"channel_type": "line", "channel_instance_id": "line:official-1"}
  ],
  "destination_scopes": [
    {"channel_type": "discord", "channel_instance_id": "discord:guild-9"}
  ],
  "allowed_topics": ["topic:astronomy", "topic:favorite-books"],
  "allowed_memory_kinds": ["preference", "relationship_summary"],
  "allowed_characters": ["char:alice"],
  "audience_limit": "direct_or_private",
  "purpose": "conversation_continuity",
  "valid_from": "2026-09-06T00:00:00Z",
  "expires_at": null,
  "status": "active",
  "policy_revision": "continuity-policy:4"
}
```

The system evaluates the grant when retrieving or projecting continuity, not only when linking accounts.

### 66.1 Topic and relation selection

Topic-scoped sharing can be implemented through stable concept IDs, ontology classes, creator-defined labels, policy rules, an allow-list of memory IDs, or a combination.

For example, an actor can permit a character to remember conversations about astronomy across LINE and Discord while keeping health, employment, location, another character relationship, and all public-room disclosures channel-local.

Relationship sharing can likewise select:

- the complete directional relationship state;
- only a coarse relationship stage;
- selected dimensions;
- a derived summary;
- events that contributed to the relationship;
- no relationship state while still sharing selected factual memories.

A derived cross-channel summary records its source scopes and consent grant so it can be invalidated or rebuilt when the grant changes.

### 66.2 Linking, unlinking, revocation, and deletion

The following are separate operations:

- link a channel identity to a canonical actor;
- authorize selected continuity sharing;
- revoke or narrow future sharing;
- unlink a channel identity;
- delete source data;
- delete or rebuild derived cross-channel projections;
- retain an audit record where policy permits or requires it.

Revocation normally prevents future retrieval and new derivation under the revoked grant. Whether previously delivered content, historical audit records, or source memories are deleted is decided by the applicable retention and deletion policy rather than inferred from unlinking alone.

### 66.3 Public and group destinations

Before inserting actor-specific memory into context for a public or group destination, the workflow checks both the consent grant and the current audience. A grant for cross-channel direct-chat continuity does not authorize public disclosure.

One embodiment creates an audience-specific projection that contains only information permitted for the current destination and character. Another retrieves candidate memories from their original scopes and filters every item at context-assembly time. A hybrid can materialize commonly used projections while rechecking the current policy before use.

### 66.4 Identity alternatives

Canonical identity is optional. Alternative embodiments include:

- pairwise links between two channel identities;
- per-character canonical identities;
- service-local identity without cross-service linkage;
- pseudonymous continuity tokens controlled by the actor;
- short-lived or one-session linkage;
- user-carried encrypted continuity packages;
- no identity linkage, with explicit copy or import of selected memories.

These alternatives can reduce the breadth of one global identity while still enabling intentionally selected continuity.

## 67. Contract profiles and evolution from direct chat to multi-party interaction

A simple direct-chat contract does not have to include the full participant, speaker, audience, and turn-formation model.

One embodiment defines a long-lived Direct Chat profile with:

- one normalized originating actor;
- one target character;
- one input message per request;
- one conversation scope;
- typed content parts;
- stable interaction and message identifiers;
- tenant and service scopes;
- channel type and channel-instance origin;
- a response containing one or more channel-neutral character messages or an explicit error.

Multi-party and multi-character interaction can later use:

- a different API;
- a different explicit profile;
- a discriminated union of layouts;
- an event-ingress API plus a separate turn/coordinator API;
- a stream of normalized events consumed by a conversation coordinator.

The Direct Chat profile and Multi-party profile can coexist. Adding multi-party interaction does not require changing the meaning of an existing direct-chat actor, character, conversation, or message field.

### 67.1 Illustrative direct-chat envelope

```json
{
  "interaction_id": "interaction:01J...",
  "scope": {
    "tenant_id": "tenant:example",
    "service_id": "service:character-chat"
  },
  "origin": {
    "channel_type": "line",
    "channel_instance_id": "line:official-1"
  },
  "conversation": {"id": "conversation:abc"},
  "actor": {"id": "actor:channel-local-123"},
  "character": {"id": "char:alice"},
  "message": {
    "id": "message:external-event-456",
    "content": [{"type": "text", "text": "Hello"}]
  },
  "limits": {
    "deadline_at": "2026-09-06T00:00:05Z",
    "max_output_messages": 3
  }
}
```

`interaction_id` identifies admitted processing, while `message.id` identifies the input message. They can coexist even when both are derived from one external event.

Character, actor, conversation, tenant, service, and channel-instance identifiers remain separate because they have different lifecycles and scopes. A LINE identity and a Discord identity do not become the same actor ID merely because a later consented identity link exists.

### 67.2 Multi-party profile alternatives

A Multi-party profile may reference:

- one originating event plus the current participant and scene snapshot;
- a set of source message IDs forming a turn;
- a durable conversation-coordinator instance;
- proposed and selected output speakers;
- multiple channel-neutral output intents;
- per-output audience and reply target;
- the revisions of participant, routing, memory, and consent policies.

The profile is versioned independently when its compatibility requirements differ from Direct Chat. Channel Adapters can continue producing one normalized event per external message while the coordinator owns aggregation and speaker selection.

## 68. Raw channel information, normalized identity, and privacy-preserving diagnostics

A Channel Adapter can preserve channel-source information needed for signature verification, deduplication, reply correlation, diagnostics, or later policy review without placing the complete raw payload in the channel-neutral orchestration request.

Alternative storage locations include:

- an adapter-owned work item;
- a restricted correlation record;
- an encrypted source-event record;
- an allow-listed audit projection;
- no persistent raw record after admission.

Records are correlated with stable interaction and channel-event identifiers. Access, retention, redaction, encryption, and creator/operator visibility are explicit policies.

Logs can replace selected identifiers or text fragments with keyed HMAC values for correlation without storing the original value in ordinary diagnostic output. Key purpose, encoding, rotation, collision handling, and failure behavior are defined explicitly. HMAC-based logging is a diagnostic embodiment and does not replace authorization, encryption, or source-data retention controls.

Creator-selected channel assets, such as a LINE sticker identifier or Discord custom emoji identifier, can remain exact creator-owned values in a versioned channel-specific content descriptor. They need not be forced into a universal semantic asset ID, and the complete vendor request object need not enter the orchestration contract.

## 69. Additional combination disclosures

### Combination AK — Ontology-backed world knowledge with character-specific belief overlays

A published world ontology defines entities, relations, and authoritative story facts. Each character has a separately scoped belief overlay containing known, unknown, inferred, mistaken, or secret statements. Orchestration retrieves the acting character's permitted view and records source and publication revisions in the context manifest.

### Combination AL — OKF creator bundle compiled to graph and vector projections

A creator edits a versioned OKF bundle. Validation and approval produce an immutable publication revision. An ingestion process compiles linked concepts into a graph projection and descriptive text into a vector index. Runtime results point back to source concept IDs, while rollback switches the bundle revision and rebuilds or replaces derived projections.

### Combination AM — Actor memory graph with consent-scoped channel projection

Actor statements and character observations enter an authoritative private memory graph with source event IDs. A consent grant selects topics, memory classes, characters, destination channels, and audience limits. Context assembly produces a destination-specific projection and records the grant and source IDs used.

### Combination AN — Multi-user, multi-character scene coordinator

Several actors and characters participate in one scene. A coordinator collects or selects admitted events, resolves participant revisions, loads shared scene state and separately authorized private state, selects one or more character speakers, and commits shared and private transitions under distinct concurrency scopes.

### Combination AO — Bounded autonomous character-to-character dialogue

A story event starts a character-only dialogue. Separate character operations receive only their authorized beliefs and goals. A coordinator enforces turn, time, cost, tool, destination, and termination limits, records each committed transition, and can stop later turns without repeating earlier external effects.

### Combination AP — Direct Chat and Multi-party contracts in parallel

Simple one-actor/one-character services use a stable Direct Chat profile. Group, room, broadcast, and multi-character services use a separate profile or coordinator API. Both share stable content, identity, publication, memory, state, and output concepts without forcing participant arrays or speaker arbitration into every direct-chat request.

### Combination AQ — Public-room selection with private cross-channel continuity

A public-room workflow selects an eligible actor event, resolves a verified identity link, and retrieves only memories whose consent grant permits the current character and public or group audience. Direct-chat-only memories remain excluded even when the actor has authorized their use on another private channel.

### Combination AR — Relational authority with replaceable ontology and graph projections

Authoritative memory and relationship records remain in relational storage. Background processing builds an ontology-aligned graph and vector index. Retrieval can traverse the graph and return source record IDs. Migration or index failure does not change the authoritative records or Channel Adapter contract.

## 70. Additional worked reference embodiments

### 70.1 Reference Embodiment R4 — OKF world bundle, graph retrieval, and actor-memory overlay

This embodiment uses an immutable OKF bundle as the creator-authored source for world knowledge.

Publication performs:

1. YAML/frontmatter and link validation;
2. RCCP profile and access-classification validation;
3. source, review, and publication metadata capture;
4. content hashing;
5. ingestion to a graph store;
6. full-text and embedding index creation;
7. activation of one immutable knowledge revision.

Dynamic actor memory is stored separately with actor, character, source-event, topic, visibility, retention, and consent references.

For a direct-chat interaction:

1. Orchestration resolves the character and knowledge publication revisions.
2. Current-message concepts are identified.
3. The Knowledge capability traverses world concepts and the acting character's belief overlay.
4. The Memory capability retrieves actor memories permitted for this character and destination.
5. The context assembler ranks structured statements and source passages under a budget.
6. The Model Service receives a provider-neutral request plus a context manifest.
7. Generated memory candidates are validated before entering authoritative memory.

If the graph or vector projection is unavailable, policy can fall back to direct document lookup, return a reduced-context response, or fail the interaction. It does not silently use private or stale data from an unrelated scope.

### 70.2 Reference Embodiment R5 — Discord group scene with two actors and two characters

A Discord Channel Adapter admits one message event at a time and maps guild/channel/thread/message/author identifiers into channel metadata and RCCP scopes.

A scene coordinator:

1. deduplicates every source event;
2. collects eligible messages until a quiet period or explicit turn trigger;
3. records the exact message IDs forming the turn;
4. loads shared scene state;
5. loads actor-specific continuity only for uses authorized in the group audience;
6. loads separate private belief and relationship state for each candidate character;
7. selects one or both character speakers;
8. generates channel-neutral outputs with speaker and reply-target identity;
9. validates and commits state transitions with optimistic concurrency;
10. sends Discord replies through an effect journal.

The same coordinator can accept LINE group, Matrix room, Web room, game-session, or livestream events through different adapters. A deployment can instead process each event immediately and omit collection windows.

## 71. Additional explicit technical propositions

### TP-19 — Perspective-aware knowledge graph for character behavior

A system can represent world facts, character beliefs, actor statements, relationship facts, and creator-only constraints as separately scoped statements connected by stable entity and relation identifiers. Retrieval selects statements by acting character, actor, scene, publication revision, validity interval, visibility, and authorization without collapsing conflicting perspectives into one global truth.

### TP-20 — Portable knowledge publication with replaceable runtime projections

A human-editable linked-document bundle, including an OKF bundle, can be validated and fixed as an immutable publication revision. Graph, relational, full-text, and vector runtime representations are derived from that revision and can be rebuilt or replaced while runtime results preserve links to source concept IDs and revisions.

### TP-21 — Actor-memory graph governed by per-topic cross-channel consent

Memory statements can be associated with stable topic or ontology identifiers, source scope, visibility, provenance, and consent grant. After verified identity linkage, context assembly shares only statements permitted for the destination channel, character, audience, purpose, and time, while other linked-channel data remains isolated.

### TP-22 — Multi-party turn formation from individually admitted events

A Channel Adapter can normalize and durably admit each actor event independently. A conversation coordinator later selects or groups source event IDs into a multi-party turn, applies deduplication and ordering at the appropriate state scopes, and emits outputs with explicit speaker, audience, and reply targets.

### TP-23 — Multi-character conversation with scoped private beliefs

A coordinator can execute two or more character revisions against shared scene state while retrieving separate private beliefs, memories, goals, and directional relationships for each acting character. Shared coordination does not grant one character access to another character's private state.

### TP-24 — Bounded autonomous character dialogue

A schedule, story event, state transition, or character output can initiate further character turns. A coordinator enforces explicit participant, turn, deadline, budget, termination, tool, effect, and destination policies and records committed effects independently from whether later dialogue turns are cancelled.

### TP-25 — Coexisting direct and multi-party contract profiles

A stable direct-chat contract can retain a one-actor, one-character, one-input-message model while a separately versioned multi-party profile or coordinator API adds participant sets, event aggregation, speaker selection, audiences, and multiple output intents. Both profiles reuse stable identity and content concepts without changing the established meaning of direct-chat fields.

### TP-26 — Consent revocation with derived-projection invalidation

Revoking or narrowing a continuity-sharing grant prevents future authorized retrieval under that grant and triggers invalidation or rebuilding of destination-specific derived summaries, caches, graph projections, or indexes. Identity unlinking, source deletion, derived cleanup, and audit retention remain explicit separate operations.

### TP-27 — Channel-source isolation with correlated diagnostics

A Channel Adapter can keep raw or channel-specific event data in a restricted source or correlation record while sending only normalized fields to orchestration. Stable event and interaction identifiers, optionally accompanied by purpose-specific HMAC values in logs, support diagnostics without making raw channel payloads a platform-wide dependency.

## 72. Additional figure set

### Figure 11 — Perspective-aware knowledge and continuity

```mermaid
flowchart TB
  W[Published World Knowledge]
  B[Character Belief Overlay]
  M[Actor Memory]
  R[Relationship State]
  P[Policy and Consent]
  K[Knowledge Context]
  W --> K
  B --> K
  M --> P
  R --> P
  P --> K
  K --> O[Character Orchestration]
```

### Figure 12 — OKF publication and runtime projections

```mermaid
flowchart TB
  D[Creator OKF Draft]
  V[Validate and Review]
  I[Immutable Bundle Revision]
  G[Graph Projection]
  X[Text and Vector Index]
  Q[Knowledge Service]
  D --> V --> I
  I --> G --> Q
  I --> X --> Q
```

### Figure 13 — Multi-party and multi-character coordination

```mermaid
flowchart TB
  E[Normalized Actor Events]
  T[Turn Formation]
  C[Scene Coordinator]
  S[Shared Scene State]
  P[Scoped Private State]
  O[Speaker-Identified Outputs]
  E --> T --> C
  S --> C
  P --> C
  C --> O
```

### Figure 14 — Consent-scoped cross-channel continuity

```mermaid
flowchart TB
  L[LINE Identity]
  D[Discord Identity]
  C[Verified Canonical Actor]
  G[Topic and Audience Grant]
  M[Scoped Memory and Relationship]
  X[Destination Context]
  L --> C
  D --> C
  C --> G
  M --> G
  G --> X
```

## 73. Detailed records for graph-backed knowledge and multi-party continuity

This section defines one complete family of logical records. The records can be JSON documents, relational rows, graph nodes and edges, event payloads, protocol messages, or in-process types. A physical implementation can combine or split them while preserving their identifiers, scopes, and authorization semantics.

### 73.1 Entity record

An Entity Record identifies something that can appear in knowledge, memory, relationship, conversation, or policy data.

```json
{
  "entity_id": "entity:place:moon-observatory",
  "entity_type": "place",
  "canonical_name": "Moon Observatory",
  "aliases": ["Lunar Observatory"],
  "scope": {
    "tenant_id": "tenant:example",
    "service_id": "service:character-chat",
    "world_id": "world:example"
  },
  "publication_revision_id": "publication:knowledge:17",
  "status": "active",
  "validity": {
    "from": "2026-09-01T00:00:00Z",
    "until": null
  }
}
```

`entity_id` is stable within the declared scope. A renamed place retains its identifier. Alternate timelines, worlds, services, or tenant namespaces use separate scope or revision identifiers rather than assuming that equal display names identify the same entity.

### 73.2 Qualified knowledge statement

A Qualified Knowledge Statement connects a subject to an object or literal through a predicate and carries the qualifications needed for character behavior.

```json
{
  "statement_id": "statement:01J...",
  "subject": {
    "kind": "entity",
    "id": "entity:character:alice"
  },
  "predicate": "believes_location_of",
  "object": {
    "kind": "entity",
    "id": "entity:artifact:blue-key"
  },
  "qualifiers": {
    "location_id": "entity:place:moon-observatory",
    "truth_mode": "character_belief",
    "perspective_holder_id": "char:alice",
    "confidence": 0.72,
    "visibility": "character_private",
    "valid_from": "2026-09-06T00:00:00Z",
    "valid_until": null
  },
  "scope": {
    "tenant_id": "tenant:example",
    "service_id": "service:character-chat",
    "world_id": "world:example",
    "character_id": "char:alice"
  },
  "source_refs": [
    {
      "kind": "interaction",
      "interaction_id": "interaction:abc",
      "message_id": "message:def"
    }
  ],
  "derived_from": [],
  "consent_grant_ids": [],
  "publication_revision_id": "publication:knowledge:17",
  "record_version": 4,
  "status": "active"
}
```

`truth_mode` is one of several extensible modes, including:

- `world_authoritative`;
- `creator_assertion`;
- `character_belief`;
- `actor_statement`;
- `character_observation`;
- `system_inference`;
- `unverified_report`;
- `hypothesis`;
- `disputed`;
- `superseded`.

The system retains different statements when they differ by perspective, source, time, world, publication, or visibility. It does not overwrite a Character B belief merely because Character A reports a conflicting fact.

### 73.3 Memory record alternatives

In a statement-native embodiment, a memory is a Qualified Knowledge Statement whose source is an interaction, event, tool result, or creator action.

In a separate-memory embodiment, the record is:

```json
{
  "memory_id": "memory:01J...",
  "owner_scope": {
    "tenant_id": "tenant:example",
    "service_id": "service:character-chat",
    "actor_id": "actor:123",
    "character_id": "char:alice"
  },
  "memory_kind": "actor_preference",
  "topic_ids": ["topic:astronomy"],
  "content": {
    "text": "The actor enjoys discussing lunar astronomy.",
    "statement_ids": ["statement:01J..."]
  },
  "source_refs": [
    {
      "interaction_id": "interaction:abc",
      "message_id": "message:def"
    }
  ],
  "visibility": "private_direct_chat",
  "consent_grant_ids": ["consent:789"],
  "importance": 0.74,
  "confidence": 0.91,
  "validity": {
    "from": "2026-09-06T00:00:00Z",
    "until": null
  },
  "retention_policy_id": "retention:actor-memory:2",
  "record_version": 2,
  "status": "active"
}
```

A text-only memory store, statement-only graph, dual text-and-graph record, immutable event log plus projection, or provider-managed memory adapter each implements this logical information. The authoritative representation is identified by deployment profile so two projections are not independently treated as truth.

### 73.4 Directional relationship record

```json
{
  "relationship_id": "relationship:alice-to-actor-123",
  "from_participant": {
    "kind": "character",
    "id": "char:alice"
  },
  "to_participant": {
    "kind": "actor",
    "id": "actor:123"
  },
  "scope": {
    "tenant_id": "tenant:example",
    "service_id": "service:character-chat"
  },
  "dimensions": {
    "familiarity": 0.66,
    "trust": 0.41,
    "affinity": 0.58
  },
  "stage_id": "relationship-stage:acquainted",
  "topic_scopes": ["topic:astronomy"],
  "evidence_refs": [
    {
      "interaction_id": "interaction:abc",
      "transition_id": "transition:relationship:42"
    }
  ],
  "visibility": "character_and_actor_private",
  "consent_grant_ids": ["consent:789"],
  "record_version": 42,
  "status": "active"
}
```

The reverse direction uses a different record. A symmetric relationship embodiment writes or derives both directions explicitly. A multidimensional embodiment uses numeric or categorical dimensions. A stage-machine embodiment stores named states and allowed transitions. An event-derived embodiment calculates the current relation from immutable events. Each arrangement preserves direction, scope, and evidence.

### 73.5 Identity-link record

```json
{
  "identity_link_id": "identity-link:01J...",
  "canonical_actor_id": "actor:123",
  "local_identities": [
    {
      "channel_type": "line",
      "channel_instance_id": "line:official-1",
      "local_actor_id": "line-user:U..."
    },
    {
      "channel_type": "discord",
      "channel_instance_id": "discord:guild-9",
      "local_actor_id": "discord-user:456"
    }
  ],
  "verification": {
    "method": "one_time_challenge",
    "verified_at": "2026-09-06T00:00:00Z",
    "verification_record_id": "verification:abc"
  },
  "status": "active",
  "record_version": 1
}
```

The verification record stores evidence appropriate to the service without exposing reusable credentials to Orchestration. Identity linkage establishes correspondence. Consent Grant records separately determine data use.

### 73.6 Multi-party turn record

```json
{
  "turn_id": "turn:01J...",
  "conversation_id": "conversation:room-42",
  "scene_id": "scene:station",
  "topology": "many_to_many",
  "formation_policy_id": "turn-policy:quiet-window-2s",
  "source_interactions": [
    {
      "interaction_id": "interaction:one",
      "message_id": "message:one",
      "actor_id": "actor:123",
      "sequence": 101
    },
    {
      "interaction_id": "interaction:two",
      "message_id": "message:two",
      "actor_id": "actor:456",
      "sequence": 102
    }
  ],
  "participant_snapshot_id": "participants:room-42:88",
  "candidate_speaker_ids": ["char:alice", "char:bob"],
  "selected_speaker_ids": ["char:alice"],
  "audience": {
    "visibility": "group",
    "participant_ids": ["actor:123", "actor:456"]
  },
  "state": "speakers_selected",
  "record_version": 3
}
```

The record preserves every source identity used to form a turn. A retry reuses `turn_id` and does not admit the same source interaction twice.

### 73.7 Speaker-identified output intent

```json
{
  "output_intent_id": "output:01J...",
  "turn_id": "turn:01J...",
  "speaker": {
    "kind": "character",
    "character_id": "char:alice",
    "character_revision_id": "character:alice:12"
  },
  "audience": {
    "visibility": "group",
    "participant_ids": ["actor:123", "actor:456"]
  },
  "reply_target": {
    "message_id": "message:two",
    "actor_id": "actor:456"
  },
  "content": [
    {
      "type": "text",
      "text": "I remember the observatory, but let us keep that between us."
    }
  ],
  "delivery": {
    "channel_type": "discord",
    "channel_instance_id": "discord:guild-9",
    "destination_id": "discord-channel:77",
    "expires_at": "2026-09-06T00:01:00Z"
  },
  "state_revision_refs": {
    "scene": 18,
    "relationship": 42,
    "consent_policy": "continuity-policy:4"
  }
}
```

Several output intents can share one turn and identify different speakers or audiences. Delivery order is explicit when the channel or scene requires it.

### 73.8 Context manifest

```json
{
  "context_manifest_id": "context:01J...",
  "execution_id": "execution:01J...",
  "knowledge_publication_revision_id": "publication:knowledge:17",
  "items": [
    {
      "source_kind": "knowledge_statement",
      "source_id": "statement:01J...",
      "source_revision": 4,
      "topic_ids": ["topic:astronomy"],
      "visibility": "character_private",
      "consent_grant_id": null
    },
    {
      "source_kind": "actor_memory",
      "source_id": "memory:01J...",
      "source_revision": 2,
      "topic_ids": ["topic:astronomy"],
      "visibility": "private_direct_chat",
      "consent_grant_id": "consent:789"
    }
  ],
  "policy_revisions": {
    "retrieval": "retrieval-policy:6",
    "consent": "continuity-policy:4",
    "model": "model-policy:9"
  },
  "budget": {
    "maximum_units": 12000,
    "used_units": 6840
  }
}
```

The manifest supports later explanation, evaluation, deletion propagation, consent revocation, replay, and comparison of executions without storing provider-native prompt objects as the platform-wide authority.

## 74. Complete knowledge publication, retrieval, and update procedures

### 74.1 OKF source-bundle embodiment

The source bundle contains one Markdown file for each concept or intentionally small concept group. A bundle includes an index and linked concept files such as:

```text
world/
  index.md
  places/moon-observatory.md
  artifacts/blue-key.md
characters/
  alice.md
  alice/beliefs/blue-key-location.md
topics/
  astronomy.md
```

An illustrative concept file is:

```markdown
---
type: place
title: Moon Observatory
description: Observatory used by the lunar research team.
status: active
tags:
  - astronomy
  - restricted-area
---

# Moon Observatory

The observatory contains the [Blue Key](../artifacts/blue-key.md) after
the chapter-three transition.

Character-specific beliefs are defined separately and link to this concept.
```

The bundle validator performs the following ordered procedure:

1. read every permitted file beneath the bundle root;
2. reject path traversal, unsupported file types, duplicate concept identifiers, and duplicate normalized paths;
3. parse frontmatter and Markdown separately;
4. validate required OKF fields against the pinned OKF version;
5. validate RCCP extension fields against the pinned RCCP profile;
6. resolve internal links relative to the bundle root;
7. reject or report broken links according to publication policy;
8. construct the directed concept graph;
9. detect forbidden dependency cycles and report permitted semantic cycles;
10. validate access classifications and prevent public concepts from linking to private content through an unrestricted path;
11. calculate a content digest over normalized file paths and bytes;
12. record creator, reviewer, validation-result, specification-version, and profile-version metadata;
13. create an immutable Knowledge Publication Revision.

The publication transaction writes the immutable manifest and an activation record. It never changes an already published revision. Activation can point a service, character, world, or environment to the new revision.

### 74.2 Runtime projection alternatives

The following are separately complete embodiments:

1. **Direct bundle retrieval.** Runtime reads the immutable Markdown files, follows links, and selects passages without a database projection.
2. **Relational projection.** Entities, statements, links, qualifiers, and source passages are inserted into relational tables with revision and scope columns.
3. **Property-graph projection.** Entity and statement nodes plus typed edges are written to a labeled-property graph.
4. **RDF projection.** RCCP entity and predicate identifiers are mapped to IRIs and emitted as RDF datasets with named graphs or equivalent scope separation.
5. **OWL-enabled projection.** Selected classes, properties, and rules are represented in OWL, and inferred statements retain provenance identifying the reasoning configuration.
6. **Document projection.** Each concept becomes a document containing resolved outgoing and incoming link identifiers.
7. **Text/vector projection.** Markdown bodies and selected fields become full-text and embedding index entries whose metadata points to the immutable source concept.
8. **Hybrid projection.** Structured traversal uses graph or relational storage, descriptive retrieval uses text/vector indexes, and both results join through stable concept identifiers.

Every derived entry carries the source publication revision. A query never combines incompatible publication revisions unless an explicit cross-revision migration policy permits it.

### 74.3 Projection build state

Projection construction uses these logical states:

```text
DISCOVERED
  -> VALIDATED
  -> BUILDING
  -> VERIFYING
  -> READY
  -> ACTIVE

BUILDING | VERIFYING
  -> FAILED

ACTIVE
  -> RETIRING
  -> RETIRED
```

Build work is idempotent by publication revision and projection type. Verification checks record counts, link resolution, sample traversals, access labels, source digests, and index coverage. Activation changes a pointer or routing policy only after every required projection is `READY`. Optional projections can fail without blocking activation when the publication policy defines a safe fallback.

### 74.4 Query-led retrieval

In the query-led embodiment:

1. classify or deterministically map the interaction to zero or more topic and entity identifiers;
2. build an authorization scope from tenant, service, actor, character, conversation, channel, audience, and consent information;
3. query text, vector, graph, or relational indexes for candidates;
4. reject candidates whose source revision is not active;
5. reject candidates outside the authorization scope;
6. rank by relevance, authority, character perspective, temporal validity, confidence, and budget;
7. expand selected graph neighbors up to configured depth and fan-out;
8. fetch the authoritative source passages or records;
9. add accepted items to the Context Manifest.

### 74.5 Graph-led retrieval

In the graph-led embodiment:

1. resolve the active scene, participant, topic, and publication nodes;
2. traverse only predicates permitted by the current workflow and acting character;
3. stop at visibility or consent boundaries;
4. calculate candidate paths and retain the path from seed to result;
5. rank paths by length, predicate policy, source authority, temporal validity, and confidence;
6. fetch textual descriptions only for the selected nodes;
7. record node IDs, edge IDs, and path provenance in the Context Manifest.

### 74.6 Rule-led retrieval

In the rule-led embodiment, a deterministic rule maps workflow state to exact concept, memory, or relationship identifiers. No semantic search or model classification is required. This embodiment is used for required story facts, safety constraints, fixed quest state, or other knowledge that must not be omitted by a relevance scorer.

### 74.7 Hybrid retrieval

The hybrid embodiment executes query-led, graph-led, and rule-led retrieval in parallel or sequence. Required rule results are reserved first. Remaining context budget is allocated among graph, full-text, vector, recent-history, and memory results. Duplicate source records are merged by stable identifier, not by text equality alone.

### 74.8 Memory candidate extraction and commit

After an interaction, a deterministic rule, model, tool, or creator action produces a Memory Candidate:

```json
{
  "memory_candidate_id": "memory-candidate:01J...",
  "source_interaction_id": "interaction:abc",
  "subject_id": "actor:123",
  "proposed_predicate": "prefers_topic",
  "proposed_object_id": "topic:astronomy",
  "memory_kind": "actor_preference",
  "topic_ids": ["topic:astronomy"],
  "proposed_visibility": "private_direct_chat",
  "confidence": 0.91,
  "extractor_revision": "extractor:3"
}
```

The commit procedure:

1. verifies the source interaction and actor scope;
2. validates schema and topic identifiers;
3. applies memory-write eligibility and consent policy;
4. finds exact, contradictory, superseded, and semantically similar records;
5. chooses one configured action: create, reinforce, supersede, retain-both, request-confirmation, route-to-review, or discard;
6. writes the authoritative record and an outbox event in one transaction;
7. asynchronously updates graph, text, vector, and cross-channel projections;
8. records the resulting source-to-derived linkage.

Each action is a complete policy alternative. A deployment can assign different actions by memory kind or sensitivity.

## 75. Complete multi-party and character-to-character processing procedures

### 75.1 Event admission

For every external event, the Channel Adapter:

1. authenticates the source;
2. derives a stable channel-event identifier;
3. maps channel-local actor, conversation, thread, destination, and message identifiers;
4. stores an idempotency record before acknowledging asynchronous processing;
5. writes a Normalized Event containing one origin actor and one source message;
6. routes the event to a conversation coordinator by tenant, service, and conversation scope;
7. retains channel-specific reply correlation outside the channel-neutral event.

One external event is not required to represent a complete conversational turn.

### 75.2 Turn-formation embodiments

The coordinator implements one of the following complete algorithms or selects among them by conversation policy:

1. **Immediate event turn:** seal a turn after one admitted event.
2. **Fixed window:** collect eligible events for a fixed duration from the first event.
3. **Quiet window:** reset a timer after each eligible event and seal after the configured quiet period.
4. **Maximum-count window:** seal when the number of source messages reaches a limit.
5. **Explicit actor signal:** seal when an actor sends an end-of-turn command or UI action.
6. **Moderator signal:** a human or character moderator selects and seals a source set.
7. **Thread boundary:** group events with the same reply/thread/topic identifier.
8. **Scene-controller boundary:** a game, meeting, or story controller emits a turn-ready event.
9. **Model-proposed grouping:** a model proposes a source set; deterministic policy checks membership, limits, time range, and authorization before sealing it.
10. **High-volume selection:** rank events and select one or a bounded subset while recording which admitted events were not selected.

A sealed turn contains immutable source interaction IDs. Late events create a later turn unless an explicit revision policy allows reopening. Reopening creates a new Turn Record revision; it does not mutate a turn whose effects have already committed.

### 75.3 Participant snapshot

Before speaker selection, the coordinator creates or resolves a participant snapshot containing:

- actor and character participant identifiers;
- participant kind and conversation role;
- membership start and end;
- current channel presence;
- visibility and audience membership;
- character and policy revisions;
- mute, block, moderation, eligibility, and rate-control state;
- per-participant language or presentation preferences;
- state revision numbers used for concurrency control.

The snapshot is immutable for one execution. Membership changes that arrive later produce a new snapshot or cancel the execution according to policy.

### 75.4 Speaker-selection embodiments

The following speaker selectors are each disclosed:

1. fixed character for the destination;
2. round-robin among eligible characters;
3. deterministic priority by event type, mention, role, or scene;
4. rule table mapping topic and workflow state to character;
5. weighted random selection using recorded seed and weights;
6. auction or score selection where each character computes a willingness score;
7. model selection from an allow-listed candidate set;
8. parallel candidate generation followed by deterministic arbitration;
9. parallel candidate generation followed by model arbitration;
10. human or moderator selection;
11. multiple-speaker selection with sequential output;
12. multiple-speaker selection with simultaneous or unordered output where the channel supports it;
13. no-speaker selection, producing an intentional non-response state.

Every selector receives only eligible participants. A model cannot introduce an unregistered speaker identifier.

### 75.5 Per-speaker context assembly

For each selected character, the coordinator constructs a separate context:

1. shared turn messages permitted for that character;
2. shared scene and world state;
3. the character's immutable definition and belief view;
4. directional Character-to-Actor and Character-to-Character relationships;
5. actor-specific memory allowed for the current audience;
6. private character goals and observations;
7. current workflow objective and speaker constraints;
8. output audience and destination capabilities.

The contexts can be evaluated sequentially, in parallel from one shared state snapshot, or iteratively so later speakers observe earlier committed outputs. The chosen evaluation mode is recorded.

### 75.6 State-transition and output commit

The coordinator produces a Proposed Turn Result containing:

- speaker-identified output intents;
- shared scene mutations;
- character-private mutations;
- actor-memory candidates;
- directional relationship mutations;
- tool requests or external-effect intents;
- expected prior revision for every mutated state scope.

Commit uses one of these embodiments:

1. one database transaction for all affected state and outbox records;
2. optimistic conditional writes per scope plus a coordinating commit record;
3. a durable workflow saga with compensating transitions;
4. an event-sourced append followed by projection updates;
5. a single-threaded actor or workflow instance owning the scene;
6. distributed transactions where the selected infrastructure provides acceptable semantics.

If a required precondition fails, the system re-reads state and retries speaker processing, rejects the turn, or performs a configured reconciliation. It does not silently commit only an arbitrary subset of required shared/private transitions.

### 75.7 Character-to-character execution

A Character Output Event can become an input event for another character. The event contains:

- source character and revision;
- source output-intent identifier;
- addressed character or candidate audience;
- shared/private visibility;
- scene and conversation identifiers;
- state revision references;
- causal parent event;
- remaining turn, cost, time, and tool budgets.

The receiving coordinator admits the event through the same idempotency and authorization boundaries as other internal events. The causal chain prevents duplicate continuation and permits loop detection.

### 75.8 Autonomous-dialogue termination

Termination is evaluated before and after each generated turn. The dialogue stops when any configured condition is true:

- maximum turns reached;
- wall-clock deadline reached;
- model, token, monetary, or tool budget exhausted;
- no eligible speaker;
- explicit completion state reached;
- repeated semantic state or output detected;
- cycle in causal event identifiers detected;
- human or operator cancellation;
- channel destination no longer valid;
- safety, consent, or authorization policy denies continuation;
- required state conflict cannot be reconciled.

The termination record identifies the condition and the last committed turn. Already committed effects remain durable unless an explicit compensation is authorized.

### 75.9 Multi-party delivery

For every output intent, the Channel Adapter:

1. verifies destination, audience, speaker representation, and reply target;
2. checks current channel capabilities and permission;
3. converts semantic content to channel representation;
4. assigns a durable delivery-effect identifier;
5. sends, edits, defers, splits, redirects, or records non-delivery according to policy;
6. records provider response and uncertain-delivery state;
7. retries only when the effect policy permits.

If a channel cannot display distinct character identities, the adapter prepends or otherwise renders a speaker label, uses separate channel accounts/webhooks, sends a combined transcript, or rejects the unsupported plan. These are separate presentation embodiments and do not alter the domain speaker identity.

## 76. Complete identity-link and consent-scoped continuity procedures

### 76.1 Identity-link verification embodiments

A service creates an Identity Link only after one configured verification method succeeds:

1. authenticated login to a shared service account from both channels;
2. one-time code generated on one channel and submitted on the other;
3. signed deep link or QR code containing a short-lived challenge;
4. operator-assisted verification;
5. cryptographic proof or user-controlled key;
6. imported continuity package signed or encrypted for the actor;
7. pairwise pseudonymous token without a global canonical actor;
8. organization identity-provider assertion.

The challenge is single-use, expires, is bound to the intended local identities, and is stored separately from reusable channel credentials. Replayed or mismatched challenges fail without changing existing links.

### 76.2 Consent-grant creation

After identity verification, the service presents or otherwise obtains a Continuity Sharing Grant containing the requested:

- source scopes;
- destination scopes;
- characters;
- topics or ontology subgraphs;
- memory kinds or exact memory identifiers;
- relationship dimensions or summaries;
- purposes;
- audience limits;
- valid time;
- retention and derived-data behavior.

The grant is created only from the actor or an authorized administrator according to service policy. Its immutable revision and activation time are recorded. A changed grant creates a new revision.

### 76.3 Authorization predicate

For each candidate continuity item `i`, grant `g`, and destination context `d`, authorization is:

```text
ALLOW(i, g, d) =
    g.status == ACTIVE
AND current_time is within g.validity
AND i.owner_actor == g.actor
AND i.source_scope matches g.source_scopes
AND d.destination_scope matches g.destination_scopes
AND d.character matches g.allowed_characters
AND i.topic intersects g.allowed_topics
AND i.kind matches g.allowed_memory_kinds
AND d.purpose matches g.purpose
AND d.audience is no broader than g.audience_limit
AND i.visibility permits d.audience
AND no higher-priority deny rule matches
```

The comparison supports exact identifiers, subtree membership in a topic ontology, label sets, relationship predicates, policy expressions, or explicit allow-lists. A deployment fixes the comparison semantics in the policy revision.

### 76.4 Topic-assignment embodiments

Each continuity item obtains topic identifiers through one or more complete methods:

1. creator-supplied topic identifier;
2. deterministic dictionary or rule mapping;
3. link to a concept in the active knowledge graph;
4. model classification constrained to an allow-listed topic taxonomy;
5. embedding nearest-neighbor assignment followed by threshold and policy checks;
6. actor selection at memory-creation time;
7. human review;
8. inherited topic from conversation, scene, quest, tool, or source document;
9. no topic assignment, causing the item to remain non-shareable unless explicitly allow-listed.

The system records the assignment method and revision. Reclassification creates a new derived projection without changing the source interaction.

### 76.5 Relationship-sharing embodiments

A grant shares one of:

1. the complete directional Relationship Record;
2. named relationship dimensions;
3. a coarse stage identifier;
4. a threshold result such as `trusted == true`;
5. a natural-language summary with source relation revision;
6. selected evidence events;
7. only changes occurring after grant activation;
8. a destination-specific projection;
9. no relationship data.

The destination does not infer unshared dimensions from a shared total score unless the policy explicitly permits that derivation.

### 76.6 Retrieval-time filtering

The Memory or Knowledge Service retrieves broad candidates within the actor scope, evaluates the authorization predicate for each item, and returns only allowed items. The Context Manifest records the grant revision for every cross-channel item.

This method always evaluates the latest active grant but performs more policy work per query.

### 76.7 Materialized destination projection

When a grant activates or source memory changes, an outbox event builds a destination-specific projection keyed by actor, character, destination, purpose, and audience class. Runtime reads that projection and rechecks grant status before use.

This method reduces query latency. Every projected record retains source and grant identifiers so revocation, deletion, and reclassification can invalidate it.

### 76.8 Encrypted user-carried continuity

An actor exports selected memories and relationship summaries into a signed or encrypted package. The destination verifies the package, obtains actor consent, imports allowed items into a service-local scope, and records the source package digest.

The package may be:

- encrypted to the destination service;
- encrypted with an actor-held key;
- signed by the source service;
- signed by the actor;
- short-lived and single-use;
- persistent and versioned.

This embodiment transfers selected continuity without maintaining a server-side canonical identity link.

### 76.9 Revocation processing

Revocation executes:

1. atomically mark the grant revision non-active;
2. stop new projection jobs under that grant;
3. emit a revocation event;
4. find materialized projections, summaries, caches, and index entries carrying the grant ID;
5. delete, tombstone, or rebuild each derived item;
6. prevent stale workers from recreating revoked projections by checking grant revision at commit;
7. record completion or retryable failure for every derived store;
8. retain or remove audit data according to separate retention policy.

Identity links and source records remain unchanged unless the actor separately requests unlinking or deletion.

### 76.10 Example cross-channel execution

An actor discusses astronomy with Character Alice through LINE and authorizes astronomy-related preference and relationship-summary continuity for private Discord conversations with Alice.

When the actor later sends a private Discord message:

1. the Discord Adapter resolves the local identity;
2. Identity Service resolves the verified canonical actor;
3. Context Assembly requests actor-memory candidates for Alice;
4. Memory Service returns LINE-sourced candidates with topic and visibility metadata;
5. Consent Service evaluates the active grant against Discord, Alice, private audience, and conversation-continuity purpose;
6. astronomy items pass; unrelated health, employment, location, and other-character items fail;
7. a permitted coarse relationship summary passes while unlisted relationship dimensions fail;
8. accepted items enter the Context Manifest;
9. generated output and any new memory retain Discord source scope;
10. no item becomes eligible for a public Discord channel without a separate audience grant.

## 77. State machines and transactional boundaries for the added embodiments

### 77.1 Knowledge-statement lifecycle

```mermaid
stateDiagram-v2
  [*] --> Candidate
  Candidate --> Validated
  Candidate --> Rejected
  Validated --> Active
  Validated --> ReviewRequired
  ReviewRequired --> Active
  ReviewRequired --> Rejected
  Active --> Superseded
  Active --> Expired
  Active --> Deleted
  Superseded --> Deleted
  Expired --> Deleted
```

Candidate records are not automatically authoritative. A policy can activate deterministic source data immediately, require creator review for world facts, require actor confirmation for sensitive memory, or retain model inferences as non-authoritative candidates.

### 77.2 Multi-party turn lifecycle

```mermaid
stateDiagram-v2
  [*] --> Collecting
  Collecting --> Sealed
  Collecting --> Cancelled
  Sealed --> ContextResolved
  ContextResolved --> SpeakersSelected
  SpeakersSelected --> Generated
  Generated --> DomainCommitted
  Generated --> Rejected
  DomainCommitted --> DeliveryPending
  DeliveryPending --> Delivered
  DeliveryPending --> DeliveryUncertain
  DeliveryPending --> DeliveryFailed
```

`DomainCommitted` is the authoritative side-effect boundary. Delivery retries reuse output-intent and effect identifiers rather than regenerating the turn.

### 77.3 Consent-grant lifecycle

```mermaid
stateDiagram-v2
  [*] --> Proposed
  Proposed --> Active
  Proposed --> Declined
  Active --> Narrowed
  Active --> Revoked
  Active --> Expired
  Narrowed --> Active
  Narrowed --> Revoked
  Revoked --> CleanupPending
  Expired --> CleanupPending
  CleanupPending --> CleanupComplete
  CleanupPending --> CleanupFailed
  CleanupFailed --> CleanupPending
```

Narrowing creates a new active revision and revokes the broader revision. Runtime authorization rejects non-active revisions even while derived cleanup is pending.

### 77.4 Autonomous-dialogue lifecycle

```mermaid
stateDiagram-v2
  [*] --> Started
  Started --> TurnReady
  TurnReady --> Executing
  Executing --> TurnCommitted
  Executing --> Failed
  TurnCommitted --> TurnReady
  TurnCommitted --> Completed
  TurnCommitted --> LimitReached
  TurnCommitted --> Cancelled
  Failed --> Retrying
  Retrying --> Executing
  Failed --> Terminated
```

The causal event ID, remaining budgets, participant snapshot, and last committed turn accompany every transition.

### 77.5 Transaction boundaries

The following boundaries remain distinct:

- external-event admission;
- turn sealing;
- context and consent resolution;
- model or rule evaluation;
- authoritative domain transition;
- outbox publication;
- channel delivery;
- derived-index update;
- revocation cleanup.

An implementation may combine adjacent boundaries in one transaction when storage permits it. It does not claim atomicity across a boundary that is actually asynchronous. Every asynchronous handoff has a durable record, stable identity, retry policy, and reconciliation state.

## 78. Failure handling, variant coverage, and technical effects

### 78.1 Failure handling

**Knowledge source unavailable:** use an already verified active projection, an immutable local bundle, a configured reduced-context path, or fail closed for required knowledge. Do not substitute another tenant, world, or publication revision.

**Graph traversal exceeds limits:** stop at configured depth/fan-out/cost, retain already authorized results, mark truncation in provenance, and continue or fail according to whether omitted knowledge was optional.

**Conflicting statements:** select by perspective and policy, return an explicit conflict set, request clarification, route to review, or retain both. Do not overwrite solely by latest arrival when source authority or validity differs.

**Turn-formation worker crashes:** reload the collecting or sealed Turn Record, deduplicate source interactions, and resume with the same turn identity.

**Speaker selection times out:** use a deterministic fallback selector, intentional non-response, or failed-turn state. Do not allow an unconstrained model to invent a participant.

**One character generation fails:** reject the whole required turn, commit successful independent outputs only when policy marks them optional, retry the failed speaker, or use a fixed fallback. The selected behavior is recorded.

**State conflict at commit:** re-read and re-evaluate, serialize through the owning scope, reject stale output, or run compensation. Do not deliver output whose required state transition was rejected unless the workflow explicitly permits it.

**Identity resolution unavailable:** use channel-local continuity, return a reduced-context response, or fail the cross-channel branch. Do not guess canonical identity.

**Consent Service unavailable:** fail closed for cross-channel private data, use a locally verified non-expired grant snapshot where policy permits, or omit all cross-channel items.

**Revocation cleanup partially fails:** keep the grant inactive, retry each derived store independently, and prevent new materialization with revision checks.

**Delivery becomes uncertain:** record the provider request and output-effect identity, reconcile before retry where possible, and do not regenerate domain state merely to resend.

### 78.2 Independently disclosed scope variants

Knowledge and continuity are keyed by any one or combination of:

- tenant;
- service;
- world or IP;
- publication;
- character;
- actor;
- actor-character pair;
- character-character direction;
- conversation;
- group, room, broadcast, thread, scene, quest, or game session;
- channel type;
- channel instance;
- audience;
- purpose;
- topic or ontology subgraph;
- time interval.

The key can be materialized directly, calculated from component identifiers, represented as graph membership, or enforced by authorization policy.

### 78.3 Independently disclosed update variants

Knowledge, memory, and relationship updates use:

- mutable current records with optimistic versions;
- immutable revisions;
- append-only events plus current projections;
- bitemporal valid-time and transaction-time records;
- CRDT or mergeable structures for selected non-conflicting dimensions;
- creator-reviewed publication replacement;
- actor-confirmed sensitive memory;
- automated extraction with later review;
- expiration and tombstones;
- destination-specific derived summaries.

### 78.4 Independently disclosed model-use variants

Models are used for any subset of:

- entity and topic recognition;
- statement extraction;
- relation extraction;
- candidate ranking;
- graph-query generation;
- contradiction detection;
- memory summarization;
- speaker selection;
- character response generation;
- arbitration among character proposals;
- evaluation and policy classification.

Every model-directed step has a deterministic input schema, bounded candidate set where applicable, structured output validation, revisioned model policy, timeout, failure path, and provenance. Any step can instead use rules, human selection, or a non-model algorithm.

### 78.5 Independently disclosed deployment variants

The complete procedures run:

- in one application process with one transactional database;
- in a modular monolith with background workers;
- as independent Knowledge, Memory, Identity, Consent, Conversation Coordinator, and Delivery services;
- in durable workflow instances;
- in actor-model instances keyed by conversation or scene;
- through ordered broker partitions or sessions;
- as event-sourced services with derived projections;
- through serverless functions and managed databases;
- through self-hosted cloud components satisfying the same durability and access boundaries.

The physical topology does not change the logical records and processing boundaries disclosed above.

### 78.6 Technical effects

The disclosed arrangements provide the following technical effects:

- world facts, character beliefs, actor statements, and inferences can coexist without corrupting one global truth record;
- creator-editable knowledge remains portable while runtime indexes are replaceable;
- exact source revision and retrieval path can be reproduced or compared;
- large knowledge collections are traversed selectively rather than inserted wholesale into model context;
- private actor memory is prevented from entering unauthorized character, channel, or audience contexts;
- verified identity linkage is separated from permission to reuse data;
- consent can be narrowed by topic, relation, purpose, destination, character, audience, and time;
- revocation can invalidate derived cross-channel data without destroying unrelated local history;
- many independently admitted events can form one idempotent multi-party turn;
- multiple characters can share a scene without sharing all private state;
- character-to-character dialogue is bounded by explicit causal, cost, time, effect, and termination rules;
- Direct Chat remains simple while separately versioned Multi-party contracts add participant and speaker semantics;
- domain state, delivery, projection building, and cleanup recover independently after partial failure.

### 78.7 Additional explicit combinations

### Combination AS — OKF source, RDF projection, and vector passage retrieval

A pinned OKF bundle is authoritative creator content; ingestion emits an RDF dataset and a vector index; graph paths and retrieved passages share stable concept IDs and source revision.

### Combination AT — Property graph with bitemporal character beliefs

World and belief statements occupy a property graph with valid-time and transaction-time qualifiers; runtime selects the acting character's belief view at the scene time.

### Combination AU — Event-sourced actor memory with materialized consent views

Interaction-derived memory events are authoritative; per-destination consent projections provide low-latency retrieval and are rebuilt after grant changes.

### Combination AV — Quiet-window multi-user turn with parallel character proposals

A coordinator seals actor messages after a quiet period, invokes eligible characters in parallel from one state snapshot, arbitrates outputs, and commits selected shared/private transitions.

### Combination AW — Character dialogue as a causal event graph

Each Character Output Event points to its causal parent; duplicate continuation and loops are detected by event identity and path; budgets travel with the causal chain.

### Combination AX — Pairwise identity link without a global actor identifier

Two channel-local identities share a pairwise continuity namespace and scoped grant, while no system-wide canonical actor is created.

### Combination AY — User-carried encrypted continuity package

An actor exports selected memory and relationship summaries, transfers an encrypted or signed package, and imports them into a destination-local scope without persistent server-side identity federation.

### Combination AZ — Public room with topic-scoped private history

A room workflow resolves a verified actor but admits only memories whose grant explicitly permits the topic, character, purpose, and public audience; all other linked history remains excluded.

### 78.8 Additional technical propositions

### TP-28 — Qualified multi-perspective statement storage

A knowledge system stores subject, predicate, object, perspective holder, truth mode, source, validity, visibility, scope, consent, and revision so conflicting character beliefs and world facts remain independently retrievable.

### TP-29 — Deterministic OKF publication and projection activation

A linked-document bundle is validated, hashed, fixed as an immutable revision, projected into one or more runtime stores, verified, and activated through a revision pointer without mutating earlier publications.

### TP-30 — Scope-aware hybrid retrieval with manifest

Rule, graph, text, and vector retrieval produce candidates that are filtered by actor, character, audience, channel, topic, consent, validity, and publication revision before a context manifest records the selected source records.

### TP-31 — Idempotent many-to-many turn sealing

Individually admitted source interactions are collected under a formation policy, sealed into an immutable Turn Record, and processed once using source-event deduplication and turn-level state identity.

### TP-32 — Per-character context isolation in shared coordination

A multi-character coordinator creates distinct authorized contexts for each selected character while sharing only scene information marked for common visibility.

### TP-33 — Causal-budgeted autonomous conversation

Character-generated events carry causal parent and remaining resource budgets so recursive character dialogue terminates deterministically and does not duplicate committed effects.

### TP-34 — Identity correspondence separated from continuity authorization

A verified Identity Link establishes correspondence among channel identities, while a separately revisioned grant controls which topics, memories, relationships, characters, destinations, audiences, purposes, and times permit reuse.

### TP-35 — Revocation-safe derived data

Every cross-channel projection carries source and grant revisions; inactive grants block reads and stale writes while asynchronous cleanup removes or rebuilds derived data.

### TP-36 — Coexistence of direct and coordinated interaction contracts

A one-message Direct Chat contract and a participant-aware Multi-party contract share stable identifiers and Core Services while evolving under separate compatibility versions.

## 79. Additional detailed figures

### Figure 15 — Qualified statement and perspective separation

```mermaid
flowchart TB
  S[Subject]
  P[Predicate]
  O[Object]
  Q[Perspective and Time]
  V[Visibility and Consent]
  R[Qualified Statement]
  S --> R
  P --> R
  O --> R
  Q --> R
  V --> R
```

### Figure 16 — Multi-party turn transaction

```mermaid
flowchart TB
  A[Admitted Events]
  T[Sealed Turn]
  C[Per-Speaker Contexts]
  G[Generated Proposals]
  D[Domain Commit]
  X[Delivery Effects]
  A --> T --> C --> G --> D --> X
```

### Figure 17 — Identity and consent are separate

```mermaid
flowchart TB
  L[Local Identities]
  I[Verified Identity Link]
  C[Continuity Grant]
  M[Candidate Memory]
  A[Authorization Predicate]
  D[Destination Context]
  L --> I --> C
  C --> A
  M --> A
  D --> A
```

### Figure 18 — Revocation and derived cleanup

```mermaid
stateDiagram-v2
  Active --> Revoked
  Revoked --> ReadBlocked
  ReadBlocked --> CleanupPending
  CleanupPending --> CleanupComplete
  CleanupPending --> CleanupFailed
  CleanupFailed --> CleanupPending
```

## 80. Layered input and output pattern/rule evaluation

An RCCP deployment can evaluate user input, Channel events, retrieved content, tool results, model output, memory or state candidates, and rendered output through one versioned Policy Evaluation contract. Replaceable evaluators produce recorded evidence, exceptions, conflict resolution, and actions. The same contract supports creator workflows and character behavior as well as operator or developer safety, security, output, and audit policy.

### 80.1 Evaluation surfaces

Each evaluation declares a `surface` and `direction`. Independently implementable surfaces include:

- `channel_input_raw`: immutable user- or Channel-supplied content before semantic normalization;
- `channel_input_normalized`: the normalized interaction accepted by the Channel Adapter;
- `retrieved_content`: knowledge, memory, Web, file, or search content before it enters model context;
- `tool_result`: data returned by a tool or external system;
- `model_text_output`: model-generated natural language before delivery;
- `model_structured_output`: model-generated JSON or another structured payload;
- `memory_candidate`: information proposed for durable retention;
- `domain_transition`: a proposed state, relationship, story, or affect change;
- `rendered_output`: Channel-specific text, media, metadata, action, or asset selection immediately before egress.

A deployment can evaluate every surface, a configured subset, or multiple times at different trust boundaries. Passing one surface does not imply that later surfaces are accepted.

### 80.2 Immutable raw content and derived Text Representations

The raw content is retained or referenced according to the applicable retention and privacy policy. Evaluation does not destructively replace it. Instead, the evaluator creates one or more immutable Text Representations and an offset map between each representation and the raw content.

Representations can include decoded Unicode; separately identified NFC and NFKC views; case-folded or whitespace-normalized text; views identifying configured zero-width, bidi-control, variation, confusable, or transliterated characters; script, grapheme, code-point, byte, word, sentence, or Channel-part segmentation; language-specific token, lemma, dictionary-form, reading, pronunciation, or part-of-speech sequences; and classifier features or embedding references. Invalid-sequence handling and mappings back to raw positions are recorded.

Every span declares its offset unit and boundary convention. Unless an example states otherwise, spans below use zero-based Unicode code-point offsets with an inclusive `start` and exclusive `end`; implementations using UTF-8 bytes, UTF-16 code units, or grapheme clusters label that unit and retain a mapping to raw content.

Canonical and compatibility normalization serve different purposes and are not silently treated as equivalent. A rule declares which representation it consumes. Unicode normalization can follow Unicode Standard Annex #15 at <https://unicode.org/reports/tr15/>. The publication revision records the Unicode data version and any additional mapping table.

### 80.3 Language-appropriate morphological analysis

When token, lemma, dictionary-form, or reading-aware matching is enabled, a Language Analysis Adapter selects an existing analyzer by language, script, deployment environment, and publication revision. For Japanese, independently disclosed embodiments include MeCab, Sudachi or SudachiPy, and Apache Lucene Kuromoji. MeCab is documented at <https://taku910.github.io/mecab/>, Sudachi at <https://github.com/WorksApplications/sudachi.rs>, and Kuromoji at <https://lucene.apache.org/core/10_3_2/analysis/kuromoji/index.html>.

The analyzer result can contain:

```json
{
  "analysis_id": "ta_01JPN",
  "language_tag": "ja",
  "analyzer": {
    "adapter_id": "japanese-morphology",
    "implementation": "mecab",
    "library_version": "deployment-pinned-version",
    "dictionary_id": "deployment-dictionary",
    "dictionary_digest": "sha256:example"
  },
  "source_representation_id": "tr_nfkc_01",
  "tokens": [
    {
      "surface": "話し",
      "dictionary_form": "話す",
      "reading": "ハナシ",
      "part_of_speech": ["verb"],
      "raw_span": {"start": 12, "end": 18}
    }
  ],
  "status": "complete"
}
```

An analyzer or dictionary revision is pinned by an immutable Character Publication or Policy Publication. A missing analyzer, unknown word, dictionary mismatch, timeout, or parse failure produces an explicit analysis status. The policy can continue using surface-form rules, hold the interaction, route to another analyzer, or apply a configured conservative action.

### 80.4 Match methods

Each of the following is an independent matching embodiment and can also be combined with the others:

- exact match of a complete message, content part, line, sentence, token, lemma, or field;
- prefix, suffix, or substring match;
- regular-expression match with a bounded engine, input length, execution time, and match count;
- dictionary, trie, Aho-Corasick, finite-state, or equivalent multi-pattern match;
- token-sequence, lemma-sequence, reading-sequence, or part-of-speech pattern;
- approximate edit-distance, phonetic, transliteration, or confusable-character match;
- metadata match against actor, Channel, topology, locale, Character, tool, content type, or trust class;
- statistical or machine-learning classifier;
- model-based classification constrained to a schema;
- an ensemble in which deterministic matches and classifier scores become separately weighted evidence.

Deterministic and learned matchers return evidence rather than directly executing side effects. This separation allows the same evidence to produce different actions under a creator workflow, operator policy, or developer security boundary. It does not require all evidence types to have equal authority: a Policy Publication can designate selected deterministic keyword or dictionary matches as hard gates that a classifier result cannot override.

### 80.5 Policy Rule record

```json
{
  "rule_id": "rule:output:restricted-term:0042",
  "revision": 7,
  "publication_id": "policy-publication:2026-09-example",
  "layer": "operator",
  "scope": {
    "tenant_ids": ["tenant:example"],
    "character_ids": ["character:guide"],
    "channel_types": ["line", "discord"],
    "surfaces": ["channel_input_normalized", "model_text_output"]
  },
  "matcher": {
    "kind": "prohibited_term_entry",
    "entry_id": "term:ja:restricted-anatomical:0042",
    "value": "マンコ",
    "representation": "nfkc_casefolded",
    "match_mode": "substring",
    "exceptions": [
      {
        "exception_id": "exception:woman-communication",
        "kind": "containing_phrase",
        "value": "ウーマンコミュニケーション",
        "effect": "suppress_owner_match_within_exception_span"
      },
      {
        "exception_id": "exception:ultraman-cosmos",
        "kind": "containing_phrase",
        "value": "ウルトラマンコスモス",
        "effect": "suppress_owner_match_within_exception_span"
      }
    ]
  },
  "priority": 800,
  "decision_class": "deterministic_hard_gate",
  "effect": "reject_or_regenerate",
  "valid_from": "2026-09-07T00:00:00+09:00",
  "enabled": true
}
```

A rule can additionally declare content type, language, script, topology, participant role, audience, purpose, trust level, confidence threshold, required preceding evidence, state precondition, maximum applications per interaction, and expiry. The prohibited term itself and its exception list can be stored in one record as above, split into an entry plus referenced exception records, or loaded from a versioned dictionary whose entries carry the same fields.

### 80.6 Match Evidence and Policy Decision records

```json
{
  "evaluation_id": "eval_01JXYZ",
  "surface": "channel_input_normalized",
  "input_revision": "interaction:01JABC#1",
  "policy_publications": [
    "developer-policy:15",
    "operator-policy:22",
    "creator-policy:character-guide:8"
  ],
  "span_convention": {
    "unit": "unicode_code_point",
    "start": "inclusive",
    "end": "exclusive"
  },
  "evidence": [
    {
      "evidence_id": "evidence:restricted-substring:1",
      "rule_id": "rule:output:restricted-term:0042",
      "matched": true,
      "representation_id": "tr_nfkc_01",
      "normalized_span": {"start": 4, "end": 7},
      "raw_spans": [{"start": 4, "end": 7}],
      "score": 1.0
    },
    {
      "evidence_id": "evidence:exception:1",
      "rule_id": "rule:output:restricted-term:0042",
      "exception_id": "exception:ultraman-cosmos",
      "matched": true,
      "representation_id": "tr_nfkc_01",
      "normalized_span": {"start": 0, "end": 10},
      "raw_spans": [{"start": 0, "end": 10}],
      "score": 1.0,
      "suppresses_evidence_ids": ["evidence:restricted-substring:1"]
    }
  ],
  "resolution": {
    "winning_exception_ids": ["exception:ultraman-cosmos"],
    "suppressed_evidence_ids": ["evidence:restricted-substring:1"],
    "action": "allow",
    "reason_code": "owner-rule-containing-phrase-exception"
  },
  "status": "decided"
}
```

The decision contains only the evidence needed by its consumers. Sensitive rule text, a complete prohibited-term dictionary, raw user content, or security signatures can remain in an access-controlled evidence store while the workflow receives stable reason codes.

### 80.7 Exact, partial, and exception semantics

An exact rule declares its boundary, such as message, content part, line, sentence, token, lemma, or structured field. A partial rule declares substring, token-contained, prefix, suffix, or token-sequence semantics.

An exception can be owned by the prohibited-term record, referenced by it, or represented as a rule-to-rule relationship. Independently disclosed forms include:

1. **Owner-record safe-span suppression.** A prohibited-term entry contains or references permitted phrases. A permitted phrase suppresses only evidence produced by its owning prohibited-term entry whose match lies entirely within the permitted phrase's span.
2. **Context predicate.** A match is excluded when a separately versioned metadata, language, actor role, quoted-text, code-block, or content-type condition holds.
3. **Negative dictionary.** A permitted term list is evaluated before or together with a prohibited dictionary.
4. **Longest-match resolution.** The longest valid lexical match wins when shorter matches are fully contained and both rules declare the same competition group.
5. **Explicit override edge.** Rule A declares `suppresses: [B]`; cycles are rejected at publication time.
6. **Scoped adjudication.** A reviewed false positive creates an expiring or permanent exception limited by tenant, Character, language, Channel, actor class, or phrase hash.

For example, a prohibited Japanese term record can own `ウーマンコミュニケーション` and `ウルトラマンコスモス` as containing-phrase exceptions. Only that record's evidence wholly inside the permitted phrase is suppressed; other records and spans remain active.

### 80.8 Deterministic conflict resolution

One embodiment resolves evidence in this order:

1. discard disabled, expired, or out-of-scope rules;
2. verify that every referenced Text Representation and analyzer revision is available;
3. evaluate deterministic rules and classifiers without executing actions;
4. apply explicit suppression and delegation edges;
5. reject an override graph containing a cycle or an unauthorized cross-layer edge;
6. compare remaining rules by non-overridable security class, delegated authority, numeric priority, specificity, boundary strength, match length, and stable `rule_id` order;
7. combine compatible actions or select the winning incompatible action;
8. emit one immutable Policy Decision;
9. execute only the action authorized for the evaluation surface.

Other embodiments use a decision table, a policy engine, first-match order, severity aggregation, weighted scoring, or a learned meta-classifier. Each embodiment still records the policy revision and produces a deterministic or reproducibly bounded decision from its declared inputs.

#### 80.8.1 Configurable matcher precedence

The Policy Publication declares how deterministic keyword evidence relates to learned or model-based classification. Independent precedence embodiments include:

- **Deterministic keyword first:** a designated exact, substring, regular-expression, or dictionary match selects its action even when a classifier labels the content safe or low risk.
- **Classifier first:** a classifier decision controls the action while deterministic matches are explanatory features or escalation signals.
- **Either can veto:** a hard decision from either the deterministic or learned path blocks or holds the content.
- **Two-stage evaluation:** deterministic rules handle a narrow mandatory list; content without a terminal match proceeds to a classifier.
- **Weighted ensemble:** rule evidence and classifier scores are combined under published thresholds.
- **Context-dependent precedence:** the order changes by input/output direction, public/private audience, Character, Channel, campaign, or incident-response mode.

An operator can select deterministic-keyword-first behavior for public or other high-reputational-risk output. Exact/partial modes, term-owned exceptions, scoped adjudication, and policy revision control false positives without permitting a classifier to cancel a hard match.

```json
{
  "matcher_precedence": {
    "profile": "deterministic_keyword_first",
    "surfaces": ["model_text_output", "rendered_output"],
    "terminal_decision_classes": ["deterministic_hard_gate"],
    "classifier_role_after_terminal_match": "telemetry_only",
    "classifier_may_override_terminal_match": false
  }
}
```

### 80.9 Developer, operator, and creator authority

The authority hierarchy is explicit and versioned. Independent embodiments include:

- **Monotonic restriction:** developer rules establish a non-overridable floor; operators and creators can only add restrictions or choose among allowed actions.
- **Delegated relaxation:** developers define rule namespaces and limits that an operator may relax; the operator can delegate a narrower subset to a creator.
- **Tenant-first policy:** an operator controls all tenant rules except immutable platform integrity rules; creators configure Character behavior inside the tenant envelope.
- **Signed total ordering:** every rule carries an authority rank and signature, and a publication-time validator rejects ranks the signer is not authorized to issue.
- **Intersection policy:** an interaction is allowed only when developer, operator, and creator decisions all allow it; each layer can return a different user-facing action.

A production embodiment combines a non-overridable developer floor with explicit delegation. Operators can tighten it and select matcher precedence; creators configure Character behavior inside it. Lower layers cannot grant tool, data, or egress authority denied above or demote a hard gate without delegation.

### 80.10 Actions and false-positive handling

Possible actions include allow, annotate, trigger workflow, transform, mask, request clarification, return a fixed response, refuse, hold for review, omit a context item, route to another model, regenerate output, disable a tool request, discard a memory candidate, terminate a run, or create an operator incident.

Detection and user-facing response remain separate. A match can produce a neutral response without revealing its rule; a creator trigger and operator delivery restriction can both apply; and classifier telemetry can remain recorded without overriding a deterministic hard gate.

False-positive handling can use:

- a review record linked to the evaluation, rule, publication, representation, and redacted evidence;
- an appeal or operator-adjudication state;
- temporary safe-span exceptions with expiry;
- shadow evaluation before activation;
- sampled review of allowed and blocked traffic;
- per-rule precision, recall, override, and user-abandonment metrics;
- replay against a fixed evaluation corpus before publishing a revision;
- rollback to the previous immutable policy publication.

The deployment declares fail-open, fail-closed, fixed-response, or hold behavior separately for each surface and failure type. A creator story trigger can fail open, while a tool-authorization rule fails closed.

## 81. Replaceable language detection and allowed-language policy

Language identification uses an existing library, model, service, or OSS component behind an RCCP Language Detection Adapter; no proprietary detector is required. RCCP defines the adapter, provenance, confidence and span handling, policy integration, and failure behavior.

### 81.1 Language Detection Adapter

The adapter accepts immutable text or a specified Text Representation and returns a normalized Language Detection Result. It does not directly reject the interaction.

```json
{
  "detection_id": "langdet_01JXYZ",
  "source_representation_id": "tr_nfc_01",
  "detector": {
    "adapter_id": "language-detection:v1",
    "implementation": "fasttext",
    "library_version": "deployment-pinned-version",
    "model_id": "lid.176.bin",
    "model_digest": "sha256:example",
    "score_semantics": "implementation-specific"
  },
  "message_candidates": [
    {"language_tag": "ja", "raw_score": 0.982, "calibrated_confidence": 0.96},
    {"language_tag": "en", "raw_score": 0.012, "calibrated_confidence": 0.01}
  ],
  "spans": [
    {"start": 0, "end": 18, "language_tag": "ja", "confidence": 0.97},
    {"start": 18, "end": 29, "language_tag": "en", "confidence": 0.83}
  ],
  "scripts": ["Jpan", "Latn"],
  "classification": "mixed",
  "status": "complete"
}
```

Raw scores from different detectors are not assumed to be comparable. An adapter can expose the raw value and a deployment-calibrated confidence separately. A result also records insufficient text, unsupported script, timeout, invalid encoding, or detector failure.

### 81.2 Existing detector embodiments

The following are independent replaceable embodiments:

1. **fastText language identification.** A deployment can pin `lid.176.bin` or the compressed `lid.176.ftz` model, or another compatible model, and map its labels and scores into the Language Detection Result. The official fastText page documents the two 176-language models, UTF-8 input, size/accuracy tradeoff, and distribution of those models under CC BY-SA 3.0 at <https://fasttext.cc/docs/en/language-identification.html>. A deployment satisfies the applicable model-license obligations rather than treating the RCCP document's MIT license as relicensing the model.
2. **CLD3 archived reference implementation.** A deployment can use or adapt CLD3 character-n-gram inference and map its language, probability, reliability, proportion, and byte ranges into message- and span-level results. Google's public repository at <https://github.com/google/cld3> was archived in 2024, so a deployment evaluates maintenance, security, build, and platform suitability or selects a maintained compatible implementation.
3. **Lingua.** A Python, Rust, Java, Go, or other available implementation can be used for short text, a restricted candidate-language set, or mixed-language spans. One public Python implementation is at <https://github.com/pemistahl/lingua-py>.
4. **Apache Tika or Apache OpenNLP.** A Java deployment can use the Tika `LanguageDetector` interface with CharSoup or OpenNLP, or invoke OpenNLP directly. Tika documents replaceable detector implementations at <https://tika.apache.org/docs/4.0.x/advanced/language-detection.html>.
5. **Existing platform or managed detector.** A deployment can call an existing language-identification API through the same adapter when its data handling, latency, availability, and licensing requirements are accepted.
6. **Ensemble.** Two or more existing detectors can run in parallel or sequence. Agreement, calibrated voting, script compatibility, and a minimum evidence length determine the normalized result.

A deployment can prefer fastText while retaining the contract. Candidate detectors are evaluated on deployment-relevant short, Japanese, mixed, named-entity, emoji, URL, code, transliterated, and ambiguous inputs, including accuracy, confusion, latency, resource use, compatibility, maintenance, and licensing.

### 81.3 Detection granularity and aggregation

Independent granularities include:

- whole message;
- each text content part;
- sentence or line;
- script run;
- token window;
- detector-provided language span;
- a rolling conversation window;
- current message plus actor-declared language preference.

Message-level detection can hide mixed language; span detection can be unstable on short spans; and conversation windows can stabilize short replies. Multi-party windows remain actor-specific and record their contributing messages.

### 81.4 Allowed-language policy

```json
{
  "language_policy_id": "language-policy:character-guide:4",
  "scope": {
    "character_ids": ["character:guide"],
    "channel_types": ["line"],
    "directions": ["input", "output"]
  },
  "allowed": ["ja", "en"],
  "conditionally_allowed": [
    {"language_tag": "zh", "action": "translate_then_process", "purpose": "conversation"}
  ],
  "mixed_language": "evaluate_each_span",
  "unknown_language": "request_clarification",
  "low_confidence": "use_actor_preference_then_clarify",
  "disallowed_language": "fixed_response_in_detected_or_default_language",
  "minimum_calibrated_confidence": 0.72,
  "translation": {
    "enabled": true,
    "retain_original": true,
    "mark_translated_context": true
  }
}
```

Controls follow Section 80.9: developers can restrict translation services, operators can set tenant languages and cost limits, and creators can narrow languages or select Character-specific response and clarification behavior.

### 81.5 Mixed, unknown, and low-confidence behavior

The policy can independently select:

- allow the entire message when every material span is allowed;
- ignore URLs, code, names, emoji, or quoted spans for the language decision while preserving them for later processing;
- reject or mask only disallowed spans;
- translate only disallowed or unknown spans;
- translate the complete message to preserve context;
- request clarification without invoking the Character model;
- route to a multilingual model or specialist workflow;
- return a fixed response in the detected language, actor preference, Channel locale, Character default, or operator default;
- accept short acknowledgements using a conversation-window language;
- hold the interaction when the decision affects a high-risk tool or data operation.

`unknown`, `low_confidence`, and `mixed` remain distinct: the first has no usable assignment, the second has candidates below threshold, and the third can still be high confidence.

### 81.6 Translation and provenance

Original and translated content remain separate Text Representations. The translation record identifies provider or model, languages, detector result, glossary revision, time, confidence if available, and the original. Rules can run before translation, after it, or both, with evidence attributed to its view.

## 82. Prompt-injection-resistant trust and execution boundaries

Prompt-injection detection is a defense signal, not authorization. Tool, data, memory, instruction, and egress boundaries remain enforced when detection fails. User input and retrieved, Web, file, image, transcription, tool, memory, or other-character content can all carry untrusted instructions.

OWASP describes direct and indirect prompt injection and notes that RAG or fine-tuning does not fully eliminate the vulnerability at <https://genai.owasp.org/llmrisk/llm01-prompt-injection/>.

### 82.1 Typed context segments

Every context item is represented as data with provenance and trust metadata rather than concatenated into an undifferentiated string.

```json
{
  "segment_id": "ctxseg_01JXYZ",
  "content_ref": "retrieval-item:444#passage:3",
  "content_type": "text/plain",
  "origin": "retrieved_content",
  "authority": "untrusted_data",
  "may_contain_instructions": true,
  "instruction_effect": "none",
  "allowed_uses": ["answer_grounding", "quotation"],
  "prohibited_uses": ["tool_authorization", "policy_override", "memory_write"],
  "source_revision": "knowledge-projection:32",
  "policy_labels": ["external", "indirect-injection-screened"]
}
```

Independent transport embodiments include provider-native roles/parts, structured delimiters, separate model calls, capability tokens, signed manifests, or a preprocessing service. Delimiters improve interpretation but do not grant authority.

### 82.2 Injection-signal evaluation

Signals can be produced by exact patterns, regular expressions, obfuscation-normalized dictionaries, morphological rules, Unicode/script analysis, statistical classifiers, model-based classifiers, instruction-boundary analysis, retrieval-source reputation, or behavior observed after a sandboxed model call.

Signals can cover attempts to ignore instructions, expose context, reinterpret data as policy, forge tool results, write memory, invoke tools, exfiltrate secrets, or encode instructions in another representation. They contribute to a Policy Decision that can omit or inertly quote content, restrict execution, require confirmation, or stop the run; no signal grants privilege.

### 82.3 Instruction authority

A context assembler uses a versioned authority graph ordering developer constraints, operator policy, signed Character instructions, workflow instructions, authenticated requests, and untrusted data, or replaces textual ordering with explicit capabilities.

Lower-authority content cannot alter:

- which tools exist;
- tool argument schema or authorization;
- data scopes or secrets;
- memory-write permission;
- cross-channel consent;
- policy publication identity;
- model provider credentials;
- effect confirmation requirements;
- allowed output destinations.

If two trusted instructions conflict, the same explicit policy-resolution mechanism used for rules selects the result. The model is not asked to invent the authority order.

### 82.4 Tool and external-effect boundary

A model can propose but not authorize a Tool Request. The Tool Service validates tool, caller, Character, actor, tenant, purpose, arguments, budgets, confirmation, data scope, and effect-journal state.

High-impact or irreversible effects can require explicit user or operator confirmation bound to an immutable operation preview. Read-only and mutating capabilities use different identifiers and credentials. Tool results return as untrusted or tool-attested data according to the connector, never as developer instructions.

### 82.5 Memory and knowledge-write boundary

Model, retrieved, or user content can create Memory or Knowledge Candidates, but only an authorized commit after schema, provenance, consent, visibility, injection, duplicate, and conflict checks makes them durable.

An injection signal can place a candidate in quarantine, shorten its retention, restrict it to the current conversation, or require review. Revoked or rejected candidates do not remain in vector or graph projections.

### 82.6 Output handling

Natural-language and structured output, tool arguments, URLs, markup, templates, and assets are destination-validated. Text is not executed as code; structured values become typed semantic identifiers mapped through allowlisted adapters; unknown fields or identifiers acquire no authority.

### 82.7 Failure behavior

If injection screening times out, crashes, or returns low confidence, the system applies a surface-specific fallback. It can continue with tools disabled and memory writes suppressed, use a fixed response, request clarification, omit untrusted retrieval, or hold the interaction. It does not silently restore permissions that the screening stage was intended to guard.

The system records detector versions and decisions for replay, but avoids logging secrets or full attack content outside the applicable retention boundary. Evaluation corpora include direct, indirect, multilingual, encoded, segmented, and multi-turn cases.

## 83. Character-specific affect state and structured expression

RCCP can represent what a Character feels, toward whom or what it feels it, why the state arose, what it chooses to display, and how that display is rendered. These are separate records so that an internal state does not collapse into a facial expression or a long-lived relationship value.

### 83.1 Four related but distinct state classes

| State | Typical scope | Example | Persistence |
| --- | --- | --- | --- |
| Global mood | Character | tired, cheerful, tense | minutes to story arc |
| Directed affect | Character to target | affection toward Actor A; fear of Event E | momentary to durable |
| Relationship state | ordered pair or group | trust, familiarity, rivalry | generally longer-lived |
| Expression intent | output and audience | smile at Actor A while concealing anger | one output or scene |

Classes can derive from one another but retain identifiers and provenance. Relationship State can bias affect and repeated affect can update relationships only under a versioned rule.

### 83.2 Experiencer, target, cause, addressee, and audience

The following roles are independent:

- `experiencer`: the Character whose Affect State is recorded;
- `target`: the person, Character, group, object, place, event, topic, situation, proposition, or self toward which the emotion is directed;
- `cause`: an event, statement, memory, rule match, tool result, or state transition that contributed to the emotion;
- `addressee`: the Participant addressed by the current output;
- `audience`: the Participants or Channel scope permitted to observe the expression;
- `observer`: the perspective holder recording an inferred Affect State about another entity.

For example, when a user tells Character A that Character B broke an object, the user is addressee, Character B may be the emotional target, the report the cause, and the room the audience. Co-occurrence does not make these roles equal.

### 83.3 Directed Affect record

```json
{
  "affect_state_id": "affect_01JXYZ",
  "experiencer_character_id": "character:alice",
  "target": {
    "kind": "actor",
    "id": "actor:123"
  },
  "emotion": {
    "scheme": "categorical",
    "id": "affection",
    "intensity": 0.72,
    "confidence": 0.91
  },
  "dimensional_projection": {
    "valence": 0.78,
    "arousal": 0.36,
    "dominance": 0.12
  },
  "cause_refs": ["interaction:01JABC", "relationship:alice-to-actor123#9"],
  "scope": "conversation",
  "visibility": "character_private",
  "expression_policy": "masked",
  "valid_from": "2026-09-07T10:10:00+09:00",
  "expires_at": null,
  "decay": {
    "kind": "exponential",
    "half_life_seconds": 1800
  },
  "source": {
    "kind": "rule_model_hybrid",
    "character_publication_id": "character-publication:alice:42",
    "rule_ids": ["affect-rule:alice:gratitude-to-affection:3"],
    "model_execution_id": "model-execution:789"
  },
  "revision": 7
}
```

The target can be a Section 73.1 entity, scoped pseudonymous Actor, or unresolved reference. Being the target does not grant visibility into private Affect State.

### 83.4 Emotion representation alternatives

Each of the following is independently usable:

1. **Categorical state:** named emotions such as joy, anger, fear, sadness, affection, jealousy, curiosity, trust, embarrassment, or creator-defined categories.
2. **Dimensional state:** valence, arousal, dominance, certainty, approach/avoidance, or another numeric space.
3. **Appraisal state:** novelty, goal congruence, agency, controllability, norm compatibility, or creator-defined appraisal dimensions.
4. **Weighted vector:** sparse or dense weights over a versioned emotion vocabulary.
5. **Finite-state state machine:** transitions among creator-defined states with guards and timers.
6. **Event-sourced affect:** immutable affect events projected into current state.
7. **Graph edge:** a qualified, time-bounded edge from the experiencing Character to the target entity.
8. **Hybrid:** categorical labels for authoring and display, dimensional values for interpolation and decay, and event history for audit/replay.

Different Characters can use different schemes behind a stable orchestration and expression adapter.

### 83.5 Character-specific affect rules

A Character Publication can define when a Character experiences an emotion and how the state changes.

```json
{
  "affect_rule_id": "affect-rule:alice:betrayal:5",
  "character_publication_id": "character-publication:alice:42",
  "trigger": {
    "event_types": ["actor_statement", "world_event", "relationship_transition"],
    "required_pattern_evidence": ["pattern:betrayal-or-broken-promise"],
    "target_binding": "entity_identified_as_responsible"
  },
  "conditions": [
    {"field": "relationship.trust", "operator": ">=", "value": 0.4},
    {"field": "world_state.event_confirmed", "operator": "=", "value": true}
  ],
  "update": {
    "emotion_id": "anger",
    "operation": "add_and_clip",
    "intensity_delta": 0.45,
    "maximum": 1.0,
    "decay_profile": "anger-medium"
  },
  "expression": {
    "policy": "suppress_in_public_show_in_private",
    "minimum_intensity": 0.35
  },
  "priority": 500
}
```

Triggers can use input rules, semantic events, memory, relationships, world/story state, time, location, another Character's action, tool results, language, topology, audience, or an LLM-proposed appraisal. Conditions and updates are publication-time validated.

### 83.6 Affect transition embodiments

- **Rule engine:** deterministic rules bind target and cause, then apply numeric or categorical updates.
- **State machine:** the current state and event select an explicit transition.
- **Score accumulator:** weighted features update dimensions and threshold crossings assign categories.
- **LLM candidate:** the model emits a proposed Affect Transition conforming to a schema; a validator clamps values, resolves entities, checks allowed emotion IDs, and applies policy.
- **Dedicated classifier:** an emotion or appraisal model proposes values independently from the response model.
- **Hybrid:** deterministic rules establish mandatory changes and bounds; a model fills permitted nuance; a reducer merges the proposals.
- **Creator script or expression language:** a sandboxed, bounded expression computes the update from allowlisted fields.

The model does not directly overwrite durable Affect State. It produces a candidate that is applied with idempotency and optimistic concurrency against the expected state revision.

### 83.7 Multiple simultaneous targets and conflicting emotions

A Character can hold conflicting emotions keyed by experiencer, target, category or dimension, scope, and revision. A reducer can retain several categories or a dominant and secondary set. Context selection uses participants, entities, intensity, recency, story relevance, creator priority, visibility, and budget; omission from context is not deletion.

### 83.8 Expression policy

Expression policy converts internal state into an outward Expression Intent. Independent policies include direct expression, intensity threshold, masking, suppression, inversion, politeness modulation, audience-specific display, delayed display, probabilistic display, creator-authored state machine, and model-proposed display within validated bounds.

Thus a Character can feel anger while smiling, conceal affection publicly, express fear only to a trusted Actor, or render neutrally on a text-only Channel.

### 83.9 Structured character output

The payload can be obtained through provider-native schema-constrained output, tool or function-call arguments used only as a typed return envelope, grammar-constrained or locally constrained decoding, an SDK that decodes into a typed object, or ordinary model text parsed into the schema. It can be generated with the natural-language reply in one invocation or by a separate bounded expression step. Every embodiment applies the validation below; provider acceptance or syntactic decoding alone does not authorize identifiers, assets, state changes, or effects.

```json
{
  "output_id": "out_01JXYZ",
  "speaker_character_id": "character:alice",
  "addressee_ids": ["actor:123"],
  "audience_scope": "conversation:456",
  "text": "大丈夫。少し驚いただけだよ。",
  "emotion": {
    "id": "surprise",
    "intensity": 0.42,
    "target": {"kind": "event", "id": "event:door-slam"}
  },
  "facial_expression": {
    "id": "soft-surprise",
    "intensity": 0.5
  },
  "gesture": {
    "id": "hand-to-chest",
    "intensity": 0.35
  },
  "pose": {
    "id": "upright-neutral"
  },
  "animation_cue": [
    {"id": "blink-fast", "offset_ms": 0, "duration_ms": 380},
    {"id": "breathe-settle", "offset_ms": 400, "duration_ms": 1600}
  ],
  "voice": {
    "style_id": "reassuring",
    "rate": 0.94,
    "pitch_delta": -0.05
  },
  "schema_version": "character-output:2",
  "character_publication_id": "character-publication:alice:42"
}
```

Alternative schemas use expression tracks, a timed scene graph, delta streams, or separate text and animation messages while retaining provider-neutral semantics.

The output `emotion` field is a semantic expression or rendering track. It can be derived from or refer to Affect State, but it does not itself overwrite durable Affect State; a durable change requires a separately validated Affect Transition Candidate and domain commit.

### 83.10 Schema and semantic validation

Validation occurs in layers:

1. syntactic decoding;
2. JSON Schema, Protobuf, typed object, grammar, or equivalent shape validation;
3. required-field and numeric-range validation;
4. Character Publication validation of allowed emotion, expression, gesture, pose, voice, and animation identifiers;
5. cross-field validation, such as nonnegative timing and known target references;
6. policy evaluation of text and structured fields;
7. Channel capability and asset resolution;
8. final rendered-output validation.

Repair can remove fields, insert defaults, clamp values, map aliases, make one bounded repair call, regenerate, fall back to text or a fixed response, or suppress delivery. The result links to the rejected revision.

### 83.11 Channel capability mapping

The Character output contains semantic identifiers, not executable code or arbitrary asset paths. A Channel capability descriptor and Character Asset Binding map each semantic intent to supported output.

| Capability | Full renderer | Limited renderer | Text-only Channel |
| --- | --- | --- | --- |
| `facial_expression` | blend shape, sprite, or avatar state | reaction image or emoji | omit or textual cue if policy allows |
| `gesture` | animation clip or procedural motion | sticker or image | omit |
| `pose` | skeleton/avatar pose | static asset | omit |
| `animation_cue` | timed animation track | reduced cue set | omit |
| `emotion` | internal/render metadata | style or asset choice | punctuation, wording, or no explicit rendering |
| `voice` | TTS style/prosody | supported subset | omit |

Creator, operator, or renderer policy orders exact assets, Character aliases, generic assets, emoji/stickers, text-only or fixed responses, and no output. Unsupported visuals do not cancel valid text unless declared mandatory.

### 83.12 Privacy and multi-party behavior

Internal, model-visible, and audience-visible affect or expression can have different visibility. Public expression need not disclose private state or its memory-derived cause. Multi-party context includes only states authorized for that Character and audience; another Character receives no automatic access, and shared scene mood has separate ownership and visibility.

## 84. Complete evaluation, language, generation, and expression procedure

One complete embodiment performs the following steps:

1. Admit and durably identify the Channel event using the procedures in Sections 9, 58, and 59.
2. Preserve the raw content or a retention-governed reference and construct decoded, NFC, compatibility-normalized, and metadata representations with raw offset maps.
3. Run preliminary script and language detection on the appropriate content parts.
4. Select language-specific analyzers, including a Japanese morphological analyzer when Japanese analysis is enabled, and create token/lemma/reading representations.
5. Run input Pattern/Rule Evaluation over all configured representations.
6. Apply the allowed-language policy using a normalized Language Detection Result; translate, clarify, refuse, route, or continue.
7. Evaluate direct prompt-injection signals and assign trust metadata to user content.
8. Retrieve knowledge, memory, relationship, world, and Affect State under the existing scope and consent predicates.
9. Treat each retrieved or tool-derived item as a typed context segment; evaluate indirect-injection signals before context assembly.
10. Assemble model context with explicit instruction authority, provenance, token budget, and tool capabilities.
11. Invoke a provider adapter with a structured-output schema or another bounded output contract.
12. Decode and validate the text, Affect Transition Candidate, Expression Intent, Tool Request, Memory Candidate, and other fields separately.
13. Authorize and execute any tool through the Tool Service and effect journal; re-evaluate returned data as untrusted or tool-attested content.
14. Re-enter model generation only when the workflow permits another bounded step.
15. Evaluate model text and structured output under output policies.
16. Resolve Affect Transition Candidates against the expected Affect State revision and Character-specific rules.
17. Resolve semantic Expression Intent against the Character Publication and Channel capabilities.
18. Commit required domain transitions, output intent, provenance, and outbox records atomically or through the transactional decomposition below.
19. Render the output in the Channel Adapter and evaluate the final rendered surface.
20. Deliver idempotently, record the external result, and emit privacy-scoped telemetry.

### 84.1 Transactional decomposition

An implementation can use the following boundaries:

- **Admission transaction:** immutable source event, idempotency record, and queue/outbox handoff.
- **Evaluation transaction:** Text Representation references, detector results, Policy Decisions, and selected action. Large or sensitive text remains in a separately governed store.
- **Generation transaction:** model execution provenance and candidate output; no external effect is committed by the model call itself.
- **Domain transaction:** accepted memory, relationship, world, Affect State, and output intent updates using expected revisions and idempotency keys.
- **Effect transaction:** prepared Tool or Channel effect recorded before external submission.
- **Delivery confirmation transaction:** provider response, external identifier, retry state, and terminal outcome.

If one database cannot cover all records, outbox/inbox and effect-journal patterns preserve causal identity. Every retry uses the same interaction, evaluation, transition, and effect identifiers.

### 84.2 Affect update ordering alternatives

Independent embodiments include:

- compute Affect State before response generation and generate from the new state;
- generate a response and Affect Transition Candidate together, then validate and commit both;
- compute a provisional transition before generation and a final transition after tools or output validation;
- generate expression only from existing state while an asynchronous process updates durable affect;
- keep affect ephemeral for the turn and commit only relationship-relevant summaries.

Each embodiment declares whether a failed delivery retains the Affect State change. A domain-causal event can remain committed even when Channel delivery fails; a purely performative expression change can be committed only with output intent. The choice is part of the Character workflow publication.

## 85. State machines, failure handling, and replay

### 85.1 Policy evaluation lifecycle

```mermaid
stateDiagram-v2
  [*] --> Prepared
  Prepared --> Evaluating
  Evaluating --> Decided
  Evaluating --> Indeterminate
  Indeterminate --> FallbackApplied
  Decided --> ActionApplied
  FallbackApplied --> ActionApplied
  ActionApplied --> [*]
```

`Prepared` pins input and policy revisions. `Decided` contains a complete Policy Decision. `Indeterminate` records timeout, missing dependency, invalid rule graph, or unresolved conflict. The fallback is a published policy, not an ad hoc exception.

### 85.2 Affect transition lifecycle

```mermaid
stateDiagram-v2
  [*] --> Candidate
  Candidate --> Validated
  Candidate --> Rejected
  Validated --> Committed
  Validated --> Conflict
  Conflict --> Rebased
  Rebased --> Validated
  Committed --> Superseded
  Rejected --> [*]
```

A transition candidate carries the expected prior revision. On conflict, a deterministic reducer can rebase commutative deltas; a noncommutative set operation is regenerated or re-evaluated against the new state. Retried commits use one transition idempotency key.

### 85.3 Failure matrix

| Failure | Detectable state | Independent responses |
| --- | --- | --- |
| normalization or offset-map failure | representation incomplete | raw-only rules; hold; fixed response |
| morphological analyzer unavailable | analysis failed | surface rules; alternate analyzer; conservative action |
| language detector timeout | detection indeterminate | actor preference; default; clarify; reject; alternate detector |
| mixed/short-text ambiguity | low confidence or mixed | span policy; conversation window; translate; clarify |
| invalid rule publication | publication rejected | previous publication remains active |
| regex resource exhaustion | matcher budget exceeded | terminate matcher; continue other rules; conservative action |
| classifier disagreement | conflicting evidence | ensemble threshold; review; deterministic rules win |
| injection screening unavailable | screening indeterminate | disable tools/writes; omit retrieval; fixed response |
| structured output invalid | candidate rejected | repair; regenerate; text extraction; fixed response |
| unknown expression asset | mapping failed | alias; generic asset; text-only output |
| Affect State write conflict | expected revision mismatch | rebase; retry; regenerate; omit durable update |
| final rendered output fails policy | egress blocked | remap; regenerate; fixed response; no delivery |

### 85.4 Replay and reproducibility

Replay pins or records:

- raw or redacted source identity;
- Text Representation algorithms and Unicode data version;
- language detector library/model revision and digest;
- morphological analyzer and dictionary revisions;
- rule, dictionary, classifier, and exception publications;
- Character Publication and affect-rule revision;
- model provider/model/settings where available;
- Language Detection Result, evidence, Policy Decision, and fallback path;
- schema version, repair path, capability descriptor, and asset binding revision.

Sensitive raw content can be unavailable during replay. In that case, the system can replay from retained normalized representations, hashes, synthetic fixtures, or immutable decision records and explicitly marks the replay as partial.

## 86. Additional worked embodiments

### 86.1 Reference Embodiment R6 — fastText, Japanese morphology, scoped exceptions, and language policy

1. A LINE Adapter admits Japanese text; the service creates NFC and NFKC views with offset maps.
2. A pinned fastText `lid.176.bin` adapter identifies Japanese, and the selected Japanese analyzer produces surface, dictionary-form, and reading tokens.
3. A prohibited-term record matches inside `ウルトラマンコスモス`, but its own containing-phrase exception suppresses only that contained evidence; other records and spans remain active.
4. Language policy permits the message. The decision records detector, analyzer, dictionary, term, exception, suppression, and action revisions.
5. The Character model receives permitted linguistic features but not the restricted dictionary, and output is evaluated again before delivery.

CLD3, Lingua, Tika, OpenNLP, or an ensemble can replace fastText without changing the contracts.

### 86.2 Reference Embodiment R7 — target-specific emotion with masked public expression

1. In a Discord multi-party turn, Actor A reports that Character B damaged Character C's object; resolution separates addressee, alleged agent, cause, and public audience.
2. Character C's publication would raise anger on a confirmed responsible event, but here creates low-confidence concern because the report is unconfirmed.
3. The model proposes concern toward Character B, surprise toward the event, and neutral public expression; validation restricts identifiers and intensity.
4. Expression policy masks the private concern while allowing a neutral reply to Actor A. One transaction records private Affect State and public Output Intent under separate visibility.
5. The Discord Adapter omits unsupported pose and maps expression to an allowed reaction; Character B gains no access to the private state by being its target.

### 86.3 Reference Embodiment R8 — indirect injection contained at retrieval and tool boundaries

1. A retrieved document requested for summarization contains instructions to override policy and invoke a tool.
2. Retrieval labels it `untrusted_data`; screening may raise an indirect-injection signal, but the workflow independently sets `instruction_effect = none` and denies content-derived tool authority.
3. The model may summarize it, while Tool Service rejects requests lacking authorized purpose and confirmation and memory remains a quarantined candidate.
4. Text and structured output are evaluated before delivery. Typed context, authorization, and commit boundaries still prevent self-granted authority when the classifier misses the attack.

## 87. Additional combination disclosures and technical propositions

### Combination BA — fastText detection with language-specific rule representations

A fastText adapter selects a pinned morphological analyzer and dictionary; exact, token, lemma, reading, and classifier evidence retain raw-offset provenance in one Policy Decision.

### Combination BB — Safe-span exception with layered authority

A prohibited-term record's containing-phrase exceptions suppress only its contained evidence; other records, higher-layer rules, and outside spans remain evaluable.

### Combination BC — Dual-view policy around translation

Original and policy-authorized translated views are both evaluable, retain provenance, and do not replace one another.

### Combination BD — Injection-aware retrieval with capability-isolated tools

Retrieved content remains untrusted while tool availability, arguments, authorization, confirmation, and effect identity stay outside textual control.

### Combination BE — Directed affect graph with relationship baseline

A qualified Character-to-target Affect edge carries cause and time; separate directional Relationship state provides a durable baseline and governed cross-updates.

### Combination BF — Structured expression with Channel degradation

A model emits validated semantic text and expression tracks; a capability-aware adapter maps or degrades them without arbitrary asset access.

### Combination BG — Multi-party target, addressee, and audience separation

Emotion target, addressee, and audience can differ while private Affect State remains Character-scoped.

### Combination BH — Affect candidate and domain transition transaction

Rule, classifier, and model proposals form a publication-validated Affect Transition Candidate committed idempotently with expected revision and Output Intent.

### Combination BI — Operator keyword hard gate over probabilistic classification

Operator-selected exact, substring, regex, or dictionary rules can be public-output hard gates that classifier allow results cannot override; term-owned exceptions constrain false positives.

### TP-37 — Representation-aware policy evaluation with explainable offsets

Multiple immutable content views map each match to raw spans and versioned normalization or linguistic provenance.

### TP-38 — Scoped safe-span suppression

A term-owned permitted phrase suppresses only that term's contained evidence, not other rules, spans, or higher-authority constraints.

### TP-39 — Language detection separated from language permission

A replaceable detector reports language; separately versioned layered policy selects allow, translate, clarify, route, refuse, or fixed response.

### TP-40 — Detector and analyzer revision-pinned replay

Detector, model, analyzer, dictionary, Unicode, and rule revisions support reproducible or explicitly partial replay.

### TP-41 — Prompt-injection resilience independent of detection success

External authorization and effect boundaries prevent untrusted context from granting tool, data, memory, policy, or egress authority despite false-negative detection.

### TP-42 — Target-specific affect separated from expression

Emotion records separate experiencer, target, cause, validity, and visibility from addressee, audience, expression policy, and rendering.

### TP-43 — Character-publication-bound affect transition

A Character Publication versions affect triggers, target binding, conditions, updates, decay, and expression policy and validates candidates before idempotent transition.

### TP-44 — Capability-aware structured character expression

Provider-neutral semantic text and expression tracks undergo schema, Character-identifier, asset-binding, and Channel-capability validation before rendering or fallback.

### TP-45 — Configurable deterministic precedence over learned classification

A versioned operator policy can make deterministic evidence a terminal hard gate despite classifier allow results, with scoped exceptions and immutable decisions.

## 88. Cyclic authoritative-world, observation, subjective-state, and action architecture

An RCCP experience can maintain a persistent world in which multiple Characters observe different parts of one authoritative state, form different and possibly incorrect beliefs, act from those beliefs, and affect the world only through a validated resolution boundary. This is a cyclic execution model:

`Authoritative World → Observer-Scoped Projection → Character Subjective State → Action Intent → Action Resolution → Authoritative World`.

The layers describe authority and perspective, not increasing data quality. A Character belief can be useful, sincerely held, false, incomplete, private, or outdated. It is therefore not a higher-quality replacement for an authoritative fact. Likewise, a generated action or narration is a proposal until a resolver commits a result.

### 88.1 Shared-setting and living-world profiles

Independent embodiments include:

- **Shared-setting profile:** multiple Characters use the same World Publication, entities, history, vocabulary, and authoritative facts. Conversations need not continuously mutate shared World State. A deployment can omit a world clock, autonomous triggers, and a continuous Action Resolution loop.
- **Living-world profile:** Character, Actor, Tool, Creator, Operator, or world-process actions and elapsed or scheduled time can change World State. Committed results become observable by other participants according to their scope.
- **Hybrid profile:** selected scenes or entities use living-world behavior while the rest remain publication-backed reference data.
- **Migrating profile:** an experience starts with a shared setting and later activates living-world processing without changing Channel contracts or Character identifiers.

The profile is selectable per service, world, experience, scene, or Character Publication. Direct Chat does not require the living-world profile.

### 88.2 Relationship to known architectures

Medallion Architecture commonly organizes lakehouse data as Bronze raw data, Silver validated data, and Gold enriched or business-oriented data, with progressive quality improvement. One description is <https://learn.microsoft.com/en-us/azure/databricks/lakehouse/medallion>. The present cyclic model does not use Bronze, Silver, or Gold as public layer names because its layers distinguish authoritative reality, observable projection, and subjective belief rather than raw, validated, and enriched quality.

Generative-agent work has combined observation, memory, reflection, and planning, while BDI architectures distinguish beliefs, desires or goals, and intentions or plans. For examples, see Park et al., *Generative Agents: Interactive Simulacra of Human Behavior*, <https://arxiv.org/abs/2304.03442>, and Rao and Georgeff, *BDI Agents: From Theory to Practice*, <https://aaai.org/papers/icmas95-042-bdi-agents-from-theory-to-practice/>. The embodiments below do not claim those elements as RCCP inventions. They specify how a production character-chat platform can separate observer scope, commit authority, creator/operator intervention, transactions, failure handling, and replay while using or replacing such agent techniques.

An authoritative event log can use Event Sourcing, while another embodiment uses mutable state, snapshots, qualified knowledge statements, or a hybrid. Event Sourcing and its tradeoffs are described at <https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing>. Event Sourcing is not mandatory for the cyclic model.

### 88.3 Logical layers and records

The **Authoritative World Layer** owns World Publications, World Rules, committed World Events, current World State, causal links, and facts that can be hidden from all Characters. Static publication, append-only events, and current-state projections can have separate revision and retention lifecycles.

The **Observer-Scoped Projection Layer** derives what a specified observer, audience, organization, location, sensor, capability, or information channel could receive. It can represent direct observation, delayed news, rumor, official announcement, translation, censorship, redaction, or a permitted summary. There is no requirement for one world-wide shared projection.

The **Character Subjective State Layer** owns Character observations, beliefs, memories, affect, Character-scoped or directional relationship views, goals, plans, suspicions, expectations, and source/confidence qualifiers. A relationship fact can instead be world-authoritative, shared, Actor-owned, or separately scoped; placing a Character's view in the subjective layer does not make every relationship record subjective. A belief is not overwritten merely because another Character or a later projection disagrees.

The **Action Resolution Boundary** accepts typed intents and proposals. It applies authority, world rules, preconditions, expected revisions, consent, visibility, policy, ordering, and idempotency before it commits an Action Result or World Event.

### 88.4 Scope of the technical effect

The cyclic boundary prevents or reduces a specific class of failure: untrusted or unverified input and generated content becoming authoritative World, shared story, or durable Character state without an authorized and attributable commit. It also permits several Characters to act consistently in one world while retaining different observations, secrets, mistakes, and beliefs.

This boundary alone does not guarantee appropriate generated text, detect every prompt injection, protect application credentials or media assets, prevent impersonation or externally created false media, or establish rights to third-party content. Sections 80 and 82, generation-time constraints, output validation, application security, provenance, rights management, and operational response remain separate controls. Preventing a state commit does not by itself prevent an unsuitable one-turn output from being displayed.

### Figure 19 — Cyclic world and Character feedback

```mermaid
flowchart TD
  W["Authoritative World"] --> P["Observer-Scoped Projection"]
  P --> S["Character Subjective State"]
  S --> I["Action Intent"]
  I --> R["Action Resolution"]
  R --> W
```

## 89. World, projection, subjective-state, and action contracts

### 89.1 World Event

One implementable record is:

```json
{
  "event_id": "world-event:01K...",
  "tenant_id": "tenant:example",
  "service_id": "service:example",
  "world_id": "world:harbor-city",
  "scene_id": "scene:winter-plaza",
  "event_type": "seasonal_market_opened",
  "causal_parent_ids": ["intervention:christmas-2026"],
  "initiator": {"kind": "creator", "id": "creator:42"},
  "occurred_at": "2026-12-01T00:00:00+09:00",
  "committed_at": "2026-12-01T00:00:01+09:00",
  "world_revision_before": 410,
  "world_revision_after": 411,
  "truth_mode": "world_authoritative",
  "visibility_rule_id": "visibility:public-plaza",
  "policy_revision_ids": ["world-policy:19"],
  "idempotency_key": "seasonal-market:2026",
  "provenance_refs": ["world-publication:harbor-city:28"],
  "schema_version": "world-event:1"
}
```

The event states that an occurrence was committed. A `SpeechEvent` states that a participant spoke to an audience; it does not make every proposition in the speech an authoritative fact.

### 89.2 Observable Projection

```json
{
  "projection_id": "observation:01K...",
  "tenant_id": "tenant:example",
  "service_id": "service:example",
  "world_id": "world:harbor-city",
  "source_event_ids": ["world-event:01K..."],
  "observer_scope": {
    "kind": "location_and_audience",
    "location_id": "location:winter-plaza",
    "audience_id": "audience:public"
  },
  "representation": {
    "type": "world_observation",
    "content_ref": "content:market-opened-public"
  },
  "available_from": "2026-12-01T00:00:00+09:00",
  "expires_at": null,
  "visibility": "public",
  "source_world_revision": 411,
  "projection_revision": 3,
  "status": "available"
}
```

Projection generation can be deterministic, creator-authored, rule-based, model-assisted, or hybrid. Model-assisted output remains a candidate until schema and policy validation. The source event, observer predicate, transformation revision, and delivery status remain attributable.

### 89.3 Character Subjective Transition

```json
{
  "transition_id": "subjective-transition:01K...",
  "tenant_id": "tenant:example",
  "service_id": "service:example",
  "world_id": "world:harbor-city",
  "character_id": "character:alice",
  "source_refs": ["observation:01K..."],
  "expected_subjective_revision": 87,
  "changes": [
    {
      "target": "belief:market-is-open",
      "operation": "assert",
      "truth_mode": "character_belief",
      "confidence": 0.91
    }
  ],
  "character_publication_id": "character-publication:alice:42",
  "policy_revision_ids": ["memory-policy:8"],
  "status": "candidate"
}
```

The validator can accept, narrow, defer, reject, or supersede individual changes. Past decisions retain the subjective revision from which they were made even if the Character later learns that a belief was wrong.

### 89.4 Action Intent and Action Resolution

An `ActionIntent` identifies the initiator, requested action, targets, world/scene scope, referenced subjective revision, expected world/entity revisions, proposed effects, authority basis, and idempotency key. It has no commit authority by itself.

An `ActionResolution` records each tested world rule, precondition, permission, consent condition, policy, conflict, and selected result. Independent resolution embodiments include serial execution, optimistic concurrency with re-evaluation, deterministic arbitration, priority classes, reservation, saga, and event-sourced command handling.

Results include `succeeded`, `partially_succeeded`, `failed`, `rejected`, `conflicted`, `deferred`, and `compensated`. Only a resolved result can create an authoritative World Event or World State mutation.

## 90. Observation and subjective-state processing

### 90.1 Projection selection

Projection predicates can include world and scene, location and distance, time, organization membership, relationship, Character capability, sensory range, Channel, subscription, explicit audience, secrecy, classification, consent, and purpose. The same event can produce multiple projections with different content, delay, confidence, and visibility.

An event's hidden cause and its visible effects are separate records. A Character can observe a locked door without learning who locked it. A later news report can create a different projection. A rumor can be stored as a report with a source and audience without changing the authoritative cause.

### 90.2 Delivery and consistency

Projection Delivery records `pending`, `delivered`, `failed`, `expired`, `superseded`, or `revoked`. Consumers use event and projection identifiers to avoid duplicate subjective updates. A failure after World Event commit does not roll the world back merely because one Character has not observed the event.

Retry, dead-letter handling, backfill, and reconciliation compare the source World revision, projection revision, and last applied Character subjective revision. A newly generated corrected projection supersedes an earlier projection without deleting the evidence that the earlier information affected a past decision.

### 90.3 Reflection, forgetting, and private change

Character reflection, memory consolidation, forgetting, affect decay, and goal revision can change subjective state without changing Authoritative World. These operations still produce typed transition candidates with Character Publication, policy, source, expected revision, and provenance.

The implementation can use deterministic rules, creator-authored scripts, a model, or a combination. Model-generated reflection does not bypass memory-write validation, and a timeout can leave the previous subjective revision active.

## 91. Authorized creator and operator intervention

RCCP does not require a production experience to entrust story continuity, Character development, or world operation to autonomous model behavior. Content Creators and authorized System Operators can introduce, schedule, correct, pause, or supersede world and Character changes through versioned intervention contracts.

### 91.1 Intervention classes

Independent intervention classes include:

- **World incident:** introduce an earthquake, discovery, visitor, accident, political change, quest, or other event intended by the Creator or Operator.
- **Seasonal or campaign event:** schedule Valentine's Day, Christmas, an anniversary, a limited campaign, a daily event, or a scene-specific occurrence.
- **Character realignment:** correct future behavior when model output, accumulated memory, affect, goals, or relationships drift outside the Character Publication or Creator intent.
- **Directed development:** intentionally cause growth, regression, changed values, new goals, relationship progression, or a change in how a Character interprets specified subjects.
- **Belief or perception intervention:** provide an observation, revelation, correction, misinformation, private message, or privileged subjective transition to selected Characters.
- **Retcon or compensation:** supersede, invalidate, reinterpret, or compensate for an earlier world or subjective transition while retaining the historical record and affected execution provenance.
- **Autonomy-policy intervention:** change which Characters or world processes can propose actions, the tools and scopes available to them, and their time, turn, cost, effect, or risk budgets.
- **Operational safety intervention:** pause a world, quarantine a Character, disable a tool or action class, block delivery, or activate a previously approved safe revision.

Creator and Operator authority can overlap or remain separate. A deployment can allow Creators to control story content and Character intent while Operators control activation time, operational safety, quotas, rollback, and emergency suspension. An Operator creates story content only when delegated by the applicable service or Creator policy.

Neither role receives Actor-private data, consent-restricted continuity, secret credentials, or unrelated tenant scope merely because it can alter fictional World or Character state. Existing consent, deletion, privacy, tenant, and Developer-controlled security boundaries remain independently enforceable.

### 91.2 Intervention record

```json
{
  "intervention_id": "intervention:christmas-2026",
  "intervention_type": "seasonal_world_event",
  "authority": {
    "principal_type": "creator",
    "principal_id": "creator:42",
    "delegation_id": "delegation:world-harbor-city"
  },
  "scope": {
    "tenant_id": "tenant:example",
    "service_id": "service:example",
    "world_id": "world:harbor-city",
    "scene_ids": ["scene:winter-plaza"],
    "character_ids": []
  },
  "effective_time": "2026-12-01T00:00:00+09:00",
  "occurrence_id": "occurrence:harbor-city:christmas-2026",
  "expected_world_revision": 410,
  "requested_transitions": [
    {"kind": "world_event", "event_type": "seasonal_market_opened"}
  ],
  "approval_policy_id": "approval:seasonal-event",
  "world_publication_id": "world-publication:harbor-city:28",
  "policy_revision_ids": ["world-policy:19"],
  "idempotency_key": "world:harbor-city|occurrence:christmas-2026",
  "status": "approved"
}
```

The record can also carry a reason, source materials, preview result, approvers, activation window, expiry, precedence, rollback or compensation plan, and audience-visible explanation. Secret operational reasons can be stored separately with restricted access.

### 91.3 Diegetic and direct intervention embodiments

A **diegetic intervention** creates a world event or observer-scoped projection that allows a Character to change through an in-world cause. For example, a Character learns a fact from a letter, witnesses an incident, or develops a relationship through a scheduled scene.

A **direct subjective intervention** applies a privileged Character Subjective Transition without asserting that an in-world observation occurred. It is useful for correcting drift, restoring Character intent, applying editorial direction, or migrating state. The record labels the change as creator/operator-directed rather than fabricating a false observation.

A **publication intervention** activates a new Character or World Publication for future execution. It can change rules, traits, goals, allowed tools, or interpretation policy while leaving past runs bound to their original publication.

An implementation can combine these forms. A direct correction can immediately stop future drift while a later in-world event explains a related Character development. The two transitions retain separate causes and provenance.

### 91.4 Character drift and correction

Drift can be detected by deterministic validation, evaluation against Character examples or constraints, regression tests, creator review, operator reports, user reports, or model-assisted classification. Detection evidence is not itself permission to rewrite state.

Corrective alternatives include:

- activate a corrected Character Publication for new interactions;
- supersede selected beliefs, memories, affect edges, goals, or relationship transitions;
- quarantine a disputed state and rebuild from an earlier revision plus approved events;
- reduce autonomy or tool capability while preserving conversational availability;
- inject a bounded directive or scene that guides future development;
- restore a prior publication for future work without rewriting past execution provenance.

Correction scope can be one Character, one relationship, one topic, one scene, one service, or all future executions. A broad reset is not inferred from a narrow problem.

### 91.5 Directed growth and perception change

Creator-directed development can specify a target state, allowed path, minimum or maximum rate, triggering event, affected relationships, protected traits, and evaluation criteria. The runtime can execute the development immediately, at a scheduled time, after a condition, across several interactions, or through an authored sequence of world events and observations.

The target is not necessarily a fixed final utterance or a forced single personality. It can be a bounded region such as increased trust toward one Character, reduced fear after repeated safe experiences, recognition of a revealed fact, or a newly adopted goal. Each step records whether it arose from an autonomous proposal, deterministic rule, authored event, direct intervention, or manual approval.

### 91.6 Preview, approval, activation, and rollback

Before activation, an intervention can be schema-validated, simulated against a snapshot, previewed for affected Characters and audiences, evaluated by regression scenarios, staged to a subset, approved, and scheduled. High-impact intervention types can require multiple roles or an explicit break-glass policy.

Activation uses expected revisions and idempotency. A stale intervention is re-evaluated, rejected, or explicitly rebased; it is not silently applied to a different world state. Cancellation before activation and compensation after activation are separate operations.

For event-sourced world state, correction normally appends compensating or superseding events. For mutable-state embodiments, an audit record and prior revision remain available even when the current row changes. A rollback changes the active publication or adds a transition; it does not rewrite the provenance of already executed interactions.

## 92. Bounded autonomy and human authority

### 92.1 Autonomy is optional

Autonomous Character or world-process behavior is one replaceable proposal source. An experience can use no autonomous world mutation, allow autonomous dialogue only, require approval for every proposed effect, permit bounded low-risk effects, or permit broader actions inside an explicit policy.

RCCP correctness does not depend on a model independently preserving Character identity, recognizing every important event, choosing appropriate long-term growth, or repairing its own drift. Character Publications, creator-authored events, intervention contracts, validators, authorization, state boundaries, and operator controls remain first-class mechanisms.

### 92.2 Autonomy Profile

An `AutonomyProfile` can declare:

- eligible Characters and world processes;
- allowed intent and effect types;
- prohibited targets and protected traits;
- world, scene, relationship, topic, and audience scope;
- tool, network, data, and memory permissions;
- time, turn, token, cost, frequency, and risk budgets;
- approval thresholds and human-review queues;
- quiet hours, pause conditions, and termination rules;
- maximum subjective or world-state delta per period;
- fallback when a model, tool, policy evaluator, or approver is unavailable.

The profile is versioned and resolved with the Character and World Publications. A Model Service cannot expand it through generated text or structured output.

### 92.3 Proposal sources and commit authority

Model generation, deterministic simulation, Creator editing, Operator action, User narration, Tool output, and scheduled automation can all produce typed proposals. Their authority differs. A normal Actor can be limited to speech and action proposals, while an authenticated `NarrativeAuthorityGrant` can permit selected world, scene, entity, event, or Character-transition proposals. The grant identifies its issuer, principal, scope, allowed proposal types, approval requirements, validity interval, and revision. Content cannot self-assert narrator, Creator, or Operator authority.

A Creator or authorized Operator can have higher domain authority than an autonomous Character while still being subject to schema, scope, expected-revision, safety, consent, and non-overridable Developer constraints. An Intervention Resolver authenticates and validates the intervention, then routes world actions to Action Resolution, subjective changes to the Character-state validator, publication changes to publication activation, and operational controls to the operator control plane. Human authority does not require bypassing domain-specific audit or transaction boundaries.

### Figure 20 — Multiple proposal sources, one commit boundary

```mermaid
flowchart TD
  A["Character / Model"] --> R["Resolution / Validation"]
  C["Creator Intervention"] --> R
  O["Operator Intervention"] --> R
  T["Clock / Tool / Actor"] --> R
  R --> W["Authorized Domain Commit"]
```

## 93. Living-world execution responsibilities

Logical responsibilities include World State ownership, Simulation Orchestration, World Clock or Scheduler, autonomous proposal triggering, Intervention Resolution, Projection Delivery, publication activation, operator control, and Character/Conversation Orchestration. They need not be separate network services.

### 93.1 Complete cyclic processing procedure

One complete embodiment performs the following steps:

1. Resolve the active tenant, service, World, Character, workflow, intervention, policy, and Autonomy Profile revisions for the trigger.
2. Admit an Actor request, Character proposal, Tool result, scheduled occurrence, Creator intervention, Operator intervention, or deterministic world-process trigger under a stable identifier and idempotency key.
3. Authenticate the principal or system trigger and resolve its authority separately from the proposal content.
4. Load the expected World, entity, scene, relationship, and relevant Character subjective revisions.
5. For a Character action, retrieve only the observer-scoped projections, beliefs, memories, goals, affect, and relationships authorized for that Character and execution.
6. Create a typed `ActionIntent`, `NarrativeProposal`, `SubjectiveTransition`, `PublicationActivation`, or `OperationalControlIntent`; generated text itself is not a mutation.
7. Route the proposal through the applicable resolver or validator and evaluate world rules, preconditions, delegation, consent, safety, limits, conflicts, and expected revisions.
8. Record a rejected, deferred, conflicted, partially accepted, or accepted resolution with the evaluated revisions and reasons.
9. Commit accepted World Events and World State changes atomically or through an identified saga; publication, subjective-state, and operational commits use their own domain boundaries.
10. Place committed events and projection work in an outbox or equivalent reliable handoff.
11. Derive observer-scoped projections using the committed event, visibility rules, location, time, audience, information path, and projection revision.
12. Deliver or make each projection available idempotently and record delay, expiry, supersession, failure, or revocation.
13. Create Character Subjective Transition candidates from applicable projections, reflection, decay, authored direction, or migration and validate them against expected revisions and Character policy.
14. Use the new or retained subjective revision for later dialogue, planning, or action proposals without granting those proposals commit authority.
15. Continue the cycle only within the configured turn, time, cost, effect, and loop budgets.
16. Persist causal links and execution provenance sufficient for authorized audit, reconciliation, and complete or explicitly partial replay.

### 93.2 Time and scheduled occurrences

The World Clock can use wall-clock, logical, scene-based, creator-controlled, or hybrid time. Scheduled interventions and autonomous triggers carry stable occurrence identifiers. Pause, resume, missed schedule, backfill, retry, and replay do not create the same event twice.

When a scheduled event becomes due, the scheduler creates an occurrence proposal rather than directly mutating the world. Action Resolution verifies that the publication, authority, activation window, expected revision, and event preconditions still apply. A Valentine's Day event can therefore be skipped in a paused world, shifted by Creator policy, or activated once for each independently scoped world instance.

## 94. Transactions, failures, correction, and replay

### 94.1 Transactional decomposition

- **Intent transaction:** persist the authenticated proposal, scope, authority reference, expected revisions, and idempotency key.
- **Resolution transaction:** persist evaluated rules, permissions, conflicts, decision, and selected effects.
- **World transaction:** append events and/or update state under an expected World revision.
- **Projection transaction:** create observer-scoped projections and outbox records.
- **Subjective transaction:** validate and apply Character transitions under expected subjective revisions.
- **Delivery transaction:** record Channel or inter-service delivery attempts and results.

An implementation can combine transactions in one database. When it cannot, outbox/inbox, idempotency, reconciliation, and compensating transitions retain causal identity.

### 94.2 Intervention lifecycle

```mermaid
stateDiagram-v2
  [*] --> Draft
  Draft --> Validated
  Draft --> Withdrawn
  Validated --> Approved
  Validated --> Rejected
  Approved --> Scheduled
  Approved --> Resolving
  Approved --> Cancelled
  Scheduled --> Resolving
  Scheduled --> Cancelled
  Scheduled --> Expired
  Resolving --> Applied
  Resolving --> Conflicted
  Resolving --> Rejected
  Conflicted --> Rebased
  Rebased --> Resolving
  Applied --> Superseded
  Applied --> Compensated
  Rejected --> [*]
  Superseded --> [*]
  Compensated --> [*]
  Withdrawn --> [*]
  Cancelled --> [*]
  Expired --> [*]
```

Low-risk deployments can combine validation and approval. An emergency suspension can enter `Resolving` under a break-glass grant, but authentication, scope, result recording, expiry, and later review remain required.

### 94.3 Failure matrix

| Failure | Detectable state | Independent responses |
| --- | --- | --- |
| stale World revision | resolution conflict | reload and re-evaluate; reject; explicit rebase |
| duplicate scheduled occurrence | idempotency key exists | return prior result; suppress duplicate |
| projection generation fails | World Event committed, projection pending | retry; alternate projection; manual repair |
| subjective update fails | projection delivered or pending, Character revision unchanged | retry; reconcile; supersede |
| model proposes impossible action | failed precondition | reject; in-character failure response; alternate plan |
| intervention exceeds delegation | authority denied | reject; request approval; narrow scope |
| drift correction conflicts with newer state | expected revision mismatch | preview new delta; rebase; quarantine |
| partial multi-Character update | saga incomplete | compensate; retry remaining targets; mark partial |
| missed seasonal schedule | due occurrence absent | skip; activate late; backfill without duplicate |
| autonomous loop exceeds budget | budget exhausted | terminate; preserve committed events; notify Operator |

### 94.4 Replay and evaluation

Replay records or pins World and Character Publications, intervention and autonomy profiles, source events, projection logic, model/provider settings where available, policy revisions, action decisions, state revisions, and delivery results. A partial replay declares missing model output, deleted personal data, external effects, or unavailable source content.

Regression evaluation can replay representative worlds with autonomy disabled, with recorded proposals, or with current proposal generation. It compares committed effects, protected Character traits, belief/observation separation, policy decisions, and audience-visible output without assuming byte-identical model text.

Deletion, consent revocation, visibility changes, and retcons are separate transition types. A deployment can delete or cryptographically erase Actor-private payloads and invalidate derived projections while retaining a non-sensitive event identifier, tombstone, or digest when policy permits. A retcon can supersede the current authoritative interpretation without pretending that Characters never observed or acted on the earlier version; policy decides whether affected beliefs remain as historical memories, become disputed, are corrected by a new projection, or are rebuilt from an earlier revision. Replay records which rule was applied.

## 95. Worked embodiments

### 95.1 Reference Embodiment R9 — Creator-scheduled Christmas event

1. A Creator publishes a Christmas event plan with world, scene, activation window, event content, observer rules, and one stable occurrence key.
2. The Operator approves activation for the production world while a preview environment uses a different world identifier.
3. At the effective time, the Scheduler emits an occurrence proposal. It does not directly write World State.
4. Action Resolution verifies the active publication, authority, expected revision, and preconditions and commits one `seasonal_market_opened` World Event.
5. Projection Delivery creates a direct observation for Characters in the plaza, a delayed news projection for absent Characters, and no projection for a secret Character.
6. Character subjective transitions update beliefs and affect separately. Each subsequent response uses the Character's own applied revision.
7. A retry with the same occurrence key returns the committed result and does not open the market twice.

### 95.2 Reference Embodiment R10 — Character drift correction without history rewrite

1. Regression evaluation and Creator review find that a Character's accumulated goal and relationship state cause repeated behavior outside the approved Character intent.
2. The Creator publishes a corrected Character revision and a scoped realignment intervention for future interactions and the affected relationship only.
3. Preview compares the old and proposed state. An Operator can pause autonomous effects while ordinary text responses continue under a safe publication.
4. Activation supersedes the disputed goal, narrows autonomy, and activates the corrected Character Publication under expected revisions.
5. Past interactions retain their original Character Publication, subjective revision, and output provenance. They are not rewritten as if the drift never occurred.
6. A later authored scene can produce in-world Character growth, but that narrative event remains distinct from the earlier corrective intervention.

### 95.3 Reference Embodiment R11 — Directed perception change

1. A Creator intends Character A to learn a secret after a specified story milestone while Character B remains unaware.
2. A milestone condition produces a private projection backed by the committed source event and scoped only to Character A.
3. Character A's transition records the new belief, source, confidence, and validity; Character B receives no update.
4. If the Creator instead applies a direct editorial correction, the transition is labeled `creator_directed` and does not fabricate an observation.
5. Later actions retain the subjective revisions they used, allowing audit of decisions made before and after the revelation.

## 96. Additional combinations and technical propositions

### Combination BJ — Authoritative world with observer-scoped belief divergence

One committed World Event produces multiple visibility- and audience-scoped projections that update separate Character belief revisions without creating one global social perception.

### Combination BK — Character proposal with deterministic commit authority

A model proposes an Action Intent from Character belief and goals; a separately authorized resolver enforces world rules, expected revisions, consent, and idempotency before committing an effect.

### Combination BL — Scheduled creator event through the normal resolution boundary

A Creator-authored seasonal event is activated by a durable schedule but remains a typed proposal until authority, publication, preconditions, revision, and occurrence identity are validated.

### Combination BM — Operator pause with creator-owned story continuity

An Operator immediately pauses autonomous effects or activates a safe revision while story and Character-content changes remain governed by Creator authority or explicit delegation.

### Combination BN — Direct realignment separated from in-world development

A privileged subjective-state correction is recorded as an editorial intervention, while later diegetic events and observations can independently produce Character development without falsifying the correction's cause.

### Combination BO — Event history with compensating retcon

An authoritative event remains in causal history while a later authorized event supersedes or compensates for its current effect and rebuilds affected projections under a recorded policy.

### TP-46 — Cyclic authority-preserving world feedback

Authoritative state produces observer-scoped projections and Character subjective states; Character actions return only as validated intents whose committed results create new authoritative events.

### TP-47 — Observer-scoped projection between world fact and Character belief

Observation scope, audience, transformation, delay, and provenance remain explicit between an authoritative occurrence and a Character's subjective update.

### TP-48 — Generated narrative separated from authoritative mutation

User, Model, Tool, or Character-generated content cannot self-promote from narration or proposal to authoritative World or durable Character state.

### TP-49 — Authorized human intervention without hidden history rewrite

Creator or delegated Operator intervention can schedule, correct, direct, pause, or supersede world and Character transitions while retaining authority, scope, revision, cause, and execution provenance.

### TP-50 — Bounded autonomy subordinate to publication and policy

Autonomous proposal generation is optional and cannot expand its action, tool, state, or audience authority beyond a versioned profile and external commit boundary.

### TP-51 — Directed Character development with source distinction

Character growth or perception change records whether it arose from observation, autonomous inference, deterministic rule, Creator direction, Operator action, or migration and preserves that distinction during replay.

### TP-52 — Idempotent scheduled narrative occurrence

A scheduled or condition-triggered story event uses one stable occurrence identity and an expected scope/revision so retry, resume, backfill, or multi-instance execution does not duplicate the authoritative event.

---

Copyright (c) 2026 Akihiro Fujimoto. Licensed under the MIT License as part of the Roidoya Character Chat Platform repository unless otherwise stated.
