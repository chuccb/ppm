/**
 * One connection: its reassembly buffer, its login state, and dispatch.
 *
 * `listen` is here too because the socket callbacks are three lines each and
 * splitting them into their own file only adds a hop for the reader.
 */

import type { Socket } from "bun";
import { Packet, PacketStream, type Reader } from "./packet.ts";
import { Op, opcodeName } from "./opcodes.ts";
import type { Store } from "./store.ts";
import { GL_ACCOUNTCONNSUCC, type GameServer, login, readGL_LOGIN_REQ } from "./login.ts";

export interface Config {
  store: Store;
  servers: readonly GameServer[];
  log: (message: string) => void;
}

class Session {
  readonly #stream = new PacketStream();
  readonly #socket: Socket<Session>;
  readonly #config: Config;
  readonly #peer: string;
  accountId: number | null = null;

  constructor(socket: Socket<Session>, config: Config) {
    this.#socket = socket;
    this.#config = config;
    this.#peer = socket.remoteAddress;
  }

  send(packet: Packet): void {
    this.#socket.write(packet.encode());
  }

  /**
   * Sent once on connect. 694 both negotiates the compression threshold and
   * triggers the client's 682 builder, so sending it again after login makes
   * the client resend credentials forever.
   */
  greet(): void {
    this.send(GL_ACCOUNTCONNSUCC());
  }

  receive(chunk: Uint8Array): void {
    this.#stream.push(chunk);
    let packets: Reader[];
    try {
      packets = [...this.#stream.drain()];
    } catch (error) {
      this.#fail("malformed frame", error);
      return;
    }
    for (const packet of packets) void this.#dispatch(packet);
  }

  async #dispatch(r: Reader): Promise<void> {
    try {
      switch (r.opcode) {
        case Op.GL_LOGIN_REQ:
          return await this.#onGL_LOGIN_REQ(r);
        case Op.GT_PING_REQ:
          return; // reply shape not yet evidenced; ignore rather than invent one
        default:
          // The client's own dispatcher silently ignores unknown opcodes. Mirror
          // that, but log so coverage gaps stay visible.
          this.#config.log(`${this.#peer}: unhandled ${opcodeName(r.opcode)} (${r.opcode})`);
          return;
      }
    } catch (error) {
      this.#fail(opcodeName(r.opcode), error);
    }
  }

  async #onGL_LOGIN_REQ(r: Reader): Promise<void> {
    const request = readGL_LOGIN_REQ(r);
    const { reply, accountId } = await login(this.#config.store, request, this.#config.servers);
    this.accountId = accountId;
    this.#config.log(
      `${this.#peer}: login ${request.account} -> ${accountId ?? "rejected"}`,
    );
    this.send(reply);
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
        config.log(`${socket.remoteAddress}: disconnected`);
      },
      error(socket, error) {
        config.log(`${socket.remoteAddress}: socket error — ${error.message}`);
      },
    },
  });
}
