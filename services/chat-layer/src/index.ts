// SPDX-License-Identifier: MIT
import express from "express";

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

app.post("/chat/v0.1/generate-replies", (req, res) => {
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

  const ok: ChatResponse = {
    request_id: requestId,
    status: "ok",
    messages: [text],
    fallback_used: false,
  };
  return res.status(200).json(ok);
});

const port = process.env.PORT || 8080;
app.listen(port, () => console.log(`chat-layer echo listening on ${port}`));