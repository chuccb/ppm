/**
 * Login result, in the client's read order.
 *
 * The client reads a raw 4-byte result word but branches on its low byte only;
 * a full s32 is written either way. Anything beyond that word is present only
 * on success. (docs/PACKETS.md §1.4)
 */

import { Packet } from "../../packet.ts";

/**
 * Named low-byte codes observed in the native UI branch. The table mirrors
 * the recovered 0x43E651 failure switch one-to-one; names carry the client-
 * side message each code triggers, with the resource id when that is the
 * only thing native proves. Codes the docs leave semantically unknown keep
 * their message id as the name instead of inventing a business meaning.
 */
export const Result = {
  GeneralFailure: 0,
  Success: 1,
  BadCredentials: 2,
  /** 0xC8: account-suspension popup (resource 0x70 via dialog helper). */
  Banned: 200,
  /** 0xC9: maintenance (0x23E). */
  Maintenance: 201,
  /** 0xCA: maintenance, second id (0x23E). */
  Maintenance2: 202,
  /** 0xCB: duplicate login (0xA4), same message as 210. */
  AlreadyOnline: 203,
  /** 0xCC: anti-addiction / play-time restriction (0x316). */
  AntiAddiction: 204,
  /** 0xCD: unnamed in native evidence; displays resource 0x317. */
  Failure317: 205,
  /** 0xCE: displays resource 0x318. */
  Failure318: 206,
  /** 0xCF: displays resource 0x319. */
  Failure319: 207,
  /** 0xD0: displays resource 0x31E via the popup helper. */
  Failure31E: 208,
  /** 0xD1: displays resource 0x31F via the popup helper. */
  Failure31F: 209,
  /** 0xD2: duplicate login (0xA4). */
  AlreadyOnline2: 210,
  /** 0xD3: formatted resource 0x387 with the code as %d argument. */
  FailureFormat211: 211,
  /** 0xD4: formatted resource 0x388 with the code as %d argument. */
  FailureFormat212: 212,
  /** 0xD5: displays resource 0x3C4. */
  Failure3C4: 213,
  /** 0xD6: GM account, IP not permitted (hardcoded English dialog). */
  GmIpDenied: 214,
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

export default function GL_LOGIN_ACK(op: number, outcome: number | Success): Packet {
  if (typeof outcome === "number") {
    return new Packet(op).label("681 result expected as a native s32 low-byte code").s32(outcome);
  }

  const { userNo, servers, n100 = 0, rawExtension } = outcome;
  if (servers.length > 0x7fff) throw new RangeError("681 server_count must fit s16");

  const p = new Packet(op)
    .s32(Result.Success)
    .label("681 user_no expected as a native s32").s32(userNo)
    .label("681 n100 charge-mode expected as a native s32").s32(n100);
  if (!rawExtension) {
    p.s32(0); // ext_count: safe default, no extension tuple follows
  } else {
    p.s32(rawExtension.gate);
    if (rawExtension.gate > 0) {
      p.label("681 raw extension s32First expected as a native s32").s32(rawExtension.s32First)
        .label("681 raw extension s32Second expected as a native s32").s32(rawExtension.s32Second)
        .label("681 raw extension featureFlag expected as a native u8").u8(rawExtension.featureFlag);
    }
  }

  p.s16(servers.length);
  for (const server of servers) {
    // These are raw2 fields; native domain/signedness is unresolved.
    p.label("681 server_id expected as a native raw2").u16(server.serverId); // signedness unresolved
    p.label("681 server name expected as per the native char[50]").strMax(server.name, MAX_SERVER_NAME_BYTES);
    p.strMax(server.host, MAX_SERVER_HOST_BYTES); // native char[16]
    // The reader gets raw2, but the selected-server consumer passes these bits
    // to a Winsock u_short endpoint port.
    p.u16(server.port);
    p.u8(server.flag); // native `flag`; its domain is not established here
    p.u16(server.group); // native raw2; signedness remains unresolved

    // The native reader consumes exactly three group records. Extra caller
    // entries are outside the wire contract and are intentionally ignored.
    for (let index = 0; index < CHANNEL_GROUP_COUNT; index++) {
      const group = server.channelGroups[index];
      if (group === undefined) {
        p.s16(0);
        continue;
      }
      p.s16(group.maxUsers);
      if (group.maxUsers <= 0) continue;
      const channel = group.channel;
      if (channel === undefined) {
        throw new RangeError("681 positive channel group needs a channel body");
      }
      p.u8(channel.type);
      p.label("681 channel name expected as per the native char[50]").strMax(channel.name, MAX_CHANNEL_NAME_BYTES);
      p.s16(channel.currentUsers);
      p.u8(channel.flag); // native `ch_flag`; its domain is not established here
      if (channel.type === 3) {
        p.u8(channel.extra ?? 0);
      }
    }
  }

  return p.s32(0).s32(0); // billing_first, billing_second
}
