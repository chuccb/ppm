# PyClassInformer RTTI 類別與 vftable 索引

> **Provenance — Fact/HIGH（匯出本身）**：本文件整理使用者於 2026-09 提供、以
> IDA + PyClassInformer 匯出的 RTTI tranche。每個位址、method count、`M` 標記、
> class 名稱、base relation 與 `Offset` 欄位值都以該輸出為來源；本文件沒有重新掃描
> 二進位來驗證或補全它們。原始 export 不在 workspace 中；此 Markdown 是此次資料的
> 可搜尋、可版本化轉錄。
>
> **Scope boundary — Fact/HIGH**：RTTI 可以確認 client 端**報出的** C++ type、
> virtual-table 與 hierarchy record；它**不能**單獨證明 opcode ownership、
> original-service policy、帳號/物品授權、持久化或任何 server success response。
> 這些結論仍須依 [`PACKETS.md`](PACKETS.md) 的 writer → caller → parser/consumer
> → state chain。

這是一份供人與 LLM 搜尋的**正規化索引**，不是新的 type recovery 或 header 重建。
原始工具輸出的 `Hierarchy Order` 中反覆出現的 `(0,-1,0)` 已壓縮為下方的 base chain
與原始 `Offset` 值；有額外 hierarchy offset 的複雜 STL/ATL 條目會保留關鍵 raw relation。

## 讀法與限制

| 原始欄位 | 本文件記法 | 可以安全解讀的內容 | 不可自行推論的內容 |
|---|---|---|---|
| `Vftable` | `0x…` | PyClassInformer 報出的 vftable 位址。此文件不假設 image base/rebase。 | 任一 slot 的函式語意，除非另有 xref/decompile。 |
| `Methods` | `N` | 工具報出的 method/vtable count。 | 完整 class size、所有 non-virtual method 或 ABI header。 |
| `Flags` | `M` 或 `—` | 保留原始輸出的 token。 | **`M` 的工具內部定義尚未由這批資料驗證**；不可把它寫成「一定是某種繼承」。 |
| `Offset` | `+0x…` | 工具在該 hierarchy record 旁報出的原始數值；本索引以它區分多個 record。 | 未取得 PyClassInformer legend 前，不用它直接計算 object field layout、總大小或任何 payload offset。 |
| `Hierarchy` | `Derived → Base…` | 此 RTTI record 報出的 inheritance path。 | 該 class 是否由 server 建立、是否是持久化 model。 |

`Flag` 欄未在某些全為 `—` 的表重複列出；這只是排版壓縮，**不是**遺失的工具欄位。

### 使用順序

1. 先以 class 名稱或 vftable 位址定位相關 native function；例如 `CLobbyMainRoom`、
   `CPopUpCreateRoom`、`CUDPManager`、`VoterMgr`。
2. 再查該 function 的 caller、寫入/讀取 primitive、global/object offset 與 consumer。
3. 若 RTTI 與目前命名衝突，保留 RTTI 事實，將既有名稱修正視為獨立工作；不得由名稱
   猜測 wire field 或 server state。
4. 多重繼承 class 要以**各 vftable record 與 offset**分別追蹤，不能只保留第一個
   `+0` path。

## 快速導覽：與現有逆向最相關的 anchor

