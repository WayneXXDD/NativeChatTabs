using FFXIVClientStructs.FFXIV.Component.GUI;
using NativeChatTabs.Core;
namespace NativeChatTabs;

// Convert through the common UI root instead of comparing coordinates belonging
// to different native parents (the close button may be inside a window component).
internal static unsafe class NativeNodeGeometry
{
    public static bool TryRead(AtkResNode* node,AtkResNode* space,out NativeTabLayout.Frame frame,
        out NativeTabLayout.Rect bounds)
    {
        frame=default; bounds=default;
        if(node==null || space==null || node->Width==0 || node->Height==0) return false;
        if(!TryRootFrame(node,out var native,out var root) ||
            !TryRootFrame(space,out var parent,out var parentRoot) || root!=parentRoot) return false;
        frame=native.InSpace(parent);
        bounds=frame.Bounds(node->Width,node->Height);
        return true;
    }
    public static bool IsVisible(AtkResNode* node)
    {
        for(int depth=0;node!=null;depth++,node=node->ParentNode)
            if(depth>=64 || !node->IsVisible()) return false;
        return true;
    }
    private static bool TryRootFrame(AtkResNode* node,out NativeTabLayout.Frame frame,out AtkResNode* root)
    {
        frame=NativeTabLayout.Frame.Identity; root=null;
        for(int depth=0;node!=null;depth++,node=node->ParentNode)
        {
            if(depth>=64 || !float.IsFinite(node->ScaleX) || !float.IsFinite(node->ScaleY) ||
                node->ScaleX==0 || node->ScaleY==0 || MathF.Abs(node->Rotation)>0.001f) return false;
            frame=frame.ThroughParent(node->X,node->Y,node->ScaleX,node->ScaleY,node->OriginX,node->OriginY);
            root=node;
        }
        return root!=null && float.IsFinite(frame.X) && float.IsFinite(frame.Y) &&
            float.IsFinite(frame.ScaleX) && float.IsFinite(frame.ScaleY) &&
            frame.ScaleX!=0 && frame.ScaleY!=0;
    }
}
