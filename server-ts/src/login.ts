/**
 * The login exchange: GL_ACCOUNTCONNSUCC -> GL_LOGIN_REQ -> GL_LOGIN_ACK.
 *
 * The client never sends credentials unprompted. 694 carries the compression
 * threshold and, in the same handler, calls the 682 builder — so 694 is the
 * trigger, and must be sent exactly once per connection. (docs/PACKETS.md §1.4)
 *
 * Builders are named after the opcode they produce, so grepping an official
 * name from the docs lands here.
 */

import { COMPRESSION_DISABLED, Packet, type Reader } from "./packet.ts";
import { Op } from "./opcodes.ts";
import type { Store } from "./store.ts";

/** Low byte of the GL_LOGIN_ACK result word; only documented codes are modelled. */
export const Result = {
  GeneralFailure: 0,
  Success: 1,
  BadCredentials: 2,
  Banned: 200,
  Maintenance: 201,
  AlreadyOnline: 210,
} as const;

export type Result = (typeof Result)[keyof typeof Result];

/** One channel inside a server's group. `extra` is read only when type is 3. */
export interface Channel {
  type: number;
  name: string;
  port: number;
  flag: number;
  extra?: number;
}

/** A server row in the list. Exactly three channel groups, per the client. */
export interface GameServer {
  id: number;
  name: string;
  host: string;
  port: number;
  flag: number;
  group: number;
  channelGroups: readonly (readonly Channel[])[];
}

/** GL_ACCOUNTCONNSUCC(694): compression threshold, and the login trigger. */
export function GL_ACCOUNTCONNSUCC(threshold = COMPRESSION_DISABLED): Packet {
  if (threshold <= 0 || threshold > COMPRESSION_DISABLED) {
    throw new RangeError(`threshold ${threshold} outside 1..0x2580`);
  }
  return new Packet(Op.GL_ACCOUNTCONNSUCC).u16(threshold);
}

/** GL_LOGIN_ACK(681), failure: just the result word. */
export function GL_LOGIN_ACK_rejected(code: Exclude<Result, 1>): Packet {
  // The client reads a raw 4-byte word but branches on the low byte only;
  // a full s32 is written either way.
  return new Packet(Op.GL_LOGIN_ACK).s32(code);
}

/** GL_LOGIN_ACK(681), success, in the client's read order. */
export function GL_LOGIN_ACK(
  userNo: number,
  servers: readonly GameServer[],
  /** Opaque billing/charge UI mode, echoed back in 143. Not a player level. */
  chargeMode = 0,
): Packet {
  const p = new Packet(Op.GL_LOGIN_ACK, 512);
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

/** GL_LOGIN_REQ(682), exactly as the client builds it — no trailing bytes. */
export interface LoginRequest {
  account: string;
  password: string;
  dataRevision: number;
  fingerprintSource: number;
  fingerprint: Uint8Array;
}

const GUARD_LOW = 0xf1e1ab0e;
const GUARD_HIGH_XOR = 0xb1a9d7c7;

export function readGL_LOGIN_REQ(r: Reader): LoginRequest {
  const account = r.str();
  const password = r.str();
  const packed = r.u64();
  const fingerprintSource = r.u8();
  const fingerprint = r.raw(24); // device/security material, separate from the guard

  if (r.remaining !== 0) throw new RangeError(`682 has ${r.remaining} trailing bytes`);

  // The revision is only decoded when the whole guard matches.
  const low = Number(packed & 0xffffffffn);
  if (low !== GUARD_LOW) throw new RangeError(`682 guard mismatch: ${low.toString(16)}`);
  const dataRevision = (Number((packed >> 32n) & 0xffffffffn) ^ GUARD_HIGH_XOR) >>> 0;

  return { account, password, dataRevision, fingerprintSource, fingerprint };
}

/**
 * Authenticate and build the reply.
 *
 * Boundary: this is the wire contract only. Entitlements, billing and what goes
 * in the server list are deployment policy, not reverse-engineered fact.
 */
export async function login(
  store: Store,
  request: LoginRequest,
  servers: readonly GameServer[],
): Promise<{ reply: Packet; accountId: number | null }> {
  const account = await store.verifyLogin(request.account, request.password);
  return account
    ? { reply: GL_LOGIN_ACK(account.id, servers), accountId: account.id }
    : { reply: GL_LOGIN_ACK_rejected(Result.BadCredentials), accountId: null };
}
