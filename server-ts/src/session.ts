/**
 * One connection: reassembly, liveness, and dispatch through the registry.
 *
 * `listen` is here too — the socket callbacks are a few lines each and a
 * separate file would only add a hop for the reader.
 */

import type { Socket } from "bun";
import { PacketStream, type Packet, type Reader } from "./packet.ts";
import { opcodeName } from "./opcodes.ts";
import type { OutboundArgs, OutboundName, Registry } from "./ops/registry.ts";
import type { Store } from "./store.ts";
import { Result, type GameServer } from "./ops/s2c/GL_LOGIN_ACK.ts";
import type { Credentials } from "./ops/c2s/GL_LOGIN_REQ.ts";
import type { Handoff } from "./ops/c2s/PM_UDPSTART_REQ.ts";
import { Result as Admission } from "./ops/s2c/PM_UDPSTART_ACK.ts";

/** How often to poll, and how long silence may last. Server-side choices. */
export const PING_INTERVAL_MS = 15_000;
export const PING_TIMEOUT_MS = 60_000;

/**
 * The client opens two TCP connections with different handshakes:
 *
 *   login    GL_ACCOUNTCONNSUCC -> GL_LOGIN_REQ    -> GL_LOGIN_ACK
 *   channel  GL_TCPCONNSUCC     -> PM_UDPSTART_REQ -> PM_UDPSTART_ACK
 *
 * Only the greeting differs, so one Session serves both.
 */
export type Role = "login" | "channel";

export interface Config {
  role: Role;
  store: Store;
  servers: readonly GameServer[];
  ops: Registry;
  log: (message: string) => void;
  /** Shown in the channel admission reply. */
  channelName?: string;
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
  reply<N extends OutboundName>(name: N, ...args: OutboundArgs<N>): void {
    this.send(this.#config.ops.build(name, ...args));
  }

  /** Sent once on connect; it is what makes the client speak first. */
  greet(): void {
    // Branch rather than a ternary: each builder takes its own arguments, so a
    // union of names would leave the call site unable to type them.
    if (this.#config.role === "login") this.reply("GL_ACCOUNTCONNSUCC");
    else this.reply("GL_TCPCONNSUCC");
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
    const handler = this.#config.ops.handlerFor(r.opcode);
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

  /**
   * Admit a connection to the channel after login.
   *
   * The handoff claim is matched against a recent login rather than trusted as
   * an identity: its `String[24]` writer has never been located, so treating it
   * as an account key would be a guess. (docs/PACKETS.md §3.15d)
   */
  admitToChannel(handoff: Handoff): void {
    this.#config.log(`${this.#peer}: channel handoff ${JSON.stringify(handoff.identity)}`);
    this.reply("PM_UDPSTART_ACK", {
      result: Admission.Success,
      channelName: this.#config.channelName ?? "Channel 1",
    });
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
