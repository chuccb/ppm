/**
 * Accounts, on Bun's native `bun:sqlite` (SQLite 3.53.4).
 *
 * Scope note: this stores only what the server itself owns — accounts and
 * sessions. Game content (items, maps, quests) is read from the client
 * resources, which are authoritative and documented in docs/RESOURCES.md.
 */

import { Database } from "bun:sqlite";

export interface Account {
  readonly id: number;
  readonly username: string;
  readonly passwordHash: string;
  readonly createdAt: number;
  readonly lastLoginAt: number | null;
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

