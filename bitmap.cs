using System;
using System.Data;

public class Bitmap
{
	public bool[] _used;
	public Bitmap(int totalBlockCount)
	{
		_used = new bool[totalBlockCount];

	}
	public bool IsUsed (int blockIndex)
	{
		return _used[blockIndex];
	} 
	public void SetUsed ( int blockIndex)
	{
		this._used[blockIndex] = true;
	}

    public void SetFree(int blockIndex)
    {
        this._used[blockIndex] = false;
    }

	public int FindFree()
	{
		for (int i =0; i < this._used.Length; i++)
		{
			if (!this._used[i])
			{
				return i;
				
			}
			
		}
		throw new Exception("Контейнер заполнен.");
	}

	public int FreeCount()
	{
		int k = 0;
		foreach(bool b in this._used)
		{
			if (!b)
			{
				k++;
			}
		}
		return k;
		
	}

	public void Write(BinaryWriter w)
	{
		foreach(bool b in _used)
		{
			w.Write(b);
		}
	}

	public static Bitmap Read(BinaryReader r, int totalBlockCount)
	{
		var b = new Bitmap(totalBlockCount);
		for( int i=0; i<totalBlockCount;i++)
		{
			b._used[i] = r.ReadBoolean();
		}
		return b;

	}
}
