import { describe, expect, test } from "bun:test";
import { Packet, decode } from "../src/packet.ts";
import { opcodeFor } from "../src/opcodes.ts";
import {
  build as buildPacket,
  handlerFor,
  summary,
  type OutboundArgs,
  type OutboundName,
} from "../src/ops/registry.ts";
import { Result, type GameServer } from "../src/ops/s2c/GL_LOGIN_ACK.ts";
import { read as readCredentials } from "../src/ops/c2s/GL_LOGIN_REQ.ts";
import dataRecvCompletedRequest from "../src/ops/c2s/GL_DATA_RECV_COMPLETED_REQ.ts";
import roomBroadcastRequest from "../src/ops/c2s/GG_ROOMBROADCAST_REQ.ts";
import msgAddRequest from "../src/ops/c2s/GL_MSG_ADD_REQ.ts";
import msgDelRequest from "../src/ops/c2s/GL_MSG_DEL_REQ.ts";
import newMsgCountRequest from "../src/ops/c2s/GL_NEW_MSG_COUNT_REQ.ts";
import voiceItemSlotRequest from "../src/ops/c2s/GL_VOICEITEMSLOT_REQ.ts";
import friendAddRequest from "../src/ops/c2s/GL_FRIEND_ADD_REQ.ts";
import friendChatRequest from "../src/ops/c2s/GL_FRIEND_CHAT_REQ.ts";
import friendDelRequest from "../src/ops/c2s/GL_FRIEND_DEL_REQ.ts";
import friendInfoRequest from "../src/ops/c2s/GL_FRIEND_INFO_REQ.ts";
import friendWhereRequest from "../src/ops/c2s/GL_FRIEND_WHERE_REQ.ts";
import msgReadRequest from "../src/ops/c2s/GL_MSG_READ_REQ.ts";
import billTokenRequest from "../src/ops/c2s/GL_BILLTOKEN_REQ.ts";
import buyCharRequest from "../src/ops/c2s/GS_BUYCHAR_REQ.ts";
import changeChannelRequest from "../src/ops/c2s/GL_CHANGECHANNEL_REQ.ts";
import changeSkillItemSlotRequest from "../src/ops/c2s/GI_CHANGE_SKILLITEMSLOT_REQ.ts";
import gamecenterGameEndRequest from "../src/ops/c2s/GG_GAMECENTER_GAME_END_REQ.ts";
import gamecenterPlayCheckRequest from "../src/ops/c2s/GG_GAMECENTER_GAME_PLAY_CHECK_REQ.ts";
import gamecenterGameStartOkRequest from "../src/ops/c2s/GG_GAMECENTER_GAME_START_OK_REQ.ts";
import gameRoomProgressTimeRequest from "../src/ops/c2s/GL_GET_GAMEROOM_PROGRESSTIME_REQ.ts";
import doVotingRequest from "../src/ops/c2s/GR_DO_VOTING.ts";
import weaponpartsEquipChangeRequest from "../src/ops/c2s/GL_WEAPONPARTS_EQUIP_CHANGE_REQ.ts";
import aiRewardItemRequest from "../src/ops/c2s/GR_AI_GET_REWARD_ITEM_REQ.ts";
import damageShieldRequest from "../src/ops/c2s/GR_AI_DAMAGE_SHIELD_REQ.ts";
import magazineStartRequest from "../src/ops/c2s/GR_AI_RECHARGE_MAGAZINE_START_REQ.ts";
import magazineEndRequest from "../src/ops/c2s/GR_AI_RECHARGE_MAGAZINE_END_REQ.ts";
import continueStartRequest from "../src/ops/c2s/GR_AI_CONTINUE_START_REQ.ts";
import startVotingRequest from "../src/ops/c2s/GR_START_VOTING_REQ.ts";
import gamecenterRankingRequest from "../src/ops/c2s/GG_GAMECENTER_RANKING_REQ.ts";
import gamecenterGameStartRequest from "../src/ops/c2s/GG_GAMECENTER_GAME_START_REQ.ts";
import gamecenterRecRequest from "../src/ops/c2s/GL_GAMECENTER_REC_REQ.ts";
import deleteGiftRequest from "../src/ops/c2s/GS_DELETEGIFT_REQ.ts";
import changeSlotRequest from "../src/ops/c2s/GI_CHANGESLOT_REQ.ts";
import changeDataRequest from "../src/ops/c2s/GI_CHANGEDATA_REQ.ts";
import forceoutRequest from "../src/ops/c2s/GR_FORCEOUT_REQ.ts";
import questAcceptDailyRequest from "../src/ops/c2s/GQ_QUEST_ACCEPT_DAILY_REQ.ts";
import questUserCompleteHonorRequest from "../src/ops/c2s/GQ_QUEST_USER_COMPLETE_HONOR_REQ.ts";
import rackingWebTokenRequest from "../src/ops/c2s/GL_RACKINGWEB_TOKEN_REQ.ts";
import levelKillLimitRequest from "../src/ops/c2s/GL_LEVEL_KILL_LIMIT_REQ.ts";
import tutorialIndexRequest from "../src/ops/c2s/GL_TUTORIALINDEX_REQ.ts";
import tutorialIndexSetRequest from "../src/ops/c2s/GL_TUTORIAL_INDEX_SET_REQ.ts";
import userListRequest from "../src/ops/c2s/GL_USERLIST_REQ.ts";

const build = <N extends OutboundName>(name: N, ...args: OutboundArgs<N>) =>
  decode(buildPacket(name, ...args).encode());

// Keep the type-only contract honest without executing invalid calls.
if (false) {
  // @ts-expect-error c2s names are not outbound builders
  buildPacket("GL_LOGIN_REQ");
  // @ts-expect-error GL_LOGIN_ACK requires its result argument
  buildPacket("GL_LOGIN_ACK");
  // @ts-expect-error the result argument is not a string
  buildPacket("GL_LOGIN_ACK", "not-a-result");
}

/** Build a 682 exactly as the client's builder does. */
function clientLoginRequest(account: string, password: string, dataRevision = 811034967) {
  const low = 0xf1e1ab0en;
  const high = BigInt((dataRevision ^ 0xb1a9d7c7) >>> 0);
  return new Packet(opcodeFor("GL_LOGIN_REQ"))
    .str(account)
    .str(password)
    .u64((high << 32n) | low)
    .u8(2)
    .zeros(24);
}

const reread = (packet: Packet) => decode(packet.encode());

describe("694 — compression threshold and login trigger", () => {
  test("carries the threshold as a u16", () => {
    const reader = build("GL_ACCOUNTCONNSUCC");
    expect(reader.opcode).toBe(694);
    expect(reader.u16()).toBe(0x2580); // compression disabled
  });

  test("preserves every valid native u16 threshold, including ignored values", () => {
    expect(reread(buildPacket("GL_ACCOUNTCONNSUCC", 0x2581)).u16()).toBe(0x2581);
    expect(reread(buildPacket("GL_ACCOUNTCONNSUCC", 0)).u16()).toBe(0);
    expect(() => buildPacket("GL_ACCOUNTCONNSUCC", 0x10000)).toThrow(RangeError);
    expect(() => buildPacket("GL_ACCOUNTCONNSUCC", 1.5)).toThrow(RangeError);
  });
});

describe("419 — add-message request", () => {
  const pipeline = (payload: Packet, replies: unknown[][]) => {
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof msgAddRequest>[1];
    msgAddRequest(reread(payload), connection);
  };
  const draft = () => new Packet(opcodeFor("GL_MSG_ADD_REQ"))
    .u8(1)
    .str("me")
    .s32(0x1357)
    .str("you")
    .str("hi")
    .str("t")
    .u16(0)
    .u8(0);

  test("parses the 8-field wire grammar and answers via the native default arm", () => {
    const replies: unknown[][] = [];
    pipeline(draft(), replies);
    expect(replies).toEqual([["GL_MSG_ADD_ACK", "you", 6, 0]]);
  });

  test("refuses trailing bytes and native sender-gate violations", () => {
    const replies: unknown[][] = [];
    expect(() => pipeline(draft().u8(0), replies)).toThrow(/trailing/);
    expect(() =>
      pipeline(
        new Packet(opcodeFor("GL_MSG_ADD_REQ"))
          .u8(1).str("me").s32(0).str("").str("hi").str("t").u16(0).u8(0),
        replies,
      ),
    ).toThrow(/1\.\.24/);
    expect(() =>
      pipeline(
        new Packet(opcodeFor("GL_MSG_ADD_REQ"))
          .u8(1).str("me").s32(0).str("you".padEnd(25, "x")).str("hi").str("t").u16(0).u8(0),
        replies,
      ),
    ).toThrow(/1\.\.24/);
    expect(() =>
      pipeline(
        new Packet(opcodeFor("GL_MSG_ADD_REQ"))
          .u8(1).str("me").s32(0).str("you").str("").str("t").u16(0).u8(0),
        replies,
      ),
    ).toThrow(/1\.\.200/);
    expect(() =>
      pipeline(
        new Packet(opcodeFor("GL_MSG_ADD_REQ"))
          .u8(1).str("me").s32(0).str("you").str("h".repeat(201)).str("t").u16(0).u8(0),
        replies,
      ),
    ).toThrow(/1\.\.200/);
  });
});

