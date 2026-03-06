# Opta TCP/IP Multi-Device Demo (WinForms)

本專案是 WinForms DEMO，**只透過 TCP/IP 與 Opta (`192.168.2.100:5000`) 溝通**，由 Opta 端去處理 Modbus。

> 本程式不直接連任何 Modbus 裝置（不使用 port 502）。

> ⚠️ 協作規範：本專案在 Issue、PR、Commit 訊息與所有回覆說明，**統一使用繁體中文**。

---

## 1. 架構說明

- WinForms App（本專案）
  - TCP Client -> `192.168.2.100:5000`（Opta 指令通道）
- Opta
  - 接收文字命令
  - 在 Opta 內部與 AI4/DO8/DIO4/DI8 做 Modbus 通訊

---

## 2. AI4 顯示規則（依最新需求）

- AI4 固定顯示 `CH0~CH7`。
- UI 不做量測值換算，**直接顯示從 Opta 回傳的值**。
- 每次輪詢 AI4 時只讀取 `READ AI4 CH<n>`（回應內已含值與 type），不再額外讀 `RAW`。
- 對 Opta 的每筆讀寫在「收到回應後」再等待 **50ms**（輪詢節流）。

---

## 3. 指令列表（完整）

可先送 `HELP`。

### 通用
- `HELP`

### AI4
- `READ AI4 CH<n>`
- `READ AI4 RAW CH<n>`
- `READ AI4 ALL`
- `SET AI4 TYPE <code>`
- `SET AI4 CH<n> TYPE <code>`

### DO8
- `READ DO8 CH<n>`
- `SET DO8 CH<n> ON|OFF`
- `READ DO8 POWERON`
- `SET DO8 POWERON <value>`
- `READ DO8 ACTIVE`
- `SET DO8 ACTIVE <value>`

### DIO4
- `READ DIO4 DI<n>`
- `READ DIO4 COUNT CH<n>`
- `SET DIO4 CLEAR CH<n>`
- `READ DIO4 ACTIVE`
- `SET DIO4 ACTIVE <value>`
- `READ DIO4 DO<n>`
- `SET DIO4 DO<n> ON|OFF`

### DI8
- `READ DI8 CH<n>`
- `READ DI8 COUNT CH<n>`
- `SET DI8 CLEAR CH<n>`
- `READ DI8 ACTIVE`
- `SET DI8 ACTIVE <value>`

---

## 4. 執行方式

1. 開啟 `OPTA_ModbusDemo.sln`
2. Build & Run（Windows / .NET 8）
   - 啟動後預設為「未連線」且「輪詢停止」，需手動按 `連線 OPTA` 與 `開始輪詢`。
3. 右側 Console 輸入命令，或用各分頁按鈕操作
4. 輪詢預設為停止，可用上方按鈕切換 `開始輪詢 / 停止輪詢`
5. 可用 `連線 OPTA` 按鈕進行連線，再按一次可斷線
6. 上方會即時顯示：
   - `Polling: RUN/STOP`
   - `I/O: READ/WRITE/IDLE/ERR`
   - `CMD`（目前正在執行的命令）
   - `Last Poll` 時間與 `Queue` 長度
- Console 會輸出 `DIAG` 診斷訊息（例如封包是否收到 `<br>`、是否偵測到伺服器重連提示、斷線主因摘要）

### 輪詢機制（最新）

- 輪詢順序固定為：`AI4 -> DO8 -> DIO4 -> DI8`。
- 輪詢觸發間隔為 250ms。
- 每個裝置都會依序輪詢各 CH 的數值/狀態。
- 輪詢在背景執行序執行，UI 只負責顯示更新，避免畫面被輪詢卡住。
- UI 會隨每筆讀值嘗試即時更新；為避免 UI 訊息佇列塞車，更新節流為最小約 100ms 一次。
- 所有讀寫命令與輪詢命令共享同一條 Opta TCP 通道。
- `SET` 指令在輪詢啟用時會先進入佇列，並插入在 `READ` 輪詢流程之間執行。
- 每一筆讀寫會在收到回應後等待 50ms，不會同時併發執行。
- `HELP` 會直接轉送給 Opta，並可顯示多行回應內容。
- 與 Opta 採「持續連線」模式，可用 `連線 OPTA` 按鈕手動重連。
- 回應收包以 `<br>` / `<br/>` 作為封包結束，不再依賴逾時視窗切割訊息。
- `OK` 開頭且命令簽章匹配（裝置/通道/關鍵字）才會採用；`ERR` 開頭回應會直接放棄該筆資料。
- 回應對齊機制會過濾 greeting/prompt 雜訊，降低「上一筆殘留回應跑到下一筆」的錯位風險。

---

## 5. 備註

- 若 Opta 離線或無法連線，讀值/控制命令會回 `ERR OPTA CONNECT FAILED`。
- 本版已移除所有 Modbus client 程式碼，避免 PC 端直接訪問 AI4/DO8/DIO4/DI8。
- 若右側持續出現連線失敗，請先確認 Opta `192.168.2.100:5000` 可達，且 Opta 回應格式為「每行一個命令回應」。
