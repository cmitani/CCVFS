using System;
using System.Collections.Generic;

public class Entry
{
    public string Name = string.Empty;
    public long OriginalSize;
    public List<int> Blocks = new List<int>();

    public void Write(BinaryWriter w)
    {
        w.Write(Name);
        w.Write(OriginalSize);
        w.Write(Blocks.Count);
        foreach (var blockIndex in Blocks)
        {
            w.Write(blockIndex);
        }
    }
    public static Entry Read(BinaryReader r)
    {
        var entry = new Entry();
        entry.Name = r.ReadString();
        entry.OriginalSize = r.ReadInt64();
        entry.Blocks = new List<int>();
        int size = r.ReadInt32();
        for (int i = 0; i < size; i++)
        {
            entry.Blocks.Add(r.ReadInt32());

        }

        return entry;
    }

    public Entry()
    { }
		


}
