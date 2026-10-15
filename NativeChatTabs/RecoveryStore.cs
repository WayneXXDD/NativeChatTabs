using System.Text.Json;
using NativeChatTabs.Core;
namespace NativeChatTabs;
internal sealed class RecoveryStore
{
    private readonly string path;
    public RecoveryStore(string directory)
    { Directory.CreateDirectory(directory); path=Path.Combine(directory,"native-fourth-tab-recovery.json"); }
    public RecoverySnapshot? Read() => File.Exists(path)
        ? JsonSerializer.Deserialize<RecoverySnapshot>(File.ReadAllText(path)) : null;
    public void Write(RecoverySnapshot snapshot)
    {
        var temporary=path+".tmp";
        var bytes=JsonSerializer.SerializeToUtf8Bytes(snapshot);
        using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None,
                  4096,FileOptions.WriteThrough))
        { stream.Write(bytes); stream.Flush(true); }
        File.Move(temporary,path,true);
    }
    public void Clear() { if(File.Exists(path)) File.Delete(path); }
}
