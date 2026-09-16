/**
 * Credentials, exactly as the client builds them — no trailing bytes accepted.
 *
 * `str account, str password, u64 guard, u8 fingerprintSource, raw[24]`.
 * The u64 is a guard pair: a fixed low dword, and the data revision XORed into
 * the high dword. It is decoded only when the whole guard matches.
 * (docs/PACKETS.md §1.4)
 */

import type { Reader } from "../../packet.ts";
import type { Connection } from "../../connection.ts";
import { Result } from "../s2c/GL_LOGIN_ACK.ts";

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

/**
 * Authenticate and reply.
 *
 * Boundary: the wire contract only. Entitlements, billing and the contents of
 * the server list are deployment policy, not reverse-engineered fact.
 */
export default async function GL_LOGIN_REQ(r: Reader, connection: Connection): Promise<void> {
  const { account, password } = read(r);
  const found = await connection.config.store.verifyLogin(account, password);

  if (!found) {
    connection.log(`login ${account} -> rejected`);
    connection.reply("GL_LOGIN_ACK", Result.BadCredentials);
    return;
  }

  const chargeMode = 0;
  const extensionCount = 0;
  connection.bindAccount(found.id);
  connection.config.admissions.issue(
    found.id,
    chargeMode,
    extensionCount,
    connection.remoteIp,
    connection.config.admissionLifetimeMs ?? 120_000,
  );
  connection.log(`login ${account} -> account ${found.id}`);
  connection.reply("GL_LOGIN_ACK", {
    userId: found.id,
    servers: connection.config.servers,
    chargeMode,
  });
}
