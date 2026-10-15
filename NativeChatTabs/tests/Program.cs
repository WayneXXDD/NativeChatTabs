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
Console.WriteLine($"Native Chat Tabs core: {assertions} checks passed.");

