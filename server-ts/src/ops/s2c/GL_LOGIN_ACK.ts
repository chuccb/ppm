/**
 * Login result, in the client's read order.
 *
 * The client reads a raw 4-byte result word but branches on its low byte only;
 * a full s32 is written either way. Anything beyond that word is present only
 * on success. (docs/PACKETS.md §1.4)
 */

import { Packet } from "../../packet.ts";

/** Low byte of the result word; only the documented codes are modelled. */
export const Result = {
  GeneralFailure: 0,
  Success: 1,
  BadCredentials: 2,
  Banned: 200,
  Maintenance: 201,
  AlreadyOnline: 210,
} as const;

export type Result = (typeof Result)[keyof typeof Result];

/** One channel in a group. `extra` is read only when `type` is 3. */
export interface Channel {
  type: number;
  name: string;
  port: number;
  flag: number;
  extra?: number;
}

/** A server row. The client expects exactly three channel groups. */
export interface GameServer {
  id: number;
  name: string;
  host: string;
  port: number;
  flag: number;
  group: number;
  channelGroups: readonly (readonly Channel[])[];
}

export interface Success {
  userNo: number;
  servers: readonly GameServer[];
  /** Opaque billing/charge UI mode, echoed back in 143. Not a player level. */
  chargeMode?: number;
}

/**
 * Two forms, distinguished by what you pass:
 *
 *   build("GL_LOGIN_ACK", Result.BadCredentials)          just the result word
 *   build("GL_LOGIN_ACK", { userNo, servers })            the full payload
 *
 * A failure really is only that word on the wire — the client branches on its
 * low byte before reading anything else.
 */
export default function (op: number, outcome: Result | Success): Packet {
  if (typeof outcome === "number") return new Packet(op).s32(outcome);

  const { userNo, servers, chargeMode = 0 } = outcome;
  const p = new Packet(op);
  p.s32(Result.Success).s32(userNo).s32(chargeMode);
  p.s32(0); // ext_count: 0 = no netcafe feature extension

  p.s16(servers.length);
  for (const server of servers) {
    if (server.channelGroups.length !== 3) {
      throw new RangeError("each server must declare exactly three channel groups");
    }
    p.s16(server.id);
    p.str(server.name); // native char[50]
    p.str(server.host); // native char[16]
    p.s16(server.port); // 16-bit pattern reused as u_short, so >32767 is fine
    p.u8(server.flag);
    p.s16(server.group);

    for (const group of server.channelGroups) {
      p.s16(group.length);
      const channel = group[0]; // the client reads at most one, whatever the count
      if (!channel) continue;
      p.u8(channel.type);
      p.str(channel.name);
      p.s16(channel.port);
      p.u8(channel.flag);
      if (channel.type === 3) p.u8(channel.extra ?? 0);
    }
  }

  return p.s32(0).s32(0); // billing_first, billing_second
}
