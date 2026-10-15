namespace NativeChatTabs.Core;

public static class NativeTabLayout
{
    public readonly record struct Rect(float Left,float Top,float Right,float Bottom)
    {
        public float Width=>Right-Left;
        public float Height=>Bottom-Top;
        public Rect Union(Rect other)=>new(MathF.Min(Left,other.Left),MathF.Min(Top,other.Top),
            MathF.Max(Right,other.Right),MathF.Max(Bottom,other.Bottom));
    }
    public readonly record struct Frame(float X,float Y,float ScaleX,float ScaleY)
    {
        public static Frame Identity=>new(0,0,1,1);
        public Frame ThroughParent(float x,float y,float scaleX,float scaleY,float originX,float originY) =>
            new((X-originX)*scaleX+originX+x,(Y-originY)*scaleY+originY+y,
                ScaleX*scaleX,ScaleY*scaleY);
        public Frame InSpace(Frame space) => new(
            (X-space.X)/NonzeroScale(space.ScaleX),(Y-space.Y)/NonzeroScale(space.ScaleY),
            ScaleX/space.ScaleX,ScaleY/space.ScaleY);
        public Rect Bounds(float width,float height) =>
            new(MathF.Min(X,X+width*ScaleX),MathF.Min(Y,Y+height*ScaleY),
                MathF.Max(X,X+width*ScaleX),MathF.Max(Y,Y+height*ScaleY));
    }
    public readonly record struct DecoratedRow(float LeftCapX,float FirstX,float SecondX,
        float RightCapX,float RightEdge)
    {
        public DecoratedRow OffsetX(float x)=>new(LeftCapX+x,FirstX+x,SecondX+x,RightCapX+x,RightEdge+x);
    }
    private static float NonzeroScale(float scale) =>
        float.IsFinite(scale) && scale!=0 ? scale :
        throw new ArgumentOutOfRangeException(nameof(scale));
    private static float ValidScale(float scale) =>
        float.IsFinite(scale) && scale>0 ? scale :
        throw new ArgumentOutOfRangeException(nameof(scale));
    public static DecoratedRow AppendAfterControls(float protectedRight,float gap,
        float leftCapWidth,float rightCapWidth,float startJoin,float endJoin,
        float tabScale,float firstWidth,float secondWidth,float tabGap)
    {
        tabScale=ValidScale(tabScale);
        var left=protectedRight+gap;
        var first=left+leftCapWidth+startJoin;
        var second=first+firstWidth*tabScale+tabGap;
        var right=second+secondWidth*tabScale+endJoin;
        return new(left,first,second,right,right+rightCapWidth);
    }
    public static bool IsHeaderControl(Rect node,Rect band)
    {
        var center=(node.Top+node.Bottom)*0.5f;
        return node.Width>0 && node.Height>0 && node.Height<=band.Height*2 &&
            center>=band.Top-2 && center<=band.Bottom+2;
    }
    public static float HitRegionWidth(float originalWidth,float currentRight,float desiredRight,float scale) =>
        originalWidth+MathF.Max(0,desiredRight-currentRight)/ValidScale(scale);
    public static float StretchWidth(float sourceWidth,float localScale,float parentDelta) =>
        MathF.Max(1,sourceWidth+parentDelta/ValidScale(localScale));
    public static float TextBoxWidth(float ownerWidth,float left,float right,float textScale) =>
        MathF.Max(1,(ownerWidth-left-right)/ValidScale(textScale));
}