describe("421/423 — mailbox key requests", () => {
  const pipeline = (handler: typeof msgDelRequest, payload: Packet, replies: unknown[][]) => {
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof msgDelRequest>[1];
    handler(reread(payload), connection);
  };

  test("421 parses {str key} and echoes it with the empty-mailbox failure status", () => {
    const replies: unknown[][] = [];
    pipeline(msgDelRequest, new Packet(opcodeFor("GL_MSG_DEL_REQ")).str("mail1"), replies);
    expect(replies).toEqual([["GL_MSG_DEL_ACK", 0, "mail1"]]);
    expect(() =>
      pipeline(msgDelRequest, new Packet(opcodeFor("GL_MSG_DEL_REQ")).str("mail1").u8(0), []),
    ).toThrow(/trailing/);
    expect(() =>
      pipeline(msgDelRequest, new Packet(opcodeFor("GL_MSG_DEL_REQ")).str(""), []),
    ).toThrow(/non-empty/);
    expect(() =>
      pipeline(msgDelRequest, new Packet(opcodeFor("GL_MSG_DEL_REQ")).str("k".repeat(20)), []),
    ).toThrow(/char\[20\]/);
  });

  test("423 mirrors the same key grammar against its own ACK name", () => {
    const replies: unknown[][] = [];
    pipeline(msgReadRequest, new Packet(opcodeFor("GL_MSG_READ_REQ")).str("mail1"), replies);
    expect(replies).toEqual([["GL_MSG_READ_ACK", 0, "mail1"]]);
    expect(() =>
      pipeline(msgReadRequest, new Packet(opcodeFor("GL_MSG_READ_REQ")).str(""), []),
    ).toThrow(/non-empty/);
  });
});

describe("429/431 — friend key requests", () => {
  const mkConnection = (replies: unknown[][], nickname: string | null) => ({
    reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    accountId: nickname === null ? null : 42,
    config: { store: { ensurePlayerIdentity: () => (nickname === null ? null : { nickname }) } },
  }) as unknown as Parameters<typeof friendAddRequest>[1];

  test("429 answers the native else arm (5), status 1 only for the self nickname", () => {
    const replies: unknown[][] = [];
    friendAddRequest(
      reread(new Packet(opcodeFor("GL_FRIEND_ADD_REQ")).str("frnd")),
      mkConnection(replies, null),
    );
    expect(replies).toEqual([["GL_FRIEND_ADD_ACK", 5, "frnd"]]);

    const selfReplies: unknown[][] = [];
    friendAddRequest(
      reread(new Packet(opcodeFor("GL_FRIEND_ADD_REQ")).str("hero")),
      mkConnection(selfReplies, "hero"),
    );
    expect(selfReplies).toEqual([["GL_FRIEND_ADD_ACK", 1, "hero"]]);

    // native send gate: non-empty with strlen <= 23
    const gateReplies: unknown[][] = [];
    friendAddRequest(
      reread(new Packet(opcodeFor("GL_FRIEND_ADD_REQ")).str("n".repeat(23))),
      mkConnection(gateReplies, null),
    );
    expect(gateReplies).toEqual([["GL_FRIEND_ADD_ACK", 5, "n".repeat(23)]]);
    expect(() =>
      friendAddRequest(reread(new Packet(opcodeFor("GL_FRIEND_ADD_REQ")).str("n".repeat(24))), mkConnection([], null)),
    ).toThrow(/23-byte/);
    expect(() =>
      friendAddRequest(
        reread(new Packet(opcodeFor("GL_FRIEND_ADD_REQ")).str("frnd").u8(0)),
        mkConnection([], null),
      ),
    ).toThrow(/trailing/);
    expect(() =>
      friendAddRequest(reread(new Packet(opcodeFor("GL_FRIEND_ADD_REQ")).str("")), mkConnection([], null)),
    ).toThrow(/non-empty/);
  });

  test("431 always answers the native failure arm (2) with the echoed key", () => {
    const replies: unknown[][] = [];
    friendDelRequest(
      reread(new Packet(opcodeFor("GL_FRIEND_DEL_REQ")).str("frnd")),
      mkConnection(replies, null),
    );
    expect(replies).toEqual([["GL_FRIEND_DEL_ACK", 2, "frnd"]]);
    expect(() =>
      friendDelRequest(reread(new Packet(opcodeFor("GL_FRIEND_DEL_REQ")).str("")), mkConnection([], null)),
    ).toThrow(/non-empty/);
    expect(() =>
      friendDelRequest(reread(new Packet(opcodeFor("GL_FRIEND_DEL_REQ")).str("n".repeat(24))), mkConnection([], null)),
    ).toThrow(/23-byte/);
  });
});

describe("435 — friend-info csv request", () => {
  const pipeline = (csv: string) => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof friendInfoRequest>[1];
    friendInfoRequest(reread(new Packet(opcodeFor("GL_FRIEND_INFO_REQ")).str(csv)), connection);
    return replies;
  };

  test("parses one comma-separated name list and echoes every row with statusRaw 0", () => {
    expect(pipeline("frndA,frndB")).toEqual([[
      "GL_FRIEND_INFO_ACK",
      [
        { nickname: "frndA", statusRaw: 0 },
        { nickname: "frndB", statusRaw: 0 },
      ],
    ]]);
    // the native builder never sends an empty list, but an empty string stays shaped
    expect(pipeline("")).toEqual([["GL_FRIEND_INFO_ACK", []]]);
  });

  test("refuses trailing bytes, oversized names, and native-capacity violations", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof friendInfoRequest>[1];
    expect(() =>
      friendInfoRequest(
        reread(new Packet(opcodeFor("GL_FRIEND_INFO_REQ")).str("a,b").u8(0)),
        connection,
      ),
    ).toThrow(/trailing/);
    expect(() => pipeline(`${"n".repeat(21)}`)).toThrow(/20-byte/);
    expect(() => pipeline("a,,b")).toThrow(/non-empty/);
    expect(() => pipeline(Array(101).fill("a").join(","))).toThrow(/100 names/);
    expect(() => pipeline("x".repeat(1024))).toThrow(/char\[1024\]/);
  });
});

describe("437 — room broadcast request", () => {
  test("parses {u8 flag, s32 len, raw[len]} and deliberately stays silent", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof roomBroadcastRequest>[1];
    roomBroadcastRequest(
      reread(new Packet(opcodeFor("GG_ROOMBROADCAST_REQ")).u8(7).s32(3).raw(new Uint8Array([1, 2, 3]))),
      connection,
    );
    expect(replies).toEqual([]);
    expect(() =>
      roomBroadcastRequest(
        reread(new Packet(opcodeFor("GG_ROOMBROADCAST_REQ")).u8(7).s32(3).raw(new Uint8Array([1, 2]))),
        connection,
      ),
    ).toThrow(); // s32 len lies about the blob: raw() bounds-check fires
    expect(() =>
      roomBroadcastRequest(
        reread(new Packet(opcodeFor("GG_ROOMBROADCAST_REQ")).u8(7).s32(-1)),
        connection,
      ),
    ).toThrow(/non-negative/);
    expect(() =>
      roomBroadcastRequest(
        reread(new Packet(opcodeFor("GG_ROOMBROADCAST_REQ")).u8(7).s32(0).u8(0)),
        connection,
      ),
    ).toThrow(/trailing/);
  });
});

