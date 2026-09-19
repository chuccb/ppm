/**
 * 419 sends one authored message draft toward a recipient (builder
 * sub_559550, re-read from PaperMan.exe.c line-level):
 *
 *   sub_592AA0(v20, dword_F2A684)   raw4 caller-defined context
 *   sub_5926F0 x4                   str ownNick (sender-side truncated
 *                                   char[24]), str toNick, str body, str title
 *   sub_5929A0                      u16 icon
 *   sub_592920                      u8 sound
 *
 * Wire: `{raw4 contextRaw, str ownNick, str toNick, str body, str title,
 * u16 iconRaw, u8 soundRaw}` — the leading field is the four-byte context,
 * NOT a u8 tag; the older shape shifted every string and is corrected here.
 *
 * Native sender gates fire before the ctor: toNick must be 1..24 bytes and
 * body 1..200 bytes (sub_559550's n24/n200 checks); ownNick is locally
 * truncated by the client and never gated there, this server keeps the same
 * char[24]-class receiver bound. The context/icon/sound words are consumed
 * only server-side — the native 420 reader never revisits them. Identity
 * fields stay raw (no store join) consistent with the 834 context.
 */
import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";
const MSG_ADD_BODY_MAX_BYTES = 200; // native sub_559550 (n200 <= 200)
const MSG_ADD_NICK_MAX_BYTES = 24; // native sub_559550 (n24 <= 24)
export default function GL_MSG_ADD_REQ(r: Reader, connection: Connection): void {
  const contextRaw = r.s32(); // sub_592AA0(dword_F2A684): caller-defined context word
  const ownNick = r.str();
  const toNick = r.str();
  const body = r.str();
  const title = r.str();
  const iconRaw = r.u16();
  const soundRaw = r.u8();
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 419`);
  // contextRaw / title / iconRaw / soundRaw stay parse-only (no proven domain).
  void contextRaw;
  void title;
  void iconRaw;
  void soundRaw;
  if (ownNick.length === 0 || ownNick.length > MSG_ADD_NICK_MAX_BYTES) {
    throw new RangeError("419 ownNick exceeds the native char[24] sender slot");
  }
  if (toNick.length === 0 || toNick.length > MSG_ADD_NICK_MAX_BYTES) {
    throw new RangeError("419 toNick must be 1..24 bytes (native sender gate)");
  }
  if (body.length === 0 || body.length > MSG_ADD_BODY_MAX_BYTES) {
    throw new RangeError("419 body must be 1..200 bytes (native sender gate)");
  }
  // TS has no mailbox model; every recipient lookup is impossible, so the only
  // honest answer is the native reader's default (unknown) branch.
  connection.reply(
    "GL_MSG_ADD_ACK",
    toNick,
    6, // xRaw outside the proven switch arms -> generic error resource, no fabricated state
    0,
  );
}
