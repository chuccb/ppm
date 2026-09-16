/**
 * The wire module list — one line per packet.
 *
 * Why this file exists: ES modules have no glob import, and a dynamic
 * `import(`./${name}.ts`)` degrades to `any`, which would make
 * `reply("GL_LOGON_ACK")` a runtime surprise instead of a compile error. This
 * list is what lets the type checker know the names and each builder's
 * arguments.
 *
 * The name appears twice per line, in the alias and the path, but adjacently —
 * a mismatch is visible at a glance. The registry also cross-checks this list
 * against the directory at startup, so neither can drift unnoticed.
 *
 * To add a packet: create `<OPCODE_NAME>.ts` here, add one line below.
 */

export { default as GL_ACCOUNTCONNSUCC } from "./GL_ACCOUNTCONNSUCC.ts";
export { default as GL_LOGIN_ACK } from "./GL_LOGIN_ACK.ts";
export { default as GL_LOGIN_REQ } from "./GL_LOGIN_REQ.ts";
export { default as GT_PING_ACK } from "./GT_PING_ACK.ts";
export { default as GT_PING_REQ } from "./GT_PING_REQ.ts";