describe("439 — friend-chat request", () => {
  test("parses {s32 context, str, str, str} and always answers the proven status-3 arm", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof friendChatRequest>[1];
    friendChatRequest(
      reread(new Packet(opcodeFor("GL_FRIEND_CHAT_REQ")).s32(0x1234).str("me").str("you").str("hi")),
      connection,
    );
    expect(replies).toEqual([["GL_FRIEND_CHAT_ACK", 3, "me", "you"]]);

    // native gate: strlen(message) <= 180
    friendChatRequest(
      reread(new Packet(opcodeFor("GL_FRIEND_CHAT_REQ")).s32(0).str("a").str("b").str("m".repeat(180))),
      connection,
    );
    expect(replies).toHaveLength(2);
    expect(() =>
      friendChatRequest(
        reread(new Packet(opcodeFor("GL_FRIEND_CHAT_REQ")).s32(0).str("a").str("b").str("m".repeat(181))),
        connection,
      ),
    ).toThrow(/180-byte/);
    expect(() =>
      friendChatRequest(
        reread(new Packet(opcodeFor("GL_FRIEND_CHAT_REQ")).s32(0).str("a").str("b").str("c").u8(0)),
        connection,
      ),
    ).toThrow(/trailing/);
  });
});

describe("441 — friend-where request", () => {
  test("always answers the proven status-0 failure arm without the conditional triple", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof friendWhereRequest>[1];
    friendWhereRequest(reread(new Packet(opcodeFor("GL_FRIEND_WHERE_REQ")).str("frnd")), connection);
    expect(replies).toEqual([["GL_FRIEND_WHERE_ACK", 0]]);
    expect(() =>
      friendWhereRequest(reread(new Packet(opcodeFor("GL_FRIEND_WHERE_REQ")).str("")), connection),
    ).toThrow(/non-empty/);
    expect(() =>
      friendWhereRequest(
        reread(new Packet(opcodeFor("GL_FRIEND_WHERE_REQ")).str("n".repeat(21))),
        connection,
      ),
    ).toThrow(/20-byte/);
    expect(() =>
      friendWhereRequest(
        reread(new Packet(opcodeFor("GL_FRIEND_WHERE_REQ")).str("frnd").u8(0)),
        connection,
      ),
    ).toThrow(/trailing/);
  });
});

describe("783/791 — mailbox-count and voice-slot requests", () => {
  const pipeline = (handler: typeof newMsgCountRequest, payload: Packet, replies: unknown[][]) => {
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof newMsgCountRequest>[1];
    handler(reread(payload), connection);
  };

  test("783 parses empty and answers with the s32 zero unread count", () => {
    const replies: unknown[][] = [];
    pipeline(newMsgCountRequest, new Packet(opcodeFor("GL_NEW_MSG_COUNT_REQ")), replies);
    expect(replies).toEqual([["GL_NEW_MSG_COUNT_ACK", 0]]);
    expect(() =>
      pipeline(newMsgCountRequest, new Packet(opcodeFor("GL_NEW_MSG_COUNT_REQ")).u8(0), []),
    ).toThrow(/trailing/);
  });

  test("791 parses empty and answers on the proven page index 0", () => {
    const replies: unknown[][] = [];
    pipeline(voiceItemSlotRequest, new Packet(opcodeFor("GL_VOICEITEMSLOT_REQ")), replies);
    expect(replies).toEqual([["GL_VOICEITEMSLOT_ACK", 0]]);
    expect(() =>
      pipeline(voiceItemSlotRequest, new Packet(opcodeFor("GL_VOICEITEMSLOT_REQ")).u8(0), []),
    ).toThrow(/trailing/);
  });
});

describe("685/689 — tutorial index requests", () => {
  test("685 parses empty and answers s32(0) (blank board)", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof tutorialIndexRequest>[1];
    tutorialIndexRequest(reread(new Packet(opcodeFor("GL_TUTORIALINDEX_REQ"))), connection);
    expect(replies).toEqual([["GL_TUTORIALINDEX_ACK", 0]]);
    expect(() =>
      tutorialIndexRequest(
        reread(new Packet(opcodeFor("GL_TUTORIALINDEX_REQ")).u8(0)),
        connection,
      ),
    ).toThrow(/trailing/);
  });

  test("689 parses one s32 and deliberately stays silent (no native case 690)", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof tutorialIndexSetRequest>[1];
    tutorialIndexSetRequest(
      reread(new Packet(opcodeFor("GL_TUTORIAL_INDEX_SET_REQ")).s32(7)),
      connection,
    );
    expect(replies).toEqual([]);
    expect(() =>
      tutorialIndexSetRequest(
        reread(new Packet(opcodeFor("GL_TUTORIAL_INDEX_SET_REQ")).s32(7).u8(0)),
        connection,
      ),
    ).toThrow(/trailing/);
  });
});

describe("704 — level/kill-limit request", () => {
  test("704 parses empty and replies the proven silent arm (0, 0.0, 0)", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof levelKillLimitRequest>[1];
    levelKillLimitRequest(
      reread(new Packet(opcodeFor("GL_LEVEL_KILL_LIMIT_REQ"))),
      connection,
    );
    expect(replies).toEqual([["GL_LEVEL_KILL_LIMIT_ACK", 0, 0, 0]]);
  });

  test("704 refuses trailing bytes", () => {
    const connection = {
      reply: () => undefined,
    } as unknown as Parameters<typeof levelKillLimitRequest>[1];
    expect(() =>
      levelKillLimitRequest(
        reread(new Packet(opcodeFor("GL_LEVEL_KILL_LIMIT_REQ")).u8(0)),
        connection,
      ),
    ).toThrow(/704/);
  });
});

describe("706 — bill-token request", () => {
  test("706 parses empty and replies the inert empty token", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof billTokenRequest>[1];
    billTokenRequest(reread(new Packet(opcodeFor("GL_BILLTOKEN_REQ"))), connection);
    expect(replies).toEqual([["GL_BILLTOKEN_ACK", ""]]);
  });

  test("706 refuses trailing bytes", () => {
    const connection = {
      reply: () => undefined,
    } as unknown as Parameters<typeof billTokenRequest>[1];
    expect(() =>
      billTokenRequest(
        reread(new Packet(opcodeFor("GL_BILLTOKEN_REQ")).u8(0)),
        connection,
      ),
    ).toThrow(/706/);
  });
});

describe("787 — ranking-web token request", () => {
  test("787 parses empty and replies hasToken=0 (no ranking-web model)", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof rackingWebTokenRequest>[1];
    rackingWebTokenRequest(
      reread(new Packet(opcodeFor("GL_RACKINGWEB_TOKEN_REQ"))),
      connection,
    );
    expect(replies).toEqual([["GL_RACKINGWEB_TOKEN_ACK", 0]]);
  });

  test("787 refuses trailing bytes", () => {
    const connection = {
      reply: () => undefined,
    } as unknown as Parameters<typeof rackingWebTokenRequest>[1];
    expect(() =>
      rackingWebTokenRequest(
        reread(new Packet(opcodeFor("GL_RACKINGWEB_TOKEN_REQ")).u8(0)),
        connection,
      ),
    ).toThrow(/787/);
  });
});

describe("876/878 — quest requests", () => {
  test("876 parses empty and replies the empty accept list (err 0, count 0)", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof questAcceptDailyRequest>[1];
    questAcceptDailyRequest(
      reread(new Packet(opcodeFor("GQ_QUEST_ACCEPT_DAILY_REQ"))),
      connection,
    );
    expect(replies).toEqual([["GQ_QUEST_ACCEPT_DAILY_ACK", 0]]);
  });

  test("876 refuses trailing bytes", () => {
    const connection = {
      reply: () => undefined,
    } as unknown as Parameters<typeof questAcceptDailyRequest>[1];
    expect(() =>
      questAcceptDailyRequest(
        reread(new Packet(opcodeFor("GQ_QUEST_ACCEPT_DAILY_REQ")).u8(0)),
        connection,
      ),
    ).toThrow(/876/);
  });

  test("878 parses exactly one u8 flag and replies err=1 (no honor model)", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof questUserCompleteHonorRequest>[1];
    questUserCompleteHonorRequest(
      reread(new Packet(opcodeFor("GQ_QUEST_USER_COMPLETE_HONOR_REQ")).u8(1)),
      connection,
    );
    expect(replies).toEqual([["GQ_QUEST_USER_COMPLETE_HONOR_ACK", 1]]);
  });

  test("878 refuses empty or longer payloads", () => {
    const connection = {
      reply: () => undefined,
    } as unknown as Parameters<typeof questUserCompleteHonorRequest>[1];
    expect(() =>
      questUserCompleteHonorRequest(
        reread(new Packet(opcodeFor("GQ_QUEST_USER_COMPLETE_HONOR_REQ"))),
        connection,
      ),
    ).toThrow(/878/);
    expect(() =>
      questUserCompleteHonorRequest(
        reread(new Packet(opcodeFor("GQ_QUEST_USER_COMPLETE_HONOR_REQ")).u8(1).u8(0)),
        connection,
      ),
    ).toThrow(/878/);
  });
});

