/**
 * Account and minimal lobby identity state on Bun's native `bun:sqlite`.
 *
 * The first server slice owns only facts needed by the login and 197/198
 * bootstrap. It does not seed inventory, weapons, prices, quests, or rewards:
 * those require a separate evidence chain in the client and resources.
 */

import { Database } from "bun:sqlite";

export interface Account {
  readonly id: number;
  readonly username: string;
  readonly passwordHash: string;
  readonly createdAt: number;
  readonly lastLoginAt: number | null;
}

export interface Stats {
  readonly wins: number;
  readonly losses: number;
  readonly kills: number;
  readonly deaths: number;
  readonly headshots: number;
  readonly combos: number;
  readonly hearts: number;
  readonly doubleKill: number;
  readonly tripleKill: number;
  readonly criticals: number;
  readonly multiKill: number;
  readonly ultraKill: number;
  readonly zKill: number;
  readonly kKill: number;
  readonly ddKill: number;
  readonly playTimeSeconds: number;
}

export interface Character {
  /** Persistent character-list slot; 198 serializes the row index separately. */
  readonly slotNo: number;
  readonly charType: number;
  /** The twelve category-relative u16 normal-appearance values read by 198. */
  readonly appearance: readonly number[];
}

/** One dirty character record carried by 218 GI_CHANGEDATA_REQ (26 bytes on wire). */
export interface CharacterDataPatch {
  /** Character-list slot; native builder slot count is `n0x14 <= 0x14`. */
  readonly slot: number;
  /** Native character type 1..15 (wire second byte; NOT a participation flag). */
  readonly characterType: number;
  /** Exactly twelve category-relative u16 appearance offsets. */
  readonly appearance: readonly number[];
}

export interface MyInfo {
  readonly userId: number;
  readonly nickname: string;
  readonly level: number;
  readonly experience: number;
  readonly gamePoints: number;
  readonly cash: number;
  /** Coupon balance: the native COUPON label's `%10d` source (+112 wire word);
   * purchase gates compare an item's price against it (`v25 <= dword_EE8D1C`).
   * No coupon model exists yet, so the projection is 0 = no coupons. */
  readonly coupon: number;
  /** Serialized character-list index emitted in the native 198/247 fields. */
  readonly selectedCharIndex: number;
  readonly stats: Stats;
  readonly characters: readonly Character[];
}

/** SQLite rows stay snake_case; only the returned projection is camelCase. */
interface AccountRow {
  id: number;
  username: string;
  password_hash: string;
  created_at: number;
  last_login_at: number | null;
}

interface PlayerRow {
  id: number;
  nickname: string;
  level: number;
  experience: number;
  game_points: number;
  cash: number;
  current_character: number;
  wins: number;
  losses: number;
  kills: number;
  deaths: number;
  headshots: number;
  combos: number;
  hearts: number;
  double_kill: number;
  triple_kill: number;
  criticals: number;
  multi_kill: number;
  ultra_kill: number;
  z_kill: number;
  k_kill: number;
  dd_kill: number;
  play_time_seconds: number;
}

interface CharacterRow {
  player_id: number;
  slot: number;
  character_type: number;
  appearance0: number;
  appearance1: number;
  appearance2: number;
  appearance3: number;
  appearance4: number;
  appearance5: number;
  appearance6: number;
  appearance7: number;
  appearance8: number;
  appearance9: number;
  appearance10: number;
  appearance11: number;
}

interface ProfileRow {
  profile_index: number;
  puzzle0: number;
  puzzle1: number;
  puzzle2: number;
  puzzle3: number;
  puzzle4: number;
  puzzle5: number;
  puzzle6: number;
  expires_at_packed_minute: number;
}

/**
 * Native `sub_522580` maps a body template to these five normal pieces.
 * `appearance0` is the body offset itself (`char_type` 1..15); the other five
 * values are the direct outputs of the native resource switch tables. The
 * optional six appearance slots are intentionally absent from this repair map.
 */
