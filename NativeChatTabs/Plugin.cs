using System.Numerics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using NativeChatTabs.Core;
namespace NativeChatTabs;

public sealed class Plugin : IDalamudPlugin, IAsyncDisposable
{
    private readonly IDalamudPluginInterface pi;
    private readonly IFramework framework;
    private readonly IClientState client;
    private readonly IGameGui gui;
    private readonly IAddonLifecycle lifecycle;
    private readonly ISigScanner scanner;
    private readonly IGameInteropProvider interop;
    private readonly IPluginLog log;
    private readonly ICommandManager commands;
    private readonly Configuration config;
    private readonly RecoveryStore recoveryStore;
    private readonly Task initialization;
    private NativeFilters? filters;
    private NativeTabBar? bar;
    private int active=-1, captureSource=3;
    private bool showSettings, initialized, disposed, stopping;
    private ulong sessionOwner;
    private string status="正在初始化原生介面工具…";
    public Plugin(IDalamudPluginInterface pi, IFramework framework, IClientState client,
        IGameGui gui, IAddonLifecycle lifecycle, ISigScanner scanner,
        IGameInteropProvider interop, IPluginLog log, ICommandManager commands)
    {
        this.pi=pi; this.framework=framework; this.client=client; this.gui=gui;
        this.lifecycle=lifecycle; this.scanner=scanner; this.interop=interop;
        this.log=log; this.commands=commands;
        config=pi.GetPluginConfig() as Configuration ?? new();
        config.Profiles ??= new(); config.Profiles.Normalize();
        recoveryStore=new RecoveryStore(pi.ConfigDirectory.FullName);
        try { config.Recovery=recoveryStore.Read(); }
        catch(Exception error) { log.Error(error,"Recovery backup cannot be read."); config.Enabled=false; }
        commands.AddHandler("/nct",new CommandInfo((_,_)=>showSettings=true)
            { HelpMessage="原生聊天頁籤：設定、啟用及復原。" });
        pi.UiBuilder.Draw+=Draw;
        pi.UiBuilder.OpenConfigUi+=OpenSettings;
        framework.Update+=Tick;
        lifecycle.RegisterListener(AddonEvent.PreSetup,"ConfigLogFilter",BeforeFilterSettings);
        initialization=InitializeAsync();
    }
    private async Task InitializeAsync()
    {
        try
        {
            await KamiToolKitLibrary.InitializeAsync(pi);
            initialized=true;
            await framework.Run(()=> { if(!disposed && config.Enabled) Guard(Activate); else status="已關閉。輸入 /nct 設定並啟用。"; });
        }
        catch(Exception error) { log.Error(error,"Native chat UI initialization failed."); status="初始化失敗："+error.Message; }
    }
    private void OpenSettings()=>showSettings=true;
    private void Save()=>pi.SavePluginConfig(config);
    private void Queue(Action action)=>_=framework.Run(()=>Guard(action));
    private void Guard(Action action)
    {
        try { action(); }
        catch(Exception error)
        {
            if(stopping) { log.Error(error,"Native chat cleanup failed."); return; }
            stopping=true;
            log.Error(error,"Native Chat Tabs stopped.");
            status="已停止："+error.Message;
            config.Enabled=false;
            try { Deactivate(); } catch(Exception restoreError) { log.Error(restoreError,"Native chat restoration needs recovery."); }
            try { Save(); } finally { stopping=false; }
        }
    }
    private unsafe void Activate()
    {
        if(!initialized || disposed) return;
        if(!client.IsLoggedIn) { status="請登入角色後再啟用。"; return; }
        if(filters!=null) return;
        var native=new NativeFilters(scanner,interop,log);
        filters=native;
        native.BeforeNativeTabChange=Restore;
        native.OriginalFilterForSave=owner=>
            config.Recovery is { } recovery && recovery.CharacterId==owner &&
            recovery.IsValidFor(owner,native.FilterSize) ? recovery.OriginalFilterBytes : null;
        try
        {
            native.BindSaveProtection();
            sessionOwner=native.OwnerId;
            RecoverPending();
            bar=new NativeTabBar(i=>config.Profiles.ExtraTabs[i].Name,
                SelectExtraSafely,Restore,()=>active,error=>Guard(()=>throw error));
            bar.Enable();
            status="已啟用。先建立四個原生頁籤；拉寬聊天框以容納額外兩頁。";
        }
        catch { Deactivate(); throw; }
    }
    private unsafe bool SettingsOpen()
    {
        var addon=gui.GetAddonByName<AtkUnitBase>("ConfigLogFilter");
        return addon!=null && addon->IsVisible;
    }
    private unsafe void ValidateSelection()
    {
        if(filters==null || bar==null || !client.IsLoggedIn)
            throw new InvalidOperationException("請先登入並啟用插件。");
        if(!bar.HasFourthAttached()) throw new InvalidOperationException("請先在原生聊天框建立四個頁籤。");
        if(SettingsOpen()) throw new InvalidOperationException("請先關閉遊戲的聊天頻道設定。");
        if(!bar.HasRoom) throw new InvalidOperationException("請拉寬聊天框，讓新增頁籤能顯示。");
        filters.BindSaveProtection();
    }
    private void SelectExtraSafely(int index)
    {
        try
        {
            ValidateSelection();
            if(!config.Profiles.ExtraTabs[index].IsConfigured(filters!.FilterSize))
                throw new InvalidOperationException("請先在 /nct 儲存這個頁籤的頻道設定。");
        }
        catch(InvalidOperationException error)
        { status=error.Message; showSettings=true; return; }
        Guard(()=>SelectExtra(index));
    }
    private unsafe string GetFourthName()
    {
        var logs=RaptureLogModule.Instance();
        if(logs==null) return "Native tab 4";
        var name=logs->GetTabName(3);
        return name==null ? "Native tab 4" : name->ToString();
    }
    private unsafe void SelectExtra(int index)
    {
        ValidateSelection();
        var native=filters!;
        var profile=config.Profiles.ExtraTabs[index];
        if(profile.FilterBytes?.Length!=native.FilterSize)
            throw new InvalidOperationException("先在 /nct 為這個頁籤儲存頻道設定。");
        if(config.Recovery is { } pending && pending.CharacterId!=native.OwnerId)
            throw new InvalidOperationException("另一个角色仍有復原備份；請先切回該角色完成復原。");
        var recovery=config.Recovery ?? new RecoverySnapshot {
            CharacterId=native.OwnerId, FilterSize=native.FilterSize,
            OriginalFilterBytes=native.Read(3),
            OriginalTabName=GetFourthName()
        };
        recovery.LastAppliedFilterBytes=(byte[])profile.FilterBytes.Clone();
        recoveryStore.Write(recovery); // Durable backup precedes any write into the game.
        config.Recovery=recovery;
        active=index;
        try
        {
            native.Apply(profile.FilterBytes);
            native.SelectFourth(bar!.Chat);
            status="目前額外頁籤："+profile.Name;
        }
        catch { Restore(); throw; }
    }
    private void Restore()
    {
        if(active<0 || filters==null) return;
        var recovery=config.Recovery;
        if(recovery!=null && recovery.CharacterId==filters.OwnerId &&
            recovery.IsValidFor(filters.OwnerId,filters.FilterSize))
        {
            filters.Apply(recovery.OriginalFilterBytes);
            recoveryStore.Clear();
            config.Recovery=null;
        }
        active=-1;
    }
    private void RecoverPending()
    {
        if(filters==null || config.Recovery is not { } recovery) return;
        if(recovery.CharacterId!=filters.OwnerId) return;
        if(!recovery.IsValidFor(filters.OwnerId,filters.FilterSize))
            throw new InvalidOperationException("備份來自不同遊戲資料結構，未自動寫入。");
        var current=filters.Read(3);
        if(recovery.CanRestore(filters.OwnerId,filters.FilterSize,current))
            filters.Apply(recovery.OriginalFilterBytes);
        // If the user changed native filters since the crash, preserve their newer choices.
        recoveryStore.Clear(); config.Recovery=null;
    }
    private void Capture(int index)
    {
        Restore();
        if(filters==null) throw new InvalidOperationException("請先啟用插件。");
        config.Profiles.Capture(index,filters.Read(captureSource),filters.FilterSize);
        Save(); status="已儲存 "+config.Profiles.ExtraTabs[index].Name+" 的頻道設定。";
    }
    private void BeforeFilterSettings(AddonEvent _,AddonArgs args)=>Guard(Restore);
    private void Tick(IFramework _)
    {
        if(disposed || !initialized || !config.Enabled) return;
        Guard(()=> {
            if(filters!=null && (!client.IsLoggedIn || filters.OwnerId!=sessionOwner)) Deactivate();
            if(filters==null && client.IsLoggedIn) Activate();
            if(active>=0 && (SettingsOpen() || !client.IsLoggedIn ||
                config.Recovery?.CharacterId!=filters?.OwnerId)) Restore();
        });
    }
    private void Deactivate()
    {
        Restore();
        bar?.Dispose(); bar=null;
        filters?.Dispose(); filters=null;
        active=-1; sessionOwner=0;
    }
    private void Draw()
    {
        if(!showSettings || disposed) return;
        ImGui.SetNextWindowSize(new Vector2(580,380),ImGuiCond.FirstUseEver);
        if(ImGui.Begin("原生聊天頁籤 / Native Chat Tabs",ref showSettings))
        {
            ImGui.TextWrapped("國際服實驗版：在原本聊天框加入第 5、6 頁。內容仍由原生第 4 頁顯示。");
            var enabled=config.Enabled;
            if(ImGui.Checkbox("啟用額外頁籤",ref enabled))
            {
                config.Enabled=enabled; Save();
                Queue(()=> { if(enabled) Activate(); else { Deactivate(); status="已關閉並還原原生頁籤。"; } });
            }
            ImGui.Separator(); ImGui.TextWrapped(status);
            ImGui.TextWrapped("先在遊戲聊天設定中設定頻道，再選來源頁籤並儲存。新增頁籤只切換顯示過濾，不改變發話頻道。");
            string[] choices=["原生第 1 頁","原生第 2 頁","原生第 3 頁","原生第 4 頁"];
            ImGui.Combo("頻道設定來源",ref captureSource,choices,choices.Length);
            for(int i=0;i<config.Profiles.ExtraTabs.Count;i++)
            {
                var index=i; var p=config.Profiles.ExtraTabs[i]; var name=p.Name;
                ImGui.PushID(i);
                if(ImGui.InputText("頁籤名稱",ref name,32))
                { p.Name=name; Save(); }
                ImGui.SameLine();
                if(ImGui.Button("儲存來源頻道")) Queue(()=>Capture(index));
                ImGui.TextUnformatted(p.FilterBytes==null ? "尚未設定頻道" : "已有頻道設定");
                ImGui.PopID();
            }
            if(ImGui.Button("回復原生第 4 頁")) Queue(()=> { Restore(); RecoverPending(); status="原生設定已還原。"; });
            ImGui.TextWrapped("需要四個未分離的原生頁籤及足夠窗寬。首次使用請先確認切換和還原；尚未完成遊戲內測試。");
        }
        ImGui.End();
    }
    private void Unsubscribe()
    {
        commands.RemoveHandler("/nct");
        pi.UiBuilder.Draw-=Draw; pi.UiBuilder.OpenConfigUi-=OpenSettings;
        framework.Update-=Tick; lifecycle.UnregisterListener(BeforeFilterSettings);
    }
    public void Dispose()=>DisposeAsync().AsTask().GetAwaiter().GetResult();
    public async ValueTask DisposeAsync()
    {
        if(disposed) return;
        disposed=true; Unsubscribe();
        try { await initialization; } catch { }
        await framework.RunOnFrameworkThread(()=> {
            Deactivate();
            if(initialized) KamiToolKitLibrary.Dispose();
        });
    }
}