describe("131 — forceout request", () => {
  test("131 parses one u8 slot and replies status=0 (silent arm, no room model)", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof forceoutRequest>[1];
    forceoutRequest(
      reread(new Packet(opcodeFor("GR_FORCEOUT_REQ")).u8(3)),
      connection,
    );
    expect(replies).toEqual([["GR_FORCEOUT_ACK", 0]]);
  });

  test("131 refuses empty or longer payloads", () => {
    const connection = {
      reply: () => undefined,
    } as unknown as Parameters<typeof forceoutRequest>[1];
    expect(() =>
      forceoutRequest(reread(new Packet(opcodeFor("GR_FORCEOUT_REQ"))), connection),
    ).toThrow(/131/);
    expect(() =>
      forceoutRequest(
        reread(new Packet(opcodeFor("GR_FORCEOUT_REQ")).u8(3).u8(0)),
        connection,
      ),
    ).toThrow(/131/);
  });
});

describe("218 — changedata request", () => {
  test("218 parses the native record grammar and replies status=1 (success arm)", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof changeDataRequest>[1];
    changeDataRequest(
      reread(new Packet(opcodeFor("GI_CHANGEDATA_REQ")).u8(2).u8(0)),
      connection,
    );
    expect(replies).toEqual([["GI_CHANGEDATA_ACK", 1]]);
  });

  test("218 walks count x 26-byte records and rejects the native cap overflow", () => {
    const rows = new Packet(opcodeFor("GI_CHANGEDATA_REQ")).u8(0).u8(1)
      .u8(3).u8(9).u16(0x1111).u16(0x2222).u16(0x3333).u16(0x4444)
      .u16(0x5555).u16(0x6666).u16(0x7777).u16(0x8888)
      .u16(0x9999).u16(0xaaaa).u16(0xbbbb).u16(0xcccc);
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof changeDataRequest>[1];
    changeDataRequest(reread(rows), connection);
    expect(replies).toEqual([["GI_CHANGEDATA_ACK", 1]]);
    expect(() =>
      changeDataRequest(
        reread(new Packet(opcodeFor("GI_CHANGEDATA_REQ")).u8(0).u8(0x15)),
        connection,
      ),
    ).toThrow(/0x14/);
    expect(() =>
      changeDataRequest(
        reread(new Packet(opcodeFor("GI_CHANGEDATA_REQ")).u8(0).u8(1).u8(0)),
        connection,
      ),
    ).toThrow(/218/);
  });
});

describe("310 — buy-char request", () => {
  test("310 parses 6 x s32 and replies status=0 (no purchase model)", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof buyCharRequest>[1];
    buyCharRequest(
      reread(new Packet(opcodeFor("GS_BUYCHAR_REQ"))
        .s32(7).s32(1).s32(2).s32(3).s32(4).s32(5)),
      connection,
    );
    expect(replies).toEqual([["GS_BUYCHAR_ACK", 0]]);
  });

  test("310 refuses payloads other than 24 bytes", () => {
    const connection = {
      reply: () => undefined,
    } as unknown as Parameters<typeof buyCharRequest>[1];
    expect(() =>
      buyCharRequest(
        reread(new Packet(opcodeFor("GS_BUYCHAR_REQ")).s32(7).s32(1)),
        connection,
      ),
    ).toThrow(/310/);
    expect(() =>
      buyCharRequest(
        reread(new Packet(opcodeFor("GS_BUYCHAR_REQ"))
          .s32(7).s32(1).s32(2).s32(3).s32(4).s32(5).u8(0)),
        connection,
      ),
    ).toThrow(/310/);
  });
});

describe("312 — change-slot request", () => {
  test("312 parses the u8 slot byte and echoes it in the ACK", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof changeSlotRequest>[1];
    changeSlotRequest(
      reread(new Packet(opcodeFor("GI_CHANGESLOT_REQ")).u8(7)),
      connection,
    );
    expect(replies).toEqual([["GI_CHANGESLOT_ACK", 7]]);
  });

  test("312 refuses empty or longer payloads", () => {
    const connection = {
      reply: () => undefined,
    } as unknown as Parameters<typeof changeSlotRequest>[1];
    expect(() =>
      changeSlotRequest(reread(new Packet(opcodeFor("GI_CHANGESLOT_REQ"))), connection),
    ).toThrow(/312/);
    expect(() =>
      changeSlotRequest(
        reread(new Packet(opcodeFor("GI_CHANGESLOT_REQ")).u8(7).u8(0)),
        connection,
      ),
    ).toThrow(/312/);
  });
});

describe("370 — change-channel request", () => {
  test("370 parses the u8 channel byte and replies status=0 (single-channel deployment)", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof changeChannelRequest>[1];
    changeChannelRequest(
      reread(new Packet(opcodeFor("GL_CHANGECHANNEL_REQ")).u8(2)),
      connection,
    );
    expect(replies).toEqual([["GL_CHANGECHANNEL_ACK", 0]]);
  });

  test("370 refuses empty or longer payloads", () => {
    const connection = {
      reply: () => undefined,
    } as unknown as Parameters<typeof changeChannelRequest>[1];
    expect(() =>
      changeChannelRequest(reread(new Packet(opcodeFor("GL_CHANGECHANNEL_REQ"))), connection),
    ).toThrow(/370/);
    expect(() =>
      changeChannelRequest(
        reread(new Packet(opcodeFor("GL_CHANGECHANNEL_REQ")).u8(2).u8(0)),
        connection,
      ),
    ).toThrow(/370/);
  });
});

describe("453 — delete-gift request", () => {
  test("453 parses 2 x s32 and replies status=0 (no gift model)", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof deleteGiftRequest>[1];
    deleteGiftRequest(
      reread(new Packet(opcodeFor("GS_DELETEGIFT_REQ")).s32(7).s32(42)),
      connection,
    );
    expect(replies).toEqual([["GS_DELETEGIFT_ACK", 0]]);
  });

  test("453 refuses payloads other than 8 bytes", () => {
    const connection = {
      reply: () => undefined,
    } as unknown as Parameters<typeof deleteGiftRequest>[1];
    expect(() =>
      deleteGiftRequest(
        reread(new Packet(opcodeFor("GS_DELETEGIFT_REQ")).s32(7)),
        connection,
      ),
    ).toThrow(/453/);
    expect(() =>
      deleteGiftRequest(
        reread(new Packet(opcodeFor("GS_DELETEGIFT_REQ")).s32(7).s32(42).u8(0)),
        connection,
      ),
    ).toThrow(/453/);
  });
});

describe("466 — change-skill-item-slot request", () => {
  test("466 parses the raw1==0 short form and replies the empty board", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof changeSkillItemSlotRequest>[1];
    changeSkillItemSlotRequest(
      reread(new Packet(opcodeFor("GI_CHANGE_SKILLITEMSLOT_REQ")).u8(1).u8(0)),
      connection,
    );
    expect(replies).toEqual([["GI_CHANGE_SKILLITEMSLOT_ACK", 0]]);
  });

  test("466 walks the raw1!=0 31-byte form and rejects wrong tails", () => {
    const bulk = new Packet(opcodeFor("GI_CHANGE_SKILLITEMSLOT_REQ")).u8(1).u8(1).u8(2);
    for (let i = 0; i < 7; i++) bulk.s32(100 + i);
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof changeSkillItemSlotRequest>[1];
    changeSkillItemSlotRequest(reread(bulk), connection);
    expect(replies).toEqual([["GI_CHANGE_SKILLITEMSLOT_ACK", 0]]);
    expect(() =>
      changeSkillItemSlotRequest(
        reread(new Packet(opcodeFor("GI_CHANGE_SKILLITEMSLOT_REQ")).u8(1)),
        connection,
      ),
    ).toThrow(/466/);
    expect(() =>
      changeSkillItemSlotRequest(
        reread(new Packet(opcodeFor("GI_CHANGE_SKILLITEMSLOT_REQ")).u8(1).u8(0).u8(0)),
        connection,
      ),
    ).toThrow(/466/);
    expect(() =>
      changeSkillItemSlotRequest(
        reread(new Packet(opcodeFor("GI_CHANGE_SKILLITEMSLOT_REQ")).u8(1).u8(1).u8(2)),
        connection,
      ),
    ).toThrow(/466/);
  });
});

