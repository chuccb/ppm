# Server handler 待辦清單

`server-ts/` 是唯一的 server implementation。這份文件只描述目前 Bun runtime
已註冊的 packet modules、尚未實作的高價值 boundary，以及下一步需要補的
native/resource evidence；它不把 client reader/writer 自動推導成 service policy。

## Current implementation surface

Bun 在 `server-ts/src/ops/registry.ts` 以目錄直接發現 packet modules，啟動時將
每個 filename 對到 `db/packets.tsv`。目前有 15 個 C2S modules 與 16 個 S2C
modules：

- Login/channel: `GL_LOGIN_REQ`、`PM_UDPSTART_REQ`、`GC_ENTERCHANNEL_REQ`，以及
  694、681、693、144、196 的 handshake replies。
- Lobby bootstrap: `GL_LOBBYIN_REQ`、`GL_MYINFO_REQ`、`GL_MYITEM_REQ`、
  `GL_INVENIN_REQ`、`GL_GAMEROOMINFO_REQ`、`GL_USERLIST_REQ`、
  `GL_FRIEND_LIST_REQ`、`GL_MSG_RECVLIST_REQ`、`GL_SHOPIN_REQ`、
  `GL_CLIENTINFO_REQ`、`GL_DATA_RECV_COMPLETED_REQ`。
- Transport: `GT_PING_REQ` / `GT_PING_ACK`。client 的方向命名與 server 的
  send/receive 方向不同，請以 `c2s/` 或 `s2c/` 目錄為準。

每個 module 只負責自己的 wire reader 或 builder；connection ordering、admission
state 與 SQLite projection 分別位於 `connection.ts`、`admission.ts`、`store.ts`。
TypeScript signatures 是 compile-time contract，不取代 reader 的 runtime
width、fixed-buffer、mask/coerce、count limit 或 malformed-input rejection。

## Current next evidence

1. **Channel admission 143→144→195→196**：取得一組成功與一組拒絕的同 revision
   capture，補出 `String[24]` writer、681 extension values、144 raw fields 與
   196 type-3 continuation。未有 evidence 前，TS 只保留 exact raw shape，不能把
   raw bytes 命名成 account、rank、billing 或 entitlement。
2. **Private UDP**：目前只實作 source-proven encrypted 19→empty-20 control。
   逐一追 `sub_595E80` cases、secondary socket、send callers、remote address 與
   correlation data flow 後，才決定是否存在可實作的 relay；不能因 opcode 存在
   就接受 P2P/NAT/gameplay datagrams。
3. **Room and battle state**：取得 111–194、730–742、902–963 的可重現 captures，
   先逐欄核對 builder、dispatcher、parser、room/session consumer，再建立 state
   machine。沒有 owner、timer、duplicate/cancel 與 success-tail evidence 時，回
   fail-closed/no-mutation，不送固定成功 ACK。
4. **Clan/tournament and matching**：先完成 756–776、718/721、983/988 的
   caller/callee、membership、queue、timeout 與 result-state audit，再決定是否
   擴充 `Store`；目前的 schema 或 UI 名稱不能當作 ownership proof。
5. **Economy and grants**：shop、gift、reward、weapon、skill、drop 與 pricing
   僅在有 native consumer、resource lookup、持有狀態與 response mutation 的完整
   evidence chain 後實作。資源存在本身不是 entitlement。

## Unimplemented request inventory

下表是下一輪優先處理的 C2S opcode。其 framing 是 native evidence；response、
mutation、ownership 與 service policy 仍未獲得授權。

| Opcode | Name | Current safe boundary |
|---:|---|---|
| 103 | `GE_LOGOUT_REQ` | 解綁 connection/session；不發未證實 ACK |
| 298 | `GS_TAKEGIFT_REQ` | 保留 request shape；不刪 gift、不回成功 |
| 300 | `GS_MOVEGIFT_REQ` | 保留 request shape；不改 inventory |
| 306 | `GG_JJGET_REQ` | 先補 reward/ownership state |
| 324 | `GG_BOMBEND_REQ` | 先補 room battle state |
| 374 | `GR_GETCRYSTAL_REQ` | 先補 room/object owner 與 result policy |
| 398 | `MASTER_SVRCLASS_REQ` | operator-only boundary 未定義，拒絕 |
| 400 | `MASTER_CONNTYPE_REQ` | operator-only boundary 未定義，拒絕 |
| 410 | `MASTER_DISLOGIN_REQ` | operator-only boundary 未定義，拒絕 |
| 412 | `MASTER_DISGMS_REQ` | operator-only boundary 未定義，拒絕 |
| 414 | `MASTER_DISLOG_REQ` | operator-only boundary 未定義，拒絕 |
| 418 | `MASTER_RESETTCPGROUPINFO_REQ` | operator-only boundary 未定義，拒絕 |
| 443 | `GG_STEALSUCK_REQ` | ACK 是 score state；未有計分狀態機，不轉發 |
| 445 | `GG_STEALPUSH_REQ` | ACK 是 score state；未有計分狀態機，不轉發 |
| 718 / 721 | room vote family | 未有 voter、timer、cancel、result owner，不回成功 |
| 730–742 | Pulp’n’Roll family | 需要 object seed、room state 與可重現 captures |
| 756–776 | clan/tournament family | 需要 membership、round、entry 與 billing/state evidence |
| 902–963 | Occupy / ground-object family | 保留已知 raw framing；不虛構 reward、seed 或成功 tail |

新增 module 前，必須在 `docs/PACKETS.md`、`docs/LAYOUTS_REQ.md`、
`docs/LAYOUTS.md` 與 `PaperMan.exe.c` 中追完：

```text
request builder → every caller and state gate → exact reader/consumer
                → field data flow → ownership/source rule
                → mutation and response → next valid client state
```

## Verification

```bash
python3 tools/verify_dispatcher_coverage.py
python3 tools/verify_native_gates.py
python3 tools/verify_resource_claims.py
python3 tools/verify_resource_coverage.py
cd server-ts
bun run typecheck
bun test
```

若環境沒有 Bun，必須明確記錄 typecheck/test 未執行；不能用離線 Python schema
smoke test 代替 TypeScript runtime verification。