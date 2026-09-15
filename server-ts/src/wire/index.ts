/**
 * The list of wire modules — one line each, name matching the filename.
 *
 * This exists only so the opcode names are known at compile time, which makes
 * `session.reply("GL_LOGON_ACK")` a type error instead of a runtime one. The
 * registry cross-checks this list against the directory at startup, so a file
 * added here but not on disk (or vice versa) fails immediately.
 */

import * as GL_ACCOUNTCONNSUCC from "./GL_ACCOUNTCONNSUCC.ts";
import * as GL_LOGIN_ACK from "./GL_LOGIN_ACK.ts";
import * as GL_LOGIN_REQ from "./GL_LOGIN_REQ.ts";
import * as GT_PING_ACK from "./GT_PING_ACK.ts";
import * as GT_PING_REQ from "./GT_PING_REQ.ts";

export const modules = {
  GL_ACCOUNTCONNSUCC,
  GL_LOGIN_ACK,
  GL_LOGIN_REQ,
  GT_PING_ACK,
  GT_PING_REQ,
};