describe("472 — gamecenter record request", () => {
  test("472 parses the u16 game_id and echoes it on an all-zero board", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof gamecenterRecRequest>[1];
    gamecenterRecRequest(
      reread(new Packet(opcodeFor("GL_GAMECENTER_REC_REQ")).u16(7)),
      connection,
    );
    expect(replies).toEqual([["GL_GAMECENTER_REC_ACK", 7]]);
  });

  test("472 refuses empty or longer payloads", () => {
    const connection = {
      reply: () => undefined,
    } as unknown as Parameters<typeof gamecenterRecRequest>[1];
    expect(() =>
      gamecenterRecRequest(reread(new Packet(opcodeFor("GL_GAMECENTER_REC_REQ"))), connection),
    ).toThrow(/472/);
    expect(() =>
      gamecenterRecRequest(
        reread(new Packet(opcodeFor("GL_GAMECENTER_REC_REQ")).u16(7).u8(0)),
        connection,
      ),
    ).toThrow(/472/);
  });
});

describe("474 — gamecenter game-start request", () => {
  test("474 parses {s16 game_id, u8 stage} and echoes with status=1", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof gamecenterGameStartRequest>[1];
    gamecenterGameStartRequest(
      reread(new Packet(opcodeFor("GG_GAMECENTER_GAME_START_REQ")).s16(7).u8(2)),
      connection,
    );
    expect(replies).toEqual([["GG_GAMECENTER_GAME_START_ACK", 7, 2]]);
  });

  test("474 refuses wrong payload widths", () => {
    const connection = {
      reply: () => undefined,
    } as unknown as Parameters<typeof gamecenterGameStartRequest>[1];
    expect(() =>
      gamecenterGameStartRequest(
        reread(new Packet(opcodeFor("GG_GAMECENTER_GAME_START_REQ")).s16(7)),
        connection,
      ),
    ).toThrow(/474/);
    expect(() =>
      gamecenterGameStartRequest(
        reread(new Packet(opcodeFor("GG_GAMECENTER_GAME_START_REQ")).s16(7).u8(2).u8(0)),
        connection,
      ),
    ).toThrow(/474/);
  });
});

describe("476 — gamecenter game-end request", () => {
  test("476 parses the 70-byte submission and answers the zero settlement", () => {
    const payload = new Packet(opcodeFor("GG_GAMECENTER_GAME_END_REQ")).s16(3);
    for (let i = 0; i < 24; i++) payload.u8(i % 4);
    for (let i = 0; i < 44; i++) payload.u8(i % 7);
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof gamecenterGameEndRequest>[1];
    gamecenterGameEndRequest(reread(payload), connection);
    expect(replies).toEqual([["GG_GAMECENTER_GAME_END_ACK", 3]]);
  });

  test("476 refuses wrong payload widths", () => {
    const connection = {
      reply: () => undefined,
    } as unknown as Parameters<typeof gamecenterGameEndRequest>[1];
    expect(() =>
      gamecenterGameEndRequest(
        reread(new Packet(opcodeFor("GG_GAMECENTER_GAME_END_REQ")).s16(3)),
        connection,
      ),
    ).toThrow(/476/);
    expect(() =>
      gamecenterGameEndRequest(
        reread(new Packet(opcodeFor("GG_GAMECENTER_GAME_END_REQ")).s16(3)
          .raw(new Uint8Array(38))
          .raw(new Uint8Array(44))
          .raw(new Uint8Array(1))),
        connection,
      ),
    ).toThrow(/476/);
  });
});

describe("478 — gamecenter play-check request", () => {
  test("478 parses exactly 36 bytes and deliberately stays silent (no native 479)", () => {
    const payload = new Packet(opcodeFor("GG_GAMECENTER_GAME_PLAY_CHECK_REQ"));
    for (let i = 0; i < 36; i++) payload.u8(i % 5);
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof gamecenterPlayCheckRequest>[1];
    gamecenterPlayCheckRequest(reread(payload), connection);
    expect(replies).toEqual([]);
  });

  test("478 refuses wrong payload widths", () => {
    const connection = {
      reply: () => undefined,
    } as unknown as Parameters<typeof gamecenterPlayCheckRequest>[1];
    expect(() =>
      gamecenterPlayCheckRequest(
        reread(new Packet(opcodeFor("GG_GAMECENTER_GAME_PLAY_CHECK_REQ")).raw(new Uint8Array(35))),
        connection,
      ),
    ).toThrow(/478/);
    expect(() =>
      gamecenterPlayCheckRequest(
        reread(new Packet(opcodeFor("GG_GAMECENTER_GAME_PLAY_CHECK_REQ")).raw(new Uint8Array(37))),
        connection,
      ),
    ).toThrow(/478/);
  });
});

describe("480 — gamecenter ranking request", () => {
  test("480 parses {s16 game_id, u8 mode} and echoes the zero board", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof gamecenterRankingRequest>[1];
    gamecenterRankingRequest(
      reread(new Packet(opcodeFor("GG_GAMECENTER_RANKING_REQ")).s16(7).u8(1)),
      connection,
    );
    expect(replies).toEqual([["GG_GAMECENTER_RANKING_ACK", 7]]);
  });

  test("480 refuses wrong payload widths", () => {
    const connection = {
      reply: () => undefined,
    } as unknown as Parameters<typeof gamecenterRankingRequest>[1];
    expect(() =>
      gamecenterRankingRequest(
        reread(new Packet(opcodeFor("GG_GAMECENTER_RANKING_REQ")).s16(7)),
        connection,
      ),
    ).toThrow(/480/);
    expect(() =>
      gamecenterRankingRequest(
        reread(new Packet(opcodeFor("GG_GAMECENTER_RANKING_REQ")).s16(7).u8(1).u8(0)),
        connection,
      ),
    ).toThrow(/480/);
  });
});

describe("483 — gamecenter game-start-ok request", () => {
  test("483 parses the s16 game_id and answers status=1 echo", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof gamecenterGameStartOkRequest>[1];
    gamecenterGameStartOkRequest(
      reread(new Packet(opcodeFor("GG_GAMECENTER_GAME_START_OK_REQ")).s16(7)),
      connection,
    );
    expect(replies).toEqual([["GG_GAMECENTER_GAME_START_OK_ACK", 7]]);
  });

  test("483 refuses wrong payload widths", () => {
    const connection = {
      reply: () => undefined,
    } as unknown as Parameters<typeof gamecenterGameStartOkRequest>[1];
    expect(() =>
      gamecenterGameStartOkRequest(reread(new Packet(opcodeFor("GG_GAMECENTER_GAME_START_OK_REQ"))), connection),
    ).toThrow(/483/);
    expect(() =>
      gamecenterGameStartOkRequest(
        reread(new Packet(opcodeFor("GG_GAMECENTER_GAME_START_OK_REQ")).s16(7).u8(0)),
        connection,
      ),
    ).toThrow(/483/);
  });
});

describe("485 — gameroom progress-time request", () => {
  test("485 parses the u8 room byte and answers n3=3 (silent unknown arm)", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof gameRoomProgressTimeRequest>[1];
    gameRoomProgressTimeRequest(
      reread(new Packet(opcodeFor("GL_GET_GAMEROOM_PROGRESSTIME_REQ")).u8(9)),
      connection,
    );
    expect(replies).toEqual([["GL_GET_GAMEROOM_PROGRESSTIME_ACK", 3]]);
  });

  test("485 refuses wrong payload widths", () => {
    const connection = {
      reply: () => undefined,
    } as unknown as Parameters<typeof gameRoomProgressTimeRequest>[1];
    expect(() =>
      gameRoomProgressTimeRequest(reread(new Packet(opcodeFor("GL_GET_GAMEROOM_PROGRESSTIME_REQ"))), connection),
    ).toThrow(/485/);
    expect(() =>
      gameRoomProgressTimeRequest(
        reread(new Packet(opcodeFor("GL_GET_GAMEROOM_PROGRESSTIME_REQ")).u8(9).u8(0)),
        connection,
      ),
    ).toThrow(/485/);
  });
});

