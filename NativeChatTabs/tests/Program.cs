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


var sameScaleRow=NativeTabLayout.Continue(328,60,1,328,60,60);
Check(sameScaleRow.FirstX==388 && sameScaleRow.SecondX==448,"extra tabs continue CWLS edge in one row");
Check(sameScaleRow.AddedWidth==120,"native controls move past both same-size tabs");
var hudScaleRow=NativeTabLayout.Continue(328,60,1.5f,328,60,60);
Check(hudScaleRow.FirstX==418 && hudScaleRow.SecondX==508,"150 percent native scale does not overlap tabs");
Check(hudScaleRow.AddedWidth==180,"HUD scaling also moves the end cap and add button");
Check(NativeTabLayout.StretchWidth(40,1.5f,30)==60,"scaled background grows by 30 parent pixels");
Check(NativeTabLayout.TextBoxWidth(90,15,15,1.5f)==40,"scaled text keeps native left and right padding");
var overlapRow=NativeTabLayout.Continue(100,60,1,102,60,60);
Check(overlapRow.FirstX==158 && overlapRow.SecondX==216,"native two pixel tab overlap is preserved");
var spacedRow=NativeTabLayout.Continue(100,60,1,96,60,60);
Check(spacedRow.FirstX==164 && spacedRow.SecondX==228,"native tab gap is preserved");
bool invalidScaleRejected=false;
try { NativeTabLayout.Continue(0,60,0,0,60,60); } catch(ArgumentOutOfRangeException) { invalidScaleRejected=true; }
Check(invalidScaleRejected,"invalid native scale rejected before geometry changes");
Console.WriteLine($"Native Chat Tabs core: {assertions} checks passed.");

