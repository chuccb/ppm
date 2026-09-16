/**
 * Entry point: read the environment, open the two listeners, log what we have.
 *
 * Implements only what docs/ establishes as fact. Where behaviour was
 * service-side and is therefore unrecoverable, this server does nothing rather
 * than invent a plausible rule — see STYLE.md.
 */

import { ChannelAdmissionRegistry } from "./admission.ts";
import { OPCODE_COUNT } from "./opcodes.ts";
import { Store } from "./store.ts";
import { UdpControlServer } from "./udp.ts";
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
  channelMaxUsers: Number(Bun.env["PM_CHANNEL_MAX_USERS"] ?? 100),
  channelCurrentUsers: Number(Bun.env["PM_CHANNEL_CURRENT_USERS"] ?? 0),
  udpHost: Bun.env["PM_UDP_HOST"] ?? Bun.env["PM_ADVERTISE_HOST"] ?? "127.0.0.1",
  udpPort: Number(Bun.env["PM_UDP_PORT"] ?? 40202),
  admissionLifetimeMs: Number(Bun.env["PM_ADMISSION_TTL_MS"] ?? 120_000),
  dbPath: Bun.env["PM_DB"] ?? "paperman.sqlite",
} as const;

const log = (message: string): void => {
  console.log(`[${new Date().toISOString()}] ${message}`);
};

const store = new Store(env.dbPath);
const admissions = new ChannelAdmissionRegistry();
// Bind UDP before TCP so a successful 196 never advertises an endpoint this
// process failed to own. The handler remains deliberately limited to 19 → 20.
const udpServer = await UdpControlServer.listen({
  hostname: env.host,
  port: env.udpPort,
  log,
});

/**
 * The server list the client receives on login, and then connects to.
 *
 * Deployment policy, not reverse-engineered fact: the wire shape is fixed, but
 * what goes in it is ours to choose.
 */
const servers: readonly GameServer[] = [
  {
    serverId: 1,
    name: "PaperMan",
    host: env.advertiseHost,
    port: env.channelPort,
    flag: 0,
    group: 0,
    channelGroups: [
      {
        maxUsers: env.channelMaxUsers,
        channel: { type: 1, name: env.channelName, currentUsers: env.channelCurrentUsers, flag: 0 },
      },
      { maxUsers: 0 },
      { maxUsers: 0 },
    ],
  },
];

const shared = {
  store,
  servers,
  log,
  admissions,
  channelName: env.channelName,
  group: 0,
  channel: 0,
  channelId: 1,
  channelType: 1,
  udpHost: env.udpHost,
  udpPort: udpServer.port,
  admissionLifetimeMs: env.admissionLifetimeMs,
};

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
log(`udp control on ${env.host}:${udpServer.port} (private 19 -> 20 only)`);
log(`${OPCODE_COUNT} opcodes known; ${summary()}`);
log(`sqlite ${store.sqliteVersion} at ${env.dbPath}`);
log(`bun ${Bun.version} (${Bun.revision.slice(0, 9)})`);

for (const signal of ["SIGINT", "SIGTERM"] as const) {
  process.on(signal, () => {
    log(`${signal}: shutting down`);
    loginServer.stop();
    channelServer.stop();
    udpServer.stop();
    store.close();
    process.exit(0);
  });
}