const CANONICAL_NORMAL_APPEARANCE: readonly (readonly [number, number, number, number, number, number])[] = [
  [1, 1, 1, 1, 1, 1],
  [2, 15, 10, 22, 12, 12],
  [3, 28, 19, 45, 25, 24],
  [4, 41, 28, 66, 36, 41],
  [5, 55, 37, 90, 47, 52],
  [6, 123, 111, 157, 99, 105],
  [7, 124, 112, 167, 109, 115],
  [8, 125, 113, 177, 119, 125],
  [9, 126, 114, 187, 129, 135],
  [10, 127, 115, 197, 139, 145],
  [11, 1096, 839, 1069, 974, 952],
  [12, 1428, 865, 1205, 1069, 1009],
  [13, 1600, 866, 1213, 1072, 1012],
  [14, 792, 385, 428, 376, 360],
  [15, 30220, 920, 10011, 10011, 10114],
];

function accountFromRow(row: AccountRow): Account {
  return {
    id: row.id,
    username: row.username,
    passwordHash: row.password_hash,
    createdAt: row.created_at,
    lastLoginAt: row.last_login_at,
  };
}

function statsFromRow(row: PlayerRow): Stats {
  return {
    wins: row.wins,
    losses: row.losses,
    kills: row.kills,
    deaths: row.deaths,
    headshots: row.headshots,
    combos: row.combos,
    hearts: row.hearts,
    doubleKill: row.double_kill,
    tripleKill: row.triple_kill,
    criticals: row.criticals,
    multiKill: row.multi_kill,
    ultraKill: row.ultra_kill,
    zKill: row.z_kill,
    kKill: row.k_kill,
    ddKill: row.dd_kill,
    playTimeSeconds: row.play_time_seconds,
  };
}

function characterFromRow(row: CharacterRow): Character {
  return {
    slotNo: row.slot,
    charType: row.character_type,
    appearance: [
      row.appearance0,
      row.appearance1,
      row.appearance2,
      row.appearance3,
      row.appearance4,
      row.appearance5,
      row.appearance6,
      row.appearance7,
      row.appearance8,
      row.appearance9,
      row.appearance10,
      row.appearance11,
    ],
  };
}

export const NEW_SKILL_PROFILE_COUNT = 5;
export const NEW_SKILL_PUZZLE_SLOT_COUNT = 7;

export interface NewSkillProfile {
  readonly puzzleItemIds: readonly number[];
  /** Native packed local-time word; profile 0 ignores it on the client. */
  /** Record dword 7 of the native 32B profile; the client pre-zeroes this
   * slot before the 0xA0 blob read, so 0 = no expiry. */
  readonly expiresAtPackedMinute: number;
}

export interface NewSkillProfileSnapshot {
  readonly selectedProfile: number;
  readonly profiles: readonly NewSkillProfile[];
}

