using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
namespace NativeChatTabs;
internal unsafe sealed class NativeTabBar : IDisposable
{
    private readonly AddonController<AddonChatLog> controller;
    private readonly Func<int, string> getName;
    private readonly Action<int> select;
    private readonly Action restore;
    private readonly Action<Exception> failed;
    private readonly Func<int> active;
    private readonly List<NativeChatTabButton> buttons = [];
    private readonly Dictionary<nint, float> movedX = [];
    private nint widenedCollision;
    private ushort originalCollisionWidth;
    public bool HasRoom { get; private set; }
    private AddonChatLog* boundChat;
    public AddonChatLog* Chat => boundChat;
    public NativeTabBar(Func<int,string> getName, Action<int> select, Action restore,
        Func<int> active, Action<Exception> failed)
    {
        this.getName=getName; this.select=select; this.restore=restore; this.active=active; this.failed=failed;
        controller = new() { AddonName="ChatLog", OnSetup=Setup,
            OnPreUpdate=BeforeUpdate, OnUpdate=Update, OnFinalize=Finalize };
    }
    public void Enable() => controller.Enable();
    public bool HasFourthAttached()
    {
        var chat=Chat;
        return chat != null && chat->TabCount >= 4 && chat->ChatTabs[3].Value != null &&
            chat->ChatTabs[3].Value->OwnerNode != null &&
            chat->ChatTabs[3].Value->OwnerNode->IsVisible();
    }
    private void Setup(AddonChatLog* chat)
    {
        try
        {
            boundChat=chat;
            if (buttons.Count != 0) return;
            var anchor=chat->ChatTabs[3].Value;
            if (anchor == null || anchor->OwnerNode == null) return;
            if(chat->TabCount<4 || chat->TabIndex>=4) return;
            var chosen=chat->ChatTabs[chat->TabIndex].Value;
            if(chosen==null || !chosen->OwnerNode->IsVisible()) return;
            if(active()<0 && !chosen->IsChecked) { chosen->SetChecked(true); return; }
            AtkComponentTab* idle=null;
            for(var j=0;j<4;j++)
                if(j!=chat->TabIndex && chat->ChatTabs[j].Value!=null &&
                    chat->ChatTabs[j].Value->OwnerNode!=null &&
                    chat->ChatTabs[j].Value->OwnerNode->IsVisible())
                { idle=chat->ChatTabs[j].Value; break; }
            if(idle==null) return;
            for(int i=0;i<2;i++)
            {
                int index=i;
                var button=NativeChatTabButton.TryCreate(idle,chosen);
                if(button==null) {
                    foreach(var existing in buttons) existing.Dispose();
                    buttons.Clear(); return;
                }
                button.Name=getName(i); button.IsVisible=false;
                button.OnClick=()=> { try { select(index); } catch(Exception e) { failed(e); } };
                buttons.Add(button);
                button.AttachNode((AtkResNode*)anchor->OwnerNode,NodePosition.AfterTarget);
            }
        }
        catch(Exception error) { failed(error); }
    }
    private void RestoreGeometry()
    {
        foreach(var pair in movedX) ((AtkResNode*)pair.Key)->SetXFloat(pair.Value);
        movedX.Clear();
        if(widenedCollision != 0) ((AtkResNode*)widenedCollision)->SetWidth(originalCollisionWidth);
        widenedCollision=0;
    }
    private void BeforeUpdate(AddonChatLog* _) => RestoreGeometry();
    private void MoveX(AtkResNode* node,float delta)
    {
        if(node == null) return;
        movedX[(nint)node]=node->X;
        node->SetXFloat(node->X+delta);
    }
    private void Update(AddonChatLog* chat)
    {
        try
        {
            if(buttons.Count == 0) Setup(chat);
            HasRoom=false;
            if(!HasFourthAttached() || buttons.Count != 2 || chat->RootNode == null)
            { foreach(var b in buttons) b.IsVisible=false; if(active()>=0) restore(); return; }
            if(active()>=0 && chat->TabIndex != 3) restore();
            var anchor=chat->ChatTabs[3].Value->OwnerNode;
            var widths=new float[2];
            for(int i=0;i<2;i++) {
                buttons[i].Name=getName(i);
                widths[i]=Math.Clamp(buttons[i].ContentWidth,50,180);
            }
            var previous=chat->ChatTabs[2].Value->OwnerNode;
            var gap=Math.Clamp(anchor->X-previous->X-previous->Width,-2,4);
            var start=anchor->X+anchor->Width+gap;
            var total=widths[0]+widths[1]+2*gap;
            HasRoom=start+total+36 <= chat->RootNode->Width;
            if(!HasRoom)
            { foreach(var b in buttons) b.IsVisible=false; if(active()>=0) restore(); return; }
            MoveX((AtkResNode*)chat->AddTabComponentNode,total);
            MoveX((AtkResNode*)chat->TabBarEndImageNode,total);
            if(chat->ControlsCollisionNode != null)
            {
                widenedCollision=(nint)chat->ControlsCollisionNode;
                originalCollisionWidth=chat->ControlsCollisionNode->Width;
                chat->ControlsCollisionNode->SetWidth((ushort)Math.Max(originalCollisionWidth,start+total+30));
            }
            for(int i=0;i<2;i++)
            {
                var b=buttons[i];
                b.Size=new Vector2(widths[i],anchor->Height);
                b.Position=new Vector2(start,anchor->Y);
                b.IsVisible=true; b.Selected=active()==i;
                start+=widths[i]+gap;
            }
            if(active()>=0) chat->ChatTabs[3].Value->SetChecked(false);
            else chat->ChatTabs[3].Value->SetChecked(chat->TabIndex==3);
        }
        catch(Exception error) { failed(error); }
    }
    private void Finalize(AddonChatLog* chat)
    {
        try { restore(); } catch(Exception error) { failed(error); }
        RestoreGeometry();
        foreach(var b in buttons) b.Dispose();
        buttons.Clear(); HasRoom=false; boundChat=null;
    }
    public void Dispose() { controller.Disable(); controller.Dispose(); }
}
