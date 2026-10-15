using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Extensions;
using KamiToolKit.Nodes;
using NativeChatTabs.Core;
namespace NativeChatTabs;

// Each cap owns its texture asset. Native nodes, events and parts are never borrowed.
internal sealed unsafe class NativeChatTabCap : ImageNode
{
    private NativeChatTabCap(string path,AtkUldPart* source)
    {
        try {
            AddPart(new Part());
            var part=PartsList[0];
            part->U=source->U; part->V=source->V;
            part->Width=source->Width; part->Height=source->Height;
            part->LoadTexture(path,resolveTheme:false);
            PartId=0;
            NodeFlags=NodeFlags.Visible;
            IsVisible=false;
        } catch { Dispose(); throw; }
    }
    public static NativeChatTabCap? TryCreate(AtkImageNode* source)
    {
        if(source==null || source->PartsList==null || source->PartsList->Parts==null ||
            source->PartId>=source->PartsList->PartCount) return null;
        var part=&source->PartsList->Parts[source->PartId];
        var asset=part->UldAsset;
        if(asset==null || asset->AtkTexture.TextureType!=TextureType.Resource ||
            asset->AtkTexture.Resource==null || asset->AtkTexture.Resource->TexFileResourceHandle==null)
            return null;
        ref var sourcePart=ref *part;
        var path=sourcePart.LoadedPath;
        return string.IsNullOrWhiteSpace(path) ? null : new NativeChatTabCap(path,part);
    }
    public void Match(AtkImageNode* source,NativeTabLayout.Frame frame,NativeTabLayout.Rect bounds,float left)
    {
        Size=new Vector2(source->Width,source->Height);
        Scale=new Vector2(frame.ScaleX,frame.ScaleY);
        Node->SetOrigin(0,0);
        Position=new Vector2(left+frame.X-bounds.Left,frame.Y);
        Node->WrapMode=source->WrapMode; ImageNodeFlags=source->Flags;
        Node->Color=source->Color;
        Node->AddRed=source->AddRed; Node->AddGreen=source->AddGreen; Node->AddBlue=source->AddBlue;
        Node->MultiplyRed=source->MultiplyRed;
        Node->MultiplyGreen=source->MultiplyGreen; Node->MultiplyBlue=source->MultiplyBlue;
        Node->Depth=source->Depth; Node->Priority=source->Priority;
        Node->DrawFlags|=1u;
        IsVisible=true;
    }
}
