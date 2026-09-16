/**
 * Credentials, exactly as the client builds them — no trailing bytes accepted.
 *
 * `str account, str password_or_token, u64 guard, u8 fingerprint_source, raw[24]`.
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
  readonly account: string;
  readonly passwordOrToken: string;
  readonly dataRevision: number;
  readonly fingerprintSource: number;
  readonly fingerprint: Uint8Array;
}

export function read(r: Reader): Credentials {
  const account = r.str();
  const passwordOrToken = r.str();
  const guard = r.u64();
  const fingerprintSource = r.u8();
  const fingerprint = r.raw(24); // device/security material, separate from the guard

  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes`);

  const low = Number(guard & 0xffffffffn);
  if (low !== GUARD_LOW) throw new RangeError(`guard mismatch: ${low.toString(16)}`);
  const dataRevision = (Number((guard >> 32n) & 0xffffffffn) ^ GUARD_HIGH_XOR) >>> 0;

  return { account, passwordOrToken, dataRevision, fingerprintSource, fingerprint };
}

/**
 * Authenticate and reply.
 *
 * Boundary: the wire contract only. Entitlements, billing and the contents of
 * the server list are deployment policy, not reverse-engineered fact.
 */
export default async function GL_LOGIN_REQ(r: Reader, connection: Connection): Promise<void> {
  const { account, passwordOrToken } = read(r);
  const found = await connection.config.store.verifyLogin(account, passwordOrToken);

  if (!found) {
    connection.log(`login ${account} -> rejected`);
    connection.reply("GL_LOGIN_ACK", Result.BadCredentials);
    return;
  }

  const n100 = 0;
  const extensionCount = 0;
  connection.bindAccount(found.id);
  connection.config.admissions.issue(
    found.id,
    n100,
    extensionCount,
    connection.remoteIp,
    connection.config.admissionLifetimeMs ?? 120_000,
  );
  connection.log(`login ${account} -> account ${found.id}`);
  // `user_no` is the native wire name. The available evidence does not prove
  // that it is the later 198 player/user row, so this private server exposes
  // the verified account row id here rather than inventing an identity join.
  connection.reply("GL_LOGIN_ACK", {
    userNo: found.id,
    servers: connection.config.servers,
    n100,
  });
}
