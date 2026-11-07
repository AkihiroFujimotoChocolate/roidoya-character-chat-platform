// SPDX-License-Identifier: MIT
import express from "express";
import { randomUUID } from "node:crypto";

type ChatRequest = {
  interaction_id?: string;
  message?: { content?: { type?: string; text?: string }[] };
};

type ChatResponse =
  | { interaction_id?: string; status: "ok"; messages: { id: string; content: { type: "text"; text: string }[] }[] }
  | { interaction_id?: string; status: "provider_error"; error: { code: string; message?: string } };

const app = express();
app.use(express.json());

app.get("/", (_req, res) => res.send("Roidoya Chat Layer — Echo v0.2"));

app.post("/chat/v0.2/generate-replies", async (req, res) => {
  const body = req.body as ChatRequest | undefined;
  const interactionId = body?.interaction_id;
  const text = body?.message?.content?.[0]?.text;

  if (typeof text !== "string") {
    const bad: ChatResponse = {
      interaction_id: interactionId,
      status: "provider_error",
      error: { code: "bad_request", message: "message.content[0].text is required" },
    };
    return res.status(400).json(bad);
  }

  if (text === "fail") {
    const bad: ChatResponse = {
      interaction_id: interactionId,
      status: "provider_error",
      error: { code: "fail_test", message: "Triggered fail test" },
    };
    return res.status(400).json(bad);
  }

  const waitNsMatch = text.match(/^wait(\d+)s$/);
  if (waitNsMatch) {
    const seconds = parseInt(waitNsMatch[1], 10);
    await new Promise(resolve => setTimeout(resolve, seconds * 1000));
  }

  const waitNmsMatch = text.match(/^wait(\d+)ms$/);
  if (waitNmsMatch) {
    const ms = parseInt(waitNmsMatch[1], 10);
    await new Promise(resolve => setTimeout(resolve, ms));
  }

  const ok: ChatResponse = {
    interaction_id: interactionId,
    status: "ok",
    messages: [{ id: randomUUID(), content: [{ type: "text", text }] }],
  };
  return res.status(200).json(ok);
});

const port = process.env.PORT || 8080;
app.listen(port, () => console.log(`chat-layer echo listening on ${port}`));
