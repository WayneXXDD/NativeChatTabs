namespace NativeChatTabs.Core;

/// <summary>An isolated edit buffer. Hidden/native-only filter bytes are preserved.</summary>
public sealed class FilterDraft
{
    private readonly byte[] original;
    private readonly byte[] values;
    private readonly HashSet<int> editable;
    public int Length=>values.Length;
    public bool Changed=>!original.AsSpan().SequenceEqual(values);
    public FilterDraft(ReadOnlySpan<byte> source,IEnumerable<int> rowIds)
    {
        if(source.Length==0) throw new ArgumentException("A native filter layout is required.");
        original=source.ToArray(); values=source.ToArray();
        editable=rowIds.ToHashSet();
        if(editable.Any(i=>i<0 || i>=values.Length))
            throw new ArgumentException("A filter row is outside the native layout.");
    }
    private void Validate(int rowId)
    {
        if(!editable.Contains(rowId)) throw new ArgumentOutOfRangeException(nameof(rowId));
    }
    public bool IsChecked(int rowId) { Validate(rowId); return values[rowId]==1; }
    public void SetChecked(int rowId,bool enabled) { Validate(rowId); values[rowId]=enabled?(byte)1:(byte)0; }
    public void SetVisible(IEnumerable<int> rowIds,bool enabled)
    {
        var indices=rowIds.ToArray();
        foreach(var index in indices) Validate(index);
        foreach(var index in indices) values[index]=enabled?(byte)1:(byte)0;
    }
    public byte[] Snapshot()=>values.ToArray();
}
