/**
 * The opcode catalogue is the single source of numeric names.
 *
 * `db/packets.tsv` is checked at module load, so packet modules can use the
 * official name and never carry a second hand-written number table. Bun can
 * read the file directly; there is no Node filesystem adapter in the server.
 */

const catalogue = await Bun.file(new URL("../../db/packets.tsv", import.meta.url)).text();
const nameToOpcode = new Map<string, number>();
const opcodeToName = new Map<number, string>();

for (const line of catalogue.split("\n")) {
  const tab = line.indexOf("\t");
  if (tab < 0) continue;

  const opcode = Number.parseInt(line.slice(0, tab), 10);
  const name = line.slice(tab + 1).trim();
  if (!Number.isInteger(opcode) || name.length === 0) continue;
  if (nameToOpcode.has(name) || opcodeToName.has(opcode)) {
    throw new Error(`duplicate opcode catalogue entry: ${opcode} ${name}`);
  }
  nameToOpcode.set(name, opcode);
  opcodeToName.set(opcode, name);
}

/**
 * Official-name-table gaps whose grammars are proven handler-by-handler in
 * PaperMan.exe.c and documented as `〔推定〕` rows in docs/LAYOUTS.md (the
 * client binary's `sub_9EAF50` registry simply omits these numbers).
 * Names and numbers are fixed by the native builders/dispatcher; nothing is
 * recomputed or invented here. Cross-check: tools/verify_tsv_registry.py
 * proves the tsv side equals the client registry for everything else.
 */
export const gapTable: ReadonlyMap<number, string> = new Map<number, string>([
  [487, "GL_MYROOMCHANGE_REQ"], // builder sub_57C450 (u8 slot; /clby clamp 1..5 -> 0..4)
  [488, "GL_MYROOMCHANGE_ACK"], // handler sub_5861C0 (u8 status,[u8 slot if 1])
  [996, "GL_BLOCK_ADD_REQ"], // builder sub_567E70 (str nick)
  [997, "GL_BLOCK_ADD_ACK"], // handler sub_567F20 (u8 result -> msgtable 1320/1323/282/1322/1330)
  [998, "GL_BLOCK_DEL_REQ"], // builder sub_568030 (str nick)
  [999, "GL_BLOCK_DEL_ACK"], // handler sub_568170 (u8 result, str nick -> msgtable 1325/1326/182)
  [1000, "GL_BLOCK_LIST_REQ"], // builder sub_567CB0 (bare refresh; auto-sent after 996 success)
  [1001, "GL_BLOCK_LIST_ACK"], // handler sub_567D50 (u16,str,s32 count,countx{s32 flags,str nick})
  [1002, "GL_BLOCKME_LIST_REQ"], // builder sub_567B30 (bare)
  [1003, "GL_BLOCKME_LIST_ACK"], // handler sub_567BD0 (u16,str,s32 count,countx str nick)
]);
const gapNameToOpcode = new Map<string, number>(
  [...gapTable].map(([opcode, name]) => [name, opcode]),
);

export const OPCODE_COUNT = opcodeToName.size;

export function opcodeName(opcode: number): string {
  return opcodeToName.get(opcode) ?? gapTable.get(opcode) ?? `UNKNOWN_${opcode}`;
}

/** Resolve an official (or handler-proven gap) packet name, failing closed on a typo. */
export function opcodeFor(name: string): number {
  const opcode = nameToOpcode.get(name) ?? gapNameToOpcode.get(name);
  if (opcode === undefined) throw new Error(`"${name}" is not in db/packets.tsv or the proven gap table`);
  return opcode;
}

/** Official tsv names plus the handler-proven gap names above. */
export function isKnownPacketName(name: string): boolean {
  return nameToOpcode.has(name) || gapNameToOpcode.has(name);
}
