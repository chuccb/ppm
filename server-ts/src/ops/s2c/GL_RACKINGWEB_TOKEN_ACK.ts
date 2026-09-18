/**
 * 787 -> 788 ranking-web token.
 *
 * Wire (sub_407360): `u8 hasToken`; when nonzero, `str token` follows
 * (NUL-terminated, native reader sub_592730; consumer strncpy caps the
 * copy at 0x10 bytes into byte_EDDE04 and discards tokens >= 16 chars).
 * With no ranking-web model this server always emits hasToken = 0, so
 * no token field is ever appended. The `token` argument stays public to
 * document the full native grammar but must remain null today.
 */

import { Packet } from "../../packet.ts";

export default function GL_RACKINGWEB_TOKEN_ACK(op: number, hasToken: 0 = 0): Packet {
  if (hasToken !== 0) {
    throw new RangeError("788 non-zero hasToken requires a token model this server does not provide");
  }
  return new Packet(op).u8(hasToken);
}
