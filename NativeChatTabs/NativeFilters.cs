using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
namespace NativeChatTabs;
// Interop patterns/layout documented by DailyRoutines MoreMessageFilterPresets.
// See THIRD-PARTY-NOTICES.md and the AGPL license.
internal unsafe sealed class NativeFilters : IDisposable
{
    private delegate int ApplyDelegate(nint config);
    private delegate void ChangeTabDelegate(AddonChatLog* addon, int tabIndex);
    private delegate uint WriteFileDelegate(nint config, byte* buffer, uint length);
    private readonly ApplyDelegate apply;
    private readonly Hook<ChangeTabDelegate> changeTab;
    private Hook<WriteFileDelegate>? writeFile;
    private nint boundConfig;
    private bool ownTabChange, serializing;
    private readonly IGameInteropProvider interop;
    private readonly IPluginLog log;
    public int FilterSize { get; }
    public Action? BeforeNativeTabChange { get; set; }
    public Func<ulong, byte[]?>? OriginalFilterForSave { get; set; }

    public NativeFilters(ISigScanner scanner, IGameInteropProvider interop, IPluginLog log)
    {
        this.interop = interop; this.log = log;
        var sizeAddress = scanner.ScanText("FF C5 81 FD ?? ?? ?? ?? 0F 82 ?? ?? ?? ?? 48 8B 0D");
        if (Marshal.ReadByte(sizeAddress, 2) != 0x81 || Marshal.ReadByte(sizeAddress, 3) != 0xFD)
            throw new InvalidOperationException("Unknown native filter-size instruction.");
        FilterSize = Marshal.ReadInt32(sizeAddress, 4);
        if (FilterSize <= 0 || 0x48L + 4L * FilterSize > sizeof(LogFilterConfig))
            throw new InvalidOperationException("Native filter layout is incompatible.");
        apply = Marshal.GetDelegateForFunctionPointer<ApplyDelegate>(scanner.ScanText(
            "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 4C 24 ?? 56 57 41 54 41 56 41 57 48 83 EC ?? 45 33 E4"));
        changeTab = interop.HookFromAddress<ChangeTabDelegate>(
            (nint)AddonChatLog.MemberFunctionPointers.ChangeTab, OnChangeTab);
        changeTab.Enable();
    }
    public ulong OwnerId => LogFilterConfig.Instance() == null ? 0 :
        ((UserFileManager.UserFileEvent*)LogFilterConfig.Instance())->CharacterContentId;
    public void BindSaveProtection()
    {
        var config = LogFilterConfig.Instance();
        if (config == null || OwnerId == 0) throw new InvalidOperationException("Character filters are not loaded.");
        if (boundConfig == (nint)config && writeFile != null) return;
        writeFile?.Dispose(); writeFile = null;
        boundConfig = (nint)config;
        var address = (*(nint**)config)[2];
        if (address == 0) throw new InvalidOperationException("Chat filter save function is unavailable.");
        writeFile = interop.HookFromAddress<WriteFileDelegate>(address, OnWriteFile);
        writeFile.Enable();
    }
    public byte[] Read(int tabIndex)
    {
        var config = LogFilterConfig.Instance();
        if (config == null || OwnerId == 0 || tabIndex is < 0 or > 3)
            throw new InvalidOperationException("Native chat filters are not available.");
        var bytes = new byte[FilterSize];
        Marshal.Copy((nint)config + 0x48 + tabIndex * FilterSize, bytes, 0, bytes.Length);
        return bytes;
    }
    private void WriteMemory(nint config, byte[] bytes)
    {
        if (config == 0 || bytes.Length != FilterSize) throw new InvalidOperationException("Invalid filter snapshot.");
        Marshal.Copy(bytes, 0, config + 0x48 + 3 * FilterSize, bytes.Length);
    }
    public void Apply(byte[] bytes)
    {
        var config = LogFilterConfig.Instance();
        var logs = RaptureLogModule.Instance();
        if (config == null || logs == null || OwnerId == 0) throw new InvalidOperationException("Chat module is unavailable.");
        WriteMemory((nint)config, bytes);
        apply((nint)config);
        logs->ChatTabIsPendingReload[3] = true;
    }
    public void SelectFourth(AddonChatLog* addon)
    {
        if (addon == null) throw new InvalidOperationException("Chat window is unavailable.");
        ownTabChange = true;
        try { addon->ChangeTab(3); }
        finally { ownTabChange = false; }
    }
    private void OnChangeTab(AddonChatLog* addon, int tabIndex)
    {
        try { if (!ownTabChange) BeforeNativeTabChange?.Invoke(); }
        catch (Exception error) { log.Error(error, "Could not restore filters before native tab change."); }
        changeTab.Original(addon, tabIndex);
    }
    private uint OnWriteFile(nint config, byte* buffer, uint length)
    {
        if (config != boundConfig || serializing) return writeFile!.Original(config, buffer, length);
        var owner = ((UserFileManager.UserFileEvent*)config)->CharacterContentId;
        var original = OriginalFilterForSave?.Invoke(owner);
        if (original == null || original.Length != FilterSize) return writeFile!.Original(config, buffer, length);
        var current = new byte[FilterSize];
        Marshal.Copy(config + 0x48 + 3 * FilterSize, current, 0, current.Length);
        serializing = true;
        try { WriteMemory(config, original); return writeFile!.Original(config, buffer, length); }
        finally { WriteMemory(config, current); serializing = false; }
    }
    public void Dispose() { changeTab.Dispose(); writeFile?.Dispose(); }
}
