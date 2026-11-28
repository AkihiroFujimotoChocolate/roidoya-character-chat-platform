// SPDX-License-Identifier: MIT
import express, { type ErrorRequestHandler } from "express";
import { readFileSync } from "node:fs";

type Connection = {
  id: string;
  channel_type: "line";
  character: { id: string };
  settings: { destination: string; channel_secret: string; channel_access_token: string };
};
type ActorMapping = { channel_type: "line"; external_id: string; actor: { id: string } };
type SmokeConfig = { connection: Connection; actors: ActorMapping[] };

function object(value: unknown): Record<string, unknown> {
  if (value === null || typeof value !== "object" || Array.isArray(value)) throw new Error();
  return value as Record<string, unknown>;
}

function text(value: unknown): string {
  if (typeof value !== "string" || value.trim().length === 0) throw new Error();
  return value;
}

function line(value: unknown): "line" {
  if (value !== "line") throw new Error();
  return value;
}

function loadConfig(): SmokeConfig {
  const path = text(process.env.SMOKE_CONFIG_PATH);
  const root = object(JSON.parse(readFileSync(path, "utf8")));
  const entry = object(root.connection);
  const settings = object(entry.settings);
  const connection: Connection = {
    id: text(entry.id),
    channel_type: line(entry.channel_type),
    character: { id: text(object(entry.character).id) },
    settings: {
      destination: text(settings.destination),
      channel_secret: text(settings.channel_secret),
      channel_access_token: text(settings.channel_access_token),
    },
  };
  if (!Array.isArray(root.actors) || root.actors.length === 0) throw new Error();
  const keys = new Set<string>();
  const actors = root.actors.map((value: unknown): ActorMapping => {
    const mapping = object(value);
    const result: ActorMapping = {
      channel_type: line(mapping.channel_type),
      external_id: text(mapping.external_id),
      actor: { id: text(object(mapping.actor).id) },
    };
    const key = JSON.stringify([result.channel_type, result.external_id]);
    if (keys.has(key)) throw new Error();
    keys.add(key);
    return result;
  });
  return { connection, actors };
}

let config: SmokeConfig;
let port: number;
try {
  config = loadConfig();
  const value = process.env.PORT ?? "8080";
  if (!/^\d+$/.test(value)) throw new Error();
  port = Number(value);
  if (!Number.isInteger(port) || port < 1 || port > 65535) throw new Error();
} catch {
  console.error("Smoke server startup failed: check configuration and PORT.");
  process.exit(1);
}

const app = express();
app.use(express.json());
app.get("/", (_req, res) => res.send("Roidoya connection management smoke server"));

app.post("/channel/v0.1/get-connection", (_req, res) => {
  const { channel_secret, channel_access_token } = config.connection.settings;
  res.json({ settings: { channel_secret, channel_access_token } });
});

app.post("/channel/v0.2/get-connection", (req, res) => {
  if (req.body?.connection?.id !== config.connection.id) {
    return res.sendStatus(404);
  }
  return res.json(config.connection);
});

app.post("/channel/v0.2/resolve-actor", (req, res) => {
  const mapping = config.actors.find(value =>
    value.channel_type === req.body?.channel_type && value.external_id === req.body?.external_id);
  if (!mapping) return res.sendStatus(404);
  return res.json({ actor: mapping.actor });
});

// JSON parser errors can contain the request body. Return a constant response and do not log them.
const handleError: ErrorRequestHandler = (_error, _req, res, _next) => {
  res.status(400).json({ error: "invalid_request" });
};
app.use(handleError);

app.listen(port, "0.0.0.0", () => console.log(`connection smoke server listening on ${port}`))
  .on("error", () => {
    console.error("Smoke server could not listen on the configured port.");
    process.exit(1);
  });
