# Native Chat Tabs 0.1.0 — 國際服原生聊天頁籤測試版

這個版本在原本 ChatLog 的頁籤列加入兩個遊戲原生 UI 按鈕。聊天內容、輸入框、物品／玩家連結與訊息捲動仍由遊戲顯示。插件沒有另一個聊天視窗。

第 5、6 頁是額外的頻道過濾設定，借用第 4 原生訊息區。原生資料容量並沒有被改成六份；新頁籤沒有獨立發話頻道、獨立捲動位置或獨立永久聊天記錄。

**已編譯與離線測試，不代表遊戲內驗證成功。預設停用。請先測試原生第 4 頁設定能正常還原。**

## 安裝測試版

在 Dalamud 自訂插件庫加入（若已加入，不必重複）：

    https://raw.githubusercontent.com/WayneXXDD/DalamudPlugins/main/pluginmaster.json

在 /xlsettings → Experimental 啟用 Get plugin testing builds。開啟 /xlplugins，重新整理清單，搜尋 Native Chat Tabs，安裝 Native Chat Tabs (Wayne, Testing)。輸入 /nct 設定並啟用；插件預設關閉。

此插件以測試版專用項目發布，尚未完成遊戲內驗證。

## 設定第 5、6 頁

1. 先在遊戲原本聊天框建立四個頁籤；第 4 頁必須合在主聊天框內。
2. 在遊戲原本的聊天設定中，將某個原生頁籤設定成想要的頻道，例如小隊或密語。
3. 開啟 /nct，選擇「頻道設定來源」，為第 5 頁按「儲存來源頻道」，並設定名稱。
4. 用相同方式儲存第 6 頁。完成後可把原生來源頁籤的頻道設定改回原本用途，額外頁籤仍保存先前設定。
5. 拉寬聊天框，讓新增頁籤能顯示，再點原生頁籤列後方的 Tab 5／Tab 6。

發話頻道仍依原本輸入框與 /p、/fc、/tell 指令。額外頁籤只控制訊息顯示。

## 遊戲內驗證

- 記下原生第 4 頁頻道過濾設定。
- 點第 5、6 頁，確認過去訊息、即時訊息及可點擊連結正常。
- 點回原生第 4 頁，確認它恢復原本的過濾設定。
- 開啟遊戲的頻道設定，確認額外頁籤退出且不覆蓋編輯。
- 調整窗寬與 HUD 縮放，確認按鈕仍可點、沒有重疊。
- 在 /nct 關閉，再卸載插件；確認第 4 頁與原生按鈕位置恢復。
- 重登同一角色確認原生設定；之後再測試切換角色。

任何一項不正常，先用 /nct「回復原生第 4 頁」並關閉插件，把錯誤訊息貼回此對話。

## 還原機制

切換到額外頁籤前，在插件設定目錄寫入原生第 4 頁的復原備份。正常切回、開啟頻道設定或卸載时還原；備份只適用相同角色與資料尺寸。異常中斷後只在目前資料符合最後套用的額外設定時自動還原，避免覆蓋之後手動修改的原生設定。

原生設定儲存攔截會在序列化時提供原始設定，完成後恢復暫時設定。此攔截與原生 UI 行為仍需要實機驗證。

## 編譯

.NET 10 SDK，以及國際服 Dalamud API 15 的 dev DLL。

先取得原始碼及固定版本的 KamiToolKit 子模組：

    git clone --recurse-submodules https://github.com/WayneXXDD/NativeChatTabs.git
    cd NativeChatTabs

執行：

    dotnet build NativeChatTabs/NativeChatTabs.csproj -c Release

如 DLL 不在預設位置：

    dotnet build NativeChatTabs/NativeChatTabs.csproj -c Release -p:DalamudLibPath=C:/your/dalamud/dev/

離線核心測試：

    dotnet run --project NativeChatTabs/tests/NativeChatTabs.Core.Tests.csproj -c Release

## 授權

Native Chat Tabs 採 AGPL-3.0-or-later，相應原始碼位於本倉庫。KamiToolKit 採 MIT；FFXIVClientStructs 由 Dalamud 提供。參考來源與固定提交記錄在 THIRD-PARTY-NOTICES.md。
