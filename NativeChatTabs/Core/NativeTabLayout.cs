namespace NativeChatTabs.Core;

// Coordinates are in the native tabs' shared parent space. Widths remain local
// to each button, so a HUD scale must be applied before advancing to its sibling.
public static class NativeTabLayout
{
    public readonly record struct Row(float FirstX,float SecondX,float AddedWidth);
    private static float ValidScale(float scale) =>
        float.IsFinite(scale) && scale>0 ? scale :
        throw new ArgumentOutOfRangeException(nameof(scale));
    public static Row Continue(float anchorX,float anchorWidth,float scaleX,
        float previousRight,float firstWidth,float secondWidth)
    {
        scaleX=ValidScale(scaleX);
        var gap=Math.Clamp(anchorX-previousRight,-2,4);
        var firstX=anchorX+anchorWidth*scaleX+gap;
        return new(firstX,firstX+firstWidth*scaleX+gap,
            (firstWidth+secondWidth)*scaleX+2*gap);
    }
    public static float StretchWidth(float sourceWidth,float localScale,float parentDelta) =>
        MathF.Max(1,sourceWidth+parentDelta/ValidScale(localScale));
    public static float TextBoxWidth(float ownerWidth,float left,float right,float textScale) =>
        MathF.Max(1,(ownerWidth-left-right)/ValidScale(textScale));
}
