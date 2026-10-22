// SPDX-License-Identifier: MIT
import http from 'node:http';
import { randomUUID } from 'node:crypto';

// A c0.2 connection tool. It performs no provider calls and has no input-triggered scenarios.
const host = process.env.SMOKE_HOST ?? '127.0.0.1';
const port = Number(process.env.SMOKE_PORT ?? '3002');
const replyText = process.env.SMOKE_REPLY_TEXT ?? 'c0.2 smoke reply.';
if (!Number.isInteger(port) || port < 0 || port > 65535 || !replyText.trim()) {
  throw new Error('Invalid smoke server port or empty reply text.');
}
const nonempty = value => typeof value === 'string' && value.trim().length > 0;
function send(res, status, body) {
  res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8' });
  res.end(JSON.stringify(body));
}
const server = http.createServer(async (req, res) => {
  if (req.method === 'GET' && req.url === '/') return send(res, 200, { tool: 'chat-api-smoke-server', contract: '0.2' });
  if (req.method !== 'POST' || req.url !== '/chat/v0.2/generate-replies') return send(res, 404, { error: 'not_found' });
  try {
    let bytes = 0; const chunks = [];
    for await (const chunk of req) {
      bytes += chunk.length;
      if (bytes > 1024 * 1024) { send(res, 413, { error: 'body_too_large' }); req.resume(); return; }
      chunks.push(chunk);
    }
    const body = JSON.parse(Buffer.concat(chunks).toString('utf8'));
    const version = req.headers['x-chat-api-version'];
    const part = body?.message?.content;
    if ((version !== undefined && version !== '0.2') ||
        !nonempty(body?.interaction_id) || !nonempty(body?.scope?.service_id) ||
        body?.origin?.channel_type !== 'line' || !nonempty(body?.origin?.channel_instance_id) ||
        !nonempty(body?.conversation?.id) || !nonempty(body?.actor?.id) || !nonempty(body?.character?.id) ||
        !nonempty(body?.message?.id) || !Array.isArray(part) || part.length !== 1 ||
        part[0]?.type !== 'text' || !nonempty(part[0]?.text)) {
      return send(res, 400, { error: 'invalid_smoke_request' });
    }
    return send(res, 200, {
      interaction_id: body.interaction_id, status: 'ok',
      messages: [{ id: `smoke-output:${randomUUID()}`, content: [{ type: 'text', text: replyText }] }]
    });
  } catch {
    // Never log request bodies, IDs, tokens, or exception text containing input.
    return send(res, 400, { error: 'invalid_smoke_request' });
  }
});
server.listen(port, host, () => {
  const address = server.address();
  console.log(`Chat API smoke server c0.2 listening at http://${host}:${address.port}`);
});
for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => server.close());
