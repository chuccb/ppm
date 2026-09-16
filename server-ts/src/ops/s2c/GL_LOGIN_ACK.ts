/**
 * Login result, in the client's read order.
 *
 * The client reads a raw 4-byte result word but branches on its low byte only;
 * a full s32 is written either way. Anything beyond that word is present only
 * on success. (docs/PACKETS.md §1.4)
 */

import { Packet } from "../../packet.ts";

/** Named low-byte codes observed in the native UI branch. */
export const Result = {
  GeneralFailure: 0,
  Success: 1,
  BadCredentials: 2,
  Banned: 200,
  Maintenance: 201,
  AlreadyOnline: 210,
} as const;

/** The native result is a raw s32; the client branches on its low byte only. */
export type Result = number;

/** One selectable channel in a group. */
export interface Channel {
  readonly type: number;
  readonly name: string;
  /** Native raw2 field shown as the USERS numerator; not a network port. */
  readonly currentUsers: number;
  /** Native `ch_flag`; its domain is not established here. */
  readonly flag: number;
  readonly extra?: number;
}

/**
 * One of the three fixed channel groups in a server row.
 *
 * The leading raw2 is the native USERS denominator/capacity field. When it is
 * positive, the native reader consumes exactly one channel record; it is not a
 * count of records that the client loops over.
 */
export interface ChannelGroup {
  readonly maxUsers: number;
  readonly channel?: Channel;
}

/** A server row. The client expects exactly three channel groups. */
export interface GameServer {
  readonly serverId: number;
  readonly name: string;
  readonly host: string;
  /** Native selector TCP endpoint port; sub_58AD90 passes it as u_short. */
  readonly port: number;
  /** Native `flag`; its domain is not established here. */
  readonly flag: number;
  readonly group: number;
  readonly channelGroups: readonly ChannelGroup[];
}

export interface Success {
  readonly userNo: number;
  readonly servers: readonly GameServer[];
  /** Opaque billing/charge UI mode, echoed back in 143. Not a player level. */
  readonly n100?: number;
}

function requireS16(value: number, field: string): void {
  if (!Number.isSafeInteger(value) || value < -0x8000 || value > 0x7fff) {
    throw new RangeError(`681 ${field} must fit s16`);
  }
}

function requireNonNegativeS16(value: number, field: string): void {
  if (!Number.isSafeInteger(value) || value < 0 || value > 0x7fff) {
    throw new RangeError(`681 ${field} must fit a non-negative s16`);
  }
}

function requireU16(value: number, field: string): void {
  if (!Number.isSafeInteger(value) || value < 0 || value > 0xffff) {
    throw new RangeError(`681 ${field} must fit u16`);
  }
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
export default function GL_LOGIN_ACK(op: number, outcome: Result | Success): Packet {
  if (typeof outcome === "number") {
    if (!Number.isSafeInteger(outcome) || outcome < -0x8000_0000 || outcome > 0x7fff_ffff) {
      throw new RangeError("681 result must fit s32");
    }
    return new Packet(op).s32(outcome);
  }

  const { userNo, servers, n100 = 0 } = outcome;
  if (!Number.isSafeInteger(userNo) || userNo < -0x8000_0000 || userNo > 0x7fff_ffff) {
    throw new RangeError("681 user_no must fit s32");
  }
  if (!Number.isSafeInteger(n100) || n100 < -0x8000_0000 || n100 > 0x7fff_ffff) {
    throw new RangeError("681 n100 must fit the s32 echoed by 143");
  }
  if (servers.length > 0x7fff) throw new RangeError("681 server_count must fit s16");
  const p = new Packet(op);
  p.s32(Result.Success).s32(userNo).s32(n100);
  p.s32(0); // ext_count: 0 = no netcafe feature extension

  p.s16(servers.length);
  for (const server of servers) {
    if (server.channelGroups.length !== 3) {
      throw new RangeError("each server must declare exactly three channel groups");
    }
    if (server.name.length > 49) {
      throw new RangeError("681 server name must fit native char[50]");
    }
    if (server.host.length > 15) {
      throw new RangeError("681 server host must fit native char[16]");
    }
    requireS16(server.serverId, "server_id");
    requireU16(server.port, "server_port");
    requireS16(server.group, "server group");

    p.s16(server.serverId);
    p.str(server.name); // native char[50]
    p.str(server.host); // native char[16]
    // The reader gets raw2, but the selected-server consumer passes these bits
    // to a Winsock u_short endpoint port.
    p.u16(server.port);
    p.u8(server.flag);
    p.s16(server.group);

    for (const group of server.channelGroups) {
      requireNonNegativeS16(group.maxUsers, "channel max_users");
      const channel = group.channel;
      if ((group.maxUsers > 0) !== (channel !== undefined)) {
        throw new RangeError("681 channel group needs one channel exactly when max_users is positive");
      }
      p.s16(group.maxUsers);
      if (!channel) continue;
      if (channel.name.length > 49) {
        throw new RangeError("681 channel name must fit native char[50]");
      }
      requireNonNegativeS16(channel.currentUsers, "channel current_users");
      p.u8(channel.type);
      p.str(channel.name);
      p.s16(channel.currentUsers);
      p.u8(channel.flag);
      if (channel.type === 3) p.u8(channel.extra ?? 0);
    }
  }

  return p.s32(0).s32(0); // billing_first, billing_second
}
