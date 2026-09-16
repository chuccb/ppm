/**
 * Entry point.
 *
 * Implements only what docs/ establishes as fact. Where behaviour was
 * service-side and is therefore unrecoverable, this server does nothing rather
 * than invent a plausible rule — see STYLE.md.
 */

import { OPCODE_COUNT } from "./opcodes.ts";
import { Store } from "./store.ts";
import type { GameServer } from "./ops/s2c/GL_LOGIN_ACK.ts";
import { listen } from "./session.ts";
import { Registry } from "./ops/registry.ts";

const host = Bun.env["PM_HOST"] ?? "0.0.0.0";
const port = Number(Bun.env["PM_PORT"] ?? 40200);
const dbPath = Bun.env["PM_DB"] ?? "paperman.sqlite";
const channelPort = Number(Bun.env["PM_CHANNEL_PORT"] ?? 40201);

const log = (message: string): void => {
  console.log(`[${new Date().toISOString()}] ${message}`);
};

// Filenames under src/ops/ are the registration; an unknown name throws here.
const ops = Registry.load();
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

const server = listen({ hostname: host, port, store, servers, ops, log });

log(`login server on ${host}:${port}`);
log(`${OPCODE_COUNT} opcodes known; ${ops.summary}`);
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
