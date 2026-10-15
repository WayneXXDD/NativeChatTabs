using System.Text;
namespace NativeChatTabs.Core;
public sealed class ExtraTabProfile
{
    private string name="Tab 5";
    private byte[]? filter;
    public Guid Id { get; set; }=Guid.NewGuid();
    public string Name { get=>name; set=>name=NormalizeName(value); }
    public byte[]? FilterBytes { get=>filter?.ToArray(); set=>filter=value?.ToArray(); }
    public bool IsConfigured(int size)=>size>0 && filter?.Length==size;
    public static string NormalizeName(string? value)
    {
        var builder=new StringBuilder(); int bytes=0;
        foreach(var rune in (value??"").Trim().EnumerateRunes())
        {
            if(Rune.IsControl(rune)) continue;
            if(bytes+rune.Utf8SequenceLength>32) break;
            builder.Append(rune); bytes+=rune.Utf8SequenceLength;
        }
        var result=builder.ToString().Trim();
        return result.Length==0 ? "Tab" : result;
    }
}
public sealed class TabProfiles
{
    public List<ExtraTabProfile> ExtraTabs { get; set; }=
        [new(){Name="Tab 5"},new(){Name="Tab 6"}];
    public void Normalize()
    {
        ExtraTabs=(ExtraTabs??[]).Where(x=>x!=null).Take(2).ToList();
        while(ExtraTabs.Count<2) ExtraTabs.Add(new(){Name="Tab "+(ExtraTabs.Count+5)});
        var ids=new HashSet<Guid>();
        foreach(var p in ExtraTabs)
        {
            p.Name=p.Name;
            if(p.Id==Guid.Empty || !ids.Add(p.Id)) { p.Id=Guid.NewGuid(); ids.Add(p.Id); }
        }
    }
    public void Capture(int index,ReadOnlySpan<byte> bytes,int expectedSize)
    {
        if(index<0 || index>=ExtraTabs.Count) throw new ArgumentOutOfRangeException(nameof(index));
        if(expectedSize<=0 || bytes.Length!=expectedSize) throw new ArgumentException("Invalid filter dimensions.");
        ExtraTabs[index].FilterBytes=bytes.ToArray();
    }
}
public sealed class RecoverySnapshot
{
    private byte[] original=[], applied=[];
    public int LayoutVersion { get; set; }=1;
    public ulong CharacterId { get; set; }
    public int FilterSize { get; set; }
    public string OriginalTabName { get; set; }="";
    public byte[] OriginalFilterBytes { get=>original.ToArray(); set=>original=value?.ToArray()??[]; }
    public byte[] LastAppliedFilterBytes { get=>applied.ToArray(); set=>applied=value?.ToArray()??[]; }
    public bool IsValidFor(ulong characterId,int size)=> LayoutVersion==1 && characterId!=0 &&
        CharacterId==characterId && size>0 && FilterSize==size &&
        original.Length==size && applied.Length==size;
    public bool CanRestore(ulong characterId,int size,ReadOnlySpan<byte> live)=>
        IsValidFor(characterId,size) && live.SequenceEqual(applied);
}
