# Connection management smoke server

Fixed connection and actor mapping server for local LINE Gateway smoke checks.
Requires Node.js 20 or newer. Both c0.1 and c0.2 endpoints run in one process.

```powershell
npm ci
npm run build
$env:PORT = "8080"
$env:SMOKE_CONFIG_PATH = Read-Host "Path to the local smoke configuration JSON"
npm start
```

The UTF-8 JSON file has this shape. Replace placeholders locally and keep the real
configuration outside this public repository. Do not print API responses containing
credentials or include them in logs, screenshots or reports.

```json
{
  "connection": {
    "id": "smoke-connection",
    "channel_type": "line",
    "character": { "id": "smoke-character" },
    "settings": {
      "destination": "REPLACE_ME",
      "channel_secret": "REPLACE_ME",
      "channel_access_token": "REPLACE_ME"
    }
  },
  "actors": [
    { "channel_type": "line", "external_id": "REPLACE_ME", "actor": { "id": "smoke-actor" } }
  ]
}
```

Register one connection and at least one actor mapping. All IDs and settings values
must be nonempty strings. Channel types must be `line`. Each `(channel_type, external_id)`
pair must be unique. Configuration is loaded at startup; restart after changing it.

| Endpoint (POST JSON) | Request | HTTP 200 response |
|---|---|---|
| `/channel/v0.1/get-connection` | `{}` | `settings` containing only `channel_secret` and `channel_access_token` |
| `/channel/v0.2/get-connection` | `{"connection":{"id":"smoke-connection"}}` | The configured connection object at the root |
| `/channel/v0.2/resolve-actor` | `{"channel_type":"line","external_id":"..."}` | `{"actor":{"id":"..."}}` |

Unknown c0.2 connections and actors return HTTP 404. Invalid configuration prevents
startup with a constant error message. Request and configuration values are not logged.
`GET /` returns a static liveness message. The server binds to `0.0.0.0` and reads `PORT`
(default 8080). Keep it on a local container network; it returns development credentials
and does not implement authentication. Only the Gateway webhook should be tunneled.

The fixed responses and 404 behavior belong to this smoke tool; they do not define
additional Gateway error contracts. This tool uses the Express dependency versions
already locked by the existing c0.1 smoke server.
