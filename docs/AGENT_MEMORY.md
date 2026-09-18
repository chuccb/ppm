# AGENT_MEMORY.md — 原生審計記憶檔

> **重建說明（2026-09-18）**：本檔前身累積 4253 行、但因從未被追蹤進 git
> 而在 sandbox 重整時遺失；其實質內容早已逐日鏡像進 tracked docs
> （S2C_NATIVE_AUDITS / SERVER_TS_EVIDENCE / WIKI_MECHANICS / PACKETS 各節）。
> 本檔自此**納入 version control**，僅記「登入起逐步實作 walkthrough」的
> 階段錨點與原生定位，詳表一律以 tracked 專項文件為準。

## Walkthrough: Login 起逐步實作（2026-09-18）

每 phase = 原生直接證據 → TS 模組/測試對位 → docs 記錄 → commit。

| Phase | 範疇 | commit | 交付物 |
|---|---|---|---|
| 1 | admission 681 / TCP grooming / login trio | `b9f02d5f` | `SERVER_TS_EVIDENCE.md` 192 行 ledger;681 Result 表; server-list s16 定向;693 區段補正(greeting 型別/ v6 spin-lock) |
| 2 | channel handshake 143/144/195/196 | `3fad207c` | 144 case 108=0x11D/result 0=0x42/3=0xA4+7082;196 result 6→0x3A6/8→0x3A7;`client_identity` String[24]=AccountName(PaperMan.exe.c:2241) |
| 3 | 大廳同步序列（本節詳記） | `8e3c66e7` | PACKETS §3.15pre-3;原生命令行 197→199→834 定序 |
| 7 | **246/247 他人資料** | (本次) | 246 builder `sub_573DE0` 本體重讀 (Packet(246)+str+resource 0xA2) 與 TS `r.str()` 吻合;247 `sub_573EB0` 全讀: ok==1→523BF0(Phase4 亂序修正已同步)+`sub_524360` 索引式單筆外觀 (u8 index, **`>=0x14` 早退不讀**—TS 護欄一致), u8 type+12×u16;TS writer 逐欄吻合零修正;524360 為 247 與 105/108 房間快照共用 reader;§3.15pre2 補重驗注;120 測試綠 |
| 6 | **434/426 好友×信箱全語法** | (本次) | `sub_55AFC0`+`sub_537F60`(434) 與 `sub_55A630`+`sub_5378C0`(426) reader/store 雙函式重讀, 與既有 doc 三式一致零漂移;native store stride/上限一次性定案: friends ≤100、nick char[21], mail ≤10、key20/name21/body201/selector2;raw4→低byte保留(**不窄化 wire**);TS 兩 builder 升級全語法記錄投影(預設仍零筆),四支 snapshot pin+九支上限 pin;120 測試綠 |
| 5 | **200/201/202 分頁背包×PartsUp** | (本次) | `sub_570AB0`+`sub_524B70` 逐欄重驗 (u8 success→[s32 start; 直到 sentinel 的 100/5120 分頁; slot/id/raw4×2/period/u8/u16 每筆]);200 clamp/錯誤 6 arm 補注;**201/202 wire 是 17B/record 非「×20B」**(heap 0x14 的 3B pad 不上線)doc 雙處修正;新增 `GL_MYPARTSUP_ACK`/`GL_EXPIRE_PARTSUP_ACK` s2c builder 共用 `writePartsUpEntry`(s2c 20→22),wire-snapshot 17B 精確 pin;116 測試綠 |
| 4 | **198 GL_MYINFO_ACK 逐欄重驗** | `e658b579` | sub_570550 主體+523BF0/524010/524660/527550/527D00+尾段全函式重讀;語意三式互證(sub_9252D0 條件器/sub_5206F0 標籤直繫/523BF0+523E10 序列化鏡像);**TS 修掉 criticals/doubleKill/tripleKill 三欄亂序**(wire=+180,+184,+176 非遞增;原寫錯序受舊 §3.2 升冪誤表影響);PACKETS §3.2+EVIDENCE 對齊;另注 696 `sub_523A90` 變體序(176 在 172 前,跳 +144 收 +116)TS 未實作 |

### Phase 3 native 錨點（+748 scene 狀態機）

- 十個 boot/on-demand C2S builder→fn 對照:
  250=`sub_574080`、252=`sub_574120`、254=`sub_5741C0`、197=`sub_5704B0`、
  199=`sub_570A00`、246=`sub_573DE0`、425=`sub_55A580`、433=`sub_55AF20`、
  834=`sub_583120`。
- caller 反查出土 **`sub_488C60` = lobby scene `*(this+748)` 狀態機**
  (每 tick 一動作、UI-ready gate `sub_522460(dword_EE3950)` at 748∈{2,3}):
  748=1 印 L"MYINFO"→**197**; 748=2 印 L"MYITEM"→**199**; 748=3 戰績快取
  (`dword_EE8D40/44`→`sub_417E30()[7..10]`; [12]>=4 記 ROOMINFO 旗標)+
  `sub_57E3F0()`; 748=4 →**834**; 748=5 `sub_407E00()`+`sub_91D730()`
  (本地、無封包)→748=6 idle。**另路徑 `sub_49F90` 也送 834+199**
  (資料重拉分支)。
- 250 由 lobby scene UI 建構函數 (載 `ui\LOBBYMAIN.XML`、
  `sub_537710(byte_EE8968, 2)`) 尾段送出 → 必為序列第一 wire 包;
  251 死協定。198 consumer `sub_570550` 尾段**無條件送 433**。
- 834 payload = raw4 `dword_F2A684` (144 之 client_request_context 原樣);
  835 空 ACK,consumer `sub_5831D0` 僅 `sub_522440` UI 刷新。
- on-demand (非 boot 鏈): 246(str 看他人)、425(s32 信箱)、252(scene 轉場
  state:=3)、254(u8 requestContextRaw, state:=7)。
- server-ts 對位: 七個 c2s + ACK 模組線形全部吻合,113 測試綠。

### Phase 3.5 git 事故教訓

sandbox 一度把分支 anchor 回 `fcbe08f4`(起點)而 worktree 保有 sprint
內容 → commit 掛錯親代被拒。救援:`git reset --hard <remote tip>` 由遠端
重建基線(先用 `git diff <tip> --name-status` 驗證差異=僅遺失檔案)、
重放單一編輯再推。**開工先 `git log origin/<branch> -1` 核對親代鏈**;
`node_modules` 在 reset 後要 `bun install` 才跑得動 tsc。
