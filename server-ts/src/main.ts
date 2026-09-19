/**
 * Process bootstrap: environment, UDP register listener, SQLite, then the two TCP
 * handshakes. Packet modules own wire fields; this file owns deployment policy.
 */

import { ChannelAdmissionRegistry } from "./admission.ts";
import { listen } from "./connection.ts";
import { OPCODE_COUNT } from "./opcodes.ts";
import { summary } from "./ops/registry.ts";
import type { GameServer } from "./ops/s2c/GL_LOGIN_ACK.ts";
import { Store } from "./store.ts";
import { UdpRegisterServer } from "./udp.ts";

function integerEnv(name: string, fallback: number, min: number, max: number): number {
  const raw = Bun.env[name];
  const value = raw === undefined ? fallback : Number(raw);
  if (!Number.isInteger(value) || value < min || value > max) {
    throw new RangeError(`${name} must be an integer in ${min}..${max}`);
  }
  return value;
}

const env = {
  host: Bun.env["PM_HOST"] ?? "0.0.0.0",
  loginPort: integerEnv("PM_PORT", 40_200, 0, 65_535),
  channelPort: integerEnv("PM_CHANNEL_PORT", 40_201, 0, 65_535),
  advertiseHost: Bun.env["PM_ADVERTISE_HOST"] ?? "127.0.0.1",
  channelName: Bun.env["PM_CHANNEL_NAME"] ?? "Channel 1",
  channelMaxUsers: integerEnv("PM_CHANNEL_MAX_USERS", 100, 0, 32_767),
  channelCurrentUsers: integerEnv("PM_CHANNEL_CURRENT_USERS", 0, 0, 32_767),
  udpHost: Bun.env["PM_UDP_HOST"] ?? Bun.env["PM_ADVERTISE_HOST"] ?? "127.0.0.1",
  udpPort: integerEnv("PM_UDP_PORT", 40_202, 0, 65_535),
  admissionLifetimeMs: integerEnv("PM_ADMISSION_TTL_MS", 120_000, 1, 2_147_483_647),
  dbPath: Bun.env["PM_DB"] ?? "paperman.sqlite",
} as const;

const log = (message: string): void => {
  console.log(`[${new Date().toISOString()}] ${message}`);
};

const store = new Store(env.dbPath);
const admissions = new ChannelAdmissionRegistry();
const udpServer = await UdpRegisterServer.listen({
  hostname: env.host,
  port: env.udpPort,
  log,
});

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
        channel: {
          channelType: 1,
          name: env.channelName,
          currentUsers: env.channelCurrentUsers,
          flag: 0,
        },
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
  channel: {
    name: env.channelName,
    group: 0,
    activeChannelIndex: 0,
    channelId: 1,
    endpoint: { host: env.udpHost, port: udpServer.port },
    channelType: 1,
    // Written into the client's unk_1D0CFE4 by the 142/196/371 readers —
    // a write-only global with zero read sites in the whole client image
    // (line-level 2026-09-19), so 0 is the honest projection.
    endpointOpaque: 0,
    clientFlags: 0,
    clientDefault: 5,
  },
  admissionLifetimeMs: env.admissionLifetimeMs,
};

const loginServer = listen({ ...shared, role: "login", hostname: env.host, port: env.loginPort });
const channelServer = listen({
  ...shared,
  role: "channel",
  hostname: env.host,
  port: env.channelPort,
});

log(`login on ${env.host}:${env.loginPort}, channel on ${env.host}:${env.channelPort}`);
log(`udp register on ${env.host}:${udpServer.port} (UDP_REGISTER 19 -> 20 only)`);
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
