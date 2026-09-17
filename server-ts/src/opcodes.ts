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

export const OPCODE_COUNT = opcodeToName.size;

export function opcodeName(opcode: number): string {
  return opcodeToName.get(opcode) ?? `UNKNOWN_${opcode}`;
}

/** Resolve an official packet name, failing closed on a typo. */
export function opcodeFor(name: string): number {
  const opcode = nameToOpcode.get(name);
  if (opcode === undefined) throw new Error(`"${name}" is not in db/packets.tsv`);
  return opcode;
}