describe("718/721 — voting requests", () => {
  test("718 parses 3 x s32 and replies status=0 (no voting model)", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof startVotingRequest>[1];
    startVotingRequest(
      reread(new Packet(opcodeFor("GR_START_VOTING_REQ")).s32(1).s32(2).s32(3)),
      connection,
    );
    expect(replies).toEqual([["GR_START_VOTING_ACK", 0]]);
  });

  test("718 refuses wrong payload widths", () => {
    const connection = {
      reply: () => undefined,
    } as unknown as Parameters<typeof startVotingRequest>[1];
    expect(() =>
      startVotingRequest(
        reread(new Packet(opcodeFor("GR_START_VOTING_REQ")).s32(1)),
        connection,
      ),
    ).toThrow(/718/);
  });

  test("721 parses the u8 vote and stays silent (no active session)", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof doVotingRequest>[1];
    doVotingRequest(
      reread(new Packet(opcodeFor("GR_DO_VOTING")).u8(1)),
      connection,
    );
    expect(replies).toEqual([]);
    expect(() =>
      doVotingRequest(
        reread(new Packet(opcodeFor("GR_DO_VOTING")).u8(1).u8(0)),
        connection,
      ),
    ).toThrow(/721/);
  });
});

describe("928 — PVE continue request", () => {
  test("928 accepts the builder-literal zero count and answers the dormant arm", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof continueStartRequest>[1];
    continueStartRequest(
      reread(new Packet(opcodeFor("GR_AI_CONTINUE_START_REQ")).s32(0)),
      connection,
    );
    expect(replies.pop()).toEqual(["GR_AI_CONTINUE_START_ACK", 0]);
  });

  test("928 enforces the native 4-byte constant wire", () => {
    const connection = {
      reply: () => undefined,
    } as unknown as Parameters<typeof continueStartRequest>[1];
    expect(() =>
      continueStartRequest(
        reread(new Packet(opcodeFor("GR_AI_CONTINUE_START_REQ")).s32(1)),
        connection,
      ),
    ).toThrow(/928/);
    expect(() =>
      continueStartRequest(
        reread(new Packet(opcodeFor("GR_AI_CONTINUE_START_REQ")).u8(0)),
        connection,
      ),
    ).toThrow(/928/);
  });
});

describe("924/926 — magazine refuel start/end requests", () => {
  test("924 parses the 2-byte wire and answers the triple denial arm", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof magazineStartRequest>[1];
    magazineStartRequest(
      reread(new Packet(opcodeFor("GR_AI_RECHARGE_MAGAZINE_START_REQ")).u8(1).u8(0)),
      connection,
    );
    expect(replies.pop()).toEqual(["GR_AI_RECHARGE_MAGAZINE_START_ACK", 1, 0]);
    expect(() =>
      magazineStartRequest(
        reread(new Packet(opcodeFor("GR_AI_RECHARGE_MAGAZINE_START_REQ")).u8(1).u8(0).u8(0)),
        connection,
      ),
    ).toThrow(/924/);
  });

  test("926 parses the 3-byte wire and answers the nonzero-status denial", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof magazineEndRequest>[1];
    magazineEndRequest(
      reread(new Packet(opcodeFor("GR_AI_RECHARGE_MAGAZINE_END_REQ")).u8(1).u8(0).s8(-1)),
      connection,
    );
    expect(replies.pop()).toEqual(["GR_AI_RECHARGE_MAGAZINE_END_ACK", 1, 0]);
    expect(() =>
      magazineEndRequest(
        reread(new Packet(opcodeFor("GR_AI_RECHARGE_MAGAZINE_END_REQ")).u8(1).u8(0)),
        connection,
      ),
    ).toThrow(/926/);
  });
});

describe("922 — defence-core damage report", () => {
  test("922 parses 10-byte wire and echoes verbatim into the broadcast", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof damageShieldRequest>[1];
    damageShieldRequest(
      reread(new Packet(opcodeFor("GR_AI_DAMAGE_SHIELD_REQ")).s16(5).s16(-3).s16(7).s32(0x40)),
      connection,
    );
    expect(replies.pop()).toEqual(["GR_AI_DAMAGE_SHIELD_ACK", 5, -3, 7, 0x40]);
  });

  test("922 enforces the 10-byte wire", () => {
    const connection = {
      reply: () => undefined,
    } as unknown as Parameters<typeof damageShieldRequest>[1];
    expect(() =>
      damageShieldRequest(
        reread(new Packet(opcodeFor("GR_AI_DAMAGE_SHIELD_REQ")).s16(5).s16(3)),
        connection,
      ),
    ).toThrow(/922/);
  });
});

describe("918 — PVE reward-draw request", () => {
  test("918 echoes idx; empty stage always answers the sentinel failure arm", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof aiRewardItemRequest>[1];
    aiRewardItemRequest(
      reread(new Packet(opcodeFor("GR_AI_GET_REWARD_ITEM_REQ")).u8(2)),
      connection,
    );
    expect(replies.pop()).toEqual(["GR_AI_GET_REWARD_ITEM_ACK", 2]);
  });

  test("918 enforces the single-byte wire", () => {
    const connection = {
      reply: () => undefined,
    } as unknown as Parameters<typeof aiRewardItemRequest>[1];
    expect(() =>
      aiRewardItemRequest(
        reread(new Packet(opcodeFor("GR_AI_GET_REWARD_ITEM_REQ")).u8(2).u8(0)),
        connection,
      ),
    ).toThrow(/918/);
  });
});

describe("912 — weapon-parts equip-change request", () => {
  test("912 parses both native arms and replies errorRaw=1", () => {
    const replies: unknown[][] = [];
    const connection = {
      reply: (name: string, ...args: unknown[]) => replies.push([name, ...args]),
    } as unknown as Parameters<typeof weaponpartsEquipChangeRequest>[1];
    weaponpartsEquipChangeRequest(
      reread(new Packet(opcodeFor("GL_WEAPONPARTS_EQUIP_CHANGE_REQ")).u8(1).s32(7).s32(8)),
      connection,
    );
    expect(replies.pop()).toEqual(["GL_WEAPONPARTS_EQUIP_CHANGE_ACK", 1]);
    weaponpartsEquipChangeRequest(
      reread(new Packet(opcodeFor("GL_WEAPONPARTS_EQUIP_CHANGE_REQ")).u8(2).s32(7).s32(8).s32(9)),
      connection,
    );
    expect(replies.pop()).toEqual(["GL_WEAPONPARTS_EQUIP_CHANGE_ACK", 1]);
  });

  test("912 refuses unknown raw0 arms and wrong tails", () => {
    const connection = {
      reply: () => undefined,
    } as unknown as Parameters<typeof weaponpartsEquipChangeRequest>[1];
    expect(() =>
      weaponpartsEquipChangeRequest(
        reread(new Packet(opcodeFor("GL_WEAPONPARTS_EQUIP_CHANGE_REQ")).u8(3).s32(7).s32(8)),
        connection,
      ),
    ).toThrow(/912/);
    expect(() =>
      weaponpartsEquipChangeRequest(
        reread(new Packet(opcodeFor("GL_WEAPONPARTS_EQUIP_CHANGE_REQ")).u8(0).s32(7)),
        connection,
      ),
    ).toThrow(/912/);
    expect(() =>
      weaponpartsEquipChangeRequest(
        reread(new Packet(opcodeFor("GL_WEAPONPARTS_EQUIP_CHANGE_REQ")).u8(2).s32(7).s32(8)),
        connection,
      ),
    ).toThrow(/912/);
  });
});

describe("834 — data-recv-completed request", () => {
  test("consumes the propagated raw4 context and replies with an empty 835", () => {
    const replies: string[] = [];
    const connection = {
      reply: (name: string) => replies.push(name),
    } as unknown as Parameters<typeof dataRecvCompletedRequest>[1];

    dataRecvCompletedRequest(
      reread(new Packet(opcodeFor("GL_DATA_RECV_COMPLETED_REQ")).s32(0x1122_3344)),
      connection,
    );
    expect(replies).toEqual(["GL_DATA_RECV_COMPLETED_ACK"]);
    expect(() =>
      dataRecvCompletedRequest(
        reread(new Packet(opcodeFor("GL_DATA_RECV_COMPLETED_REQ")).s32(0).u8(1)),
        connection,
      ),
    ).toThrow(/trailing/);
  });
});

