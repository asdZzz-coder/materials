# 物料整理

整理部門內物料的 Windows 小程式。每項物料只記：**名稱、分類、規格、數量、單位、備註**。
（不記儲位、料號、經手人、安全庫存、金額。）

- 左側分類（全部物料 / 自訂分類 / 未分類），可新增、重新命名、刪除；刪除分類時物料會移到「未分類」
- 搜尋名稱、規格、單位、備註
- 數量可有小數；清單上數量為 0 會顯示紅色，右鍵可快速 +1 / −1
- Excel 匯出 / 匯入（欄位：名稱、分類、規格、數量、單位、備註）；匯入時名稱與規格相同的會更新
- 淺色 / 深色 / 跟隨系統主題
- 資料存在 `%AppData%\MaterialsKeeper\materials.json`

## 安裝

到 [Releases](https://github.com/asdZzz-coder/materials/releases) 下載 `MaterialsKeeper-ClickOnce.zip`，
解壓縮後執行「安裝.cmd」。已安裝的電腦開啟程式時會自動檢查新版並詢問是否更新。

## 發佈新版

推送 `v*` 標籤（例如 `v1.0.1`），GitHub Actions 會跑測試、用 ClickOnce 打包並建立 Release：

```bash
git tag v1.0.1
git push origin v1.0.1
```

## 開發

```bash
dotnet test materials.Tests/materials.Tests.csproj
```
