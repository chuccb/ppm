/**
 * PaperMan private-server entry point.
 *
 * Implements only what the reverse-engineering notes establish as fact. Where
 * behaviour was service-side and is therefore unrecoverable (see the many
 * UNRESOLVED markers in docs/), this server does nothing rather than invent a
 * plausible rule.
 */

import { assertCatalogue } from "./codec/opcodes.ts";
import { Store } from "./db/schema.ts";
import type { ServerEntry } from "./handlers/login.ts";
import { listen } from "./net/listener.ts";

const HOST = Bun.env["PM_HOST"] ?? "0.0.0.0";
const PORT = Number(Bun.env["PM_PORT"] ?? 40200);
const DB_PATH = Bun.env["PM_DB"] ?? "paperman.sqlite";

const log = (message: string): void => {
  console.log(`[${new Date().toISOString()}] ${message}`);
};

// Fail fast if the opcode catalogue and this build have drifted apart.
assertCatalogue();

const store = new Store(DB_PATH);

// Deployment policy, not reverse-engineered fact: which servers to advertise.
const servers: readonly ServerEntry[] = [
  {
    id: 1,
    name: "PaperMan",
    host: Bun.env["PM_ADVERTISE_HOST"] ?? "127.0.0.1",
    port: Number(Bun.env["PM_CHANNEL_PORT"] ?? 40201),
    flag: 0,
    group: 0,
    channelGroups: [
      [{ type: 1, name: "Channel 1", port: Number(Bun.env["PM_CHANNEL_PORT"] ?? 40201), flag: 0 }],
      [],
      [],
    ],
  },
];

const listener = listen({ hostname: HOST, port: PORT, store, servers, log });

log(`login server on ${HOST}:${PORT}`);
log(`sqlite ${store.sqliteVersion} at ${DB_PATH}`);
log(`bun ${Bun.version} (${Bun.revision.slice(0, 9)})`);

const shutdown = (signal: string): void => {
  log(`${signal}: shutting down`);
  listener.stop();
  store.close();
  process.exit(0);
};

process.on("SIGINT", () => {
  shutdown("SIGINT");
});
process.on("SIGTERM", () => {
  shutdown("SIGTERM");
});
