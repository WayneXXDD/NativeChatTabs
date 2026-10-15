namespace NativeChatTabs.Core;

// Missing fields in older configurations use the extra clearance by default.
public sealed class TabPlacement
{
    public const int DefaultHorizontalOffset=24;
    private int horizontalOffset=DefaultHorizontalOffset;
    private int verticalOffset;
    public int HorizontalOffset {
        get=>horizontalOffset;
        set=>horizontalOffset=Math.Clamp(value,-4096,4096);
    }
    public int VerticalOffset {
        get=>verticalOffset;
        set=>verticalOffset=Math.Clamp(value,-4096,4096);
    }
    public void Reset() {
        horizontalOffset=DefaultHorizontalOffset;
        verticalOffset=0;
    }
}