describe("105 — user-list request", () => {
  test("accepts only the value the native writer can emit", () => {
    const replies: string[] = [];
    const connection = {
      reply: (name: string) => replies.push(name),
    } as unknown as Parameters<typeof userListRequest>[1];
    const packet = (value: number) => new Packet(opcodeFor("GL_USERLIST_REQ")).u8(value);

    userListRequest(reread(packet(1)), connection);
    expect(replies).toEqual(["GL_USERLIST_ACK"]);
    expect(() => userListRequest(reread(packet(0)), connection)).toThrow(/native writer emits/);
  });
});

describe("682 — login request", () => {
  test("parses the exact client shape", () => {
    const reader = reread(clientLoginRequest("alice", "hunter2"));
    const request = readCredentials(reader);
    expect(request.account).toBe("alice");
    expect(request.passwordOrToken).toBe("hunter2");
    expect(request.dataRevision).toBe(811034967);
    expect(request.fingerprintSource).toBe(2);
    expect(request.fingerprint).toHaveLength(24);
  });

  test("rejects a bad guard dword", () => {
    const packet = new Packet(opcodeFor("GL_LOGIN_REQ"))
      .str("a")
      .str("b")
      .u64(0n) // guard absent
      .u8(0)
      .zeros(24);
    expect(() => readCredentials(reread(packet))).toThrow(/guard mismatch/);
  });

  test("rejects trailing bytes", () => {
    const packet = clientLoginRequest("alice", "pw").u8(0xff);
    expect(() => readCredentials(reread(packet))).toThrow(/trailing/);
  });

  test("rejects a truncated fingerprint", () => {
    const packet = new Packet(opcodeFor("GL_LOGIN_REQ"))
      .str("a")
      .str("b")
      .u64(0xf1e1ab0en)
      .u8(0)
      .zeros(8);
    expect(() => readCredentials(reread(packet))).toThrow(RangeError);
  });

  test("rejects source and raw fingerprint combinations the native builder cannot emit", () => {
    const sourceZero = new Packet(opcodeFor("GL_LOGIN_REQ"))
      .str("a")
      .str("b")
      .u64(0xf1e1ab0en)
      .u8(0)
      .zeros(23)
      .u8(1);
    expect(() => readCredentials(reread(sourceZero))).toThrow(/source 0/);

    const sourceMac = new Packet(opcodeFor("GL_LOGIN_REQ"))
      .str("a")
      .str("b")
      .u64(0xf1e1ab0en)
      .u8(1)
      .zeros(6)
      .zeros(17)
      .u8(1);
    expect(() => readCredentials(reread(sourceMac))).toThrow(/source 1/);

    const sourceStorage = new Packet(opcodeFor("GL_LOGIN_REQ"))
      .str("a")
      .str("b")
      .u64(0xf1e1ab0en)
      .u8(2)
      .zeros(23)
      .u8(1);
    expect(() => readCredentials(reread(sourceStorage))).toThrow(/source 2/);
  });
});

