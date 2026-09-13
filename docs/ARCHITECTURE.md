# PaperMan 私服全景架構 (廿九輪融會貫通版)

> 29 輪逆向的知識總圖 — 每個結論都可在 PACKETS/RESOURCES/LAYOUTS 找到
> 逐行證據與互證鏈。

## 1. 完整生命週期 (實測定案的因果鏈)

```
【帳號伺服器 TCP :40200】
connect ──► server 發 694 (門檻 0x2580) ──► client 送 682 (帳密+MAC指紋)
        ──► server 回 681 (result=1 + 伺服器清單 + ext 等級gate + Tricod)
        ──► client 進帳號大廳 (state 2)
選頻道: 195 → 196; 進大廳: 250 (無回包) → client 自拉:
  197→198 MyInfo (統計佈局=任務cond對映!) 199→200 背包(28B條目)
  105→106 名單(exp!) 107→108 房間清單 433→434 好友 425→426 信箱
心跳: server 每30s 發 102, client 回 101

【房間流程】
111 建房→112 (room_uid) / 216 密碼→217 / 113 進房→114(sub_type多態)
125 房聊→126廣播 121 換圖→122 127 ready→128 135 換位→136
129 開戰→130廣播(17欄+16×s32) → 各員 183 載入完→184 → 187→188 開打

【戰鬥 (P2P + relay)】
戰鬥伺服器: connect → 693 → client 143 (回送 n100+ext_count 雙token)
  → 144 → UDP 打洞 (私有編號 2-34, sub_595E80)
GG 中繼三模式: slot前綴轉發 / 復活六模式同構 / 聊天過濾
戰後: 133→134 回房; GP_CH*C 戰績上報 (絕對值+MAX單調);
  ACK 自動推進任務 (sub_92EF00 事件)
```

## 2. 五層封包處理架構 (client 端)

| 層 | 位置 | 處理 |
|---|---|---|
| ① dispatcher | sub_58B010 | 306 case 主分發 |
| ② 場景 vtable | sub_407360→CLobbyShop 等 | 699-723/788 |
| ③ 登入層 | 0x43E651 | 681/694/882 |
| ④ 轉蛋控制器 | 0x84A000 | 701 (11組轉輪) |
| ⑤ 語音 vtable | sub_885D00 | 792/794/796 |
| + UDP 層 | sub_595E80 | 私有 2-34 + 153-164 |

## 3. 資料層 (7 表 37,044 條真實日版)

item 21,164 (id=基底+偏移編碼) / quest 844 (cond 雙機制) /
map 123 (模式bitmask) / weapon_parts 10,648 (8組) /
parts_ability 413 (31欄彈道) / recommend 3,180 / protocol 670

## 4. 加密四件套 (全部互逆驗證)

1. wire AES-128-ECB (key=트렁크점령전머지, 黃金向量三實作互證)
2. wire LZSS (dist≤1023, len 3-66, 門檻 694 協商)
3. pmFile per-byte 滾動 (keystream FA5387AD/0F3A94AA/48945DCA/1A68DCCF)
4. data.pat 容器 (pmFile→ROL混淆→zlib 1.2.3→CRC自帶表)

## 5. Server 現況

- handlers: 42 個獨立 opcode (19 個經通用轉發器)
- 覆蓋: 登入/大廳/商店(買賣禮)/好友/信箱/任務/戰隊/戰績/
  房間全流程/開戰鏈/戰鬥中繼/查人/場景
- 死協定 ~80 條已定性 (PM 中控/GV 工具/韓版安全) — 無需實作
- 待辦: docs/TODO_HANDLERS.md (照自動序列施工)

## 6. 關鍵互證鏈 (12+ 次資料↔逆向對撞全中)

id基底↔目錄段分佈 / QuestTerm↔sub_9252D0 cond / 8×parts↔8改裝組 /
事件號↔Quest cond21-36 / 驗證段↔稱號·拼圖段 / char_type↔ICT代號 /
hand*.tga↔試衣間 / AI獎勵xml↔919 / 994助攻↔cond36 /
pepachi swf↔701轉輪 / partsability段↔weaponparts組 / 130↔269 elapsed
