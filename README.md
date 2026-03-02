# Opta Modbus TCP Multi-Device Demo (WinForms)

本專案提供一個 **WinForms 操作介面 DEMO**，用來展示以下四種裝置的分頁式監看與控制流程：

- AI4（類比輸入）
- DIO4（數位輸入/輸出）
- DO8（數位輸出）
- DI8（數位輸入）

> 目前版本重點是完成 UI 與命令互動流程（頁面/按鈕/命令解析）。

---

## 1. 系統架構概念

- Opta Controller：`192.168.2.100:5000`
- 裝置：
  - AI4：`192.168.2.111`
  - DO8：`192.168.2.112`
  - DIO4：`192.168.2.113`
  - DI8：`192.168.2.114`

WinForms UI 採用四個 Tab 分頁，每頁只放對應裝置功能。

---

## 2. AI4 顯示規則（重要）

### 2.1 固定顯示 8 port

AI4 分頁 **固定顯示 `CH0~CH7`**（8 列）。

### 2.2 Type 與模式

- Single-ended（單端）
  - `0x0101~0x0105`
  - `0x0201~0x0202`
  - 每個 CH 顯示自身值。

- Differential（差動）
  - `0x0106~0x010A`
  - `0x0203`
  - 成對：`CH0-CH1`、`CH2-CH3`、`CH4-CH5`、`CH6-CH7`
  - 差值結果由 **較小編號 CH** 輸出（CH0/CH2/CH4/CH6）
  - 高編號 CH 會標記為 follower / N/A。

---

## 3. UI 功能總覽

### AI4 分頁
- 8 通道資料表（含 Mode/Pair/Owner/Type/Value）
- Type 快捷按鈕（全域與單通道）
- `READ AI4` / `SET AI4 TYPE` 命令

### DIO4 分頁
- DI 狀態
- Counter 讀取與清除
- DO 狀態與 ON/OFF
- Active 讀寫

### DO8 分頁
- CH0~CH7 輸出狀態
- CH ON/OFF
- PowerOn / Active 讀寫

### DI8 分頁
- DI 狀態、Count
- Counter Clear
- Active 讀寫

### 右側 Console
- 可直接輸入命令（Enter 送出）
- 顯示命令與回應紀錄

---

## 4. 支援命令

### 通用
- `HELP`
- `STATUS`

### AI4
- `READ AI4 CH<n>`
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

## 5. 執行方式

1. 以 Visual Studio / Rider 開啟 `OPTA_ModbusDemo.sln`
2. 設定執行平台為 Windows + .NET 8
3. Build & Run
4. 在右側命令列輸入命令，或點擊各分頁快捷按鈕

---

## 6. 備註

- 此版本為 UI/命令流程 DEMO，便於確認操作流程與顯示規則。
- 若要串接真實 Modbus TCP 與 Serial，建議下一步加入：
  - TCP Client / Command Dispatcher 抽象層
  - 裝置通訊重試與連線狀態管理
  - 背景輪詢與 thread-safe UI 更新