| Anchor | RTTI 事實 | 現有文件中的可用交叉點 | 安全使用範圍 |
|---|---|---|---|
| `CLobbyMainRoom` | `0xAE0A84`, 34 methods, `CLobbyScreen` base at `+0x0` | [`PACKETS.md` §3.15](PACKETS.md#315-房間系統-七輪讀畢)、[`ARCHITECTURE.md`](ARCHITECTURE.md) | 將建房/房單 UI path 定位為 client lobby screen；不代表 server room authority。 |
| `CPopUpCreateRoom` | package/document `0xAEA3D4`, control `0xAEA31C +0x4`, event container `0xAEA314 +0xE0` | 111 builder/caller evidence in `PACKETS.md` | 對照 UI object 的多重繼承與 callback vtable；111 欄位仍以 sender/caller 為準。 |
| `CClientData` / `CRoomInfo` / `CUserInfo` | `0xAEB274` / `0xAEBA3C` / `0xAEC1A4` | `PACKETS.md` §3.2、§3.15 | 搜尋 client state container；不從 RTTI 反推其 field layout。 |
| `Packet` | `0xAEE4F8`, 1 method | [`PACKETS.md` §1](PACKETS.md#1-packet-類-0x591ac0-系列-vftable-packetvftable-0xaee4f8) | 驗證既有 `Packet::vftable` anchor 的同一類別。 |
| `CUDPManager` / `CUDPNetworkManager` | `0xAEE54C` / `0xAEE584` | `PACKETS.md` §2.5-2.6、`server-ts/src/udp.ts` | 作為 UDP transport investigation 的 xref anchor；不解除 Wiki/動機資料的證據隔離。 |
| `VoterMgr` / `IVotingNetwork` | `VoterMgr` has `Voter +0x0` and `IVotingNetwork +0x30` records | `PACKETS.md` dispatcher notes; `SERVER_TS_EVIDENCE.md` Part II | 證明 client-side voting types exist；不證明 original server vote timer/outcome/membership policy。 |
| `CyGameModes::*` | 15 lobby-UI types and corresponding game-mode/factory family | [`RESOURCES.md` §4b](RESOURCES.md#4b-cfgmaplistpat-格式-十五輪以真實檔案實測修正) | 補強 mode implementation family 的查找入口；mode value mapping 仍以 factory/resource/consumer evidence 為準。 |

## 1. Lobby、帳號與房間 UI

### 1.1 Lobby screen / manager family

| Type | vftable | Methods | Flag | Base chain | Offset |
|---|---:|---:|:---:|---|---:|
| `CLobbyManager` | `0xADE19C` | 1 | — | root | `+0x0` |
| `CLobbyPartsUpRoom` | `0xADE1A4` | 34 | — | `CLobbyPartsUpRoom → CLobbyScreen` | `+0x0` |
| `CLobbyQuest` | `0xADE234` | 34 | — | `CLobbyQuest → CLobbyScreen` | `+0x0` |
| `CLobbyChannel` | `0xADECBC` | 34 | — | `CLobbyChannel → CLobbyScreen` | `+0x0` |
| `CLobbyServerData` | `0xADEDE4` | 1 | — | root | `+0x0` |
| `CLobbyCharMake` | `0xADEF5C` | 34 | — | `CLobbyCharMake → CLobbyScreen` | `+0x0` |
| `CLobbyClan` | `0xADF2DC` | 34 | M | `CLobbyClan → CLobbyScreen` | `+0x0` |
| `CLobbyGameRoom` | `0xADF98C` | 34 | — | `CLobbyGameRoom → CLobbyScreen` | `+0x0` |
| `CLobbyGameStart` | `0xAE06FC` | 34 | — | `CLobbyGameStart → CLobbyScreen` | `+0x0` |
| `CLobbyJoinGame` | `0xAE07B4` | 34 | — | `CLobbyJoinGame → CLobbyScreen` | `+0x0` |
| `CLobbyLogin` | `0xAE086C` | 34 | — | `CLobbyLogin → CLobbyScreen` | `+0x0` |
| `CLobbyMainRoom` | `0xAE0A84` | 34 | — | `CLobbyMainRoom → CLobbyScreen` | `+0x0` |
| `CLobbyPaperGameCenter` | `0xAE164C` | 34 | — | `CLobbyPaperGameCenter → CLobbyScreen` | `+0x0` |
| `CLobbyScreen` | `0xAE1C2C` | 34 | — | root | `+0x0` |
| `CLobbyShop` | `0xAE1CE4` | 34 | — | `CLobbyShop → CLobbyScreen` | `+0x0` |
| `CLobbyTournamentGameRoom` | `0xAE265C` | 34 | — | `CLobbyTournamentGameRoom → CLobbyScreen` | `+0x0` |
| `CLobbyTournamentMainRoom` | `0xAE2A24` | 34 | — | `CLobbyTournamentMainRoom → CLobbyScreen` | `+0x0` |
| `CLobbyStoreRoom` | `0xAE6A1C` | 34 | — | `CLobbyStoreRoom → CLobbyScreen` | `+0x0` |
| `CLobbyPresent` | `0xAE813C` | 34 | — | `CLobbyPresent → CLobbyScreen` | `+0x0` |
| `CLobbyNewSkillMix` | `0xAE840C` | 34 | — | `CLobbyNewSkillMix → CLobbyScreen` | `+0x0` |
| `CLobbyWareHouse` | `0xAE927C` | 34 | — | `CLobbyWareHouse → CLobbyScreen` | `+0x0` |
| `MessengerUtil` | `0xAE11D8` | 1 | — | root | `+0x0` |
| `ChattingRoom` | `0xAFCBE4` | 1 | — | root | `+0x0` |
| `DirectJoinGame` | `0xB19254` | 1 | — | root | `+0x0` |

### 1.2 Room creation / lobby popup and callback records

下表在適用處保留同一 concrete type 的多個 RTTI record。`CEventFuncContainer<…>`
列是 exporter 報出的另一個 base/vftable record；本索引不賦予它獨立的 gameplay 或
server-domain 意義。

| Concrete UI type | package/document record | complex-control record | callback/event record |
|---|---|---|---|
| `CChangeClanEmblem` | `0xADE524`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xADE46C`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | `0xADE464`, 1, M, `CEventFuncContainer<CChangeClanEmblem,void>`, `+0xE0` |
| `CLobbyClan` | — | `CLobbyScreen`: `0xADF2DC`, 34, M, `+0x0` | `0xADF2D4`, 1, M, `CEventFuncContainer<CLobbyClan,unsigned char>`, `+0x70` |
| `CUIPopUpSendMessage` | `0xADF5E4`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xADF52C`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | — |
| `CBlockListPopup` | `0xAE1614`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAE155C`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | — |
| `CUILobbyChannel` | `0xAE3F84`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAE3ECC`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | — |
| `CUILobbyMatchList` | `0xAE40C4`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAE400C`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | — |
| `CUILobbyOption` | `0xAE4A4C`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAE4994`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | — |
| `CUILobbyOptionKey` | `0xAE4CC4`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAE4C0C`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | — |
| `CUILobbyOptionMacro` | `0xAE4DAC`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAE4CF4`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | — |
| `CUILobbyOptionQuickJoin` | `0xAE4EA4`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAE4DEC`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | — |
| `CUILobbyOptionSystem1` | `0xAE51EC`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAE5134`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | — |
| `CUILobbyOptionSystem2` | `0xB22CEC`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xB22C34`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | — |
| `CUILobbyStoreNewSkillSys` | `0xAE58D4`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAE581C`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | — |
| `CUILobbyTNMTFinalAward` | `0xAE5D44`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAE5C8C`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | — |
| `CUILobbyTNMTPlayerInfo` | `0xAE6254`, 4, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAE619C`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | — |
| `CUIPopUpAddFriend` | `0xAE67E4`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAE672C`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | — |
| `CUIPopUpInfoOfClan` | `0xAE6914`, 4, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAE685C`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | `0xAE6850`, 1, M, `CEventFuncContainer<CUIPopUpInfoOfClan,void>`, `+0xE0` |
| `CUILobbyStoreDress` | `0xAE6CFC`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAE6C44`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | — |
| `CUILobbyStorePaper` | `0xAE6DC4`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAE6D0C`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | — |
| `CUILobbyStoreSlotItem` | `0xAE6E8C`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAE6DD4`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | — |
| `CUILobbyStorePaperCode` | `0xAE720C`, 4, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAE7154`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | `0xAE714C`, 1, M, `CEventFuncContainer<CUILobbyStorePaperCode,void>`, `+0xE0` |
| `CUILobbyStoreWeapon` | `0xAE73F4`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAE733C`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | — |
| `CUILobbyTutorialAttack` | `0xAE75EC`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAE7534`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | — |
| `CPopupTeamShuffle` | `0xAE975C`, 4, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAE96A4`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | — |
| `CPopupClanClassChanged` | `0xAE9A7C`, 4, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAE99C4`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | `0xAE99BC`, 1, M, `CEventFuncContainer<CPopupClanClassChanged,void>`, `+0xE0` |
| `CPopupClanEmblem` | `0xAE9BC4`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAE9B0C`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | `0xAE9B04`, 1, M, `CEventFuncContainer<CPopupClanEmblem,void>`, `+0xE0` |
| `CPopupClanJoinWaitingList` | `0xAE9CF4`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAE9C3C`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | `0xAE9C34`, 1, M, `CEventFuncContainer<CPopupClanJoinWaitingList,void>`, `+0xE0` |
| `CPopupClanMessage` | `0xAE9E34`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAE9D7C`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | `0xAE9D74`, 1, M, `CEventFuncContainer<CPopupClanMessage,void>`, `+0xE0` |
| `CPopupClanSearch` | `0xAEA13C`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAEA084`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | `0xAEA07C`, 1, M, `CEventFuncContainer<CPopupClanSearch,void>`, `+0xE0` |
| `CPopupCreateClan` | `0xAEA29C`, 4, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAEA1E4`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | `0xAEA1DC`, 1, M, `CEventFuncContainer<CPopupCreateClan,bool>`, `+0xE0` |
| `CPopUpCreateRoom` | `0xAEA3D4`, 4, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAEA31C`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | `0xAEA314`, 1, M, `CEventFuncContainer<CPopUpCreateRoom,bool>`, `+0xE0` |
| `CPopupDuplicatedItem` | `0xAEA5A4`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAEA4EC`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | `0xAEA4E4`, 1, M, `CEventFuncContainer<CPopupDuplicatedItem,void>`, `+0xE0` |
| `CPopupItemInfo` | `0xAEAAA4`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAEA9EC`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | `0xAEA9E4`, 1, M, `CEventFuncContainer<CPopupItemInfo,bool>`, `+0xE0` |
| `CpopupShopGift` | `0xAEAE04`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAEAD4C`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | `0xAEAD44`, 1, M, `CEventFuncContainer<CpopupShopGift,void>`, `+0xE0` |
| `CPopUpTipMenu` | `0xAEB0DC`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAEB024`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | `0xAEB01C`, 1, M, `CEventFuncContainer<CPopUpTipMenu,void>`, `+0xE0` |
| `CpopupUserInfo` | `0xAEB1EC`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAEB134`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | `0xAEB12C`, 1, M, `CEventFuncContainer<CpopupUserInfo,void>`, `+0xE0` |
| `CUIPopUpClanRank` | `0xAFAB8C`, 4, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAFAAD4`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | `0xAFAACC`, 1, M, `CEventFuncContainer<CUIPopUpClanRank,void>`, `+0xE0` |
| `CUIPopUpNickName` | `0xAFAE44`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAFAD8C`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | `0xAFAD84`, 1, M, `CEventFuncContainer<CUIPopUpNickName,void>`, `+0xE0` |
| `CpopupRecord` | `0xAFAF4C`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xAFAE94`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | `0xAFAE8C`, 1, M, `CEventFuncContainer<CpopupRecord,void>`, `+0xE0` |
| `CUIPopUPDefault` | `0xB22F94`, 3, M, `CUIPackage → CUIDocument`, `+0x0` | `0xB22EDC`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | `0xB22ED4`, 1, M, `CEventFuncContainer<CUIPopUPDefault,bool>`, `+0xE0` |
| `PackageItem` | `0xB1B25C`, 6, M, `CUIPackage → CUIDocument`, `+0x0` | `0xB1B1A4`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | `0xB1B19C`, 1, M, `CEventFuncContainer<PackageItem,void>`, `+0xE0` |
| `pmSlotMachineGet` | `0xB0EE9C`, 4, M, `CUIPackage → CUIDocument`, `+0x0` | `0xB0EDE4`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | `0xB0EDD8`, 1, M, `CEventFuncContainer<pmSlotMachineGet,void>`, `+0xE0` |
| `pmSlotMachineWelcome` | `0xB0F6A4`, 4, M, `CUIPackage → CUIDocument`, `+0x0` | `0xB0F5EC`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | `0xB0F5E4`, 1, M, `CEventFuncContainer<pmSlotMachineWelcome,void>`, `+0xE0` |
| `CGacchaPopupGet` | `0xB19E64`, 4, M, `CUIPackage → CUIDocument`, `+0x0` | `0xB19DAC`, 45, M, `CUIComplexControl → CUIControl`, `+0x4` | `0xB19DA0`, 1, M, `CEventFuncContainer<CGacchaPopupGet,void>`, `+0xE0` |

### 1.3 Tutorial UI: additional `CLobbyScreen` and event bases

| Type | vftable records reported by RTTI |
|---|---|
| `CUILobbyTutorialbook` | package/document `0xAE7924`, 3, M, `+0x0`; control `0xAE786C`, 45, M, `+0x4`; `CLobbyScreen` `0xAE77DC`, 34, M, `+0xE0`; event `0xAE77D0`, 1, M, `CEventFuncContainer<…> +0x150` |
| `CUILobbyTutorialGuide` | package/document `0xAE7B8C`, 3, M, `+0x0`; control `0xAE7AD4`, 45, M, `+0x4`; `CLobbyScreen` `0xAE7A44`, 34, M, `+0xE0`; event `0xAE7A3C`, 1, M, `CEventFuncContainer<…> +0x150` |
| `CUILobbyTutorialIntro` | package/document `0xAE7D2C`, 3, M, `+0x0`; control `0xAE7C74`, 45, M, `+0x4`; `CLobbyScreen` `0xAE7BE4`, 34, M, `+0xE0`; event `0xAE7BD8`, 1, M, `CEventFuncContainer<…> +0x150` |
| `CUILobbyTutorialMove` | package/document `0xAE7E3C`, 3, M, `+0x0`; control `0xAE7D84`, 45, M, `+0x4` |
| `CUILobbyTutorialReserve` | package/document `0xAE8044`, 3, M, `+0x0`; control `0xAE7F8C`, 45, M, `+0x4` |

## 2. Core UI framework, lists, and common controls

### 2.1 Framework roots, documents, and columns

| Type | vftable | Methods | Flag | Base chain / raw relation | Offset |
|---|---:|---:|:---:|---|---:|
| `CUIDocument` | `0xADE5B4` | 2 | — | root | `+0x0` |
| `CUIPackage` (document base) | `0xADE67C` | 3 | M | `CUIPackage → CUIDocument` | `+0x0` |
| `CUIPackage` (control base) | `0xADE5C4` | 45 | M | `CUIPackage → CUIComplexControl → CUIControl` | `+0x4` |
| `CUIComplexControl` | `0xADE68C` | 45 | — | `CUIComplexControl → CUIControl` | `+0x0` |
| `CUIControl` | `0xADE744` | 34 | — | root | `+0x0` |
| `CUINewSkillCombiLeft` | `0xAE8DCC` | 45 | — | `→ CUIComplexControl → CUIControl` | `+0x0` |
| `CUINewSkillCombiRight` | `0xAE8FDC` | 45 | — | `→ CUIComplexControl → CUIControl` | `+0x0` |
| `CUINewSkillLeft` | `0xAE90BC` | 45 | — | `→ CUIComplexControl → CUIControl` | `+0x0` |
| `CUINewSkillRight` | `0xAE919C` | 45 | — | `→ CUIComplexControl → CUIControl` | `+0x0` |
| `CUITemplate` | `0xAE8EB4` | 45 | M | `→ CUIComplexControl → CUIControl`; separate `CStaticCreate<CUITemplate>` path | `+0x0`; raw static path `+125` |
| `CUICharSlot` | `0xAF7D04` | 45 | — | `→ CUIComplexControl → CUIControl` | `+0x0` |
| `CClanMember` | `0xAF7E10` | 3 | — | `CClanMember → Contents` | `+0x0` |
| `Contents` | `0xAF7E20` | 3 | — | root | `+0x0` |
| `CClanReservedMember` | `0xAF7E30` | 3 | — | `→ Contents` | `+0x0` |
| `CUIColumnString` | `0xAF7E98` | 3 | — | `→ CUIColumn` | `+0x0` |
| `CUIColumn` | `0xAF7EA8` | 3 | — | root | `+0x0` |
| `CUIColumnImage` | `0xAF7EB8` | 3 | — | `→ CUIColumn` | `+0x0` |
| `CUIColumnPendent` | `0xAF7EC8` | 3 | — | `→ CUIColumn` | `+0x0` |
| `CUIColumnSpriteElement` | `0xAF7ED8` | 3 | — | `→ CUIColumn` | `+0x0` |
| `CUIColumnCustomSprite` | `0xAF7EE8` | 3 | — | `→ CUIColumn` | `+0x0` |

### 2.2 Generic UI controls

下列每個 `CStaticCreate<…>` relation 是匯出中的另一條 hierarchy path。為避免把
它誤作一般 class field，本索引保留其 raw offset，且不由此推導 object layout。

| Type | vftable | Methods | Flag | Primary base chain | Additional raw base path |
|---|---:|---:|:---:|---|---|
| `CUIComboBox` | `0xAF7F1C` | 45 | M | `→ CUIComplexControl → CUIControl`, `+0x0` | `CStaticCreate<CUIComboBox> +125` |
| `CUIEventRect` | `0xAF830C` | 34 | M | `→ CUIControl`, `+0x0` | `CStaticCreate<CUIEventRect> +60` |
| `CUIFriendColumn` | `0xAF83F4` | 45 | — | `→ CUIComplexControl → CUIControl`, `+0x0` | — |
| `CUIGiftItemSlot` | `0xAF8504` | 45 | — | `→ CUIComplexControl → CUIControl`, `+0x0` | — |
| `CUIItemSlot` | `0xAF866C` | 45 | — | `→ CUIComplexControl → CUIControl`, `+0x0` | — |
| `CUIMessageColumn` | `0xAF8B54` | 45 | — | `→ CUIComplexControl → CUIControl`, `+0x0` | — |
| `CUIMovingSprite` | `0xAF8FE4` | 34 | M | `→ CUIControl`, `+0x0` | `CStaticCreate<CUIMovingSprite> +60` |
| `CUIScaleSprite` | `0xAF9074` | 34 | M | `→ CUIControl`, `+0x0` | `CStaticCreate<CUIScaleSprite> +60` |
| `CUICheckBox` | `0xAF9104` | 34 | M | `→ CUIControl`, `+0x0` | `CStaticCreate<CUICheckBox> +60` |
| `CUIRadioBox` | `0xAF9194` | 34 | M | `→ CUIControl`, `+0x0` | `CStaticCreate<CUIRadioBox> +60` |
| `CUIHSlider` | `0xAF9224` | 34 | M | `→ CUIControl`, `+0x0` | `CStaticCreate<CUIHSlider> +60` |
| `CUIButton` | `0xAF92B4` | 34 | M | `→ CUIControl`, `+0x0` | `CStaticCreate<CUIButton> +60` |
| `CUITabPage` | `0xAF9344` | 45 | M | `→ CUIComplexControl → CUIControl`, `+0x0` | `CStaticCreate<CUITabPage> +125` |
| `CUITab` | `0xAF93FC` | 45 | M | `→ CUIComplexControl → CUIControl`, `+0x0` | `CStaticCreate<CUITab> +125` |
| `CUIItemSlotContainer` | `0xAF94B4` | 45 | M | `→ CUIComplexControl → CUIControl`, `+0x0` | `CStaticCreate<CUIItemSlotContainer> +125` |
| `CUIMenuPop` | `0xAF956C` | 45 | M | `→ CUIComplexControl → CUIControl`, `+0x0` | `CStaticCreate<CUIMenuPop> +125` |
| `CUISelect` | `0xAF9624` | 46 | M | `→ CUIComplexControl → CUIControl`, `+0x0` | `CStaticCreate<CUISelect> +125` |
| `CUIFriendList` | `0xAF96E4` | 45 | M | `→ CUIComplexControl → CUIControl`, `+0x0` | `CStaticCreate<CUIFriendList> +125` |
| `CUIBlockList` | `0xAF979C` | 45 | M | `→ CUIComplexControl → CUIControl`, `+0x0` | `CStaticCreate<CUIBlockList> +125` |
| `CUIQuestSlotContainer` | `0xAF9854` | 45 | M | `→ CUIComplexControl → CUIControl`, `+0x0` | `CStaticCreate<CUIQuestSlotContainer> +125` |
| `CUIUserSlot` | `0xAF990C` | 45 | M | `→ CUIComplexControl → CUIControl`, `+0x0` | `CStaticCreate<CUIUserSlot> +125` |
| `CUIRecycleSlotContainer` | `0xAF99C4` | 45 | M | `→ CUIComplexControl → CUIControl`, `+0x0` | `CStaticCreate<CUIRecycleSlotContainer> +125` |
| `CUICharSlotContainer` | `0xAF9A7C` | 45 | M | `→ CUIComplexControl → CUIControl`, `+0x0` | `CStaticCreate<CUICharSlotContainer> +125` |
| `CUIWeaponSlotContainer` | `0xAF9B34` | 45 | M | `→ CUIComplexControl → CUIControl`, `+0x0` | `CStaticCreate<CUIWeaponSlotContainer> +125` |
| `CUISlotItemsLeftContainer` | `0xAF9BEC` | 45 | M | `→ CUIComplexControl → CUIControl`, `+0x0` | `CStaticCreate<CUISlotItemsLeftContainer> +125` |
| `CUISlotItemsRightContainer` | `0xAF9CA4` | 45 | M | `→ CUIComplexControl → CUIControl`, `+0x0` | `CStaticCreate<CUISlotItemsRightContainer> +125` |
| `CUINewSkillRightContainer` | `0xAF9D5C` | 45 | M | `→ CUIComplexControl → CUIControl`, `+0x0` | `CStaticCreate<CUINewSkillRightContainer> +125` |
| `CUINewSkillLeftContainer` | `0xAF9E14` | 45 | M | `→ CUIComplexControl → CUIControl`, `+0x0` | `CStaticCreate<CUINewSkillLeftContainer> +125` |
| `CUINewSkillCombiRightContainer` | `0xAF9ECC` | 45 | M | `→ CUIComplexControl → CUIControl`, `+0x0` | `CStaticCreate<CUINewSkillCombiRightContainer> +125` |
| `CUINewSkillCombiLeftContainer` | `0xAF9F84` | 45 | M | `→ CUIComplexControl → CUIControl`, `+0x0` | `CStaticCreate<CUINewSkillCombiLeftContainer> +125` |
| `CUIHScroll` | `0xAFA03C` | 45 | M | `→ CUIComplexControl → CUIControl`, `+0x0` | `CStaticCreate<CUIHScroll> +125` |
| `CUIListBox` | `0xAFA0F4` | 45 | M | `→ CUIComplexControl → CUIControl`, `+0x0` | `CStaticCreate<CUIListBox> +125` |
| `CUIKeys` | `0xAFA1AC` | 34 | M | `→ CUIControl`, `+0x0` | `CStaticCreate<CUIKeys> +60` |
| `CUIEdit` | `0xAFA23C` | 34 | M | `→ CUIControl`, `+0x0` | `CStaticCreate<CUIEdit> +60` |
| `CUIEditBox` | `0xAFA2CC` | 36 | M | `→ CUIControl`, `+0x0` | `CStaticCreate<CUIEditBox> +60` |
| `CUIWaiterList` | `0xAFA364` | 45 | M | `→ CUIComplexControl → CUIControl`, `+0x0` | `CStaticCreate<CUIWaiterList> +125` |
| `CUIMessageList` | `0xAFA41C` | 45 | M | `→ CUIComplexControl → CUIControl`, `+0x0` | `CStaticCreate<CUIMessageList> +125` |
| `CUIListControl` | `0xAFA4D4` | 45 | M | `→ CUIComplexControl → CUIControl`, `+0x0` | `CStaticCreate<CUIListControl> +125` |
| `CUICalendar` | `0xAFA58C` | 45 | M | `→ CUIComplexControl → CUIControl`, `+0x0` | `CStaticCreate<CUICalendar> +125` |
| `CUIToggleButton` | `0xAFA644` | 34 | M | `→ CUIControl`, `+0x0` | `CStaticCreate<CUIToggleButton> +60` |
| `CUIGiftItemSlotContainer` | `0xAFA6D4` | 45 | M | `→ CUIComplexControl → CUIControl`, `+0x0` | `CStaticCreate<CUIGiftItemSlotContainer> +125` |
| `CUIImageScroll` | `0xAFA78C` | 45 | M | `→ CUIComplexControl → CUIControl`, `+0x0` | `CStaticCreate<CUIImageScroll> +125` |
| `CUISkillFreeSetItemSlotContainer` | `0xAFA844` | 45 | M | `→ CUIComplexControl → CUIControl`, `+0x0` | `CStaticCreate<CUISkillFreeSetItemSlotContainer> +125` |
| `CUIRecycleSlot` | `0xB12CAC` | 45 | — | `→ CUIComplexControl → CUIControl`, `+0x0` | — |
| `CUISkillFreeSetItemSlot` | `0xAFB064` | 45 | — | `→ CUIComplexControl → CUIControl`, `+0x0` | — |
| `CUISlotItemsLeft` | `0xAFB13C` | 45 | — | `→ CUIComplexControl → CUIControl`, `+0x0` | — |
| `CUISlotItemsRight` | `0xAFB214` | 45 | — | `→ CUIComplexControl → CUIControl`, `+0x0` | — |
| `CUISlot` | `0xAFB384` | 45 | — | `→ CUIComplexControl → CUIControl`, `+0x0` | — |
| `CUIWeaponSlot` | `0xAFB574` | 45 | — | `→ CUIComplexControl → CUIControl`, `+0x0` | — |

## 3. Client data, networking, transport, and audio

### 3.1 Client/lobby data and TCP/UDP classes

| Type | vftable | Methods | Flag | Base chain | Offset |
|---|---:|---:|:---:|---|---:|
| `pmFile` | `0xADEC70` | 1 | — | root | `+0x0` |
| `CClientData` | `0xAEB274` | 1 | — | root | `+0x0` |
| `tItemSlotToClient` | `0xAEB27C` | 2 | — | `tItemSlotToClient → tItemSlot → CSlotBase<int,9>` | `+0x0`; raw base at `+0x4` |
| `LobbyDevice` | `0xAEB8E0` | 1 | — | root | `+0x0` |
| `LobbyFontMgr` | `0xAEB908` | 1 | — | root | `+0x0` |
| `LobbyFont` | `0xAEB910` | 1 | — | root | `+0x0` |
| `CMyData` | `0xAEB944` | 1 | — | root | `+0x0` |
| `CObjectBack` | `0xAEB98C` | 1 | — | root | `+0x0` |
| `CObjectInterface` | `0xAEB99C` | 1 | — | root | `+0x0` |
| `CObjectBtnTypeNormal` | `0xAEB9A4` | 1 | — | root | `+0x0` |
| `CObjectBtnTypeSolo` | `0xAEB9AC` | 1 | — | root | `+0x0` |
| `CObjectEditBox` | `0xAEB9B4` | 1 | — | root | `+0x0` |
| `CObjectCheckBox` | `0xAEB9C8` | 1 | — | root | `+0x0` |
| `CObjectSelect` | `0xAEB9D0` | 1 | — | root | `+0x0` |
| `CRoomInfo` | `0xAEBA3C` | 1 | — | root | `+0x0` |
| `CUserInfo` | `0xAEC1A4` | 1 | — | root | `+0x0` |
| `CUserInGR` | `0xAEC1CC` | 1 | — | root | `+0x0` |
| `nBase::cToken` | `0xAEC878` | 1 | — | root | `+0x0` |
| `ClientSocket` | `0xAEC8AC` | 2 | — | root | `+0x0` |
| `GameNetwork` | `0xAECB24` | 1 | — | root | `+0x0` |
| `LobbyNetwork` | `0xAED4B4` | 1 | — | root | `+0x0` |
| `NetworkManager` | `0xAEDC4C` | 1 | — | root | `+0x0` |
| `Packet` | `0xAEE4F8` | 1 | — | root | `+0x0` |
| `CUDPManager` | `0xAEE54C` | 2 | — | root | `+0x0` |
| `CUDPNetworkManager` | `0xAEE584` | 1 | — | root | `+0x0` |
| `CUDPSocket` | `0xAEE62C` | 1 | — | root | `+0x0` |
| `CUDPThread` | `0xAEE654` | 1 | — | root | `+0x0` |
| `CPmFileSystemBase` | `0xB56880` | 7 | — | root | `+0x0` |
| `CPmFileSystem` | `0xB568A0` | 1 | — | root | `+0x0` |
| `CPmFileSystemFolder` | `0xB568B8` | 7 | — | `→ CPmFileSystemBase` | `+0x0` |
| `CPmFileSystemZip` | `0xB5696C` | 7 | — | `→ CPmFileSystemBase` | `+0x0` |
| `CZipMemFile` | `0xB5698C` | 14 | — | `→ CZipAbstractFile` | `+0x0` |
| `CZipAbstractFile` | `0xB569C8` | 14 | — | root | `+0x0` |
| `CZipFile` | `0xB628AC` | 12 | — | `→ CZipAbstractFile` | `+0x0` |
| `CZipPathComponent` | `0xB628E8` | 1 | — | root | `+0x0` |
| `CZipException` | `0xB62934` | 2 | — | `→ std::exception` | `+0x0` |
| `CZipArchive` | `0xB62940` | 3 | — | root | `+0x0` |
| `CZipFileHeader` | `0xB62950` | 1 | — | root | `+0x0` |
| `CZipCryptograph` | `0xB6295C` | 8 | — | root | `+0x0` |
| `CZipCrc32Cryptograph` | `0xB62980` | 8 | — | `→ CZipCryptograph` | `+0x0` |
| `CZipCentralDir` | `0xB629D4` | 1 | — | root | `+0x0` |
| `CZipAutoBuffer` | `0xB629DC` | 1 | — | root | `+0x0` |
| `CZipStorage` | `0xB629EC` | 1 | — | root | `+0x0` |
| `CZipCompressor` | `0xB62A40` | 11 | — | root | `+0x0` |
| `CZipCompressor::COptions` | `0xB62A70` | 3 | — | root | `+0x0` |
| `ZipArchiveLib::CBaseLibCompressor::COptions` | `0xB62A80` | 3 | — | `→ CZipCompressor::COptions` | `+0x0` |
| `ZipArchiveLib::CDeflateCompressor::COptions` | `0xB62A90` | 3 | — | `→ CBaseLibCompressor::COptions → CZipCompressor::COptions` | `+0x0` |
| `ZipArchiveLib::CBaseLibCompressor` | `0xB62AA0` | 12 | — | `→ CZipCompressor` | `+0x0` |
| `ZipArchiveLib::CDeflateCompressor` | `0xB62AD4` | 12 | — | `→ CBaseLibCompressor → CZipCompressor` | `+0x0` |

### 3.2 Audio family

| Type | vftable | Methods | Base chain / offset |
|---|---:|---:|---|
| `MilesAudioSystem` | `0xAEE67C` | 7 | root, `+0x0` |
| `MilesListener` | `0xAEE6BC` | 2 | `MilesListener → ISoundObject`, `+0x0` |
| `ISoundObject` | `0xAEE6EC` | 2 | root, `+0x0` |
| `MilesSource` | `0xAEE6FC` | 22 | root, `+0x0` |
| `SoundGroup` | `0xAEE78C` | 20 | root, `+0x0` |
| `SoundGroupStatic` | `0xAEE7E4` | 20 | `→ SoundGroup`, `+0x0` |
| `SoundGroupDynamic` | `0xAEE83C` | 20 | `→ SoundGroup`, `+0x0` |
| `SoundGroupFieldEffect` | `0xAEE8B4` | 20 | `→ SoundGroupDynamic → SoundGroup`, `+0x0` |
| `SoundGroupBgm` | `0xAEE93C` | 20 | `→ SoundGroupStatic → SoundGroup`, `+0x0` |
| `SoundProc` | `0xAEEAE8` | 1 | root, `+0x0` |
| `SoundSourceProp` | `0xAEEE98` | 2 | `→ ISoundSourceProp`, `+0x0` |
| `ISoundSourceProp` | `0xAEEEA4` | 1 | root, `+0x0` |
| `PaperAnalyst` | `0xAEF04C` | 1 | `→ ISoundSpaceAnalyst`, `+0x0` |
| `ISoundSpaceAnalyst` | `0xAEF054` | 1 | root, `+0x0` |
| `CSound` | `0xB0C3F8` | 1 | root, `+0x0` |
| `CStreamingSound` | `0xB0C400` | 1 | `→ CSound`, `+0x0` |
| `FireNozzleEffect` (sound base) | `0xB0F7D4` | 2, M | `→ ISoundObject`, `+0x1C` |
| `FireNozzleEffect` (attachment base) | `0xB0F7E0` | 8, M | `→ IGunAttachEffect`, `+0x0` |
| `IGunAttachEffect` | `0xB0F804` | 8 | root, `+0x0` |
| `CHandEffect` | `0xB0F864` | 2 | `→ ISoundObject`, `+0x0` |
| `WaterGun` (sound base) | `0xB0FFF4` | 2, M | `WaterGun → FireNozzleEffect → ISoundObject`, `+0x1C` |
| `WaterGun` (attachment base) | `0xB10000` | 8, M | `WaterGun → FireNozzleEffect → IGunAttachEffect`, `+0x0` |

## 4. Game modes, app state, voting, and game-rule types

### 4.1 Lobby UI mode family

| Type | vftable | Methods | Base chain |
|---|---:|---:|---|
| `CyGameModes::CyGameModeLobbyUI` | `0xB0251C` | 17 | root |
| `CyTeamMatchModeLobbyUI` | `0xB02564` | 17 | `→ CyGameModeLobbyUI` |
| `CyIndividualSurvivalModeLobbyUI` | `0xB0263C` | 17 | `→ CyGameModeLobbyUI` |
| `CyDefuseBombModeLobbyUI` | `0xB026D4` | 17 | `→ CyGameModeLobbyUI` |
| `CyTeamSurvivalModeLobbyUI` | `0xB0271C` | 17 | `→ CyGameModeLobbyUI` |
| `CyStealModeLobbyUI` | `0xB0278C` | 17 | `→ CyGameModeLobbyUI` |
| `CyPracticeModeLobbyUI` | `0xB02814` | 17 | `→ CyGameModeLobbyUI` |
| `CyTutorialModeLobbyUI` | `0xB02874` | 17 | `→ CyGameModeLobbyUI` |
| `CyChattingRoomModeLobbyUI` | `0xB02904` | 17 | `→ CyGameModeLobbyUI` |
| `CyPulpnRollModeLobbyUI` | `0xB0294C` | 17 | `→ CyGameModeLobbyUI` |
| `CyGunShootingModeLobbyUI` | `0xB02994` | 17 | `→ CyGameModeLobbyUI` |
| `CyWeaponTestModeLobbyUI` | `0xB029FC` | 17 | `→ CyGameModeLobbyUI` |
| `CyOccupyModeLobbyUI` | `0xB02A64` | 17 | `→ CyGameModeLobbyUI` |
| `CyAIMultiModeLobbyUI` | `0xB02ACC` | 17 | `→ CyGameModeLobbyUI` |
| `CyTeamSoccerModeLobbyUI` | `0xB02B14` | 17 | `→ CyGameModeLobbyUI` |
| `CyOccupyRenewalModeLobbyUI` | `0xB02B5C` | 17 | `→ CyGameModeLobbyUI` |

### 4.2 Game-mode implementation and factory family

| Type | vftable | Methods | Base chain |
|---|---:|---:|---|
| `CyTeamSoccerMode` | `0xB02BCC` | 29 | `→ CyGameMode` |
| `CyGameMode` | `0xB0313C` | 29 | root |
| `CyOccupyRenewalModeFactory` | `0xB03224` | 3 | `→ CyGameModeFactory` |
| `CyTeamSoccerModeFactory` | `0xB03234` | 3 | `→ CyGameModeFactory` |
| `CyAIMultiModeFactory` | `0xB03244` | 3 | `→ CyGameModeFactory` |
| `CyOccupyModeFactory` | `0xB03254` | 3 | `→ CyGameModeFactory` |
| `CyWeaponTestModeFactory` | `0xB03264` | 3 | `→ CyGameModeFactory` |
| `CyGunShootingModeFactory` | `0xB03274` | 3 | `→ CyGameModeFactory` |
| `CyPulpnRollModeFactory` | `0xB03284` | 3 | `→ CyGameModeFactory` |
| `CyChattingRoomModeFactory` | `0xB03294` | 3 | `→ CyGameModeFactory` |
| `CyTutorialModeFactory` | `0xB032A4` | 3 | `→ CyGameModeFactory` |
| `CyPracticeModeFactory` | `0xB032B4` | 3 | `→ CyGameModeFactory` |
| `CyStealModeFactory` | `0xB032C4` | 3 | `→ CyGameModeFactory` |
| `CyTeamSurvivalModeFactory` | `0xB032D4` | 3 | `→ CyGameModeFactory` |
| `CyDefuseBombModeFactory` | `0xB032E4` | 3 | `→ CyGameModeFactory` |
| `CyIndividualSurvivalModeFactory` | `0xB032F4` | 3 | `→ CyGameModeFactory` |
| `CyTeamMatchModeFactory` | `0xB03304` | 3 | `→ CyGameModeFactory` |
| `CyGameModeFactory` | `0xB03314` | 3 | root |
| `CyAIMULTIMode` | `0xB03584` | 29 | `→ CyGameMode` |
| `CyChattingRoomMode` | `0xB041B4` | 29 | `→ CyGameMode` |
| `CyDefuseBombMode` | `0xB04254` | 29 | `→ CyGameMode` |
| `CyGunShootingMode` | `0xB04E8C` | 29 | `→ CyGameMode` |
| `CyIndividualSurvivalMode` | `0xB0510C` | 29 | `→ CyGameMode` |
| `CyOccupyMode` | `0xB051B4` | 31 | `→ CyGameMode` |
| `CyOccupyRenewalMode` | `0xB059EC` | 29 | `→ CyGameMode` |
| `CyPracticeMode` | `0xB062E4` | 29 | `→ CyGameMode` |
| `CyPulpnRollMode` | `0xB06384` | 30 | `→ CyGameMode` |
| `CyStealMode` | `0xB06C94` | 29 | `→ CyGameMode` |
| `CyTeamMatchMode` | `0xB06F14` | 29 | `→ CyGameMode` |
| `CyTeamSurvivalMode` | `0xB071DC` | 29 | `→ CyGameMode` |
| `CyTutorialMode` | `0xB073E4` | 29 | `→ CyGameMode` |
| `CyWeaponTestMode` | `0xB07484` | 29 | `→ CyGameMode` |

### 4.3 Application state, voting, and related roots

| Type | vftable | Methods | Flag | Base chain / offset |
|---|---:|---:|:---:|---|
| `CGameRule` | `0xAF6EDC` | 1 | — | root, `+0x0` |
| `CyGameModes::CyUserEventAlarmUI` | `0xB02E7C` | 3 | — | root, `+0x0` |
| `CyMouse::CyLobbyMouseCursor` | `0xB0906C` | 5 | — | `→ CyMouseCursor`, `+0x0` |
| `CyAppFramework::CyGames::CyInGame` | `0xB09154` | 8 | — | `→ CyGame`, `+0x0` |
| `CyAppFramework::CyGames::CyGame` | `0xB09178` | 8 | — | root, `+0x0` |
| `CyInSubExitToChannel` | `0xB0919C` | 5 | — | `→ CyInSubGame`, `+0x0` |
| `CyInSubExitToGameRoom` | `0xB091B4` | 5 | — | `→ CyInSubGame`, `+0x0` |
| `CyInSubPlaying` | `0xB091CC` | 5 | — | `→ CyInSubGame`, `+0x0` |
| `CyInSubReady` | `0xB091E4` | 5 | — | `→ CyInSubGame`, `+0x0` |
| `CyInSubGame` | `0xB091FC` | 5 | — | root, `+0x0` |
| `CyInSubLoading` | `0xB0923C` | 5 | — | `→ CyInSubGame`, `+0x0` |
| `CyOutGame` | `0xB0927C` | 8 | — | `→ CyGame`, `+0x0` |
| `CyOutSubLobby` | `0xB092A0` | 6 | — | `→ CyOutSubGame`, `+0x0` |
| `CyOutSubGame` | `0xB092BC` | 6 | — | root, `+0x0` |
| `CyOutSubTestScene` | `0xB09304` | 6 | — | `→ CyOutSubGame`, `+0x0` |
| `CyOutSubStartLoGo` | `0xB09334` | 6 | — | `→ CyOutSubGame`, `+0x0` |
| `CyOutSubStartup` | `0xB09370` | 6 | — | `→ CyOutSubGame`, `+0x0` |
| `Voter` | `0xB22FCC` | 3 | — | root, `+0x0` |
| `VoterMgr` (`IVotingNetwork` base) | `0xB2306C` | 7 | M | `VoterMgr → IVotingNetwork`, `+0x30` |
| `VoterMgr` (`Voter` base) | `0xB2308C` | 7 | M | `VoterMgr → Voter`, `+0x0` |
| `IVotingNetwork` | `0xB230AC` | 7 | — | root, `+0x0` |
| `CVoteTargetList` | `0xB230CC` | 8 | — | `→ IVoteTargetList`, `+0x0` |
| `IVoteTargetList` | `0xB230F0` | 8 | — | root, `+0x0` |
| `CVotingTargetUI` | `0xB23114` | 7 | — | `→ IVotingTargetUI → IVotingUI`, `+0x0` |
| `IVotingTargetUI` | `0xB23134` | 7 | — | `→ IVotingUI`, `+0x0` |
| `CVotingApprovalUI` | `0xB23154` | 4 | — | `→ IVotingApprovalUI → IVotingUI`, `+0x0` |
| `IVotingApprovalUI` | `0xB23168` | 4 | — | `→ IVotingUI`, `+0x0` |
| `IVotingUI` | `0xB2317C` | 4 | — | root, `+0x0` |
| `CVotingStateUI` | `0xB231DC` | 15 | — | `→ IVotingStateUI → IVotingUI`, `+0x0` |
| `IVotingStateUI` | `0xB2321C` | 14 | — | `→ IVotingUI`, `+0x0` |

## 5. Gameplay, engine, entities, bots, and effects

### 5.1 Player, camera, weapon, projectile, and game objects

| Type | vftable | Methods | Flag | Base chain / offset |
|---|---:|---:|:---:|---|
| `CPaperCtrl` | `0xAEF41C` | 34 | — | `→ IPaperCtrl`, `+0x0` |
| `CNaniObj` | `0xAEF650` | 1 | — | root, `+0x0` |
| `CNaniCtrl` | `0xAEF658` | 1 | — | root, `+0x0` |
| `CPaperModel` | `0xAEF864` | 1 | — | root, `+0x0` |
| `CheckHitAble_MoreConrrectNess` | `0xAEF870` | 2 | — | `→ ICheckHitAble`, `+0x0` |
| `ICheckHitAble` | `0xAEF87C` | 2 | — | root, `+0x0` |
| `CPendant` | `0xAEF97C` | 1 | — | root, `+0x0` |
| `CPendantCtrl` | `0xAEF9D4` | 1 | — | root, `+0x0` |
| `CDamageWareHouseForBot` | `0xAF0348` | 1 | — | root, `+0x0` |
| `CDamageDetect` | `0xAF0454` | 5 | — | `→ IDamageDetect`, `+0x0` |
| `IDamageDetect` | `0xAF046C` | 3 | — | root, `+0x0` |
| `CEffCtrl` | `0xAF152C` | 1 | — | root, `+0x0` |
| `CBeffData` | `0xAF1664` | 1 | — | root, `+0x0` |
| `CBFeffDataCtrl` | `0xAF16A0` | 1 | — | root, `+0x0` |
| `CDeffData` | `0xAF16CC` | 1 | — | root, `+0x0` |
| `CFeffData` | `0xAF1718` | 1 | — | root, `+0x0` |
| `CKeffData` | `0xAF1758` | 1 | — | root, `+0x0` |
| `CLeffData` | `0xAF17A4` | 1 | — | root, `+0x0` |
| `CNeffData` | `0xAF17F4` | 1 | — | root, `+0x0` |
| `CReffData` | `0xAF1838` | 1 | — | root, `+0x0` |
| `CGameUICtrl` | `0xAF19F8` | 1 | — | root, `+0x0` |
| `CGameUISDir` | `0xAF2344` | 1 | — | root, `+0x0` |
| `CUIChatRender` | `0xAF23B4` | 1 | — | root, `+0x0` |
| `CUISummary` | `0xAF304C` | 1 | — | root, `+0x0` |
| `CUISysMessage` | `0xAF6CB4` | 1 | — | root, `+0x0` |
| `CBaseCamera` | `0xB0AA80` | 7 | — | root, `+0x0` |
| `CFirstPersonCamera` | `0xB0AAA0` | 7 | — | `→ CBaseCamera`, `+0x0` |
| `CModelViewerCamera` | `0xB0AAC0` | 7 | — | `→ CBaseCamera`, `+0x0` |
| `CItemAbility` | `0xB0C47C` | 1 | — | root, `+0x0` |
| `CItemAbilityEffect` | `0xB0C4C8` | 6 | — | `→ CTriggerBullet → CTriggerBase`, `+0x0` |
| `CTriggerBullet` | `0xB0C4E4` | 4 | — | `→ CTriggerBase`, `+0x0` |
| `CTriggerBase` | `0xB0C4F8` | 2 | — | root, `+0x0` |
| `CAbilityOperatorMinus` | `0xB0CB70` | 1 | — | `→ CAbilityOperator`, `+0x0` |
| `CAbilityOperatorPlus` | `0xB0CB78` | 1 | — | `→ CAbilityOperator`, `+0x0` |
| `CAbilityOperatorPersentMinus` | `0xB0CB80` | 1 | — | `→ CAbilityOperator`, `+0x0` |
| `CAbilityOperatorPersentPlus` | `0xB0CB88` | 1 | — | `→ CAbilityOperator`, `+0x0` |
| `CAbilityOperator` | `0xB0CB90` | 1 | — | root, `+0x0` |
| `CBulletShot` | `0xB0F6EC` | 1 | — | root, `+0x0` |
| `RocketTrailMgr` | `0xB0F74C` | 11 | — | `→ MissileObjDecorator → MissileObject`, `+0x0` |
| `MissileObjDecorator` | `0xB0F77C` | 11 | — | `→ MissileObject`, `+0x0` |
| `ThrownFlameRocketObject` | `0xB0FC54` | 11 | — | `→ MissileObject`, `+0x0` |
| `ThrownFlameFireObject` | `0xB0FF78` | 11 | — | `→ MissileObject`, `+0x0` |
| `ThrownFlameLaserObject` | `0xB10194` | 12 | — | `→ MissileObject`, `+0x0` |
| `CNewSkillFlash` | `0xB104FC` | 1 | — | root, `+0x0` |
| `CArrowObj` | `0xB130F4` | 1 | — | root, `+0x0` |
| `CArrowCtrl` | `0xB13100` | 1 | — | root, `+0x0` |

### 5.2 `qp_engine` entity and map-gimmick family

| Type | vftable | Methods | Base chain |
|---|---:|---:|---|
| `qp_engine::CEntity` | `0xB0CFC4` | 14 | root |
| `qp_engine::CEntClient` | `0xB0D004` | 15 | `→ CEntity` |
| `qp_engine::CEntityManager` | `0xB0D068` | 1 | root |
| `qp_engine::CTriggerClipGimmick` | `0xB0D070` | 14 | `→ CTrigger → CEntity` |
| `qp_engine::CEntMover` | `0xB0D0D0` | 14 | `→ CEntity` |
| `qp_engine::CEntFuncPlat` | `0xB0D10C` | 14 | `→ CEntMover → CEntity` |
| `qp_engine::CEntFuncButton` | `0xB0D154` | 14 | `→ CEntMover → CEntity` |
| `qp_engine::CEntFuncStatic` | `0xB0D190` | 14 | `→ CEntMover → CEntity` |
| `qp_engine::CEntFuncRotating` | `0xB0D1CC` | 14 | `→ CEntMover → CEntity` |
| `qp_engine::CEntFuncBobbing` | `0xB0D208` | 14 | `→ CEntMover → CEntity` |
| `qp_engine::CEntFuncPendulum` | `0xB0D244` | 14 | `→ CEntMover → CEntity` |
| `qp_engine::CEntFuncTrain` | `0xB0D28C` | 14 | `→ CEntMover → CEntity` |
| `qp_engine::CEntFuncDoor` | `0xB0D2C8` | 14 | `→ CEntMover → CEntity` |
| `qp_engine::CEntFuncGimmick` | `0xB0D36C` | 16 | `→ CEntMover → CEntity` |
| `qp_engine::CItem` | `0xB0D6C4` | 15 | `→ CEntity` |
| `qp_engine::CDropItem` | `0xB0D704` | 15 | `→ CItem → CEntity` |
| `qp_engine::CMapSpawnItem` | `0xB0D744` | 15 | `→ CItem → CEntity` |
| `qp_engine::CPulpItem` | `0xB0D784` | 15 | `→ CItem → CEntity` |
| `qp_engine::CMapData` | `0xB0D880` | 1 | root |
| `qp_engine::CRenderer` | `0xB0DA7C` | 1 | `→ CDrawException` |
| `CDrawException` | `0xB0DA84` | 1 | root |
| `qp_engine::CTargetPush` | `0xB0DBCC` | 14 | `→ CEntity` |
| `qp_engine::CTargetDelay` | `0xB0DC08` | 14 | `→ CEntity` |
| `qp_engine::CTargetPrint` | `0xB0DC44` | 14 | `→ CEntity` |
| `qp_engine::CTargetAreaName` | `0xB0DC80` | 14 | `→ CEntity` |
| `qp_engine::CTargetDSP` | `0xB0DCBC` | 14 | `→ CEntity` |
| `qp_engine::CTargetSpeaker` | `0xB0DCF8` | 14 | `→ CEntity` |
| `qp_engine::CTargetTeleporter` | `0xB0DD34` | 14 | `→ CEntity` |
| `qp_engine::CTargetRelay` | `0xB0DD70` | 14 | `→ CEntity` |
| `qp_engine::CTargetKill` | `0xB0DDAC` | 14 | `→ CEntity` |
| `qp_engine::CTargetPosition` | `0xB0DDE8` | 14 | `→ CEntity` |
| `qp_engine::CTargetLocation` | `0xB0DE24` | 14 | `→ CEntity` |
| `qp_engine::CTargetGive` | `0xB0DE60` | 14 | `→ CEntity` |
| `qp_engine::CTrigger` | `0xB0DEE0` | 14 | `→ CEntity` |
| `qp_engine::CTriggerMulti` | `0xB0DF1C` | 14 | `→ CTrigger → CEntity` |
| `qp_engine::CTriggerAlways` | `0xB0DF58` | 14 | `→ CTrigger → CEntity` |
| `qp_engine::CTriggerPush` | `0xB0DF94` | 14 | `→ CTrigger → CEntity` |
| `qp_engine::CTriggerTeleport` | `0xB0DFD0` | 14 | `→ CTrigger → CEntity` |
| `qp_engine::CTriggerDoor` | `0xB0E00C` | 14 | `→ CTrigger → CEntity` |
| `qp_engine::CTriggerPlat` | `0xB0E048` | 14 | `→ CTrigger → CEntity` |
| `qp_engine::CTriggerHurt` | `0xB0E084` | 14 | `→ CTrigger → CEntity` |
| `qp_engine::CTriggerTimer` | `0xB0E0C0` | 14 | `→ CTrigger → CEntity` |
| `qp_engine::CPathCorner` | `0xB0E0FC` | 14 | `→ CEntity` |
| `qp_engine::CTriggerArea` | `0xB0E138` | 14 | `→ CTrigger → CEntity` |
| `qp_engine::CTriggerjump` | `0xB0E174` | 14 | `→ CTrigger → CEntity` |
| `qp_engine::CTriggerDart` | `0xB0E1B0` | 14 | `→ CTrigger → CEntity` |
| `qp_engine::CTriggerObstacle` | `0xB0E1EC` | 14 | `→ CTrigger → CEntity` |
| `MapGimmickForMultiMode` | `0xB1AFB0` | 12 | `→ MapGimmickSystem → MapGimmickChecker` |
| `MapGimmickForSingleMode` | `0xB1AFE4` | 12 | `→ MapGimmickSystem → MapGimmickChecker` |
| `MapGimmickSystem` | `0xB1B018` | 12 | `→ MapGimmickChecker` |
| `MapGimmickChecker` | `0xB1B058` | 12 | root |
| `GimmickProperties` | `0xB1B04C` | 2 | `→ CUIDocument` |

### 5.3 Bot, pathfinding, and AI family

| Type | vftable | Methods | Flag | Base chain / offset |
|---|---:|---:|:---:|---|
| `SimpleGameAIManager` | `0xB04E68` | 8 | — | `→ AIGameManager → BaseGameEntity`, `+0x0` |
| `AIGameManager` | `0xB1394C` | 8 | — | `→ BaseGameEntity`, `+0x0` |
| `BaseGameEntity` | `0xB13970` | 3 | — | root, `+0x0` |
| `CMonsterSuicideFighter` (monster base) | `0xB13B20` | 8 | M | `→ CMonster → BaseGameEntity`, `+0x0` |
| `CMonsterSuicideFighter` (attack-process base) | `0xB13B18` | 1 | M | `→ CMonster → CBotAttackProcess`, `+0x8` |
| `CMonsterSniper` (monster base) | `0xB13B7C` | 8 | M | `→ CMonster → BaseGameEntity`, `+0x0` |
| `CMonsterSniper` (attack-process base) | `0xB13B74` | 1 | M | `→ CMonster → CBotAttackProcess`, `+0x8` |
| `CMonsterCharger` (monster base) | `0xB13BC0` | 8 | M | `→ CMonster → BaseGameEntity`, `+0x0` |
| `CMonsterCharger` (attack-process base) | `0xB13BB8` | 1 | M | `→ CMonster → CBotAttackProcess`, `+0x8` |
| `CMonsterTargetting` | `0xB13B44` | 5 | — | `→ State<CMonster>`, `+0x0` |
| `State<CMonster>` | `0xB13B5C` | 5 | — | root, `+0x0` |
| `CMonsterCharging` | `0xB13BA0` | 5 | — | `→ State<CMonster>`, `+0x0` |
| `CMonster` (attack-process base) | `0xB16144` | 1 | M | `→ CBotAttackProcess`, `+0x8` |
| `CMonster` (entity base) | `0xB1614C` | 8 | M | `→ BaseGameEntity`, `+0x0` |
| `CBotAttackProcess` | `0xB13F44` | 1 | — | root, `+0x0` |
| `CBotCollider` | `0xAFF208` | 6 | — | `→ CUserMoveCtrl`, `+0x0` |
| `CUserMoveCtrl` | `0xB01D14` | 1 | — | root, `+0x0` |
| `PathFinderUsingFixedData` | `0xB1442C` | 12 | — | `→ PathFinderUsingComputedData → PathFinder → IPathFinder`, `+0x0` |
| `PathFinderUsingComputedData` | `0xB14460` | 12 | — | `→ PathFinder → IPathFinder`, `+0x0` |
| `PathFinder` | `0xB144D0` | 12 | — | `→ IPathFinder`, `+0x0` |
| `IPathFinder` | `0xB15188` | 9 | — | root, `+0x0` |
| `CBotNaviStepCtrl` | `0xB14494` | 7 | — | `→ IBotNaviStepCtrl`, `+0x0` |
| `IBotNaviStepCtrl` | `0xB144B4` | 6 | — | root, `+0x0` |
| `CLookAheadChecker` | `0xB151B0` | 3 | — | `→ ILookAheadChecker`, `+0x0` |
| `ILookAheadChecker` | `0xB151C0` | 3 | — | root, `+0x0` |
| `AASWorld` | `0xB151D0` | 2 | — | `→ IAASWorld`, `+0x0` |
| `IAASWorld` | `0xB151DC` | 2 | — | root, `+0x0` |
| `CheckGoStraight` | `0xB15208` | 1 | — | root, `+0x0` |
| `CheckMoveAble` | `0xB152F0` | 1 | — | root, `+0x0` |
| `StateMachine<BotMove>` | `0xB158EC` | 1 | — | root, `+0x0` |
| `CDelayMoveSpeedBot` | `0xB158F4` | 2 | — | `→ CyGameWeapons::CyDelayMoveSpeedRatio`, raw base `+0x4` |
| `BotFreeFall` | `0xB15900` | 5 | — | `→ State<BotMove>`, `+0x0` |
| `State<BotMove>` | `0xB15918` | 5 | — | root, `+0x0` |
| `BotMoveRouteFail` | `0xB15930` | 5 | — | `→ State<BotMove>`, `+0x0` |
| `IRoutePredicter` | `0xB15990` | 3 | — | root, `+0x0` |
| `RoutePredicter` | `0xB159A0` | 3 | — | `→ IRoutePredicter`, `+0x0` |
| `CAASPredictRouteImpl` | `0xB159B0` | 3 | — | `→ IAASPredictRouteImpl`, `+0x0` |
| `VerifiedRoutes` | `0xB159C0` | 2 | — | `→ RoutePosStore → std::vector<Area> → std::_Vector_val<Area> → std::_Container_base`; raw vector base `+0x4` |
| `IAASPredictRouteImpl` | `0xB159CC` | 2 | — | root, `+0x0` |
| `RoutePosStore` | `0xB159D8` | 1 | — | `→ std::vector<Area> → std::_Vector_val<Area> → std::_Container_base`; raw vector base `+0x4` |
| `BotMoveReRoute` | `0xB159E0` | 5 | — | `→ State<BotMove>`, `+0x0` |
| `BotAirEffect` | `0xB159F8` | 5 | — | `→ State<BotMove>`, `+0x0` |
| `BotNuckBack` | `0xB15A10` | 5 | — | `→ State<BotMove>`, `+0x0` |
| `BotMoveRoute` | `0xB15A28` | 5 | — | `→ State<BotMove>`, `+0x0` |
| `BotJumpingTrigger` | `0xB15A40` | 5 | — | `→ State<BotMove>`, `+0x0` |
| `RoutePosVerifier` | `0xB15A94` | 1 | — | `→ IRoutePosVerifier`, `+0x0` |
| `IRoutePosVerifier` | `0xB15A9C` | 1 | — | root, `+0x0` |

## 6. Documents, properties, game-center, and small utility types

| Type | vftable | Methods | Base chain / offset |
|---|---:|---:|---|
| `CLaserProperty` | `0xAF7050` | 2 | `→ CUIDocument`, `+0x0` |
| `CFrustum` | `0xAF74CC` | 2 | `→ IFrustum`, `+0x0` |
| `IFrustum` | `0xAF74D8` | 2 | root, `+0x0` |
| `CSpritePackageMgr` | `0xAF7ABC` | 1 | `→ CSingleton<CSpritePackageMgr>`, raw base `+0x4` |
| `CSpritePackage` | `0xAF7AC4` | 3 | `→ CUIDocument`, `+0x0` |
| `DShowMovie` | `0xAFC34C` | 1 | root, `+0x0` |
| `tricod::Log_Mock` | `0xAFC3D4` | 2 | `→ CTricodLog`, `+0x0` |
| `CTricodLog` | `0xAFC3E0` | 2 | root, `+0x0` |
| `tricod::pmADCrativeStatic` | `0xAFC78C` | 1 | root, `+0x0` |
| `BillBoardSpriteMgr` | `0xAFCBB4` | 1 | root, `+0x0` |
| `ConsoleVar<float>` | `0xAFD134` | 2 | `→ ConsoleCommand`, `+0x0` |
| `ConsoleVar<int>` | `0xAFD140` | 2 | `→ ConsoleCommand`, `+0x0` |
| `ConsoleCommandRoot` | `0xAFD14C` | 2 | `→ ConsoleCommand`, `+0x0` |
| `ConsoleCommand` | `0xAFD158` | 2 | root, `+0x0` |
| `ConsoleVarCharAbility` | `0xAFD1C4` | 2 | `→ ConsoleCommand`, `+0x0` |
| `MissileObject` | `0xAFEFA0` | 11 | root, `+0x0` |
| `WireFrameNozzleObject` | `0xAFF090` | 13 | `→ MissileObject`, `+0x0` |
| `MissileObjMgr` | `0xAFF0E8` | 1 | root, `+0x0` |
| `CUpdateManager` | `0xB0133C` | 1 | root, `+0x0` |
| `CViewObj` | `0xB01E44` | 1 | root, `+0x0` |
| `CTUTPackage` | `0xB0756C` | 5 | `→ CUIDocument`, `+0x0` |
| `CMouseStateRecvGame` | `0xB07A6C` | 2 | `→ CMouseStateRecv`, `+0x0` |
| `CMouseStateRecv` | `0xB07A78` | 2 | root, `+0x0` |
| `CDXUTControl` | `0xB09FC4` | 20 | root, `+0x0` |
| `CDXUTStatic` | `0xB0A01C` | 20 | `→ CDXUTControl`, `+0x0` |
| `CDXUTButton` | `0xB0A074` | 20 | `→ CDXUTStatic → CDXUTControl`, `+0x0` |
| `CDXUTCheckBox` | `0xB0A0CC` | 21 | `→ CDXUTButton → CDXUTStatic → CDXUTControl`, `+0x0` |
| `CDXUTRadioButton` | `0xB0A12C` | 22 | `→ CDXUTCheckBox → CDXUTButton → CDXUTStatic → CDXUTControl`, `+0x0` |
| `CDXUTComboBox` | `0xB0A18C` | 20 | `→ CDXUTButton → CDXUTStatic → CDXUTControl`, `+0x0` |
| `CDXUTSlider` | `0xB0A1E4` | 20 | `→ CDXUTControl`, `+0x0` |
| `CDXUTScrollBar` | `0xB0A23C` | 20 | `→ CDXUTControl`, `+0x0` |
| `CDXUTListBox` | `0xB0A2A4` | 20 | `→ CDXUTControl`, `+0x0` |
| `CDXUTEditBox` | `0xB0A2FC` | 20 | `→ CDXUTControl`, `+0x0` |
| `CDXUTIMEEditBox` | `0xB0A35C` | 23 | `→ CDXUTEditBox → CDXUTControl`, `+0x0` |
| `CNewSkillProperty` | `0xB1076C` | 6 | `→ CUIDocument`, `+0x0` |
| `CTNMTAwardProperty` | `0xB10DE4` | 5 | `→ CUIDocument`, `+0x0` |
| `CTNMTMListProperty` | `0xB10FCC` | 3 | `→ CUIDocument`, `+0x0` |
| `CTNMTPInfoProperty` | `0xB1119C` | 3 | `→ CUIDocument`, `+0x0` |
| `CTournamentManager` | `0xB112C4` | 1 | root, `+0x0` |
| `CGameInUserVoiceCustomize` | `0xB112EC` | 6 | `→ IVoiceCustomize`, `+0x0` |
| `IVoiceCustomize` | `0xB11308` | 6 | root, `+0x0` |
| `CMyVoiceCustomize` | `0xB11344` | 6 | `→ IVoiceCustomize`, `+0x0` |
| `CUIVCustomizebattleANDemotion` | `0xB1138C` | 4 | root, `+0x0` |
| `CUIVCustomizeZXV` | `0xB11A10` | 3 | root, `+0x0` |
| `CVCustomizeScriptProperty` | `0xB12284` | 4 | `→ CUIDocument`, `+0x0` |
| `CXIGNCODE` | `0xB12644` | 11 | `→ CSecurity`, `+0x0` |
| `CSecurity` | `0xB12674` | 10 | root, `+0x0` |
| `CEffectBase` | `0xB126C0` | 5 | root, `+0x0` |
| `CEffectCommon` | `0xB1270C` | 5 | `→ CEffectSnow → CEffectBase`, `+0x0` |
| `CEffectCommonForBot` | `0xB12724` | 5 | `→ CEffectSnow → CEffectBase`, `+0x0` |
| `CEffectFire` / `CEffectFireforBot` | `0xB12764` / `0xB12780` | 6 / 5 | both `→ CEffectBase`, `+0x0` |
| `CEffectGlue` / `CEffectGlueforBot` | `0xB127C4` / `0xB127DC` | 5 / 5 | both `→ CEffectBase`, `+0x0` |
| `CEffectLiquid` / `CEffectLiquidforBot` | `0xB1281C` / `0xB12834` | 5 / 5 | both `→ CEffectBase`, `+0x0` |
| `CEffectSheep` / `CEffectSheepforBot` | `0xB129D4` / `0xB129EC` | 5 / 5 | both `→ CEffectBase`, `+0x0` |
| `CEffectSnow` / `CEffectSnowforBot` | `0xB12A34` / `0xB12A4C` | 5 / 5 | both `→ CEffectBase`, `+0x0` |
| `CheckHitAble_Original` | `0xB12A8C` | 2 | `→ ICheckHitAble`, `+0x0` |
| `CFaceChatScriptProperty` | `0xB12FA4` | 4 | `→ CUIDocument`, `+0x0` |
| `sNetCafeInfo` | `0xB233F8` | 1 | root, `+0x0` |
| `sWeaponSlotInGame` | `0xB23404` | 3 | `→ sNetCafeInfo`, `+0x0` |

## 7. Runtime / third-party RTTI anchors

這些列入表是因為它們屬於使用者提供的 export。通常只應用於辨識 external
runtime/library code、避免誤認為 PaperMan domain logic；不應直接變成 server 實作依據。

### 7.1 Standard library, Boost, and PugiXML

| Type | vftable | Methods | Base chain / notable raw offset |
|---|---:|---:|---|
| `std::length_error` | `0xADE338` | 2 | `→ std::logic_error → std::exception`, `+0x0` |
| `std::logic_error` | `0xADE344` | 2 | `→ std::exception`, `+0x0` |
| `std::out_of_range` | `0xADE350` | 2 | `→ std::logic_error → std::exception`, `+0x0` |
| `std::bad_alloc` | `0xADE378` | 2 | `→ std::exception`, `+0x0` |
| `std::ios_base` | `0xAF7620` | 1 | `→ std::_Iosb<int>`, raw base `+0x4` |
| `std::basic_ios<char,…>` | `0xAF7628` | 1 | `→ std::ios_base → std::_Iosb<int>`, raw ios-base hierarchy relation `+0x4` |
| `std::basic_istringstream<char,…>` | `0xAF7770` | 1 | `→ basic_istream → basic_ios → ios_base → _Iosb`; raw stream base `+0x50` |
| `std::basic_istream<char,…>` | `0xAF7778` | 1 | `→ basic_ios → ios_base → _Iosb`; raw stream base `+0x8` |
| `std::basic_stringbuf<char,…>` | `0xAF7790` | 14 | `→ basic_streambuf`, `+0x0` |
| `std::basic_streambuf<char,…>` | `0xAF77CC` | 14 | root, `+0x0` |
| `std::ios_base::failure` | `0xAF783C` | 2 | `→ std::runtime_error → std::exception`, `+0x0` |
| `std::runtime_error` | `0xAF7848` | 2 | `→ std::exception`, `+0x0` |
| `std::ctype<char>` | `0xAF786C` | 11 | `→ ctype_base → locale::facet`, `+0x0` |
| `std::ctype_base` | `0xAF789C` | 1 | `→ locale::facet`, `+0x0` |
| `std::locale::facet` | `0xAF78A4` | 1 | root, `+0x0` |
| `std::basic_ifstream<char,…>` | `0xB568D8` | 1 | `→ basic_istream → basic_ios → ios_base → _Iosb`; raw stream base `+0x58` |
| `std::basic_filebuf<char,…>` | `0xB568E8` | 14 | `→ basic_streambuf`, `+0x0` |
| `std::codecvt<char,char,int>` | `0xB56924` | 8 | `→ codecvt_base → locale::facet`, `+0x0` |
| `std::codecvt_base` | `0xB56948` | 4 | `→ locale::facet`, `+0x0` |
| `std::locale::_Locimp` | `0xB56A30` | 1 | `→ locale::facet`, `+0x0` |
| `type_info` | `0xB56B04` | 1 | root, `+0x0` |
| `std::exception` | `0xB56B38` | 2 | root, `+0x0` |
| `std::bad_cast` / `std::bad_typeid` | `0xB56B58` / `0xB56B64` | 2 / 2 | both `→ std::exception`, `+0x0` |
| `std::__non_rtti_object` | `0xB56B70` | 2 | `→ bad_typeid → exception`, `+0x0` |
| `std::bad_exception` | `0xB575B8` | 2 | `→ std::exception`, `+0x0` |
| `boost::thread_exception` | `0xB62658` | 2 | `→ std::exception`, `+0x0` |
| `boost::lock_error` | `0xB62664` | 2 | `→ thread_exception → exception`, `+0x0` |
| `boost::bad_function_call` | `0xB62670` | 2 | `→ runtime_error → exception`, `+0x0` |
| `boost::thread_resource_error` | `0xB6269C` | 2 | `→ thread_exception → exception`, `+0x0` |
| `std::basic_ostream<char,…>` | `0xB626DC` | 1 | `→ basic_ios → ios_base → _Iosb`, raw base `+0x4` |
| `std::basic_ostringstream<char,…>` | `0xB626E4` | 1 | `→ basic_ostream → basic_ios → ios_base → _Iosb`, raw base `+0x4C` |
| `std::num_put<char,…>` | `0xB626EC` | 9 | `→ locale::facet`, `+0x0` |
| `std::numpunct<char>` | `0xB62724` | 6 | `→ locale::facet`, `+0x0` |
| `std::ctype<wchar_t>` | `0xB628F4` | 15 | `→ ctype_base → locale::facet`, `+0x0` |
| `pug::xml_attribute` | `0xB55C2C` | 1 | root, `+0x0` |
| `pug::xml_node` | `0xB55C34` | 1 | root, `+0x0` |
| `pug::xml_node::xml_node_iterator` | `0xB55C3C` | 16 | `→ xml_iterator → random_access_iterator_tag → bidirectional → forward → input`; raw iterator base `+0x4` |
| `pug::xml_parser` | `0xB55C80` | 1 | root, `+0x0` |
| `pug::xml_iterator<xml_node,…>` | `0xB55C8C` | 16 | iterator-tag chain, `+0x0` / raw tag base `+0x4` |
| `pug::forward_class<xml_node>` | `0xB55CD0` | 1 | root, `+0x0` |

### 7.2 Other supplied external/COM records

| Type | vftable | Methods | Base chain / offset |
|---|---:|---:|---|
| `boost::detail::sp_counted_base` | `0xAF0F08` | 4 | root, `+0x0` |
| `boost::detail::sp_counted_impl_p<CGunModel>` | `0xAF0EF4` | 4 | `→ sp_counted_base`, `+0x0` |
| `boost::detail::sp_counted_impl_p<CBeffData>` | `0xAF166C` | 4 | `→ sp_counted_base`, `+0x0` |
| `boost::detail::sp_counted_impl_p<CDeffData>` | `0xAF16E4` | 4 | `→ sp_counted_base`, `+0x0` |
| `boost::detail::sp_counted_impl_p<CFeffData>` | `0xAF1720` | 4 | `→ sp_counted_base`, `+0x0` |
| `boost::detail::sp_counted_impl_p<CKeffData>` | `0xAF1770` | 4 | `→ sp_counted_base`, `+0x0` |
| `boost::detail::sp_counted_impl_p<CLeffData>` | `0xAF17AC` | 4 | `→ sp_counted_base`, `+0x0` |
| `boost::detail::sp_counted_impl_p<CNeffData>` | `0xAF1800` | 4 | `→ sp_counted_base`, `+0x0` |
| `boost::detail::sp_counted_impl_p<CReffData>` | `0xAF1840` | 4 | `→ sp_counted_base`, `+0x0` |
| `boost::detail::sp_counted_impl_p<CBallCase>` | `0xAF1890` | 4 | `→ sp_counted_base`, `+0x0` |
| `boost::detail::sp_counted_impl_p<CBombLine>` | `0xAF1944` | 4 | `→ sp_counted_base`, `+0x0` |
| `boost::detail::sp_counted_impl_p<CUISysMessageData>` | `0xAF6E88` | 4 | `→ sp_counted_base`, `+0x0` |
| `boost::detail::sp_counted_impl_p<CUIAIMultiSysMessageData>` | `0xAF6E9C` | 4 | `→ sp_counted_base`, `+0x0` |
| `boost::detail::sp_counted_impl_p<UIImage>` | `0xAFAFEC` | 4 | `→ sp_counted_base`, `+0x0` |
| `boost::detail::sp_counted_impl_p<IDirect3DTexture9*>` | `0xAFC748` | 4 | `→ sp_counted_base`, `+0x0` |
| `boost::detail::sp_counted_impl_p<CHandPAPData>` | `0xB1A490` | 4 | `→ sp_counted_base`, `+0x0` |
| `boost::detail::sp_counted_impl_p<C1stPartsModelPAPData>` | `0xB1A4A4` | 4 | `→ sp_counted_base`, `+0x0` |
| `boost::detail::sp_counted_impl_p<CGunPAPData>` | `0xB1A4B8` | 4 | `→ sp_counted_base`, `+0x0` |
| `boost::detail::sp_counted_impl_p<CHandMOTData>` | `0xB1A4CC` | 4 | `→ sp_counted_base`, `+0x0` |
| `boost::detail::sp_counted_impl_p<CPaperMOTData>` | `0xB1A4E0` | 4 | `→ sp_counted_base`, `+0x0` |
| `CTricodClient` | `0xB55614` | 2 | `→ TRICOD_INTERFACES::ClientInterface`, `+0x0` |
| `TRICOD_INTERFACES::ClientInterface` | `0xB55620` | 2 | root, `+0x0` |
| `tricod::GenericHTTPClient` | `0xB560C8` | 1 | root, `+0x0` |
| `CTricod3DClient` (debug observer) | `0xB56104` | 9, M | `→ IObserver3DforDebug`, `+0x4` |
| `CTricod3DClient` (observer) | `0xB5612C` | 19, M | `→ IObserver3D`, `+0x0` |
| `IObserver3DforDebug` | `0xB5617C` | 9 | root, `+0x0` |
| `IObserver3D` | `0xB561A4` | 18 | root, `+0x0` |
| `DontDuplicateImpressionBridge` | `0xB561F0` | 4 | `→ BridgeInterface`, `+0x0` |
| `TRICOD_INTERFACES::BridgeInterface` | `0xB56204` | 4 | root, `+0x0` |
| `CTricod3D_DX9` | `0xB5622C` | 31 | `→ CTricod3D_DX → CTricod3D → IT3dDevice → ObserverInterface`, `+0x0` |
| `IT3dDevice` | `0xB562BC` | 17 | `→ ObserverInterface`, `+0x0` |
| `CTricod3D` | `0xB5630C` | 30 | `→ IT3dDevice → ObserverInterface`, `+0x0` |
| `CTricod3D_DX` | `0xB5639C` | 31 | `→ CTricod3D → IT3dDevice → ObserverInterface`, `+0x0` |
| `iga::__imp__CTricodInnerAABBoxAccesor` | `0xB5643C` | 2 | `→ CTricodInnerAABBoxAccesor`, `+0x0` |
| `iga::CADBoard` | `0xB56454` | 9 | `→ CTricodADObject`, `+0x0` |
| `iga::CObjPoly` | `0xB5647C` | 1 | root, `+0x0` |
| `iga::CADBoardDX9` | `0xB564BC` | 11 | `→ CADBoardDX → CADBoard → CTricodADObject`, `+0x0` |
| `iga::CADBoardDX` | `0xB564FC` | 11 | `→ CADBoard → CTricodADObject`, `+0x0` |
| `_com_error` | `0xB5652C` | 1 | root, `+0x0` |

## 8. Follow-up evidence boundaries

| Observation in this RTTI tranche | Classification | What to do next | What not to do |
|---|---|---|---|
| `CPopUpCreateRoom`, `CLobbyMainRoom`, and the mode lobby UI classes are present. | **Fact/HIGH** for client type existence. | Use as xref anchors for 111/112 and room UI behavior. | Do not infer server room creation policy from the class names alone. |
| `VoterMgr`, `IVotingNetwork`, target/approval/state UI types are present. | **Fact/HIGH** for a client voting implementation family. | Use the vftables to locate client voting functions and verify request/ACK consumers. | Do not re-register 718/721 or fabricate vote queue/timer/result server state. |
| `CUDPManager`, UDP socket/thread/network manager are present. | **Fact/HIGH** for client UDP class family. | Continue the bounded `sub_596670` transport/state analysis before expanding UDP. | Do not infer that all UDP/private opcodes are safe to accept or relay. |
| `CClientData`, room/user info, `Packet`, and file-system classes are present. | **Fact/HIGH** for named client-side types. | Cross-reference constructors, readers, writers and object offsets when a concrete field is investigated. | Do not turn RTTI roots into a guessed server object layout. |
| Many item, ability, NewSkill, effect, shop, tournament and game-center UI classes are present. | **Fact/HIGH** for client presentation/implementation types. | Treat names as search hints alongside resources and packet chains. | Do not use them to grant items, infer catalog ownership, price, entitlement, default equipment or success responses. |

## Maintenance

- If a later PyClassInformer export is available as a file, keep the original artifact
  outside generated source and update this index with its version, input SHA-256, IDA
  version, image base, and exact exporter settings.
- Add a new type only with all reported vftable records for that type; preserve multiple
  inheritance offsets rather than collapsing them to the primary record.
- When an RTTI anchor leads to a packet or storage conclusion, record the **actual
  caller/callee/data-flow evidence** in `PACKETS.md` or `RESOURCES.md`, not here.
