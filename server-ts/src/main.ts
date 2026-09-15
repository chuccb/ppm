/**
 * Entry point.
 *
 * Implements only what docs/ establishes as fact. Where behaviour was
 * service-side and is therefore unrecoverable, this server does nothing rather
 * than invent a plausible rule — see STYLE.md.
 */

import { assertCatalogue } from "./opcodes.ts";
import { Store } from "./store.ts";
import type { GameServer } from "./login.ts";
import { listen } from "./session.ts";

const host = Bun.env["PM_HOST"] ?? "0.0.0.0";
const port = Number(Bun.env["PM_PORT"] ?? 40200);
const dbPath = Bun.env["PM_DB"] ?? "paperman.sqlite";
const channelPort = Number(Bun.env["PM_CHANNEL_PORT"] ?? 40201);

const log = (message: string): void => {
  console.log(`[${new Date().toISOString()}] ${message}`);
};

assertCatalogue(); // fail fast if the opcode catalogue and this build disagree

const store = new Store(dbPath);

// Deployment policy, not reverse-engineered fact: what to advertise.
const servers: readonly GameServer[] = [
  {
    id: 1,
    name: "PaperMan",
    host: Bun.env["PM_ADVERTISE_HOST"] ?? "127.0.0.1",
    port: channelPort,
    flag: 0,
    group: 0,
    channelGroups: [[{ type: 1, name: "Channel 1", port: channelPort, flag: 0 }], [], []],
  },
];

const server = listen({ hostname: host, port, store, servers, log });

log(`login server on ${host}:${port}`);
log(`sqlite ${store.sqliteVersion} at ${dbPath}`);
log(`bun ${Bun.version} (${Bun.revision.slice(0, 9)})`);

for (const signal of ["SIGINT", "SIGTERM"] as const) {
  process.on(signal, () => {
    log(`${signal}: shutting down`);
    server.stop();
    store.close();
    process.exit(0);
  });
}
