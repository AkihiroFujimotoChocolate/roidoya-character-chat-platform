# Chat API smoke server v0.2

Local HTTP echo server for LINE Gateway connection checks and manual fault experiments.
Requires Node.js 20 or newer.

```powershell
npm ci
npm run build
$env:PORT = "8080"
npm start
```

POST JSON to /chat/v0.2/generate-replies using the c0.2 fields
interaction_id, origin, actor, character and message.
The first message.content text part is used as the command or echoed text.
The server checks that message.content[0].text is a string, as the v0.1 tool checks message.text.

| Text | Response |
|---|---|
| fail | HTTP 400, status provider_error, error.code fail_test |
| wait5s | Wait five seconds, then echo wait5s |
| wait100ms | Wait 100 milliseconds, then echo wait100ms |
| Other text | HTTP 200 and the exact input text, including whitespace and special characters |

Wait commands match ^wait(\\d+)s$ or ^wait(\\d+)ms$.
Successful responses copy interaction_id and return one message with a fresh UUID
and one text content part. Missing text returns HTTP 400 with error.code bad_request.
The v0.1 API path is not registered. Version headers are optional.
Only the listening port is logged; request bodies and credentials are not logged.
The injected errors are smoke-tool behavior for observing the Gateway.
