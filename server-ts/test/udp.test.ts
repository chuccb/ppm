import { afterEach, describe, expect, test } from "bun:test";
import { Packet, decode } from "../src/packet.ts";
import { UdpRegisterServer, readUdpRegisterRequest } from "../src/udp.ts";

let server: UdpRegisterServer | undefined;

afterEach(() => {
  server?.stop();
  server = undefined;
});

describe("UDP_REGISTER 19<->20", () => {
  test("reads the complete source-proven opcode 19 body", () => {
    const reader = decode(
      new Packet(19)
        .u8(0)
        .u8(4)
        .s8(0)
        .s8(4)
        .s32(123)
        .str("alice")
        .encode(),
    );

    expect(readUdpRegisterRequest(reader)).toEqual({
      activeChannelIndex: 0,
      roomSlot: 4,
      modeEqualsTwoFlag: 0,
      depSlot: 4,
      playerId: 123,
      nickname: "alice",
    });
  });

  test("requires the observed -2 sentinel on the special branch", () => {
    const reader = decode(new Packet(19).u8(0).u8(0).s8(1).s8(0).s32(1).str("a").encode());
    expect(() => readUdpRegisterRequest(reader)).toThrow(/-2/);
  });

  test("answers opcode 19 with an encrypted empty opcode 20", async () => {
    server = await UdpRegisterServer.listen({ hostname: "127.0.0.1", port: 0, log: () => {} });

    const response = new Promise<Uint8Array>((resolve) => {
      void Bun.udpSocket({
        connect: { hostname: "127.0.0.1", port: server!.port },
        socket: {
          data(socket, bytes) {
            resolve(new Uint8Array(bytes));
            socket.close();
          },
        },
      }).then((client) => {
        client.send(
          new Packet(19)
            .u8(0)
            .u8(0)
            .s8(0)
            .s8(0)
            .s32(1)
            .str("alice")
            .encode(),
        );
      });
    });

    const reader = decode(await response);
    expect(reader.opcode).toBe(20);
    expect(reader.remaining).toBe(0);
  });
});