describe("681 — login ack", () => {
  const servers: GameServer[] = [
    {
      serverId: 1,
      name: "PaperMan",
      host: "127.0.0.1",
      port: 40201,
      flag: 0,
      group: 0,
      channelGroups: [
        { maxUsers: 100, channel: { type: 1, name: "Channel 1", currentUsers: 0, flag: 0 } },
        { maxUsers: 0 },
        { maxUsers: 0 },
      ],
    },
  ];

  test("failure writes a full raw s32 word", () => {
    const reader = build("GL_LOGIN_ACK", 0x1234_5678);
    expect(reader.opcode).toBe(681);
    expect(reader.s32()).toBe(0x1234_5678);
    expect(reader.remaining).toBe(0);
  });

  test("keeps the named bad-credentials code available", () => {
    const reader = build("GL_LOGIN_ACK", Result.BadCredentials);
    expect(reader.s32()).toBe(2);
    expect(reader.remaining).toBe(0);
  });

  test("the Result table mirrors the native failure switch one-to-one", () => {
    // Recovered 0x43E651 switch, including the 0xC8..0xD6 message routing.
    const native: ReadonlyArray<[keyof typeof Result, number]> = [
      ["GeneralFailure", 0],
      ["Success", 1],
      ["BadCredentials", 2],
      ["Banned", 200],
      ["Maintenance", 201],
      ["Maintenance2", 202],
      ["AlreadyOnline", 203],
      ["AntiAddiction", 204],
      ["Failure317", 205],
      ["Failure318", 206],
      ["Failure319", 207],
      ["Failure31E", 208],
      ["Failure31F", 209],
      ["AlreadyOnline2", 210],
      ["FailureFormat211", 211],
      ["FailureFormat212", 212],
      ["Failure3C4", 213],
      ["GmIpDenied", 214],
    ];
    for (const [name, code] of native) {
      // Pin the value as number: bun's matcher generic narrows T to the
      // indexed-const literal union and would then reject a plain number.
      expect(Result[name] as number).toBe(code);
      const reader = decode(new Packet(681).s32(Result[name]).encode());
      expect(reader.s32()).toBe(code);
      expect(reader.remaining).toBe(0);
    }
  });

  test("rejects a result outside native s32", () => {
    expect(() => buildPacket("GL_LOGIN_ACK", 0x8000_0000)).toThrow(/result/);
    expect(() => buildPacket("GL_LOGIN_ACK", -0x8000_0001)).toThrow(/result/);
  });

  test("success round-trips in the documented field order", () => {
    const reader = build("GL_LOGIN_ACK", { userNo: 7, servers });

    expect(reader.s32()).toBe(Result.Success);
    expect(reader.s32()).toBe(7); // user_no
    expect(reader.s32()).toBe(0); // charge mode
    expect(reader.s32()).toBe(0); // ext_count

    expect(reader.s16()).toBe(1); // server_count
    expect(reader.s16()).toBe(1); // server_id
    expect(reader.str()).toBe("PaperMan");
    expect(reader.str()).toBe("127.0.0.1");
    expect(reader.u16()).toBe(40201); // selected-server TCP endpoint port
    expect(reader.u8()).toBe(0); // flag
    expect(reader.s16()).toBe(0); // group

    expect(reader.s16()).toBe(100); // max_users / USERS denominator
    expect(reader.u8()).toBe(1); // ch_type
    expect(reader.str()).toBe("Channel 1");
    expect(reader.s16()).toBe(0); // current_users / USERS numerator
    expect(reader.u8()).toBe(0); // ch_flag
    expect(reader.s16()).toBe(0); // group 1 max_users, empty
    expect(reader.s16()).toBe(0); // group 2 max_users, empty

    expect(reader.s32()).toBe(0); // billing_first
    expect(reader.s32()).toBe(0); // billing_second
    expect(reader.remaining).toBe(0);
  });

  test("writes exactly one raw extension tuple when its positive gate is explicit", () => {
    const reader = build("GL_LOGIN_ACK", {
      userNo: 7,
      rawExtension: { gate: 2, s32First: -11, s32Second: 0x1234_5678, featureFlag: 1 },
      servers,
    });

    reader.s32(); // result
    reader.s32(); // user_no
    reader.s32(); // n100
    expect(reader.s32()).toBe(2); // positive gate is not a tuple count
    expect(reader.s32()).toBe(-11);
    expect(reader.s32()).toBe(0x1234_5678);
    expect(reader.u8()).toBe(1);
    expect(reader.s16()).toBe(1); // server_count follows the one tuple
  });

  test("validates the raw extension tuple without changing the safe default", () => {
    const defaultReader = build("GL_LOGIN_ACK", { userNo: 7, servers });
    defaultReader.s32();
    defaultReader.s32();
    defaultReader.s32();
    expect(defaultReader.s32()).toBe(0); // no extension tuple follows

    const negativeGateReader = build("GL_LOGIN_ACK", {
      userNo: 7,
      rawExtension: { gate: -1, s32First: 0, s32Second: 0, featureFlag: 0 },
      servers,
    });
    negativeGateReader.s32();
    negativeGateReader.s32();
    negativeGateReader.s32();
    expect(negativeGateReader.s32()).toBe(-1); // <= 0 also has no tuple
    expect(negativeGateReader.s16()).toBe(1); // server_count is next

    expect(() => buildPacket("GL_LOGIN_ACK", {
      userNo: 7,
      rawExtension: { gate: 1, s32First: 0x8000_0000, s32Second: 0, featureFlag: 0 },
      servers,
    })).toThrow(/s32First/);
    expect(() => buildPacket("GL_LOGIN_ACK", {
      userNo: 7,
      rawExtension: { gate: 1, s32First: 0, s32Second: 0, featureFlag: 0x100 },
      servers,
    })).toThrow(/featureFlag/);
  });

  test("a type-3 channel carries the extra byte", () => {
    const reader = build("GL_LOGIN_ACK", {
      userNo: 1,
      servers: [
        {
          ...servers[0]!,
          channelGroups: [
            { maxUsers: 100, channel: { type: 3, name: "AI", currentUsers: 0, flag: 0, extra: 9 } },
            { maxUsers: 0 },
            { maxUsers: 0 },
          ],
        },
      ],
    });
    for (let i = 0; i < 4; i++) reader.s32();
    reader.s16();
    reader.s16();
    reader.str();
    reader.str();
    reader.s16();
    reader.u8();
    reader.s16();
    expect(reader.s16()).toBe(100); // max_users
    expect(reader.u8()).toBe(3); // ch_type
    expect(reader.str()).toBe("AI");
    expect(reader.s16()).toBe(0); // current_users
    expect(reader.u8()).toBe(0);
    expect(reader.u8()).toBe(9); // extra, only present for type 3
  });

  test("defaults type-3 extra and ignores extra on other channel types", () => {
    const missingExtra = build("GL_LOGIN_ACK", {
      userNo: 1,
      servers: [{
        ...servers[0]!,
        channelGroups: [
          { maxUsers: 100, channel: { type: 3, name: "AI", currentUsers: 0, flag: 0 } },
          { maxUsers: 0 },
          { maxUsers: 0 },
        ],
      }],
    });
    for (let i = 0; i < 4; i++) missingExtra.s32();
    missingExtra.s16();
    missingExtra.s16();
    missingExtra.str();
    missingExtra.str();
    missingExtra.s16();
    missingExtra.u8();
    missingExtra.s16();
    expect(missingExtra.s16()).toBe(100);
    expect(missingExtra.u8()).toBe(3);
    expect(missingExtra.str()).toBe("AI");
    expect(missingExtra.s16()).toBe(0);
    expect(missingExtra.u8()).toBe(0);
    expect(missingExtra.u8()).toBe(0);

    const ignoredExtra = build("GL_LOGIN_ACK", {
      userNo: 1,
      servers: [{
        ...servers[0]!,
        channelGroups: [
          { maxUsers: 100, channel: { type: 1, name: "Normal", currentUsers: 0, flag: 0, extra: 9 } },
          { maxUsers: 0 },
          { maxUsers: 0 },
        ],
      }],
    });
    for (let i = 0; i < 4; i++) ignoredExtra.s32();
    ignoredExtra.s16();
    ignoredExtra.s16();
    ignoredExtra.str();
    ignoredExtra.str();
    ignoredExtra.s16();
    ignoredExtra.u8();
    ignoredExtra.s16();
    expect(ignoredExtra.s16()).toBe(100);
    expect(ignoredExtra.u8()).toBe(1);
    expect(ignoredExtra.str()).toBe("Normal");
    expect(ignoredExtra.s16()).toBe(0);
    expect(ignoredExtra.u8()).toBe(0);
    expect(ignoredExtra.s16()).toBe(0); // no extra byte for type 1
  });

  test("emits three group slots without requiring an exact input length", () => {
    const reader = build("GL_LOGIN_ACK", {
      userNo: 1,
      servers: [{
        ...servers[0]!,
        channelGroups: [
          { maxUsers: 0 },
          { maxUsers: 0 },
          { maxUsers: 0 },
          { maxUsers: 100, channel: { type: 1, name: "ignored", currentUsers: 0, flag: 0 } },
        ],
      }],
    });
    for (let i = 0; i < 4; i++) reader.s32();
    reader.s16();
    reader.s16();
    reader.str();
    reader.str();
    reader.s16();
    reader.u8();
    reader.s16();
    expect(reader.s16()).toBe(0);
    expect(reader.s16()).toBe(0);
    expect(reader.s16()).toBe(0);
    expect(reader.s32()).toBe(0);
    expect(reader.s32()).toBe(0);
    expect(reader.remaining).toBe(0);
  });

  test("keeps operational raw2 counts signed and width-safe", () => {
    const reader = build("GL_LOGIN_ACK", {
      userNo: 1,
      servers: [{
        ...servers[0]!,
        serverId: 0xffff,
        group: 0xffff,
        channelGroups: [
          { maxUsers: -1 },
          { maxUsers: 1, channel: { type: 1, name: "Signed", currentUsers: -2, flag: 0 } },
          { maxUsers: 0 },
        ],
      }],
    });
    for (let i = 0; i < 4; i++) reader.s32();
    reader.s16();
    expect(reader.u16()).toBe(0xffff); // native raw2 server_id
    reader.str();
    reader.str();
    reader.u16();
    reader.u8();
    expect(reader.u16()).toBe(0xffff); // native raw2 group
    expect(reader.s16()).toBe(-1); // operational max_users remains signed
    expect(reader.s16()).toBe(1);
    expect(reader.u8()).toBe(1);
    expect(reader.str()).toBe("Signed");
    expect(reader.s16()).toBe(-2);
    expect(reader.u8()).toBe(0);
    expect(reader.s16()).toBe(0);
    expect(reader.s32()).toBe(0);
    expect(reader.s32()).toBe(0);
    expect(reader.remaining).toBe(0);
  });

  test("rejects names that overrun native fixed buffers", () => {
    expect(() =>
      buildPacket("GL_LOGIN_ACK", {
        userNo: 1,
        servers: [{ ...servers[0]!, name: "s".repeat(50) }],
      }),
    ).toThrow(/server name/);
    expect(() =>
      buildPacket("GL_LOGIN_ACK", {
        userNo: 1,
        servers: [{
          ...servers[0]!,
          channelGroups: [
            { maxUsers: 1, channel: { type: 1, name: "c".repeat(50), currentUsers: 0, flag: 0 } },
            { maxUsers: 0 },
            { maxUsers: 0 },
          ],
        }],
      }),
    ).toThrow(/channel name/);
  });

  test("keeps the s32 login words unmasked", () => {
    const reader = build("GL_LOGIN_ACK", { userNo: 7, n100: 0x1234_5678, servers });
    reader.s32();
    reader.s32();
    expect(reader.s32()).toBe(0x1234_5678);
    expect(() => buildPacket("GL_LOGIN_ACK", { userNo: 0x8000_0000, servers })).toThrow(/user_no/);
    expect(() => buildPacket("GL_LOGIN_ACK", { userNo: 1, n100: 0x8000_0000, servers })).toThrow(/n100/);
    expect(() => buildPacket("GL_LOGIN_ACK", {
      userNo: 1,
      servers: [{ ...servers[0]!, serverId: Number.NaN }],
    })).toThrow(/server_id/);
  });

  test("requires a channel body when the native group gate is positive", () => {
    expect(() =>
      buildPacket("GL_LOGIN_ACK", {
        userNo: 1,
        servers: [{
          ...servers[0]!,
          channelGroups: [{ maxUsers: 100 }, { maxUsers: 0 }, { maxUsers: 0 }],
        }],
      }),
    ).toThrow(/positive channel group needs a channel body/);
  });

});

describe("registry", () => {
  test("direction comes from the folder, not the REQ/ACK suffix", () => {
    // GT_PING_ACK is an _ACK the server sends; GT_PING_REQ is a _REQ it
    // receives. A suffix rule would get both backwards.
    expect(handlerFor(opcodeFor("GT_PING_REQ"))).toBeDefined();
    expect(handlerFor(opcodeFor("GT_PING_ACK"))).toBeUndefined();
    expect(() => buildPacket("GT_PING_ACK")).not.toThrow();
  });

  test("the registry exposes both operation folders at startup", () => {
    expect(summary()).toMatch(/^c2s 59 \(/);
    expect(summary()).toMatch(/\), s2c 58 \(/);
    expect(summary()).toContain("GL_LOGIN_ACK");
    expect(summary()).toContain("GL_LOGIN_REQ");
  });
});
