using System.Text;
using System.Text.Json;
using NativeChatTabs;
using NativeChatTabs.Core;
int assertions=0;
void Check(bool condition,string name) { if(!condition) throw new Exception(name); assertions++; }
var profiles=new TabProfiles();
Check(profiles.ExtraTabs.Count==2,"exactly two extra native profiles");
Check(profiles.ExtraTabs[0].Name=="Tab 5" && profiles.ExtraTabs[1].Name=="Tab 6","default labels");
Check(!profiles.ExtraTabs[0].IsConfigured(3),"unconfigured profile rejected");
byte[] filter=[1,0,1]; profiles.Capture(0,filter,3); filter[0]=0;
Check(profiles.ExtraTabs[0].FilterBytes![0]==1,"capture owns bytes");
var returned=profiles.ExtraTabs[0].FilterBytes!; returned[0]=0;
Check(profiles.ExtraTabs[0].FilterBytes![0]==1,"getter cannot mutate stored filters");
Check(profiles.ExtraTabs[0].IsConfigured(3),"matching dimensions accepted");
Check(!profiles.ExtraTabs[0].IsConfigured(4),"wrong dimensions rejected");
Check(!profiles.ExtraTabs[0].IsConfigured(0),"zero size rejected");
bool rejected=false;
try{profiles.Capture(1,[1,0],3);}catch(ArgumentException){rejected=true;}
Check(rejected,"truncated capture rejected");
Check(!profiles.ExtraTabs[1].IsConfigured(3),"failed capture keeps other profile untouched");
profiles.ExtraTabs[0].Name=string.Concat(Enumerable.Repeat("聊天室😀",10));
var name=profiles.ExtraTabs[0].Name;
Check(Encoding.UTF8.GetByteCount(name)<=32,"UTF8 name budget");
Check(!name.EnumerateRunes().Any(r=>r.Value==0xFFFD),"no split UTF8 rune");
profiles.ExtraTabs[0].Name=" \n\t "; Check(profiles.ExtraTabs[0].Name=="Tab","empty name fallback");
profiles.ExtraTabs[0].Id=Guid.Empty; profiles.ExtraTabs[1].Id=Guid.Empty; profiles.Normalize();
Check(profiles.ExtraTabs.Select(p=>p.Id).Distinct().Count()==2,"duplicate ids corrected");
Check(profiles.ExtraTabs.All(p=>p.Id!=Guid.Empty),"empty ids corrected");
profiles.ExtraTabs.Clear(); profiles.Normalize(); Check(profiles.ExtraTabs.Count==2,"empty config repaired");
var recovery=new RecoverySnapshot{CharacterId=7,FilterSize=3,OriginalFilterBytes=[0,1,0],LastAppliedFilterBytes=[1,0,1]};
Check(recovery.CanRestore(7,3,[1,0,1]),"same character exact virtual fingerprint recovers");
Check(!recovery.CanRestore(8,3,[1,0,1]),"different character rejected");
Check(!recovery.CanRestore(0,3,[1,0,1]),"no character rejected");
Check(!recovery.CanRestore(7,4,[1,0,1]),"different layout size rejected");
Check(!recovery.CanRestore(7,3,[0,0,1]),"later user edits preserved");
var copy=recovery.OriginalFilterBytes; copy[0]=1;
Check(recovery.OriginalFilterBytes[0]==0,"recovery original owns bytes");
recovery.LayoutVersion=2; Check(!recovery.CanRestore(7,3,[1,0,1]),"unknown layout rejected"); recovery.LayoutVersion=1;
var roundTrip=JsonSerializer.Deserialize<RecoverySnapshot>(JsonSerializer.Serialize(recovery))!;
Check(roundTrip.CanRestore(7,3,[1,0,1]),"recovery JSON roundtrip");
roundTrip.OriginalFilterBytes=[1]; Check(!roundTrip.CanRestore(7,3,[1,0,1]),"incomplete original backup rejected");
var directory=Path.Combine(Path.GetTempPath(),"NativeChatTabs-tests-"+Guid.NewGuid().ToString("N"));
try
{
    var store=new RecoveryStore(directory);
    Check(store.Read()==null,"new recovery store empty");
    store.Write(recovery);
    Check(store.Read()!.CanRestore(7,3,[1,0,1]),"durable recovery store");
    Check(!File.Exists(Path.Combine(directory,"native-fourth-tab-recovery.json.tmp")),"atomic replacement complete");
    store.Clear(); Check(store.Read()==null,"restored backup cleared");
}
finally{Directory.Delete(directory,true);}

