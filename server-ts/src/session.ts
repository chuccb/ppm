/**
 * One connection: reassembly, liveness, and dispatch through the registry.
 *
 * `listen` is here too — the socket callbacks are a few lines each and a
 * separate file would only add a hop for the reader.
 */

import type { Socket } from "bun";
import { PacketStream, type Packet, type Reader } from "./packet.ts";
import { opcodeName } from "./opcodes.ts";
import type { BuilderArgs, BuilderName, Registry } from "./wire.ts";
import type { Store } from "./store.ts";
import type { GameServer } from "./wire/GL_LOGIN_ACK.ts";
import { Result } from "./wire/GL_LOGIN_ACK.ts";
import type { Credentials } from "./wire/GL_LOGIN_REQ.ts";

/** How often to poll, and how long silence may last. Server-side choices. */
export const PING_INTERVAL_MS = 15_000;
export const PING_TIMEOUT_MS = 60_000;

export interface Config {
  store: Store;
  servers: readonly GameServer[];
  wire: Registry;
  log: (message: string) => void;
}

export class Session {
  readonly #stream = new PacketStream();
  readonly #socket: Socket<Session>;
  readonly #config: Config;
  readonly #peer: string;
  /** Serialises dispatch so replies keep the order the requests arrived in. */
  #queue: Promise<void> = Promise.resolve();
  #heartbeat: ReturnType<typeof setInterval> | null = null;
  #lastSeen = Date.now();
  accountId: number | null = null;

  constructor(socket: Socket<Session>, config: Config) {
    this.#socket = socket;
    this.#config = config;
    this.#peer = socket.remoteAddress;
  }

  send(packet: Packet): void {
    this.#socket.write(packet.encode());
  }

  /** Build by opcode name and send. The name is checked at compile time. */
  reply<N extends BuilderName>(name: N, ...args: BuilderArgs<N>): void {
    this.send(this.#config.wire.build(name, ...args));
  }

  /** Sent once on connect; it is what triggers the client to log in. */
  greet(): void {
    this.reply("GL_ACCOUNTCONNSUCC");
    this.#heartbeat = setInterval(() => {
      if (Date.now() - this.#lastSeen > PING_TIMEOUT_MS) {
        this.#config.log(`${this.#peer}: no ping reply, closing`);
        this.#socket.end();
        return;
      }
      this.reply("GT_PING_ACK");
    }, PING_INTERVAL_MS);
  }

  dispose(): void {
    if (this.#heartbeat !== null) clearInterval(this.#heartbeat);
    this.#heartbeat = null;
  }

  receive(chunk: Uint8Array): void {
    this.#lastSeen = Date.now();
    this.#stream.push(chunk);

    let packets: Reader[];
    try {
      packets = [...this.#stream.drain()];
    } catch (error) {
      this.#fail("malformed frame", error);
      return;
    }

    // Handlers are async (argon2), so dispatching concurrently would let a fast
    // reply overtake a slow one. The client pairs replies to requests by order.
    for (const packet of packets) {
      this.#queue = this.#queue.then(() => this.#dispatch(packet));
    }
  }

  async #dispatch(r: Reader): Promise<void> {
    const handler = this.#config.wire.handlerFor(r.opcode);
    if (!handler) {
      // The client's own dispatcher silently ignores unknown opcodes. Mirror
      // that, but log so coverage gaps stay visible.
      this.#config.log(`${this.#peer}: unhandled ${opcodeName(r.opcode)} (${r.opcode})`);
      return;
    }
    try {
      await handler(r, this);
    } catch (error) {
      this.#fail(opcodeName(r.opcode), error);
    }
  }

  /**
   * Authenticate and reply.
   *
   * Boundary: the wire contract only. Entitlements, billing and what goes in
   * the server list are deployment policy, not reverse-engineered fact.
   */
  async login(credentials: Credentials): Promise<void> {
    const account = await this.#config.store.verifyLogin(
      credentials.account,
      credentials.password,
    );
    this.accountId = account?.id ?? null;
    this.#config.log(
      `${this.#peer}: login ${credentials.account} -> ${this.accountId ?? "rejected"}`,
    );

    this.reply(
      "GL_LOGIN_ACK",
      account ? { userNo: account.id, servers: this.#config.servers } : Result.BadCredentials,
    );
  }

  #fail(what: string, error: unknown): void {
    this.#config.log(`${this.#peer}: ${what} — ${(error as Error).message}, closing`);
    this.#socket.end();
  }
}

export function listen(config: Config & { hostname: string; port: number }) {
  return Bun.listen<Session>({
    hostname: config.hostname,
    port: config.port,
    socket: {
      open(socket) {
        socket.data = new Session(socket, config);
        config.log(`${socket.remoteAddress}: connected`);
        socket.data.greet();
      },
      data(socket, chunk) {
        socket.data.receive(new Uint8Array(chunk));
      },
      close(socket) {
        socket.data.dispose();
        config.log(`${socket.remoteAddress}: disconnected`);
      },
      error(socket, error) {
        config.log(`${socket.remoteAddress}: socket error — ${error.message}`);
      },
    },
  });
}
