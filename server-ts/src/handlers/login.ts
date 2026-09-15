/**
 * Login exchange: 694 -> 682 -> 681.
 *
 * Ordering (docs/PACKETS.md §1.4): the client does not send credentials on its
 * own. `GL_ACCOUNTCONNSUCC(694)` carries the compression threshold and, in the
 * same handler, invokes the 682 builder — so 694 is the login trigger. It must
 * be sent exactly once per connection: sending it again after login makes the
 * client resend 682 forever.
 */

import { PacketWriter, type PacketReader } from "../codec/packet.ts";
import { COMPRESSION_DISABLED } from "../codec/frame.ts";
import { Op } from "../codec/opcodes.ts";
import type { Store } from "../db/schema.ts";

/** `GL_LOGIN_REQ(682)` — structurally exact, no trailing bytes accepted. */
export interface LoginRequest {
  readonly account: string;
  readonly password: string;
  readonly dataRevision: number;
  readonly fingerprintSource: number;
  readonly fingerprint: Uint8Array;
}

/** Guard constants from the client's 682 builder. */
const GUARD_LOW = 0xf1e1ab0e;
const GUARD_HIGH_XOR = 0xb1a9d7c7;
const FINGERPRINT_BYTES = 24;

export function parseLoginRequest(reader: PacketReader): LoginRequest {
  const account = reader.str();
  const password = reader.str();
  const packed = reader.u64();
  const fingerprintSource = reader.u8();
  const fingerprint = reader.raw(FINGERPRINT_BYTES);

  if (reader.remaining !== 0) {
    throw new RangeError(`682 has ${reader.remaining} unexpected trailing bytes`);
  }

  // The packed dword pair is only decoded when the whole guard matches.
  const low = Number(packed & 0xffffffffn);
  const high = Number((packed >> 32n) & 0xffffffffn);
  if (low !== GUARD_LOW) {
    throw new RangeError(`682 guard mismatch: low dword ${low.toString(16)}`);
  }
  const dataRevision = (high ^ GUARD_HIGH_XOR) >>> 0;

  return { account, password, dataRevision, fingerprintSource, fingerprint };
}

/** Low byte of the 681 result word. Only the documented codes are modelled. */
export const LoginResult = {
  GeneralFailure: 0,
  Success: 1,
  BadCredentials: 2,
  Banned: 200,
  Maintenance: 201,
  AlreadyOnline: 210,
} as const;

export type LoginResultCode = (typeof LoginResult)[keyof typeof LoginResult];

export interface ChannelEntry {
  readonly type: number;
  readonly name: string;
  readonly port: number;
  readonly flag: number;
  /** Only read by the client when `type === 3`. */
  readonly extra?: number;
}

export interface ServerEntry {
  readonly id: number;
  readonly name: string;
  readonly host: string;
  readonly port: number;
  readonly flag: number;
  readonly group: number;
  /** Exactly three groups; the client reads at most one channel from each. */
  readonly channelGroups: readonly (readonly ChannelEntry[])[];
}

export interface LoginSuccess {
  readonly userNo: number;
  /** Opaque billing/charge UI mode echoed back in 143. Not a player level. */
  readonly chargeMode: number;
  readonly servers: readonly ServerEntry[];
  readonly billingFirst: number;
  readonly billingSecond: number;
}

/** `GL_ACCOUNTCONNSUCC(694)` — compression threshold, and the login trigger. */
export function buildAccountConnSucc(threshold = COMPRESSION_DISABLED): PacketWriter {
  if (threshold <= 0 || threshold > COMPRESSION_DISABLED) {
    throw new RangeError(`threshold ${threshold} outside 1..0x2580`);
  }
  return new PacketWriter(Op.GL_ACCOUNTCONNSUCC).u16(threshold);
}

export function buildLoginFailure(code: LoginResultCode): PacketWriter {
  // The client reads a raw 4-byte word but branches on the low byte only;
  // a complete s32 is written either way.
  return new PacketWriter(Op.GL_LOGIN_ACK).s32(code);
}

export function buildLoginSuccess(info: LoginSuccess): PacketWriter {
  const packet = new PacketWriter(Op.GL_LOGIN_ACK, 512);
  packet.s32(LoginResult.Success);
  packet.s32(info.userNo);
  packet.s32(info.chargeMode);
  packet.s32(0); // ext_count: 0 = no netcafe feature extension

  packet.s16(info.servers.length);
  for (const server of info.servers) {
    if (server.channelGroups.length !== 3) {
      throw new RangeError("each server must declare exactly three channel groups");
    }
    packet.s16(server.id);
    packet.str(server.name); // char[50] native, <= 49 bytes
    packet.str(server.host); // char[16] native, <= 15 bytes
    packet.s16(server.port); // 16-bit pattern reused as u_short
    packet.u8(server.flag);
    packet.s16(server.group);

    for (const group of server.channelGroups) {
      packet.s16(group.length);
      // The client reads at most one entry even when the count is higher.
      const channel = group[0];
      if (!channel) continue;
      packet.u8(channel.type);
      packet.str(channel.name);
      packet.s16(channel.port);
      packet.u8(channel.flag);
      if (channel.type === 3) packet.u8(channel.extra ?? 0);
    }
  }

  packet.s32(info.billingFirst);
  packet.s32(info.billingSecond);
  return packet;
}

export interface LoginOutcome {
  readonly packet: PacketWriter;
  readonly accountId: number | null;
}

/**
 * Authenticate and build the 681 reply.
 *
 * Boundary: this implements the *wire contract* only. Entitlements, billing and
 * the server list contents are deployment policy, not reverse-engineered fact.
 */
export async function handleLogin(
  store: Store,
  request: LoginRequest,
  servers: readonly ServerEntry[],
): Promise<LoginOutcome> {
  const account = await store.verifyLogin(request.account, request.password);
  if (!account) {
    return { packet: buildLoginFailure(LoginResult.BadCredentials), accountId: null };
  }
  return {
    packet: buildLoginSuccess({
      userNo: account.id,
      chargeMode: 0,
      servers,
      billingFirst: 0,
      billingSecond: 0,
    }),
    accountId: account.id,
  };
}
