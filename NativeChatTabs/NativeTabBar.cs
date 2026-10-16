using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using NativeChatTabs.Core;
namespace NativeChatTabs;
internal unsafe sealed class NativeTabBar : IDisposable
{
    private readonly AddonController<AddonChatLog> controller;
    private readonly Func<int,string> getName;
    private readonly Action<int> select;
    private readonly Action restore;
    private readonly Action<Exception> failed;
    private readonly Func<int> active;
    private readonly Func<Vector2> getOffset;
    private readonly Action openSettings;
    private readonly List<NativeChatTabButton> buttons=[];
    private NativeChatTabCap? leftCap,rightCap;
    private CircleButtonNode? settingsButton;
    private AtkCollisionNode* widenedCollision;
    private Vector2 originalCollisionPosition,originalCollisionSize;
    private AddonChatLog* boundChat;
    public bool IsReady { get; private set; }
    public string? LayoutHint { get; private set; }
    public AddonChatLog* Chat=>boundChat;
    public NativeTabBar(Func<int,string> getName,Action<int> select,Action restore,
        Func<int> active,Func<Vector2> getOffset,Action openSettings,Action<Exception> failed)
    {
        this.getName=getName; this.select=select; this.restore=restore; this.active=active; this.getOffset=getOffset; this.openSettings=openSettings; this.failed=failed;
        controller=new() { AddonName="ChatLog",OnSetup=Setup,OnPreUpdate=BeforeUpdate,OnUpdate=Update,OnFinalize=Finalize };
    }
    public void Enable()=>controller.Enable();
    public bool HasFourthAttached()
    {
        var chat=Chat;
        return chat!=null && chat->TabCount>=4 && chat->ChatTabs[3].Value!=null &&
            chat->ChatTabs[3].Value->OwnerNode!=null &&
            chat->ChatTabs[3].Value->OwnerNode->IsVisible();
    }
    private void Setup(AddonChatLog* chat)=>boundChat=chat;
    private void CreateButtons(AddonChatLog* chat)
    {
        if(buttons.Count!=0) return;
        var anchor=chat->ChatTabs[3].Value;
        if(anchor==null || anchor->OwnerNode==null || anchor->OwnerNode->ParentNode==null ||
            chat->TabCount<4 || chat->TabIndex>=4) return;
        var chosen=chat->ChatTabs[chat->TabIndex].Value;
        if(chosen==null || chosen->OwnerNode==null || !chosen->OwnerNode->IsVisible()) return;
        if(active()<0 && !chosen->IsChecked) { chosen->SetChecked(true); return; }
        AtkComponentTab* idle=null;
        for(int j=0;j<4;j++)
            if(j!=chat->TabIndex && chat->ChatTabs[j].Value!=null &&
                chat->ChatTabs[j].Value->OwnerNode!=null &&
                chat->ChatTabs[j].Value->OwnerNode->IsVisible())
            { idle=chat->ChatTabs[j].Value; break; }
        if(idle==null) return;
        leftCap=NativeChatTabCap.TryCreate(chat->TabBarStartImageNode);
        rightCap=NativeChatTabCap.TryCreate(chat->TabBarEndImageNode);
        if(leftCap==null || rightCap==null) {
            DisposeVisuals();
            LayoutHint="等待原生頁籤的左右端帽載入。";
            return;
        }
        AtkResNode* after=(AtkResNode*)anchor->OwnerNode;
        leftCap.AttachNode(after,NodePosition.AfterTarget); after=(AtkResNode*)leftCap.Node;
        for(int i=0;i<2;i++)
        {
            int index=i;
            var button=NativeChatTabButton.TryCreate(idle,chosen);
            if(button==null) { DisposeVisuals(); return; }
            button.Name=getName(i); button.IsVisible=false;
            button.OnClick=()=> { try { select(index); } catch(Exception error) { failed(error); } };
            buttons.Add(button);
            button.AttachNode(after,NodePosition.AfterTarget); after=(AtkResNode*)button.Node;
        }
        rightCap.AttachNode(after,NodePosition.AfterTarget);
        settingsButton=new CircleButtonNode {
            Icon=CircleButtonIcon.GearCog,
            Size=new Vector2(28,28),
            IsVisible=false,
            TextTooltip="Native Chat Tabs settings (/nct)",
            OnClick=()=> { try { openSettings(); } catch(Exception error) { failed(error); } }
        };
        settingsButton.AttachNode((AtkResNode*)rightCap.Node,NodePosition.AfterTarget);
    }
    private bool IsExtraButton(AtkResNode* node)
    {
        if(settingsButton!=null && (AtkResNode*)settingsButton.Node==node) return true;
        foreach(var button in buttons) if((AtkResNode*)button.Node==node) return true;
        return false;
    }
    private static float IncludeControl(AtkResNode* node,AtkResNode* space,float right)
    {
        return node!=null && NativeNodeGeometry.IsVisible(node) &&
            NativeNodeGeometry.TryRead(node,space,out _,out var bounds)
            ? MathF.Max(right,bounds.Right) : right;
    }
    private float NativeControlsRight(AddonChatLog* chat,AtkResNode* space,
        NativeTabLayout.Rect band,float right)
    {
        right=IncludeControl((AtkResNode*)chat->AddTabComponentNode,space,right);
        right=IncludeControl((AtkResNode*)chat->SettingsComponentNode,space,right);
        right=IncludeControl((AtkResNode*)chat->NoviceNetworkComponentNode,space,right);
        if(chat->UldManager.NodeList==null)
            throw new InvalidOperationException("原生聊天控制按鈕尚未就緒。");
        return ScanControls(&chat->UldManager,space,band,right,0);
    }
    private float ScanControls(AtkUldManager* manager,AtkResNode* space,
        NativeTabLayout.Rect band,float right,int depth)
    {
        if(depth>=8) return right;
        if(manager->NodeList==null || manager->NodeListCount==0) return right;
        if(manager->NodeListCount>4096)
            throw new InvalidOperationException("原生聊天控制按鈕尚未就緒。");
        // A close button may live inside the native window component. Scan its
        // children too, without treating the entire window as an occupied button.
        for(int i=0;i<manager->NodeListCount;i++)
        {
            var node=manager->NodeList[i];
            if(node==null || (ushort)node->Type<(ushort)NodeType.Component || IsExtraButton(node) ||
                !NativeNodeGeometry.IsVisible(node)) continue;
            var component=((AtkComponentNode*)node)->Component;
            if(component==null) continue;
            var type=component->GetComponentType();
            if(type is ComponentType.Button or ComponentType.CheckBox or ComponentType.RadioButton or
                    ComponentType.Tab or ComponentType.HoldButton)
                if(NativeNodeGeometry.TryRead(node,space,out _,out var bounds) &&
                    NativeTabLayout.IsHeaderControl(bounds,band))
                    right=MathF.Max(right,bounds.Right);
            right=ScanControls(&component->UldManager,space,band,right,depth+1);
        }
        return right;
    }
    private static NativeTabLayout.Rect SettingsBounds(AddonChatLog* chat,AtkResNode* space,
        NativeTabLayout.Rect band,float left,float offsetY,Vector2 fallbackSize)
    {
        var size=fallbackSize;
        var top=band.Top+(band.Height-size.Y)*0.5f;
        var native=chat->SettingsComponentNode;
        if(native!=null && NativeNodeGeometry.TryRead((AtkResNode*)native,space,out _,out var ownerBounds))
        {
            size=new Vector2(ownerBounds.Width,ownerBounds.Height);
            top=ownerBounds.Top;
            var component=native->Component;
            if(component!=null && component->GetComponentType()==ComponentType.Button)
            {
                var background=((AtkComponentButton*)component)->ButtonBGNode;
                if(background!=null &&
                    NativeNodeGeometry.TryRead(background,space,out _,out var imageBounds) &&
                    imageBounds.Width<=ownerBounds.Width*2 && imageBounds.Height<=ownerBounds.Height*2)
                {
                    // The native image can be larger than the component's hit box.
                    size=new Vector2(imageBounds.Width,imageBounds.Height);
                    top=ownerBounds.Top+(ownerBounds.Height-size.Y)*0.5f;
                }
            }
        }
        return new(left,top+offsetY,left+size.X,top+offsetY+size.Y);
    }
    private void RestoreHitRegion()
    {
        if(widenedCollision==null) return;
        widenedCollision->SetPositionFloat(originalCollisionPosition.X,originalCollisionPosition.Y);
        widenedCollision->SetWidth((ushort)originalCollisionSize.X);
        widenedCollision->SetHeight((ushort)originalCollisionSize.Y);
        widenedCollision=null;
    }
    private void BeforeUpdate(AddonChatLog* chat)=>RestoreHitRegion();
    private void ExtendHitRegion(AddonChatLog* chat,AtkResNode* space,NativeTabLayout.Rect extraBounds)
    {
        var collision=chat->ControlsCollisionNode;
        if(collision==null ||
            !NativeNodeGeometry.TryRead((AtkResNode*)collision,space,out var frame,out var bounds) ||
            frame.ScaleX<=0 || frame.ScaleY<=0) return;
        var expanded=bounds.Union(extraBounds);
        // Never move an ancestor of the game's own visual nodes. The extra
        // buttons also carry their own collision nodes at the configured position.
        if(collision->ChildNode!=null)
            expanded=expanded with { Left=bounds.Left,Top=bounds.Top };
        if(expanded==bounds) return;
        originalCollisionPosition=new Vector2(collision->X,collision->Y);
        originalCollisionSize=new Vector2(collision->Width,collision->Height);
        widenedCollision=collision;
        collision->SetPositionFloat(
            originalCollisionPosition.X+(expanded.Left-bounds.Left)*collision->ScaleX/frame.ScaleX,
            originalCollisionPosition.Y+(expanded.Top-bounds.Top)*collision->ScaleY/frame.ScaleY);
        collision->SetWidth((ushort)Math.Clamp(MathF.Ceiling(expanded.Width/frame.ScaleX),1,ushort.MaxValue));
        collision->SetHeight((ushort)Math.Clamp(MathF.Ceiling(expanded.Height/frame.ScaleY),1,ushort.MaxValue));
    }
    private void HideExtras()
    {
        foreach(var button in buttons) button.IsVisible=false;
        if(leftCap!=null) leftCap.IsVisible=false;
        if(rightCap!=null) rightCap.IsVisible=false;
        if(settingsButton!=null) settingsButton.IsVisible=false;
        IsReady=false;
        if(active()>=0) restore();
    }
    private void Update(AddonChatLog* chat)
    {
        try
        {
            IsReady=false; LayoutHint=null;
            if(buttons.Count==0) CreateButtons(chat);
            if(!HasFourthAttached() || buttons.Count!=2 || leftCap==null || rightCap==null || settingsButton==null ||
                chat->RootNode==null)
            { HideExtras(); return; }
            if(active()>=0 && chat->TabIndex!=3) restore();
            var anchor=chat->ChatTabs[3].Value->OwnerNode;
            var space=anchor->ParentNode;
            var firstTab=chat->ChatTabs[0].Value;
            var start=chat->TabBarStartImageNode; var end=chat->TabBarEndImageNode;
            if(firstTab==null || !NativeNodeGeometry.TryRead((AtkResNode*)anchor,space,out _,out var anchorBounds) ||
                !NativeNodeGeometry.TryRead((AtkResNode*)firstTab->OwnerNode,space,out _,out var firstBounds) ||
                !NativeNodeGeometry.TryRead((AtkResNode*)start,space,out var startFrame,out var startBounds) ||
                !NativeNodeGeometry.TryRead((AtkResNode*)end,space,out var endFrame,out var endBounds))
            { LayoutHint="等待原生頁籤的位置與端帽資料。"; HideExtras(); return; }
            var widths=new float[2];
            for(int i=0;i<2;i++)
            {
                buttons[i].Name=getName(i);
                if(!buttons[i].MatchNativeLayout(chat->ChatTabs[3].Value))
                { HideExtras(); return; }
                widths[i]=Math.Max(anchor->Width,buttons[i].ContentWidth);
            }
            var previous=chat->ChatTabs[2].Value;
            var gap=previous!=null &&
                NativeNodeGeometry.TryRead((AtkResNode*)previous->OwnerNode,space,out _,out var previousBounds)
                ? Math.Clamp(anchorBounds.Left-previousBounds.Right,-2,4) : 0;
            var band=new NativeTabLayout.Rect(startBounds.Left,
                MathF.Min(startBounds.Top,endBounds.Top),endBounds.Right,MathF.Max(startBounds.Bottom,endBounds.Bottom));
            var protectedRight=NativeControlsRight(chat,space,band,MathF.Max(anchorBounds.Right,endBounds.Right));
            var offset=getOffset();
            var row=NativeTabLayout.AppendAfterControls(protectedRight,8,startBounds.Width,endBounds.Width,
                firstBounds.Left-startBounds.Right,endBounds.Left-anchorBounds.Right,
                anchor->ScaleX,widths[0],widths[1],gap).OffsetX(offset.X);
            // The tab strip may extend beyond the message panel. Only the
            // header's hit region grows; the chat frame and body keep their size.
            var settingsBounds=SettingsBounds(chat,space,band,row.RightEdge+8,offset.Y,
                new Vector2(28*anchor->ScaleX,28*anchor->ScaleY));
            var groupBounds=new NativeTabLayout.Rect(
                MathF.Min(row.LeftCapX,row.FirstX),
                MathF.Min(MathF.Min(startBounds.Top,endBounds.Top),anchorBounds.Top)+offset.Y,
                row.RightEdge,
                MathF.Max(MathF.Max(startBounds.Bottom,endBounds.Bottom),anchorBounds.Bottom)+offset.Y);
            ExtendHitRegion(chat,space,groupBounds.Union(settingsBounds));
            settingsButton.Size=new Vector2(settingsBounds.Width,settingsBounds.Height);
            settingsButton.Position=new Vector2(settingsBounds.Left,settingsBounds.Top);
            settingsButton.IsVisible=true;
            leftCap.Match(start,startFrame,startBounds,row.LeftCapX);
            rightCap.Match(end,endFrame,endBounds,row.RightCapX);
            leftCap.Y+=offset.Y; rightCap.Y+=offset.Y;
            for(int i=0;i<2;i++)
            {
                var button=buttons[i];
                button.Size=new Vector2(widths[i],anchor->Height);
                button.Position=new Vector2((i==0?row.FirstX:row.SecondX)+anchor->X-anchorBounds.Left,anchor->Y+offset.Y);
                button.IsVisible=true; button.Selected=active()==i;
            }
            if(active()>=0) chat->ChatTabs[3].Value->SetChecked(false);
            else chat->ChatTabs[3].Value->SetChecked(chat->TabIndex==3);
            IsReady=true;
        }
        catch(Exception error) { failed(error); }
    }
    private void DisposeVisuals()
    {
        settingsButton?.Dispose(); settingsButton=null;
        foreach(var button in buttons) button.Dispose();
        buttons.Clear();
        leftCap?.Dispose(); leftCap=null;
        rightCap?.Dispose(); rightCap=null;
    }
    private void Finalize(AddonChatLog* chat)
    {
        try { restore(); } catch(Exception error) { failed(error); }
        RestoreHitRegion(); DisposeVisuals(); IsReady=false; LayoutHint=null; boundChat=null;
    }
    public void Dispose() { controller.Disable(); controller.Dispose(); }
}
