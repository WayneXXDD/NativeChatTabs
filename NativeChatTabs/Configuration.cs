using Dalamud.Configuration;
using NativeChatTabs.Core;
namespace NativeChatTabs;
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool Enabled { get; set; }
    public TabProfiles Profiles { get; set; } = new();
    public TabPlacement Placement { get; set; } = new();
    public RecoverySnapshot? Recovery { get; set; }
}
