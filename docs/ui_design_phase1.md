# Opta Modbus TCP Multi-Device Demo - UI 第五版（AI4 顯示規則定稿）設計分析

> 依最新確認：AI4 畫面必須顯示 `CH0~CH7` 共 8 個 port。僅在設為差動類型（`0x0106~0x010A`、`0x0203`）時，才以兩 port 一組計算；且差動結果由**較小編號 port**對應輸出（例如 CH0-CH1 差值輸出歸 CH0）。

## 1) AI4 顯示/輸出規則（定稿）
- **固定顯示 8 個通道**：`CH0~CH7` 一律在 UI 上可見。
- **一般模式（單端）**
  - Type：`0x0101~0x0105`、`0x0201~0x0202`
  - 每個 CH 直接顯示自身量測值。
- **差動模式（雙端差值）**
  - Type：`0x0106~0x010A`、`0x0203`
  - 成對組合：`CH0-CH1`、`CH2-CH3`、`CH4-CH5`、`CH6-CH7`
  - 差動輸出歸屬：由**較小編號**通道輸出（`CH0/CH2/CH4/CH6`）
  - 對應較大編號通道（`CH1/CH3/CH5/CH7`）在 UI 標記為「Pair follower / N/A」。

## 2) AI4 分頁資訊架構（更新）
1. **模式提示卡**
   - 顯示目前 Type 是否為單端或差動。
2. **8 通道表格（固定 8 列）**
   - 欄位：`CH`、`Port`、`Mode`、`Pair`、`Output Owner`、`Raw/Calc`、`Value`
   - 差動時：同組兩列都保留，但只有低編號列有有效輸出值。
3. **Type 控制區**
   - `SET AI4 TYPE <code>`、`SET AI4 CH<n> TYPE <code>`
   - 提示文字：切換至差動 type 後，輸出值寫在低編號通道。

## 3) 其他分頁（維持）
- **DIO4**：DI/DO/Counter/Clear/Active
- **DO8**：CH ON/OFF + POWERON/ACTIVE
- **DI8**：CH/COUNT/CLEAR/ACTIVE

## 4) 落地建議（待確認）
- AI4 Grid 使用固定 8 筆資料模型，不再切換 8/4 筆模式。
- 差動計算結果寫入 low channel row，high channel row 顯示 `N/A (paired with CHx)`。
- 欄位色彩區分 owner/follower，避免誤操作。

## 5) 本階段狀態
- ✅ 已完成：UI 第五版設計稿（8 通道固定顯示 + 差動輸出歸屬規則）。
- ❌ 尚未進行：`Form1.Designer.cs` / `Form1.cs` 實作。
