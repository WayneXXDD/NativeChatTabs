using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Classes;
using KamiToolKit.Extensions;
using KamiToolKit.Nodes;
using Lumina.Data.Parsing.Uld;
namespace NativeChatTabs;

// Copies appearance values, never native texture/node ownership.
internal unsafe sealed class NativeChatTabButton : ButtonBase
{
    private VisualState? normal, selected;
    private bool isSelected;
    private string name="";
    private NativeChatTabButton(StateSnapshot idle,StateSnapshot chosen)
    {
        try {
            normal=Build(idle); selected=Build(chosen);
            Data->Nodes[0]=normal.Label.NodeId;
            Data->Nodes[1]=normal.Sprites[0].Node.NodeId;
            InitializeComponentEvents();
            Size=idle.OwnerSize; ApplySelection();
        } catch { Dispose(); throw; }
    }
    public static NativeChatTabButton? TryCreate(AtkComponentTab* idle,AtkComponentTab* chosen)
    {
        if(idle==null || chosen==null || idle==chosen) return null;
        StateSnapshot idleStyle,chosenStyle;
        try { idleStyle=Capture(idle); chosenStyle=Capture(chosen); }
        catch(InvalidOperationException) { return null; }
        return new NativeChatTabButton(idleStyle,chosenStyle);
    }
    public string Name {
        get=>name;
        set {
            if(name==value) return;
            name=value;
            if(normal!=null) normal.Label.String=value;
            if(selected!=null) selected.Label.String=value;
        }
    }
    public bool Selected {
        get=>isSelected;
        set { if(isSelected==value) return; isSelected=value; ApplySelection(); }
    }
    public float ContentWidth=>MathF.Ceiling(MathF.Max(Measure(normal),Measure(selected)));
    private static float Measure(VisualState? state)=>state==null ? 0 :
        state.Label.GetTextDrawSize(considerScale:false).X+state.LeftPadding+state.RightPadding;
    protected override void OnSizeChanged() {
        base.OnSizeChanged(); Layout(normal); Layout(selected);
    }
    private void ApplySelection() {
        if(normal==null || selected==null) return;
        foreach(var sprite in normal.Sprites) sprite.Node.IsVisible=!isSelected;
        foreach(var sprite in selected.Sprites) sprite.Node.IsVisible=isSelected;
        normal.Label.IsVisible=!isSelected; selected.Label.IsVisible=isSelected;
        ApplyStyle((AtkResNode*)Node,isSelected?selected.OwnerStyle:normal.OwnerStyle);
    }
    private void Layout(VisualState? state) {
        if(state==null) return;
        var dx=Width-state.OwnerSize.X; var dy=Height-state.OwnerSize.Y;
        foreach(var sprite in state.Sprites) {
            var position=sprite.Position; var size=sprite.Size;
            if(size.X>=state.OwnerSize.X*0.5f) size.X=MathF.Max(1,size.X+dx);
            else if(position.X>=state.OwnerSize.X*0.5f) position.X+=dx;
            if(size.Y>=state.OwnerSize.Y*0.5f) size.Y=MathF.Max(1,size.Y+dy);
            sprite.Node.Position=position; sprite.Node.Size=size;
        }
        state.Label.Position=state.LabelPosition;
        state.Label.Size=new Vector2(MathF.Max(1,Width-state.LeftPadding-state.RightPadding),state.LabelSize.Y);
    }
    private VisualState Build(StateSnapshot snapshot) {
        var sprites=new List<Sprite>();
        foreach(var source in snapshot.Sprites) {
            NodeBase visual;
            if(source.Type==NodeType.NineGrid) {
                var grid=new NineGridNode(); grid.AttachNode(this); grid.AddPart(new Part());
                LoadPart(grid.PartsList[0],source.Part); grid.PartId=0;
                grid.Offsets=source.Offsets; grid.BlendMode=source.BlendMode;
                grid.PartsRenderType=source.PartsRenderType; visual=grid;
            } else {
                var image=new ImageNode(); image.AttachNode(this); image.AddPart(new Part());
                LoadPart(image.PartsList[0],source.Part); image.PartId=0;
                image.Node->WrapMode=source.WrapMode; image.ImageNodeFlags=source.ImageFlags;
                visual=image;
            }
            visual.Position=source.Style.Position; visual.Size=source.Style.Size;
            ApplyStyle((AtkResNode*)visual,source.Style);
            sprites.Add(new(visual,source.Style.Position,source.Style.Size));
        }
        var label=new TextNode(); label.AttachNode(this);
        label.FontType=(FontType)(snapshot.Text.AlignmentFontType>>4);
        label.AlignmentType=(AlignmentType)(snapshot.Text.AlignmentFontType&0xF);
        label.FontSize=snapshot.Text.FontSize; label.LineSpacing=snapshot.Text.LineSpacing;
        label.CharSpacing=snapshot.Text.CharSpacing;
        label.TextFlags=snapshot.Text.Flags&~(TextFlags.FontCache|TextFlags.LinkData);
        label.Node->TextColor=snapshot.Text.TextColor; label.Node->EdgeColor=snapshot.Text.EdgeColor;
        label.Node->BackgroundColor=snapshot.Text.BackgroundColor;
        label.Position=snapshot.Text.Style.Position; label.Size=snapshot.Text.Style.Size;
        ApplyStyle((AtkResNode*)label.Node,snapshot.Text.Style);
        var left=MathF.Max(0,snapshot.Text.Style.Position.X);
        var right=MathF.Max(0,snapshot.OwnerSize.X-left-snapshot.Text.Style.Size.X);
        return new(snapshot.OwnerSize,snapshot.OwnerStyle,sprites,label,
            snapshot.Text.Style.Position,snapshot.Text.Style.Size,left,right);
    }
    private static void LoadPart(AtkUldPart* destination,PartSnapshot source) {
        if(destination==null) throw new InvalidOperationException("無法建立聊天頁籤材質。");
        destination->U=source.U; destination->V=source.V;
        destination->Width=source.Width; destination->Height=source.Height;
        destination->LoadTexture(source.Path,resolveTheme:false);
    }
    private static StateSnapshot Capture(AtkComponentTab* tab) {
        var owner=tab->OwnerNode; var text=tab->ButtonTextNode; var manager=&tab->UldManager;
        if(owner==null || text==null || manager->NodeList==null ||
            manager->NodeListCount==0 || manager->NodeListCount>512 ||
            owner->Width==0 || owner->Height==0)
            throw new InvalidOperationException("原生聊天頁籤尚未就緒。");
        var sprites=new List<SpriteSnapshot>();
        for(var i=0;i<manager->NodeListCount;i++) {
            var node=manager->NodeList[i];
            if(node==null || (node->Type!=NodeType.Image && node->Type!=NodeType.NineGrid) ||
                !VisibleWithinOwner(node,(AtkResNode*)owner) || node->Width==0 || node->Height==0) continue;
            if(node->Width<=16 && node->Height<=16) continue; // Do not copy unread indicators.
            var style=CaptureStyle(node,RelativePosition(node,(AtkResNode*)owner));
            if(node->Type==NodeType.NineGrid) {
                var grid=(AtkNineGridNode*)node;
                sprites.Add(new(NodeType.NineGrid,style,CapturePart(grid->PartsList,grid->PartId),
                    new Vector4(grid->TopOffset,grid->BottomOffset,grid->LeftOffset,grid->RightOffset),
                    grid->BlendMode,grid->PartsTypeRenderType,0,0));
            } else {
                var image=(AtkImageNode*)node;
                sprites.Add(new(NodeType.Image,style,CapturePart(image->PartsList,image->PartId),
                    Vector4.Zero,0,0,image->WrapMode,image->Flags));
            }
        }
        if(sprites.Count==0) throw new InvalidOperationException("等待原生聊天頁籤外觀載入。");
        return new(new Vector2(owner->Width,owner->Height),
            CaptureStyle((AtkResNode*)owner,Vector2.Zero),sprites,
            new(CaptureStyle((AtkResNode*)text,RelativePosition((AtkResNode*)text,(AtkResNode*)owner)),
                text->AlignmentFontType,text->FontSize,text->LineSpacing,text->CharSpacing,
                text->TextFlags,text->TextColor,text->EdgeColor,text->BackgroundColor));
    }
    private static PartSnapshot CapturePart(AtkUldPartsList* list,uint id) {
        if(list==null || list->Parts==null || id>=list->PartCount)
            throw new InvalidOperationException("原生頁籤材質尚未載入。");
        ref var part=ref list->Parts[id]; var asset=part.UldAsset;
        if(asset==null || asset->AtkTexture.TextureType!=TextureType.Resource ||
            asset->AtkTexture.Resource==null || asset->AtkTexture.Resource->TexFileResourceHandle==null)
            throw new InvalidOperationException("原生頁籤材質尚未载入。");
        var path=part.LoadedPath;
        if(string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("等待原生頁籤材質。");
        return new(path,part.U,part.V,part.Width,part.Height);
    }
    private static bool VisibleWithinOwner(AtkResNode* node,AtkResNode* owner) {
        for(int depth=0;node!=null && node!=owner;depth++) {
            if(depth>=32 || !node->IsVisible()) return false;
            node=node->ParentNode;
        }
        return true;
    }
    private static Vector2 RelativePosition(AtkResNode* node,AtkResNode* owner) {
        var result=new Vector2(node->X,node->Y); var parent=node->ParentNode;
        for(int depth=0;parent!=null && parent!=owner;depth++) {
            if(depth>=32 || MathF.Abs(parent->Rotation)>0.001f ||
                MathF.Abs(parent->ScaleX-1)>0.001f || MathF.Abs(parent->ScaleY-1)>0.001f)
                throw new InvalidOperationException("不支援的原生頁籤內部座標。");
            result+=new Vector2(parent->X,parent->Y); parent=parent->ParentNode;
        }
        return result;
    }
    private static NodeStyle CaptureStyle(AtkResNode* n,Vector2 position)=>new(position,
        new Vector2(n->Width,n->Height),new Vector2(n->ScaleX,n->ScaleY),n->Rotation,
        new Vector2(n->OriginX,n->OriginY),n->Color,n->AddRed,n->AddGreen,n->AddBlue,
        n->MultiplyRed,n->MultiplyGreen,n->MultiplyBlue,n->Depth,n->Priority);
    private static void ApplyStyle(AtkResNode* n,NodeStyle s) {
        n->ScaleX=s.Scale.X; n->ScaleY=s.Scale.Y; n->Rotation=s.Rotation;
        n->OriginX=s.Origin.X; n->OriginY=s.Origin.Y; n->Color=s.Color;
        n->AddRed=s.AddRed; n->AddGreen=s.AddGreen; n->AddBlue=s.AddBlue;
        n->MultiplyRed=s.MultiplyRed; n->MultiplyGreen=s.MultiplyGreen; n->MultiplyBlue=s.MultiplyBlue;
        n->Depth=s.Depth; n->Priority=s.Priority; n->DrawFlags|=1u;
    }
    private sealed record VisualState(Vector2 OwnerSize,NodeStyle OwnerStyle,List<Sprite> Sprites,
        TextNode Label,Vector2 LabelPosition,Vector2 LabelSize,float LeftPadding,float RightPadding);
    private sealed record Sprite(NodeBase Node,Vector2 Position,Vector2 Size);
    private sealed record StateSnapshot(Vector2 OwnerSize,NodeStyle OwnerStyle,List<SpriteSnapshot> Sprites,TextSnapshot Text);
    private sealed record SpriteSnapshot(NodeType Type,NodeStyle Style,PartSnapshot Part,Vector4 Offsets,
        uint BlendMode,byte PartsRenderType,byte WrapMode,ImageNodeFlags ImageFlags);
    private readonly record struct PartSnapshot(string Path,ushort U,ushort V,ushort Width,ushort Height);
    private readonly record struct TextSnapshot(NodeStyle Style,byte AlignmentFontType,byte FontSize,
        byte LineSpacing,byte CharSpacing,TextFlags Flags,ByteColor TextColor,ByteColor EdgeColor,ByteColor BackgroundColor);
    private readonly record struct NodeStyle(Vector2 Position,Vector2 Size,Vector2 Scale,float Rotation,
        Vector2 Origin,ByteColor Color,short AddRed,short AddGreen,short AddBlue,
        byte MultiplyRed,byte MultiplyGreen,byte MultiplyBlue,float Depth,ushort Priority);
}