const SCHEMA = `
CREATE TABLE IF NOT EXISTS account (
  id            INTEGER PRIMARY KEY AUTOINCREMENT,
  username      TEXT    NOT NULL UNIQUE,
  password_hash TEXT    NOT NULL,
  created_at    INTEGER NOT NULL,
  last_login_at INTEGER
) STRICT;

CREATE TABLE IF NOT EXISTS player (
  id              INTEGER PRIMARY KEY AUTOINCREMENT,
  account_id      INTEGER NOT NULL UNIQUE REFERENCES account(id) ON DELETE CASCADE,
  nickname        TEXT    NOT NULL UNIQUE,
  level           INTEGER NOT NULL DEFAULT 1,
  experience      INTEGER NOT NULL DEFAULT 0,
  game_points     INTEGER NOT NULL DEFAULT 0,
  cash            INTEGER NOT NULL DEFAULT 0,
  current_character INTEGER NOT NULL DEFAULT 0 CHECK (current_character BETWEEN 0 AND 19),
  tutorial_index  INTEGER NOT NULL DEFAULT 0
) STRICT;

CREATE TABLE IF NOT EXISTS player_stats (
  player_id       INTEGER PRIMARY KEY REFERENCES player(id) ON DELETE CASCADE,
  wins            INTEGER NOT NULL DEFAULT 0,
  losses          INTEGER NOT NULL DEFAULT 0,
  kills           INTEGER NOT NULL DEFAULT 0,
  deaths          INTEGER NOT NULL DEFAULT 0,
  headshots       INTEGER NOT NULL DEFAULT 0,
  combos          INTEGER NOT NULL DEFAULT 0,
  hearts          INTEGER NOT NULL DEFAULT 0,
  double_kill     INTEGER NOT NULL DEFAULT 0,
  triple_kill     INTEGER NOT NULL DEFAULT 0,
  criticals       INTEGER NOT NULL DEFAULT 0,
  multi_kill      INTEGER NOT NULL DEFAULT 0,
  ultra_kill      INTEGER NOT NULL DEFAULT 0,
  z_kill          INTEGER NOT NULL DEFAULT 0,
  k_kill          INTEGER NOT NULL DEFAULT 0,
  dd_kill         INTEGER NOT NULL DEFAULT 0,
  play_time_seconds INTEGER NOT NULL DEFAULT 0
) STRICT;

CREATE TABLE IF NOT EXISTS player_character (
  player_id       INTEGER NOT NULL REFERENCES player(id) ON DELETE CASCADE,
  slot            INTEGER NOT NULL CHECK (slot BETWEEN 0 AND 19),
  character_type  INTEGER NOT NULL CHECK (character_type BETWEEN 1 AND 15),
  appearance0     INTEGER NOT NULL DEFAULT 0 CHECK (appearance0 BETWEEN 0 AND 65535),
  appearance1     INTEGER NOT NULL DEFAULT 0 CHECK (appearance1 BETWEEN 0 AND 65535),
  appearance2     INTEGER NOT NULL DEFAULT 0 CHECK (appearance2 BETWEEN 0 AND 65535),
  appearance3     INTEGER NOT NULL DEFAULT 0 CHECK (appearance3 BETWEEN 0 AND 65535),
  appearance4     INTEGER NOT NULL DEFAULT 0 CHECK (appearance4 BETWEEN 0 AND 65535),
  appearance5     INTEGER NOT NULL DEFAULT 0 CHECK (appearance5 BETWEEN 0 AND 65535),
  appearance6     INTEGER NOT NULL DEFAULT 0 CHECK (appearance6 BETWEEN 0 AND 65535),
  appearance7     INTEGER NOT NULL DEFAULT 0 CHECK (appearance7 BETWEEN 0 AND 65535),
  appearance8     INTEGER NOT NULL DEFAULT 0 CHECK (appearance8 BETWEEN 0 AND 65535),
  appearance9     INTEGER NOT NULL DEFAULT 0 CHECK (appearance9 BETWEEN 0 AND 65535),
  appearance10    INTEGER NOT NULL DEFAULT 0 CHECK (appearance10 BETWEEN 0 AND 65535),
  appearance11    INTEGER NOT NULL DEFAULT 0 CHECK (appearance11 BETWEEN 0 AND 65535),
  PRIMARY KEY (player_id, slot)
) STRICT;

CREATE TABLE IF NOT EXISTS new_skill_profile_state (
  player_id       INTEGER PRIMARY KEY REFERENCES player(id) ON DELETE CASCADE,
  selected_profile INTEGER NOT NULL DEFAULT 0 CHECK (selected_profile BETWEEN 0 AND 4)
) STRICT;

CREATE TABLE IF NOT EXISTS new_skill_profiles (
  player_id       INTEGER NOT NULL REFERENCES player(id) ON DELETE CASCADE,
  profile_index   INTEGER NOT NULL CHECK (profile_index BETWEEN 0 AND 4),
  puzzle0         INTEGER NOT NULL DEFAULT 0,
  puzzle1         INTEGER NOT NULL DEFAULT 0,
  puzzle2         INTEGER NOT NULL DEFAULT 0,
  puzzle3         INTEGER NOT NULL DEFAULT 0,
  puzzle4         INTEGER NOT NULL DEFAULT 0,
  puzzle5         INTEGER NOT NULL DEFAULT 0,
  puzzle6         INTEGER NOT NULL DEFAULT 0,
  expires_at_packed_minute INTEGER NOT NULL DEFAULT 0,
  PRIMARY KEY (player_id, profile_index)
) STRICT;
`;

