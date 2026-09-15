/**
 * Credentials, exactly as the client builds them — no trailing bytes accepted.
 *
 * `str account, str password, u64 guard, u8 fingerprintSource, raw[24]`.
 * The u64 is a guard pair: a fixed low dword, and the data revision XORed into
 * the high dword. It is decoded only when the whole guard matches.
 * (docs/PACKETS.md §1.4)
 */

import type { Reader } from "../packet.ts";
import type { Session } from "../session.ts";

const GUARD_LOW = 0xf1e1ab0e;
const GUARD_HIGH_XOR = 0xb1a9d7c7;

export interface Credentials {
  account: string;
  password: string;
  dataRevision: number;
  fingerprintSource: number;
  fingerprint: Uint8Array;
}

export function read(r: Reader): Credentials {
  const account = r.str();
  const password = r.str();
  const guard = r.u64();
  const fingerprintSource = r.u8();
  const fingerprint = r.raw(24); // device/security material, separate from the guard

  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes`);

  const low = Number(guard & 0xffffffffn);
  if (low !== GUARD_LOW) throw new RangeError(`guard mismatch: ${low.toString(16)}`);
  const dataRevision = (Number((guard >> 32n) & 0xffffffffn) ^ GUARD_HIGH_XOR) >>> 0;

  return { account, password, dataRevision, fingerprintSource, fingerprint };
}

export default async function (r: Reader, session: Session): Promise<void> {
  await session.login(read(r));
}
