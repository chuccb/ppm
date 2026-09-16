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

const MAX_SERVER_NAME_BYTES = 49; // native char[50], including NUL
const MAX_SERVER_HOST_BYTES = 15; // native char[16], including NUL
const MAX_CHANNEL_NAME_BYTES = 49; // native char[50], including NUL
const CHANNEL_GROUP_COUNT = 3; // native `for (j = 0; j < 3; ++j)`

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
 * One channel group in a server row.
 *
 * The leading raw2 is the native USERS denominator/capacity field. When it is
 * positive, the native reader consumes exactly one channel record; it is not a
 * count of records that the client loops over.
 */
export interface ChannelGroup {
  readonly maxUsers: number;
  readonly channel?: Channel;
}

/** A server row; the writer always emits the client's three group slots. */
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

/**
 * The positive `ext_count` arm is intentionally raw. Native consumes exactly
 * one tuple when the gate is positive; it does not loop `ext_count` times.
 */
export interface RawExtension {
  /** Native `ext_count` gate; any positive value enables the tuple. */
  readonly gate: number;
  readonly s32First: number;
  readonly s32Second: number;
  readonly featureFlag: number;
}

export interface Success {
  readonly userNo: number;
  readonly servers: readonly GameServer[];
  /** Opaque billing/charge UI mode, echoed back in 143. Not a player level. */
  readonly n100?: number;
  /**
   * Optional native extension projection. Production login keeps this absent
   * and therefore writes the safe `gate = 0` arm; callers that possess an
   * official extension configuration may provide the exact raw tuple.
   */
  readonly rawExtension?: RawExtension;
}

function requireS32(value: number, field: string): void {
  if (!Number.isInteger(value) || value < -0x8000_0000 || value > 0x7fff_ffff) {
    throw new RangeError(`681 ${field} must fit s32`);
  }
}

function requireU8(value: number, field: string): void {
  if (!Number.isInteger(value) || value < 0 || value > 0xff) {
    throw new RangeError(`681 ${field} must fit u8`);
  }
}

function requireS16(value: number, field: string): void {
  if (!Number.isInteger(value) || value < -0x8000 || value > 0x7fff) {
    throw new RangeError(`681 ${field} must fit s16`);
  }
}

/** Raw16 accepts either signed notation or the full unsigned bit pattern. */
function requireRaw16(value: number, field: string): void {
  if (!Number.isInteger(value) || value < -0x8000 || value > 0xffff) {
    throw new RangeError(`681 ${field} must fit raw2`);
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
    requireS32(outcome, "result");
    return new Packet(op).s32(outcome);
  }

  const { userNo, servers, n100 = 0, rawExtension } = outcome;
  requireS32(userNo, "user_no");
  requireS32(n100, "n100");
  if (servers.length > 0x7fff) throw new RangeError("681 server_count must fit s16");
  const p = new Packet(op);
  p.s32(Result.Success).s32(userNo).s32(n100);

  if (!rawExtension) {
    p.s32(0); // ext_count: safe default, no extension tuple follows
  } else {
    requireS32(rawExtension.gate, "raw extension gate");
    p.s32(rawExtension.gate);
    if (rawExtension.gate > 0) {
      requireS32(rawExtension.s32First, "raw extension s32First");
      requireS32(rawExtension.s32Second, "raw extension s32Second");
      requireU8(rawExtension.featureFlag, "raw extension featureFlag");
      p.s32(rawExtension.s32First).s32(rawExtension.s32Second).u8(rawExtension.featureFlag);
    }
  }

  p.s16(servers.length);
  for (const server of servers) {
    if (server.name.length > MAX_SERVER_NAME_BYTES) {
      throw new RangeError("681 server name must fit native char[50]");
    }
    if (server.host.length > MAX_SERVER_HOST_BYTES) {
      throw new RangeError("681 server host must fit native char[16]");
    }
    if (!Number.isInteger(server.port) || server.port < 0 || server.port > 0xffff) {
      throw new RangeError("681 server_port must fit u16");
    }
    requireU8(server.flag, "server flag");

    // These are raw2 fields; native domain/signedness is unresolved.
    requireRaw16(server.serverId, "server_id");
    requireRaw16(server.group, "group");
    p.s16(server.serverId);
    p.str(server.name); // native char[50]
    p.str(server.host); // native char[16]
    // The reader gets raw2, but the selected-server consumer passes these bits
    // to a Winsock u_short endpoint port.
    p.u16(server.port);
    p.u8(server.flag);
    p.s16(server.group);

    for (let index = 0; index < CHANNEL_GROUP_COUNT; index++) {
      const group = server.channelGroups[index];
      if (group === undefined) {
        p.s16(0);
        continue;
      }

      requireS16(group.maxUsers, "channel max_users");
      p.s16(group.maxUsers);
      if (group.maxUsers <= 0) continue;

      const channel = group.channel;
      if (channel === undefined) {
        throw new RangeError("681 positive channel group needs a channel body");
      }
      if (channel.name.length > MAX_CHANNEL_NAME_BYTES) {
        throw new RangeError("681 channel name must fit native char[50]");
      }
      requireU8(channel.type, "channel type");
      requireU8(channel.flag, "channel flag");
      requireS16(channel.currentUsers, "channel current_users");
      p.u8(channel.type);
      p.str(channel.name);
      p.s16(channel.currentUsers);
      p.u8(channel.flag);
      if (channel.type === 3) {
        const extra = channel.extra ?? 0;
        requireU8(extra, "channel extra");
        p.u8(extra);
      }
    }
  }

  return p.s32(0).s32(0); // billing_first, billing_second
}