// Direct filter editing uses LogFilter row IDs, never chat LogKind IDs.
var draft=new FilterDraft(new byte[]{0,9,0,7,1,0},new[]{0,2,4});
Check(!draft.Changed,"opening filter editor does not change saved settings");
draft.SetChecked(2,true);
Check(draft.IsChecked(2),"checkbox updates the requested filter row");
Check(draft.Snapshot()[1]==9 && draft.Snapshot()[3]==7,"hidden native filter bytes preserved");
Check(draft.Snapshot()[4]==1,"editing one row preserves other channels");
Check(draft.Changed,"unsaved edits detected");
draft.SetVisible(new[]{0,2},false);
Check(!draft.IsChecked(0) && !draft.IsChecked(2) && draft.IsChecked(4),"bulk changes only visible rows");
bool invalidRow=false;
try{draft.SetVisible(new[]{0,1},true);}catch(ArgumentOutOfRangeException){invalidRow=true;}
Check(invalidRow && !draft.IsChecked(0),"invalid bulk change is atomic");
var independent=draft.Snapshot(); independent[4]=0;
Check(draft.IsChecked(4),"saved edit snapshot cannot mutate draft");
var secondDraft=new FilterDraft(new byte[]{0,9,0,7,0,0},new[]{0,2,4});
secondDraft.SetChecked(0,true);
Check(!draft.IsChecked(0) && secondDraft.IsChecked(0),"extra tabs have independent message selections");
var saved=draft.Snapshot(); profiles.Capture(1,saved,saved.Length); saved[4]=0;
Check(profiles.ExtraTabs[1].FilterBytes![4]==1,"edited filters save through profile cloning");
bool invalidLayout=false;
try{_=new FilterDraft(new byte[]{0,0},new[]{2});}catch(ArgumentException){invalidLayout=true;}
Check(invalidLayout,"filter catalogue bounds checked");



var protectedRow=NativeTabLayout.AppendAfterControls(500,8,24,24,0,0,1,80,80,0);
Check(protectedRow.LeftCapX==508 && protectedRow.FirstX==532,"new left cap begins after all native controls");
Check(protectedRow.SecondX==612 && protectedRow.RightCapX==692 && protectedRow.RightEdge==716,
    "tab pair ends with its own native right cap");
Check(protectedRow.LeftCapX>500,"no part of new group covers the native close button");
var scaledRow=NativeTabLayout.AppendAfterControls(500,8,36,36,0,0,1.5f,80,80,0);
Check(scaledRow.FirstX==544 && scaledRow.SecondX==664 && scaledRow.RightEdge==820,
    "scaled cap and button widths leave native controls accessible");
var joinedRow=NativeTabLayout.AppendAfterControls(500,8,24,24,-2,-2,1,80,80,-2);
Check(joinedRow.FirstX==530 && joinedRow.SecondX==608 && joinedRow.RightCapX==686,
    "native cap seams and tab overlap are preserved");
var band=new NativeTabLayout.Rect(0,20,500,50);
Check(NativeTabLayout.IsHeaderControl(new(460,20,492,50),band),"header close button reserves space");
Check(!NativeTabLayout.IsHeaderControl(new(0,55,400,80),band),"chat input on another row is excluded");
Check(!NativeTabLayout.IsHeaderControl(new(0,0,900,500),band),"full window rectangle is excluded");
Check(!NativeTabLayout.IsHeaderControl(new(0,20,0,50),band),"empty control has no occupied area");
var windowFrame=NativeTabLayout.Frame.Identity.ThroughParent(10,10,1.5f,1.5f,0,0);
var closeFrame=NativeTabLayout.Frame.Identity.ThroughParent(400,20,1,1,0,0)
    .ThroughParent(20,0,1,1,0,0).ThroughParent(10,10,1.5f,1.5f,0,0);
