/**
 * The client's answer to the channel greeting: a handoff claim from login.
 *
 * `str identity, s32 chargeMode, u8 1, s32 extCount` (`sub_555C60`). The
 * middle byte is a hardcoded literal 1, and the two integers are echoed back
 * from the login reply.
 *
 * `identity` comes from a native `String[24]`, so at most 23 ANSI bytes. Its
 * writer has not been located, so it is **not** treated as an account or
 * nickname key — only as a value to match against a recent login. It is not a
 * cryptographic credential either. (docs/PACKETS.md §3.15d)
 */

import type { Reader } from "../../packet.ts";
import type { Connection } from "../../connection.ts";
import { Result } from "../s2c/PM_UDPSTART_ACK.ts";

/** Native `String[24]`, so 23 bytes plus the NUL. */
export const IDENTITY_MAX_BYTES = 23;

export interface Handoff {
  identity: string;
  /** Echoed from the login reply; opaque billing/charge UI mode. */
  chargeMode: number;
  extCount: number;
}

export function read(r: Reader): Handoff {
  const identity = r.str();
  const chargeMode = r.s32();
  const literal = r.u8();
  const extCount = r.s32();

  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes`);
  if (literal !== 1) throw new RangeError(`expected the literal 1, got ${literal}`);
  if (identity.length > IDENTITY_MAX_BYTES) {
    throw new RangeError(`identity longer than the client's ${IDENTITY_MAX_BYTES}-byte buffer`);
  }

  return { identity, chargeMode, extCount };
}

export default function PM_UDPSTART_REQ(r: Reader, connection: Connection): void {
  const { identity } = read(r);
  connection.log(`channel handoff ${JSON.stringify(identity)}`);

  connection.reply("PM_UDPSTART_ACK", {
    result: Result.Success,
    channelName: connection.config.channelName,
  });
}
