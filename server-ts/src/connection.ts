/**
 * One TCP session: frame reassembly, handshake state, ordered dispatch and
 * liveness. Packet field order stays in `src/ops`; this file only decides
 * which state may reach a handler.
 */

import type { Socket } from "bun";
import { ChannelAdmissionRegistry } from "./admission.ts";
import { PacketStream, type Packet, type Reader } from "./packet.ts";
import { opcodeFor, opcodeName } from "./opcodes.ts";
import { build, handlerFor, type OutboundArgs, type OutboundName } from "./ops/registry.ts";
import type { Store } from "./store.ts";
import type { GameServer } from "./ops/s2c/GL_LOGIN_ACK.ts";
import type { Type3Tail } from "./ops/s2c/GC_ENTERCHANNEL_ACK.ts";

export const PING_INTERVAL_MS = 15_000;
export const PING_TIMEOUT_MS = 60_000;

const PING_REQ = opcodeFor("GT_PING_REQ");
const LOGIN_REQ = opcodeFor("GL_LOGIN_REQ");
const HANDOFF_REQ = opcodeFor("PM_UDPSTART_REQ");
const ENTER_CHANNEL_REQ = opcodeFor("GC_ENTERCHANNEL_REQ");

export type Role = "login" | "channel";

/** Values needed by the one channel advertised in GL_LOGIN_ACK. */
export interface ChannelConfig {
  readonly name: string;
  readonly group: number;
  /** Native wire `active_channel_index` (142/196). */
  readonly activeChannelIndex: number;
  /** Native wire `channel_id` (196). */
  readonly channelId: number;
  readonly endpoint: {
    readonly host: string;
    readonly port: number;
  };
  /** Native wire `channel_type` / 681 `ch_type`. */
  readonly channelType: number;
  readonly type3Tail?: Type3Tail;
  readonly endpointOpaque: number;
  readonly clientFlags: number;
  readonly clientDefault: number;
}

export interface Config {
  readonly role: Role;
  readonly store: Store;
  readonly servers: readonly GameServer[];
  readonly log: (message: string) => void;
  readonly admissions: ChannelAdmissionRegistry;
  readonly channel: ChannelConfig;
  readonly admissionLifetimeMs: number;
}

/** Bun reports a TCP peer as `host:port`; admission is bound to the host. */
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

export class Connection {
  readonly config: Config;
  accountId: number | null = null;
  channelEntryCompleted = false;

  readonly #socket: Socket<Connection>;
  readonly #peer: string;
  readonly #remoteIp: string;
  readonly #stream = new PacketStream();
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

  /** The greeting makes the client start the matching handshake. */
  greet(): void {
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

    // Password verification is async. Chain handlers so replies cannot pass
    // each other on the wire; the client pairs several replies by arrival order.
    for (const packet of packets) {
      this.#queue = this.#queue.then(() => this.#dispatch(packet));
    }
  }

  async #dispatch(reader: Reader): Promise<void> {
    const name = opcodeName(reader.opcode);

    if (this.config.role === "login") {
      if (reader.opcode === LOGIN_REQ && this.authenticated) {
        this.log("repeated GL_LOGIN_REQ after successful login — ignored");
        return;
      }
      if (reader.opcode !== PING_REQ && reader.opcode !== LOGIN_REQ) {
        this.log(`rejected ${name} on login listener`);
        return;
      }
    } else if (reader.opcode === LOGIN_REQ) {
      this.log("rejected GL_LOGIN_REQ on channel listener");
      return;
    } else if (!this.channelEntryCompleted) {
      const handshake = reader.opcode === PING_REQ ||
        reader.opcode === HANDOFF_REQ ||
        reader.opcode === ENTER_CHANNEL_REQ;
      if (!handshake) {
        this.log(`rejected ${name} before successful channel entry`);
        return;
      }
    }

    const handler = handlerFor(reader.opcode);
    if (!handler) {
      this.log(`unhandled ${name} (${reader.opcode})`);
      return;
    }

    try {
      await handler(reader, this);
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
