/**
 * UDP 工作階段註冊交換:private op19 `UDP_REGISTER_REQ`〔推定〕進、空體
 * op20 `UDP_REGISTER_ACK`〔推定〕出(docs/PACKETS.md 命名總表與 D 節;op20
 * 設 `byte_1D0CFE7`=1,是所有戰鬥 UDP 流量的總閘)。
 *
 * op19 wire grammar(client builder `sub_596670`;500ms 重送,>100 停):
 *   u8  active_channel_index  `*sub_417D00()`(由 142 PM_CONNECT_ACK 帶入)
 *   u8  roomSlot              CMyData+5
 *   s8  mode-equals-two flag  native `(n2==2)`;boolean
 *   u8  depSlot               mode-2 分支上為 native -2→0xFE 哨兵
 *   s32 playerId              CMyData+844;client self-report,非 server 權威
 *   str nickname              CMyData+896;同上
 *
 * UDP uses the same 8-byte header and AES-CFB framing as TCP, but never uses
 * TCP LZ compression (`sub_595980`, `sub_595A60`).
 */

import { Packet, decode, type Reader } from "./packet.ts";

export const UDP_REGISTER_REQ_OPCODE = 19;
export const UDP_REGISTER_ACK_OPCODE = 20;

export interface UdpRegisterRequest {
  readonly activeChannelIndex: number;
  readonly roomSlot: number;
  readonly modeEqualsTwoFlag: number;
  readonly depSlot: number;
  /** Client self-report from CMyData+844, not server authority. */
  readonly playerId: number;
  /** Client self-report from CMyData+896, not server authority. */
  readonly nickname: string;
}

export function readUdpRegisterRequest(r: Reader): UdpRegisterRequest {
  if (r.opcode !== UDP_REGISTER_REQ_OPCODE) {
    throw new RangeError(`expected UDP_REGISTER_REQ opcode ${UDP_REGISTER_REQ_OPCODE}, got ${r.opcode}`);
  }

  const activeChannelIndex = r.u8();
  const roomSlot = r.u8();
  const modeEqualsTwoFlag = r.s8();
  const depSlot = r.s8();
  const playerId = r.s32();

  if (modeEqualsTwoFlag !== 0 && modeEqualsTwoFlag !== 1) {
    throw new RangeError("UDP_REGISTER_REQ has a non-boolean (n2==2) flag");
  }
  if (modeEqualsTwoFlag === 1 && depSlot !== -2) {
    throw new RangeError("UDP_REGISTER_REQ is missing its native -2 depSlot sentinel");
  }
  if (r.remaining < 1) throw new RangeError("UDP_REGISTER_REQ has no nickname");

  const nickname = r.str(r.remaining - 1);
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing UDP bytes`);

  return { activeChannelIndex, roomSlot, modeEqualsTwoFlag, depSlot, playerId, nickname };
}

export interface UdpRegisterServerConfig {
  readonly hostname: string;
  readonly port: number;
  readonly log: (message: string) => void;
}

export class UdpRegisterServer {
  readonly #socket: Bun.udp.Socket<"buffer">;
  readonly #log: (message: string) => void;

  private constructor(socket: Bun.udp.Socket<"buffer">, log: (message: string) => void) {
    this.#socket = socket;
    this.#log = log;
  }

  static async listen(config: UdpRegisterServerConfig): Promise<UdpRegisterServer> {
    let server: UdpRegisterServer | undefined;
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
    server = new UdpRegisterServer(socket, config.log);
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
      if (reader.opcode !== UDP_REGISTER_REQ_OPCODE) {
        this.#log(`udp ${address}:${port}: ignored opcode ${reader.opcode}`);
        return;
      }

      const request = readUdpRegisterRequest(reader);
      this.#log(
        `udp ${address}:${port}: register ${request.activeChannelIndex}/${request.roomSlot}` +
          ` player ${request.playerId} ${JSON.stringify(request.nickname)}`,
      );
      const sent = this.#socket.send(new Packet(UDP_REGISTER_ACK_OPCODE).encode(), port, address);
      if (!sent) this.#log(`udp ${address}:${port}: register ack was back-pressured`);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      this.#log(`udp ${address}:${port}: ignored malformed datagram — ${message}`);
    }
  }
}
