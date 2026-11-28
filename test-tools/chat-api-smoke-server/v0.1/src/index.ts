// SPDX-License-Identifier: MIT
import express, { type ErrorRequestHandler } from "express";

type ChatRequest = {
  request_id?: string;
  message?: { text?: string };
};

type ChatResponse =
  | { request_id?: string; status: "ok"; messages: string[]; fallback_used?: false }
  | { request_id?: string; status: "provider_error"; error: { code: string; message?: string } };

const app = express();
app.use(express.json()); // default 100kb limit

app.get("/", (_req, res) => res.send("Roidoya Chat Layer — Echo v0.1"));

app.post("/chat/v0.1/generate-replies", async (req, res) => {
  const body = req.body as ChatRequest | undefined;
  const requestId = body?.request_id;
  const text = body?.message?.text;

  if (typeof text !== "string") {
    const bad: ChatResponse = {
      request_id: requestId,
      status: "provider_error",
      error: { code: "bad_request", message: "message.text is required" },
    };
    return res.status(400).json(bad);
  }

  // Test: "fail" returns 400
  if (text === "fail") {
    const bad: ChatResponse = {
      request_id: requestId,
      status: "provider_error",
      error: { code: "fail_test", message: "Triggered fail test" },
    };
    return res.status(400).json(bad);
  }

  // Test: "waitNs" waits N seconds
  const waitNsMatch = text.match(/^wait(\d+)s$/);
  if (waitNsMatch) {
    const seconds = parseInt(waitNsMatch[1], 10);
    await new Promise(resolve => setTimeout(resolve, seconds * 1000));
  }

  // Test: "waitNms" waits N milliseconds
  const waitNmsMatch = text.match(/^wait(\d+)ms$/);
  if (waitNmsMatch) {
    const ms = parseInt(waitNmsMatch[1], 10);
    await new Promise(resolve => setTimeout(resolve, ms));
  }

  const ok: ChatResponse = {
    request_id: requestId,
    status: "ok",
    messages: [text],
    fallback_used: false,
  };
  return res.status(200).json(ok);
});

// JSON parser errors can contain the request body. Do not pass them to the default logger.
const handleError: ErrorRequestHandler = (_error, _req, res, _next) => {
  res.status(400).json({ error: "invalid_request" });
};
app.use(handleError);

const portText = process.env.PORT ?? "8080";
const port = Number(portText);
if (!/^\d+$/.test(portText) || !Number.isInteger(port) || port < 1 || port > 65535) {
  console.error("Smoke server startup failed: check PORT.");
  process.exit(1);
}
app.listen(port, "0.0.0.0", () => console.log(`chat-layer echo listening on ${port}`))
  .on("error", () => {
    console.error("Smoke server could not listen on the configured port.");
    process.exit(1);
  });
