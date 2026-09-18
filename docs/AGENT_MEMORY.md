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
| 13 | **437 GG_ROOMBROADCAST** | (本次) | builder `sub_55B430` wire=`{u8 flag, s32 len, raw[len]}`,**無直接呼叫者**(訊息表/fn ptr);dispatcher 無 437/438 case ⇒ client 不解析 438 ⇒ **TS 解析驗線後刻意沉默**(不造任何 ACK 行為);4 pins;132 測試綠,c2s 25 |
| 12 | **435→436 friend info CSV 鏈** | (本次) | 435 `sub_55B0A0`(空 table 不送;否則 21B-stride 名單逗號相連入 String[1028]→ 單一 `str csv`) ✓;436 `sub_55B2C0`(`u8 count, rows×{str key(24B local), u8 statusRaw, [==1: str where(20B), u8 raw]}` → `sub_5382D0(key,st,where,raw+1)` + UI(matched,count,1)) ✓;**唯一證明區分是 ==1 帶 context,不造 online 語義**。TS: parser 寬上限(≤1023B csv/每段≤20B/≤100 rows/trailing 拒絕)→ 回 `statusRaw=0` rows echo(無 friend table);pbuilder 用 strMax(20/19);registry guard 拒絕 `.constants.ts` 側車 → 常數改內嵌;8 pins;131 測試綠,c2s 24/s2c 27 |
| 11 | **429→430／431→432 friend key 對** | (本次) | 429 `sub_55A860`(閘: 非空≤23B + self `sub_537740`/dup `sub_538200` 本地攔截)+431 `sub_55AD00`(閘: 非空≤23B): wire 恰 `str key` ✓。430 `sub_55AA90`(status 0 插入表→`0x1E7`;1..4→`0x1E8..0x1EB`;else→`0x1EC`;恆送空 433 via `sub_55AF20`) ✓;432 `sub_55AE10`(0 刪→`sub_538010`+433;1→`0x1ED`;2→`0x1EE`) ✓。TS 四模組(parser: 非空/≤23B/trailing 拒絕;builder `u8+strMax(23)` 以原生 24B ACK local 為界): 無 friend table ⇒ 429 self→1 else→5(原生 else 臂),431 恆→2;6 pins;129 測試綠,c2s 23/s2c 26 |
| 10 | **421→422／423→424 mailbox key 對** | (本次) | 421 `sub_55A1E0`(兩閘: key 非空≤20B + `sub_537DE0` 存在證明)+423 `sub_55A3C0`(mirror, `sub_537E90==0` 未讀才送): wire 恰 `str key` ✓。422 `sub_55A310`(非零→`sub_537A80` 刪+UI,零→resource `0x1E3`)+424 `sub_55A4F0`(非零→`sub_537D20` 寫 89,零→resource `0x1E4`)——ACK 為 `{u8 statusRaw, str key}`。TS 新增四模組(c2s parse nonempty str/trailing 拒絕/key cap=char[20]=19B;s2c emit `u8+strMax(19)`); 空信箱→**statusRaw=0** 唯一誠實回覆,勿造 status enum;6 pins(4 snapshot+2 parser);127 測試綠,c2s 21/s2c 24 |
| 9 | **834/835 data-recv-completed 鏈** | (本次) | 834 builder `sub_583120` 本體=Packet(834)+`sub_592AA0(dword_F2A684)` raw4;anti-陷阱: 144 consumer 結尾 `raw4 v69→dword_F2A684`+`u8 v66≠0→byte_EE8CB1` 一併複驗。dword_EE8CB4/EE8CB1 兩套 self-bit 分家 ✓。835 consumer `sub_5831D0` 本體=`sub_522440(dword_EE3950)` 單行,**不讀任何欄位** — 空 ACK byte-exact。三式零漂移;新增 834 parser pin(s32+trailing 拒絕)+835 空 pin,124 測試綠 |
| 8b | **252/253 shop＋254/255 invenin 重驗** | (本次) | 環境疏漏排整 (sandbox 重置後 fetch+reset 回 b42cf822; bun 以 npm-global 重裝)。252 `sub_574120` 本體(Packet(252)+state:=3)與 call-site SFX `sub_9CCEC0(10)`=trans.wav 補證;253 零 consumer 覆查(dispatcher 174–315+opcode-getter 掃描)零漂移—TS 不需更動。254 `sub_5741C0`=Packet+`sub_592920(u8)`+state:=7 ✓ 對位。255 `sub_574270` 全讀: header `{u8 mode(0/1),s32 uid,u8 contextRaw,u8 unknown}`→mode==0 追讀 swap tuple `{u8,u8,s32}`+`sub_67D870`;`uid==EE8CB4` 才讀 `u8 selectedProfile(<5)`+`sub_592500(0xA0)` blob+`sub_4BDD80`—TS mode1-only self 投影、160B blob 逐 byte 吻合;GL_INVENIN_ACK style-sweep (primitive 域接管的寬度檢查移除) |
| 8 | **codec 驗證根除重構** | (本次) | 根治問題: TS primitives 本來靜截斷 (`v & 0xff`/`v | 0`), 才被迫在 writer 流上堆 requireXxx;改為 packet.ts 型別真化: u8/s8/u16/s16/u32/s32 域全識別+非整數剔除、u64 bigint 域、f32 finite+fround 域, 新增 `strMax(text, maxBytes)` 自帶原生 buffer 上限, 加一次性 `label()` 欄位標籤 (消費後即清除, 失敗時前綴錯誤);8 支 writer 全掃 (681/198/247/200/196/201/202/434/426) requireU8/S16/U16/S32/Raw16/Raw4 全刪, 關係型護欄 (listCount/size 配對、sentinel、catalog 域) 保留;887 測試綠 (123 tests), tsc 乾淨 |
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
