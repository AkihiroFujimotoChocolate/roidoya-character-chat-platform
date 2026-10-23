# Chat API smoke server — c0.2

A development connection tool for the c0.2 LINE Gateway path. This is not an
Orchestration implementation or a Model substitute. It makes no provider calls,
stores no conversation history, and does not log request data.

Run with Node.js 18 or later; no dependency installation is needed:

```sh
cd tools/chat-api-smoke-server/v0.2
npm start
```

The defaults are `http://127.0.0.1:3002` and the fixed reply `c0.2 smoke reply.`.
Set `SMOKE_HOST`, `SMOKE_PORT`, or `SMOKE_REPLY_TEXT` before starting to override
them. Keep this internal endpoint off public tunnels; expose only the Gateway's
LINE webhook. Reply configuration changes require restarting this tool.

The tool accepts `POST /chat/v0.2/generate-replies` with a c0.2 normal request:
`interaction_id`, `origin.channel_type=line`,
`origin.channel_instance_id`, `actor.id`, `character.id`, and
`message.id` / `message.content` containing one text part. It returns the same
interaction ID, `status=ok`, and one text message with a fresh output message ID.
The optional `X-Chat-Api-Version` header must be `0.2` if present.

Phase 1 sends five root fields: `interaction_id`, `origin`, `actor`, `character`,
and `message`. It sends no `scope` or `conversation`. It uses a fixed Character
ID and the c0.1-style actor ID (`line-user-<LINE user ID>`). The tool
accepts these values without maintaining an identity map, permissions, history,
or conversation state. A successful exchange checks the connection and response
shape; it does not demonstrate conversation separation or durable identity.

There are no `fail` / `wait` input commands, deadline fields, output limits, or
scenario-control API. Basic malformed-request handling is tool hygiene, not a
claim of complete c0.2 error conformance. Existing c0.1 tools are unchanged.
