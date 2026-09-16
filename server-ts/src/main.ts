/**
 * Entry point: read the environment, open the two listeners, log what we have.
 *
 * Implements only what docs/ establishes as fact. Where behaviour was
 * service-side and is therefore unrecoverable, this server does nothing rather
 * than invent a plausible rule — see STYLE.md.
 */

import { OPCODE_COUNT } from "./opcodes.ts";
import { Store } from "./store.ts";
import type { GameServer } from "./ops/s2c/GL_LOGIN_ACK.ts";
import { listen } from "./connection.ts";
import { summary } from "./ops/registry.ts";

/** Every tunable, in one place, so none of them is hidden mid-file. */
const env = {
  host: Bun.env["PM_HOST"] ?? "0.0.0.0",
  loginPort: Number(Bun.env["PM_PORT"] ?? 40200),
  channelPort: Number(Bun.env["PM_CHANNEL_PORT"] ?? 40201),
  /** What the login reply tells clients to connect to; may differ from `host`. */
  advertiseHost: Bun.env["PM_ADVERTISE_HOST"] ?? "127.0.0.1",
  channelName: Bun.env["PM_CHANNEL_NAME"] ?? "Channel 1",
  dbPath: Bun.env["PM_DB"] ?? "paperman.sqlite",
} as const;

const log = (message: string): void => {
  console.log(`[${new Date().toISOString()}] ${message}`);
};

const store = new Store(env.dbPath);

/**
 * The server list the client receives on login, and then connects to.
 *
 * Deployment policy, not reverse-engineered fact: the wire shape is fixed, but
 * what goes in it is ours to choose.
 */
const servers: readonly GameServer[] = [
  {
    id: 1,
    name: "PaperMan",
    host: env.advertiseHost,
    port: env.channelPort,
    flag: 0,
    group: 0,
    channelGroups: [
      [{ type: 1, name: env.channelName, port: env.channelPort, flag: 0 }],
      [],
      [],
    ],
  },
];

const shared = { store, servers, log, channelName: env.channelName };

// Two listeners, one per handshake. The client reaches the second using the
// host and port it read from the server list above.
const loginServer = listen({ ...shared, role: "login", hostname: env.host, port: env.loginPort });
const channelServer = listen({
  ...shared,
  role: "channel",
  hostname: env.host,
  port: env.channelPort,
});

log(`login on ${env.host}:${env.loginPort}, channel on ${env.host}:${env.channelPort}`);
log(`${OPCODE_COUNT} opcodes known; ${summary()}`);
log(`sqlite ${store.sqliteVersion} at ${env.dbPath}`);
log(`bun ${Bun.version} (${Bun.revision.slice(0, 9)})`);

for (const signal of ["SIGINT", "SIGTERM"] as const) {
  process.on(signal, () => {
    log(`${signal}: shutting down`);
    loginServer.stop();
    channelServer.stop();
    store.close();
    process.exit(0);
  });
}
