/**
 * One TCP connection: reassembly, liveness, dispatch, and sending.
 *
 * Nothing here is packet-specific — each opcode is handled entirely by its own
 * module under `src/ops/c2s/`. `listen` lives here too, since its callbacks are
 * a few lines each and a separate file would only add a hop.
 */

import type { Socket } from "bun";
import { PacketStream, type Packet, type Reader } from "./packet.ts";
import { opcodeName } from "./opcodes.ts";
import { build, handlerFor, type OutboundArgs, type OutboundName } from "./ops/registry.ts";
import type { Store } from "./store.ts";
import type { GameServer } from "./ops/s2c/GL_LOGIN_ACK.ts";

/** How often to poll, and how long silence may last. Server-side choices. */
export const PING_INTERVAL_MS = 15_000;
export const PING_TIMEOUT_MS = 60_000;

/**
 * The client makes two connections, with mirrored handshakes:
 *
 *   login    GL_ACCOUNTCONNSUCC -> GL_LOGIN_REQ    -> GL_LOGIN_ACK
 *   channel  GL_TCPCONNSUCC     -> PM_UDPSTART_REQ -> PM_UDPSTART_ACK
 *
 * Only the greeting differs, so one Connection serves both.
 */
export type Role = "login" | "channel";

export interface Config {
  role: Role;
  store: Store;
  servers: readonly GameServer[];
  log: (message: string) => void;
  /** Reported in the channel admission reply. */
  channelName?: string;
}

export class Connection {
  readonly config: Config;
  /** Set by GL_LOGIN_REQ once credentials check out. */
  accountId: number | null = null;

  readonly #socket: Socket<Connection>;
  readonly #stream = new PacketStream();
  readonly #peer: string;
  /** Serialises dispatch so replies keep the order the requests arrived in. */
  #queue: Promise<void> = Promise.resolve();
  #heartbeat: ReturnType<typeof setInterval> | null = null;
  #lastSeen = Date.now();

  constructor(socket: Socket<Connection>, config: Config) {
    this.#socket = socket;
    this.config = config;
    this.#peer = socket.remoteAddress;
  }

  log(message: string): void {
    this.config.log(`${this.#peer}: ${message}`);
  }

  send(packet: Packet): void {
    this.#socket.write(packet.encode());
  }

  /** Build an outbound packet by name and send it. Names are checked by tsc. */
  reply<N extends OutboundName>(name: N, ...args: OutboundArgs<N>): void {
    this.send(build(name, ...args));
  }

  /** Sent once on connect; it is what makes the client speak first. */
  greet(): void {
    // Branch rather than a ternary: each builder takes its own arguments, so a
    // union of names would leave the call site unable to type them.
    if (this.config.role === "login") this.reply("GL_ACCOUNTCONNSUCC");
    else this.reply("GL_TCPCONNSUCC");

    this.#heartbeat = setInterval(() => {
      if (Date.now() - this.#lastSeen > PING_TIMEOUT_MS) {
        this.log("no ping reply, closing");
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
    const handler = handlerFor(r.opcode);
    if (!handler) {
      // The client's own dispatcher silently ignores unknown opcodes. Mirror
      // that, but log so coverage gaps stay visible.
      this.log(`unhandled ${opcodeName(r.opcode)} (${r.opcode})`);
      return;
    }
    try {
      await handler(r, this);
    } catch (error) {
      this.#fail(opcodeName(r.opcode), error);
    }
  }

  #fail(what: string, error: unknown): void {
    this.log(`${what} — ${(error as Error).message}, closing`);
    this.#socket.end();
  }
}

export function listen(config: Config & { hostname: string; port: number }) {
  return Bun.listen<Connection>({
    hostname: config.hostname,
    port: config.port,
    socket: {
      open(socket) {
        socket.data = new Connection(socket, config);
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
