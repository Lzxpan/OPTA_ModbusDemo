# Opta TCP/IP Multi-Device Demo (WinForms)

本專案是 WinForms DEMO，**只透過 TCP/IP 與 Opta (`192.168.2.100:5000`) 溝通**，由 Opta 端去處理 Modbus。

> 本程式不直接連任何 Modbus 裝置（不使用 port 502）。

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
- 每次輪詢 AI4 時，會同時讀取：
  - `READ AI4 CH<n>`（值與 type）
  - `READ AI4 RAW CH<n>`（raw）
- 對 Opta 的每筆讀寫間隔固定為 **50ms**（輪詢節流）。

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
3. 右側 Console 輸入命令，或用各分頁按鈕操作
4. 可用上方按鈕切換 `開始輪詢 / 停止輪詢`
5. 可用 `連線 OPTA` 按鈕獨立測試與 Opta (`192.168.2.100:5000`) 的連線
6. 上方會即時顯示：
   - `Polling: RUN/STOP`
   - `I/O: READ/WRITE/IDLE/ERR`
   - `CMD`（目前正在執行的命令）
   - `Last Poll` 時間與 `Queue` 長度

### 輪詢機制（最新）

- 輪詢順序固定為：`AI4 -> DO8 -> DIO4 -> DI8`。
- 每個裝置都會依序輪詢各 CH 的數值/狀態。
- 所有讀寫命令與輪詢命令共享同一條 Opta TCP 通道。
- `SET` 指令在輪詢啟用時會先進入佇列，並插入在 `READ` 輪詢流程之間執行。
- 每一筆讀寫間隔固定 50ms，不會同時併發執行。
- `HELP` 會直接轉送給 Opta，並可顯示多行回應內容。

---

## 5. 備註

- 若 Opta 離線或無法連線，讀值/控制命令會回 `ERR OPTA CONNECT FAILED`。
- 本版已移除所有 Modbus client 程式碼，避免 PC 端直接訪問 AI4/DO8/DIO4/DI8。
- 若右側持續出現連線失敗，請先確認 Opta `192.168.2.100:5000` 可達，且 Opta 回應格式為「每行一個命令回應」。
