/**
 * The directly evidenced private UDP control exchange: opcode 19 in, empty
 * opcode 20 out.
 *
 * UDP uses the same 8-byte header and AES-CFB framing as TCP, but never uses
 * TCP LZ compression (`sub_595980`, `sub_595A60`). The reported channel,
 * slot, player id, and nickname are client data, not server authority.
 */

import { Packet, decode, type Reader } from "./packet.ts";

const REQUEST_OPCODE = 19;
const COMPLETION_OPCODE = 20;

export interface UdpControlRequest {
  readonly activeChannelIndex: number;
  readonly currentRoomSlot: number;
  readonly sourceModeEqualsTwoFlag: number;
  readonly sourceDependentSlot: number;
  readonly clientReportedPlayerId: number;
  readonly localNickname: string;
}

export function readUdpControlRequest(r: Reader): UdpControlRequest {
  if (r.opcode !== REQUEST_OPCODE) {
    throw new RangeError(`expected UDP opcode ${REQUEST_OPCODE}, got ${r.opcode}`);
  }

  const activeChannelIndex = r.u8();
  const currentRoomSlot = r.u8();
  const sourceModeEqualsTwoFlag = r.s8();
  const sourceDependentSlot = r.s8();
  const clientReportedPlayerId = r.s32();

  if (sourceModeEqualsTwoFlag !== 0 && sourceModeEqualsTwoFlag !== 1) {
    throw new RangeError("UDP opcode 19 has a non-boolean source-mode flag");
  }
  if (sourceModeEqualsTwoFlag === 1 && sourceDependentSlot !== -2) {
    throw new RangeError("UDP opcode 19 is missing its native -2 sentinel");
  }
  if (r.remaining < 1) throw new RangeError("UDP opcode 19 has no nickname");

  const localNickname = r.str(r.remaining - 1);
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing UDP bytes`);

  return {
    activeChannelIndex,
    currentRoomSlot,
    sourceModeEqualsTwoFlag,
    sourceDependentSlot,
    clientReportedPlayerId,
    localNickname,
  };
}

export interface UdpControlServerConfig {
  readonly hostname: string;
  readonly port: number;
  readonly log: (message: string) => void;
}

export class UdpControlServer {
  readonly #socket: Bun.udp.Socket<"buffer">;
  readonly #log: (message: string) => void;

  private constructor(socket: Bun.udp.Socket<"buffer">, log: (message: string) => void) {
    this.#socket = socket;
    this.#log = log;
  }

  static async listen(config: UdpControlServerConfig): Promise<UdpControlServer> {
    let server: UdpControlServer | undefined;
    const socket = await Bun.udpSocket<"buffer">({
      hostname: config.hostname,
      port: config.port,
      binaryType: "buffer",
      socket: {
        data(_socket, bytes, port, address) {
          server?.receive(bytes, port, address);
        },
      },
    });
    server = new UdpControlServer(socket, config.log);
    return server;
  }

  get port(): number {
    return this.#socket.port;
  }

  stop(): void {
    this.#socket.close();
  }

  receive(bytes: Uint8Array, port: number, address: string): void {
    try {
      const reader = decode(bytes);
      if (reader.opcode !== REQUEST_OPCODE) {
        this.#log(`udp ${address}:${port}: ignored opcode ${reader.opcode}`);
        return;
      }

      const request = readUdpControlRequest(reader);
      this.#log(
        `udp ${address}:${port}: control ${request.activeChannelIndex}/${request.currentRoomSlot}` +
          ` player ${request.clientReportedPlayerId} ${JSON.stringify(request.localNickname)}`,
      );
      const sent = this.#socket.send(new Packet(COMPLETION_OPCODE).encode(), port, address);
      if (!sent) this.#log(`udp ${address}:${port}: completion was back-pressured`);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      this.#log(`udp ${address}:${port}: ignored malformed datagram — ${message}`);
    }
  }
}
