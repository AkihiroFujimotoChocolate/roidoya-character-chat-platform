# Chat API smoke server v0.1

Local HTTP echo server for LINE Gateway smoke checks. Requires Node.js 20 or newer.

```powershell
npm ci
npm run build
$env:PORT = "8080"
npm start
```

POST JSON to `/chat/v0.1/generate-replies`. The server reads `message.text` and copies
`request_id` into its response.

| Input text | Response |
|---|---|
| `fail` | HTTP 400, `status: provider_error`, `error.code: fail_test` |
| `wait5s` | Wait five seconds, then echo the input |
| `wait250ms` | Wait 250 milliseconds, then echo the input |
| Other text | HTTP 200 with the input as the single string in `messages` and `fallback_used: false` |

Missing text returns HTTP 400 with `error.code: bad_request`. Wait commands match
`^wait(\d+)s$` or `^wait(\d+)ms$`. Malformed JSON returns a constant error response.
Only the listening port is logged; parser errors, bodies, identifiers and credentials
are not logged. The server binds to `0.0.0.0`, validates `PORT` (default 8080), and runs
as a separate process from the v0.2 tool. Keep it on a local container network;
only the Gateway webhook should be tunneled.
