import { describe, expect, test } from "bun:test";
import { Store } from "../src/store.ts";

describe("store", () => {
  test("runs on the pinned SQLite version", () => {
    const store = new Store();
    expect(store.sqliteVersion).toBe("3.53.4");
    store.close();
  });

  test("creates and finds an account", async () => {
    const store = new Store();
    const created = await store.createAccount("alice", "hunter2");
    expect(created.username).toBe("alice");
    expect(created.id).toBeGreaterThan(0);
    expect(created.lastLoginAt).toBeNull();

    expect(store.findAccount("alice")?.id).toBe(created.id);
    expect(store.findAccount("nobody")).toBeNull();
    store.close();
  });

  test("passwords are hashed, not stored", async () => {
    const store = new Store();
    const account = await store.createAccount("bob", "s3cret");
    expect(account.passwordHash).not.toContain("s3cret");
    expect(account.passwordHash.startsWith("$argon2")).toBe(true);
    store.close();
  });

  test("verifies logins and stamps the time", async () => {
    const store = new Store();
    await store.createAccount("carol", "pw");

    expect(await store.verifyLogin("carol", "wrong")).toBeNull();
    expect(await store.verifyLogin("ghost", "pw")).toBeNull();

    const ok = await store.verifyLogin("carol", "pw");
    expect(ok?.username).toBe("carol");
    expect(store.findAccount("carol")?.lastLoginAt).toBeGreaterThan(0);
    store.close();
  });

  test("rejects usernames the client could never send", async () => {
    const store = new Store();
    // sub_43DD60 allows only [0-9A-Za-z@].
    expect(Store.isValidUsername("Ab9@")).toBe(true);
    expect(Store.isValidUsername("has space")).toBe(false);
    expect(Store.isValidUsername("drop;table")).toBe(false);
    expect(Store.isValidUsername("")).toBe(false);
    await expect(store.createAccount("bad user", "pw")).rejects.toThrow(RangeError);
    store.close();
  });

  test("usernames are unique", async () => {
    const store = new Store();
    await store.createAccount("dave", "pw");
    await expect(store.createAccount("dave", "pw2")).rejects.toThrow();
    store.close();
  });
});