/**
 * A valid argon2 hash of a throwaway value, verified against on the
 * unknown-user path so a miss costs the same as a wrong password. Measured:
 * ~132 ms either way, so the response time does not reveal who exists.
 */
const ABSENT_USER_HASH =
  "$argon2id$v=19$m=65536,t=2,p=1$YWJjZGVmZ2hpamtsbW5vcA$" +
  "5xk0YQe0h8f4S2n4l6cQ5w0yk5vJ1b3n5tR7uV9wXyA";

export class Store {
  readonly #db: Database;

  constructor(path = ":memory:") {
    this.#db = new Database(path, { create: true, strict: true });
    // WAL keeps readers non-blocking; both pragmas are per-connection.
    this.#db.exec("PRAGMA journal_mode = WAL");
    this.#db.exec("PRAGMA foreign_keys = ON");
    this.#db.exec(SCHEMA);
    // 2026-09-19: tutorial_index was added to the canonical player schema;
    // databases created earlier receive it once via this guarded ALTER.
    if (!this.#db.query<{ name: string }, []>(
      "SELECT name FROM pragma_table_info('player') WHERE name = 'tutorial_index'",
    ).get()) {
      this.#db.exec("ALTER TABLE player ADD COLUMN tutorial_index INTEGER NOT NULL DEFAULT 0");
    }
  }

  get sqliteVersion(): string {
    const row = this.#db.query<{ v: string }, []>("SELECT sqlite_version() AS v").get();
    return row?.v ?? "unknown";
  }

  /**
   * The client restricts credentials to [0-9A-Za-z@] before it will even send
   * opcode 682 (`sub_43DD60`), so anything outside that set cannot come from a
   * legitimate client and is rejected here too. This server projects the
   * account name into the native 198 nickname field without inventing an alias;
   * that field is `CClientData::char[24]`, so keep the shared value to 23 bytes.
   */
  static isValidUsername(name: string): boolean {
    return name.length > 0 && name.length <= 23 && /^[0-9A-Za-z@]+$/.test(name);
  }

  async createAccount(username: string, password: string): Promise<Account> {
    if (!Store.isValidUsername(username)) {
      throw new RangeError(`username ${JSON.stringify(username)} is not client-representable`);
    }
    const hash = await Bun.password.hash(password);
    const now = Date.now();
    this.#db
      .query(
        "INSERT INTO account (username, password_hash, created_at) VALUES ($u, $h, $t)",
      )
      .run({ u: username, h: hash, t: now });
    const account = this.findAccount(username);
    if (!account) throw new Error("account insert did not round-trip");
    return account;
  }

  findAccount(username: string): Account | null {
    const row = this.#db
      .query<AccountRow, { u: string }>("SELECT * FROM account WHERE username = $u")
      .get({ u: username });
    return row ? accountFromRow(row) : null;
  }

  /**
   * Creates the minimal private-server MyInfo projection needed by 198.
   * The canonical character type 1 and its six native body-template values are
   * source-proven; no weapon, item, currency, or reward is granted here.
   */
  ensurePlayerIdentity(accountId: number): MyInfo | null {
    const existing = this.getMyInfoByAccountId(accountId);
    if (existing) return existing;

    const account = this.#db
      .query<{ username: string }, { id: number }>("SELECT username FROM account WHERE id = $id")
      .get({ id: accountId });
    if (!account) return null;

    // The account validation already keeps this ASCII name within the native
    // nickname buffer, so no invented alias is needed.
    const nickname = account.username;
    this.#db.exec("BEGIN IMMEDIATE");
    try {
      const inserted = this.#db
        .query("INSERT INTO player (account_id, nickname) VALUES ($a, $n)")
        .run({ a: accountId, n: nickname });
      if (inserted.changes !== 1) throw new Error("player insert did not affect one row");

      const player = this.#db
        .query<{ id: number }, []>("SELECT last_insert_rowid() AS id")
        .get();
      if (!player) throw new Error("player insert did not return an id");

      this.#db.query("INSERT INTO player_stats (player_id) VALUES ($p)").run({ p: player.id });
      this.#db
        .query(
          `INSERT INTO player_character (
             player_id, slot, character_type,
             appearance0, appearance1, appearance2, appearance3, appearance4, appearance5
           ) VALUES ($p, 0, 1, 1, 1, 1, 1, 1, 1)`,
        )
        .run({ p: player.id });
      this.ensureNewSkillProfileRows(player.id);
      this.#db.exec("COMMIT");
    } catch (error) {
      this.#db.exec("ROLLBACK");
      throw error;
    }

    return this.getMyInfoByAccountId(accountId);
  }

  getMyInfoByNickname(nickname: string): MyInfo | null {
    const row = this.#db
      .query<{ user_id: number }, { n: string }>(
        "SELECT id AS user_id FROM player WHERE nickname = $n",
      )
      .get({ n: nickname });
    return row ? this.getMyInfo(row.user_id) : null;
  }

  private getMyInfoByAccountId(accountId: number): MyInfo | null {
    const row = this.#db
      .query<{ user_id: number }, { a: number }>(
        "SELECT id AS user_id FROM player WHERE account_id = $a",
      )
      .get({ a: accountId });
    return row ? this.getMyInfo(row.user_id) : null;
  }

  getNewSkillProfileSnapshot(userId: number): NewSkillProfileSnapshot {
    this.#db.exec("BEGIN IMMEDIATE");
    try {
      this.ensureNewSkillProfileRows(userId);
      const state = this.#db
        .query<{ selected_profile: number }, { p: number }>(
          "SELECT selected_profile FROM new_skill_profile_state WHERE player_id = $p",
        )
        .get({ p: userId });
      const rows = this.#db
        .query<ProfileRow, { p: number }>(
          `SELECT profile_index, puzzle0, puzzle1, puzzle2, puzzle3, puzzle4, puzzle5, puzzle6,
                  expires_at_packed_minute
             FROM new_skill_profiles
            WHERE player_id = $p
            ORDER BY profile_index`,
        )
        .all({ p: userId });

      if (!state || rows.length !== NEW_SKILL_PROFILE_COUNT) {
        throw new Error("NewSkill profile bootstrap did not create a complete snapshot");
      }
      for (const [index, row] of rows.entries()) {
        if (row.profile_index !== index) {
          throw new Error("NewSkill profile snapshot has a non-contiguous profile index");
        }
      }

      const snapshot = {
        selectedProfile: state.selected_profile,
        profiles: rows.map((row) => ({
          puzzleItemIds: [
            row.puzzle0,
            row.puzzle1,
            row.puzzle2,
            row.puzzle3,
            row.puzzle4,
            row.puzzle5,
            row.puzzle6,
          ],
          expiresAtPackedMinute: row.expires_at_packed_minute,
        })),
      } satisfies NewSkillProfileSnapshot;
      this.#db.exec("COMMIT");
      return snapshot;
    } catch (error) {
      this.#db.exec("ROLLBACK");
      throw error;
    }
  }

  private ensureNewSkillProfileRows(userId: number): void {
    this.#db
      .query(
        `INSERT OR IGNORE INTO new_skill_profile_state(player_id, selected_profile)
         VALUES ($p, 0)`,
      )
      .run({ p: userId });
    for (let profileIndex = 0; profileIndex < NEW_SKILL_PROFILE_COUNT; profileIndex++) {
      this.#db
        .query(
          `INSERT OR IGNORE INTO new_skill_profiles(player_id, profile_index)
           VALUES ($p, $i)`,
        )
        .run({ p: userId, i: profileIndex });
    }
  }

  /**
   * Repair only the native-proven six-word normal prefix. A zero body or an
   * already canonical body is safe to complete; a nonzero noncanonical body
   * is retained as raw historical state because the native evidence does not
   * identify the owning server policy. Every nonzero stored word wins over a
   * missing canonical default, and slots 6..11 are never guessed.
   */
  private repairCanonicalCharacter(row: CharacterRow): CharacterRow {
    const canonical = CANONICAL_NORMAL_APPEARANCE[row.character_type - 1];
    if (!canonical || (row.appearance0 !== 0 && row.appearance0 !== canonical[0])) return row;

    const appearance: [number, number, number, number, number, number] = [
      row.appearance0,
      row.appearance1,
      row.appearance2,
      row.appearance3,
      row.appearance4,
      row.appearance5,
    ];
    let changed = false;
    for (let index = 0; index < appearance.length; index++) {
      if (appearance[index] === 0) {
        appearance[index] = canonical[index]!;
        changed = true;
      }
    }
    if (!changed) return row;

    this.#db
      .query(
        `UPDATE player_character
            SET appearance0 = $a0, appearance1 = $a1, appearance2 = $a2,
                appearance3 = $a3, appearance4 = $a4, appearance5 = $a5
          WHERE player_id = $p AND slot = $s`,
      )
      .run({
        p: row.player_id,
        s: row.slot,
        a0: appearance[0],
        a1: appearance[1],
        a2: appearance[2],
        a3: appearance[3],
        a4: appearance[4],
        a5: appearance[5],
      });

    return {
      ...row,
      appearance0: appearance[0]!,
      appearance1: appearance[1]!,
      appearance2: appearance[2]!,
      appearance3: appearance[3]!,
      appearance4: appearance[4]!,
      appearance5: appearance[5]!,
    };
  }

  /** 686 read-back: per-player tutorial marker (literal 145 hides TUTO_NEW; sub_4422B0). */
  getTutorialIndex(userId: number): number {
    const row = this.#db
      .query<{ tutorial_index: number }, { p: number }>(
        "SELECT tutorial_index FROM player WHERE id = $p",
      )
      .get({ p: userId });
    return row?.tutorial_index ?? 0;
  }

  /** 689 persists the client-reported tutorial step verbatim (no native ACK exists). */
  setTutorialIndex(userId: number, tutorialIndex: number): void {
    if (!Number.isSafeInteger(userId) || userId <= 0 || !Number.isSafeInteger(tutorialIndex)) {
      throw new RangeError("tutorial index write needs integer ids");
    }
    const result = this.#db
      .query("UPDATE player SET tutorial_index = $t WHERE id = $p")
      .run({ t: tutorialIndex, p: userId });
    if (result.changes !== 1) throw new Error(`player ${userId} missing for tutorial index write`);
  }

  /** 312 is the CHARSLOT selection (CClientData+88 as-is); persist so 198 reflects it. */
  setCurrentCharacter(userId: number, slot: number): boolean {
    const exists = this.#db
      .query<{ c: number }, { p: number; s: number }>(
        "SELECT COUNT(*) AS c FROM player_character WHERE player_id = $p AND slot = $s",
      )
      .get({ p: userId, s: slot });
    if (!exists || exists.c !== 1) return false;
    this.#db
      .query("UPDATE player SET current_character = $s WHERE id = $p")
      .run({ s: slot, p: userId });
    return true;
  }

  /**
   * 218 rows were already applied optimistically on the client (sub_525450
   * sends only dirty records); the server must reach the same state or the
   * next login would undo the player's appearance change. All-or-nothing:
   * unknown slots (e.g. unowned character) roll the transaction back so the
   * handler can honestly answer the failure status.
   */
  applyCharacterData(userId: number, rows: readonly CharacterDataPatch[]): boolean {
    this.#db.exec("BEGIN IMMEDIATE");
    try {
      for (const row of rows) {
        if (!Number.isSafeInteger(row.slot) || row.slot < 0 || row.slot > 19) {
          throw new Error("bad slot");
        }
        if (!Number.isSafeInteger(row.characterType) || row.characterType < 1 || row.characterType > 15) {
          throw new Error("bad character type");
        }
        if (row.appearance.length !== 12 || !row.appearance.every((v) => Number.isSafeInteger(v) && v >= 0 && v <= 0xffff)) {
          throw new Error("bad appearance");
        }
        const params: Record<string, number> = { p: userId, s: row.slot, t: row.characterType };
        row.appearance.forEach((value, index) => { params[`a${index}`] = value; });
        const result = this.#db
          .query(
            `UPDATE player_character
                SET character_type = $t,
                    appearance0 = $a0, appearance1 = $a1, appearance2 = $a2,
                    appearance3 = $a3, appearance4 = $a4, appearance5 = $a5,
                    appearance6 = $a6, appearance7 = $a7, appearance8 = $a8,
                    appearance9 = $a9, appearance10 = $a10, appearance11 = $a11
              WHERE player_id = $p AND slot = $s`,
          )
          .run(params);
        if (result.changes !== 1) throw new Error("unknown character slot");
      }
      this.#db.exec("COMMIT");
      return true;
    } catch {
      this.#db.exec("ROLLBACK");
      return false;
    }
  }

  /** Load the native 198 MyInfo projection by its user ID. */
  getMyInfo(userId: number): MyInfo | null {
    const row = this.#db
      .query<PlayerRow, { u: number }>(
        `SELECT p.id, p.nickname, p.level, p.experience, p.game_points, p.cash,
                p.current_character,
                s.wins, s.losses, s.kills, s.deaths, s.headshots, s.combos, s.hearts,
                s.double_kill, s.triple_kill, s.criticals, s.multi_kill, s.ultra_kill,
                s.z_kill, s.k_kill, s.dd_kill, s.play_time_seconds
           FROM player p
           JOIN player_stats s ON s.player_id = p.id
          WHERE p.id = $u`,
      )
      .get({ u: userId });
    if (!row) return null;

    const characters = this.#db
      .query<CharacterRow, { p: number }>(
        `SELECT player_id, slot, character_type,
                appearance0, appearance1, appearance2, appearance3, appearance4, appearance5,
                appearance6, appearance7, appearance8, appearance9, appearance10, appearance11
           FROM player_character WHERE player_id = $p ORDER BY slot`,
      )
      .all({ p: row.id })
      .map((character) => this.repairCanonicalCharacter(character))
      .map(characterFromRow);
    // The DB stores the persistent slot key, while native 198/247 serialize
    // only the ordered character rows. Map the key to that compact wire index;
    // never send the persistent slot number as the native selected index.
    const selectedCharIndex = characters.findIndex(
      (character) => character.slotNo === row.current_character,
    );
    if (selectedCharIndex < 0) {
      throw new Error("current_character does not identify a serialized character");
    }

    return {
      userId: row.id,
      nickname: row.nickname,
      level: row.level,
      experience: row.experience,
      gamePoints: row.game_points,
      cash: row.cash,
      coupon: 0,
      selectedCharIndex,
      stats: statsFromRow(row),
      characters,
    };
  }

  /** Returns the account on success, or null for unknown user / bad password. */
  async verifyLogin(username: string, password: string): Promise<Account | null> {
    const account = this.findAccount(username);
    if (!account) {
      await Bun.password.verify(password, ABSENT_USER_HASH).catch(() => false);
      return null;
    }
    const ok = await Bun.password.verify(password, account.passwordHash);
    if (!ok) return null;
    this.#db
      .query("UPDATE account SET last_login_at = $t WHERE id = $i")
      .run({ t: Date.now(), i: account.id });
    return account;
  }

  close(): void {
    this.#db.close();
  }
}
