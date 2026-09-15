/**
 * Per-connection state and dispatch.
 *
 * One `Session` owns one TCP socket, its reassembly buffer, and its login
 * state. Dispatch is a map from opcode to handler so unregistered opcodes can
 * be ignored the way the client's own dispatcher does (`default: return`).
 */

import { FrameStream, encodeFrame } from "../codec/frame.ts";
import type { PacketReader, PacketWriter } from "../codec/packet.ts";
import { Op, opcodeName } from "../codec/opcodes.ts";
import type { Store } from "../db/schema.ts";
import {
  type ServerEntry,
  buildAccountConnSucc,
  handleLogin,
  parseLoginRequest,
} from "../handlers/login.ts";

export interface SessionSink {
  write(bytes: Uint8Array): void;
  close(): void;
  readonly remote: string;
}

export interface SessionConfig {
  readonly store: Store;
  readonly servers: readonly ServerEntry[];
  readonly log: (message: string) => void;
}

export class Session {
  readonly #stream = new FrameStream();
  readonly #sink: SessionSink;
  readonly #config: SessionConfig;

  /** 694 must be sent exactly once; resending it loops the client on 682. */
  #greeted = false;
  #accountId: number | null = null;

  constructor(sink: SessionSink, config: SessionConfig) {
    this.#sink = sink;
    this.#config = config;
  }

  get accountId(): number | null {
    return this.#accountId;
  }

  send(packet: PacketWriter): void {
    this.#sink.write(encodeFrame(packet));
  }

  /** Sent once on connect: it is what makes the client send 682. */
  greet(): void {
    if (this.#greeted) return;
    this.#greeted = true;
    this.send(buildAccountConnSucc());
  }

  onData(chunk: Uint8Array): void {
    this.#stream.push(chunk);
    let readers: PacketReader[];
    try {
      readers = [...this.#stream.drain()];
    } catch (error) {
      this.#config.log(
        `${this.#sink.remote}: malformed frame, closing — ${(error as Error).message}`,
      );
      this.#sink.close();
      return;
    }
    for (const reader of readers) void this.#dispatch(reader);
  }

  async #dispatch(reader: PacketReader): Promise<void> {
    try {
      switch (reader.opcode) {
        case Op.GL_LOGIN_REQ:
          await this.#onLogin(reader);
          return;
        case Op.GT_PING_REQ:
          // Echo shape is not yet evidenced; ignore rather than invent one.
          return;
        default:
          // The client's dispatcher silently ignores unknown opcodes; mirror it,
          // but log so gaps in coverage are visible.
          this.#config.log(
            `${this.#sink.remote}: unhandled ${opcodeName(reader.opcode)} (${reader.opcode})`,
          );
          return;
      }
    } catch (error) {
      this.#config.log(
        `${this.#sink.remote}: ${opcodeName(reader.opcode)} failed — ${(error as Error).message}`,
      );
      this.#sink.close();
    }
  }

  async #onLogin(reader: PacketReader): Promise<void> {
    const request = parseLoginRequest(reader);
    const outcome = await handleLogin(this.#config.store, request, this.#config.servers);
    this.#accountId = outcome.accountId;
    this.#config.log(
      `${this.#sink.remote}: login ${request.account} -> ` +
        (outcome.accountId === null ? "rejected" : `account ${outcome.accountId}`),
    );
    this.send(outcome.packet);
  }
}
