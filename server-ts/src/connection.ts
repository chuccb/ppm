/**
 * One TCP connection: reassembly, liveness, dispatch, and sending.
 *
 * Nothing here is packet-specific — each opcode is handled entirely by its own
 * module under `src/ops/c2s/`. `listen` lives here too, since its callbacks are
 * a few lines each and a separate file would only add a hop.
 */

import type { Socket } from "bun";
import { ChannelAdmissionRegistry } from "./admission.ts";
import { PacketStream, type Packet, type Reader } from "./packet.ts";
import { opcodeFor, opcodeName } from "./opcodes.ts";
import { build, handlerFor, type OutboundArgs, type OutboundName } from "./ops/registry.ts";
import type { Store } from "./store.ts";
import type { GameServer } from "./ops/s2c/GL_LOGIN_ACK.ts";

/** How often to poll, and how long silence may last. Server-side choices. */
export const PING_INTERVAL_MS = 15_000;
export const PING_TIMEOUT_MS = 60_000;

const GT_PING_REQ = opcodeFor("GT_PING_REQ");
const GL_LOGIN_REQ = opcodeFor("GL_LOGIN_REQ");
const PM_UDPSTART_REQ = opcodeFor("PM_UDPSTART_REQ");
const GC_ENTERCHANNEL_REQ = opcodeFor("GC_ENTERCHANNEL_REQ");

/** Bun reports a TCP peer as `host:port`; admission is bound to the host only. */
function remoteIp(address: string): string {
  if (address.startsWith("[")) {
    const end = address.indexOf("]");
    if (end > 1) return address.slice(1, end);
  }

  const separator = address.lastIndexOf(":");
  const port = address.slice(separator + 1);
  return separator >= 0 && /^\d+$/.test(port)
    ? address.slice(0, separator)
    : address;
}

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
  readonly role: Role;
  readonly store: Store;
  readonly servers: readonly GameServer[];
  readonly log: (message: string) => void;
  /** Shared between the login and channel listeners. */
  readonly admissions: ChannelAdmissionRegistry;
  /** Reported in the channel admission reply and the 681 channel list. */
  readonly channelName: string;
  /** The one advertised group/channel accepted by this single-channel host. */
  readonly group?: number;
  readonly channel?: number;
  readonly channelId?: number;
  /** Endpoint copied into the successful 196 tail. */
  readonly udpHost?: string;
  readonly udpPort?: number;
  /** Opaque, source-proven values in the successful 196 tail. */
  readonly channelType?: number;
  readonly endpointOpaque?: number;
  readonly clientFlags?: number;
  readonly clientDefault?: number;
  /** A 681 admission expires if the client never opens its channel socket. */
  readonly admissionLifetimeMs?: number;
}

export class Connection {
  readonly config: Config;
  /** Set by GL_LOGIN_REQ or PM_UDPSTART_REQ once the account is known. */
  accountId: number | null = null;
  /** True only after a successful 195 → 196 channel selection. */
  channelEntryCompleted = false;

  readonly #socket: Socket<Connection>;
  readonly #stream = new PacketStream();
  readonly #peer: string;
  readonly #remoteIp: string;
  /** Serialises dispatch so replies keep the order the requests arrived in. */
  #queue: Promise<void> = Promise.resolve();
  #heartbeat: ReturnType<typeof setInterval> | null = null;
  #lastSeen = Date.now();

  constructor(socket: Socket<Connection>, config: Config) {
    this.#socket = socket;
    this.config = config;
    this.#peer = socket.remoteAddress;
    this.#remoteIp = remoteIp(this.#peer);
  }

  get remoteIp(): string {
    return this.#remoteIp;
  }

  get authenticated(): boolean {
    return this.accountId !== null;
  }

  log(message: string): void {
    this.config.log(`${this.#peer}: ${message}`);
  }

  send(packet: Packet): void {
    this.#socket.write(packet.encode());
  }

  /** Build an outbound packet by name and send it. Names and args are typed;
   * files are checked at startup. */
  reply<N extends OutboundName>(name: N, ...args: OutboundArgs<N>): void {
    this.send(build(name, ...args));
  }

  bindAccount(accountId: number): void {
    if (!Number.isSafeInteger(accountId) || accountId <= 0) {
      throw new RangeError("accountId must be a positive safe integer");
    }
    if (this.authenticated) throw new Error("connection is already authenticated");
    this.accountId = accountId;
  }

  completeChannelEntry(): void {
    if (this.config.role !== "channel") throw new Error("only a channel connection can enter");
    if (!this.authenticated) throw new Error("channel entry requires an authenticated handoff");
    if (this.channelEntryCompleted) throw new Error("channel entry already completed");
    this.channelEntryCompleted = true;
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
    const name = opcodeName(r.opcode);

    if (this.config.role === "login") {
      if (r.opcode === GL_LOGIN_REQ && this.authenticated) {
        this.log("repeated GL_LOGIN_REQ after successful login — ignored");
        return;
      }
      if (r.opcode !== GT_PING_REQ && r.opcode !== GL_LOGIN_REQ) {
        this.log(`rejected ${name} on login listener`);
        return;
      }
    } else if (r.opcode === GL_LOGIN_REQ) {
      this.log("rejected GL_LOGIN_REQ on channel listener");
      return;
    } else if (!this.channelEntryCompleted) {
      if (
        r.opcode !== GT_PING_REQ &&
        r.opcode !== PM_UDPSTART_REQ &&
        r.opcode !== GC_ENTERCHANNEL_REQ
      ) {
        this.log(`rejected ${name} before successful channel entry`);
        return;
      }
    }

    const handler = handlerFor(r.opcode);
    if (!handler) {
      // The client's own dispatcher silently ignores unknown opcodes. Mirror
      // that, but log so coverage gaps stay visible.
      this.log(`unhandled ${name} (${r.opcode})`);
      return;
    }
    try {
      await handler(r, this);
    } catch (error) {
      this.#fail(name, error);
    }
  }

  #fail(what: string, error: unknown): void {
    const message = error instanceof Error ? error.message : String(error);
    this.log(`${what} — ${message}, closing`);
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