var closeInRow=closeFrame.InSpace(windowFrame).Bounds(24,24);
Check(closeInRow.Left==420 && closeInRow.Right==444 && closeInRow.Top==20,
    "nested native close button is measured in the tabs' shared parent space");
var mirroredCap=new NativeTabLayout.Frame(24,20,-1,1).Bounds(24,30);
Check(mirroredCap.Left==0 && mirroredCap.Right==24,"mirrored native end cap keeps its full width");
var scaledOrigin=NativeTabLayout.Frame.Identity.ThroughParent(100,20,1.5f,1.5f,20,0).Bounds(60,20);
Check(scaledOrigin.Left==90 && scaledOrigin.Right==180,"native transform origin is accounted for");
Check(NativeTabLayout.StretchWidth(40,1.5f,30)==60,"scaled background grows by 30 parent pixels");
Check(NativeTabLayout.TextBoxWidth(90,15,15,1.5f)==40,"scaled text keeps native left and right padding");
bool invalidScaleRejected=false;
try { NativeTabLayout.AppendAfterControls(500,8,24,24,0,0,0,80,80,0); }
catch(ArgumentOutOfRangeException) { invalidScaleRejected=true; }
Check(invalidScaleRejected,"invalid native scale rejected before geometry changes");

Check(NativeTabLayout.HitRegionWidth(500,500,716,1)==716,
    "new tabs remain inside the interactive header region");
Check(NativeTabLayout.HitRegionWidth(200,350,500,1.5f)==300,
    "header hit bounds extend in their own scale without moving native buttons");

var placement=JsonSerializer.Deserialize<TabPlacement>("{}")!;
Check(placement.HorizontalOffset==24 && placement.VerticalOffset==0,
    "older settings use extra close-button clearance by default");
placement.HorizontalOffset=73; placement.VerticalOffset=-18;
var savedPlacement=JsonSerializer.Deserialize<TabPlacement>(JsonSerializer.Serialize(placement))!;
Check(savedPlacement.HorizontalOffset==73 && savedPlacement.VerticalOffset==-18,
    "horizontal and vertical placement survive saved settings");
var shiftedRow=protectedRow.OffsetX(savedPlacement.HorizontalOffset);
Check(shiftedRow.LeftCapX==581 && shiftedRow.FirstX==605 &&
    shiftedRow.SecondX==685 && shiftedRow.RightCapX==765 && shiftedRow.RightEdge==789,
    "both native caps and both tabs move together");
Check(shiftedRow.RightEdge-shiftedRow.LeftCapX==protectedRow.RightEdge-protectedRow.LeftCapX,
    "changing position preserves the tab pair size and spacing");
var originalHeader=new NativeTabLayout.Rect(0,20,500,50);
var upperPair=new NativeTabLayout.Rect(581,-10,789,20);
var upperHit=originalHeader.Union(upperPair);
Check(upperHit==new NativeTabLayout.Rect(0,-10,789,50),
    "moving the pair upward includes its new click bounds");
var lowerHit=originalHeader.Union(new NativeTabLayout.Rect(581,80,789,110));
Check(lowerHit==new NativeTabLayout.Rect(0,20,789,110),
    "moving the pair downward includes its new click bounds");
Check(originalHeader==new NativeTabLayout.Rect(0,20,500,50),
    "placement calculations preserve original header bounds for restoration");
savedPlacement.Reset();
Check(savedPlacement.HorizontalOffset==24 && savedPlacement.VerticalOffset==0,
    "reset returns the whole pair to its default clear position");
savedPlacement.HorizontalOffset=int.MaxValue; savedPlacement.VerticalOffset=int.MinValue;
Check(savedPlacement.HorizontalOffset==4096 && savedPlacement.VerticalOffset==-4096,
    "saved offsets remain within native UI coordinate limits");
Console.WriteLine($"Native Chat Tabs core: {assertions} checks passed.");
