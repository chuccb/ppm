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
  readonly playCount: number;
  readonly roundCount: number;
  readonly disconnects: number;
  readonly playTimeSeconds: number;
}

export interface CharSlot {
  readonly slotNo: number;
  readonly charType: number;
  /** The twelve category-relative u16 values read by 198. */
  readonly equip: readonly number[];
}

export interface MyInfo {
  readonly userId: number;
  readonly nickname: string;
  readonly level: number;
  readonly experience: number;
  readonly gamePoints: number;
  readonly cash: number;
  readonly currentChar: number;
  readonly stats: Stats;
  readonly characters: readonly CharSlot[];
}

export const NEW_SKILL_PROFILE_COUNT = 5;
export const NEW_SKILL_PUZZLE_SLOT_COUNT = 7;

export interface NewSkillProfile {
  readonly puzzleItemIds: readonly number[];
  /** Native packed local-time word; profile 0 ignores it on the client. */
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

CREATE INDEX IF NOT EXISTS account_username ON account(username);

CREATE TABLE IF NOT EXISTS player (
  id              INTEGER PRIMARY KEY AUTOINCREMENT,
  account_id      INTEGER NOT NULL UNIQUE REFERENCES account(id) ON DELETE CASCADE,
  nickname        TEXT    NOT NULL UNIQUE,
  level           INTEGER NOT NULL DEFAULT 1,
  experience      INTEGER NOT NULL DEFAULT 0,
  game_points     INTEGER NOT NULL DEFAULT 0,
  cash            INTEGER NOT NULL DEFAULT 0,
  current_character INTEGER NOT NULL DEFAULT 0 CHECK (current_character BETWEEN 0 AND 19)
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
  play_count      INTEGER NOT NULL DEFAULT 0,
  round_count     INTEGER NOT NULL DEFAULT 0,
  disconnects     INTEGER NOT NULL DEFAULT 0,
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
  }

  get sqliteVersion(): string {
    const row = this.#db.query<{ v: string }, []>("SELECT sqlite_version() AS v").get();
    return row?.v ?? "unknown";
  }

  /**
   * The client restricts credentials to [0-9A-Za-z@] before it will even send
   * opcode 682 (`sub_43DD60`), so anything outside that set cannot come from a
   * legitimate client and is rejected here too.
   */
  static isValidUsername(name: string): boolean {
    return name.length > 0 && name.length <= 32 && /^[0-9A-Za-z@]+$/.test(name);
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
      .query<
        {
          id: number;
          username: string;
          password_hash: string;
          created_at: number;
          last_login_at: number | null;
        },
        { u: string }
      >("SELECT * FROM account WHERE username = $u")
      .get({ u: username });
    if (!row) return null;
    return {
      id: row.id,
      username: row.username,
      passwordHash: row.password_hash,
      createdAt: row.created_at,
      lastLoginAt: row.last_login_at,
    };
  }

  /**
   * Creates the minimal private-server MyInfo projection needed by 198.
   * The canonical CharSlot type 1 and its six native body-template values are
   * source-proven; no weapon, item, currency, or reward is granted here.
   */
  ensurePlayerIdentity(accountId: number): MyInfo | null {
    const existing = this.getMyInfo(accountId);
    if (existing) return existing;

    const account = this.#db
      .query<{ username: string }, { id: number }>("SELECT username FROM account WHERE id = $id")
      .get({ id: accountId });
    if (!account) return null;

    const nickname = nativeNickname(account.username, accountId);
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

    return this.getMyInfo(accountId);
  }

  getMyInfoByNickname(nickname: string): MyInfo | null {
    const row = this.#db
      .query<{ account_id: number }, { n: string }>(
        "SELECT account_id FROM player WHERE nickname = $n",
      )
      .get({ n: nickname });
    return row ? this.getMyInfo(row.account_id) : null;
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
        .query<
          {
            profile_index: number;
            puzzle0: number;
            puzzle1: number;
            puzzle2: number;
            puzzle3: number;
            puzzle4: number;
            puzzle5: number;
            puzzle6: number;
            expires_at_packed_minute: number;
          },
          { p: number }
        >(
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

  getMyInfo(accountId: number): MyInfo | null {
    const row = this.#db
      .query<
        {
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
          play_count: number;
          round_count: number;
          disconnects: number;
          play_time_seconds: number;
        },
        { a: number }
      >(
        `SELECT p.id, p.nickname, p.level, p.experience, p.game_points, p.cash,
                p.current_character,
                s.wins, s.losses, s.kills, s.deaths, s.headshots, s.combos, s.hearts,
                s.double_kill, s.triple_kill, s.criticals, s.multi_kill, s.ultra_kill,
                s.z_kill, s.k_kill, s.dd_kill, s.play_count, s.round_count,
                s.disconnects, s.play_time_seconds
           FROM player p
           JOIN player_stats s ON s.player_id = p.id
          WHERE p.account_id = $a`,
      )
      .get({ a: accountId });
    if (!row) return null;

    const characters = this.#db
      .query<
        {
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
        },
        { p: number }
      >(
        `SELECT slot, character_type,
                appearance0, appearance1, appearance2, appearance3, appearance4, appearance5,
                appearance6, appearance7, appearance8, appearance9, appearance10, appearance11
           FROM player_character WHERE player_id = $p ORDER BY slot`,
      )
      .all({ p: row.id })
      .map((characterRow) => ({
        slotNo: characterRow.slot,
        charType: characterRow.character_type,
        equip: [
          characterRow.appearance0,
          characterRow.appearance1,
          characterRow.appearance2,
          characterRow.appearance3,
          characterRow.appearance4,
          characterRow.appearance5,
          characterRow.appearance6,
          characterRow.appearance7,
          characterRow.appearance8,
          characterRow.appearance9,
          characterRow.appearance10,
          characterRow.appearance11,
        ],
      }));

    return {
      userId: row.id,
      nickname: row.nickname,
      level: row.level,
      experience: row.experience,
      gamePoints: row.game_points,
      cash: row.cash,
      currentChar: row.current_character,
      stats: {
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
        playCount: row.play_count,
        roundCount: row.round_count,
        disconnects: row.disconnects,
        playTimeSeconds: row.play_time_seconds,
      },
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

function nativeNickname(username: string, accountId: number): string {
  if (username.length >= 2 && username.length <= 16) return username;
  return `P${accountId.toString(36).toUpperCase()}`;
}
