using System;
using System.Linq;


public class Header
{
    public byte[] Magic = new byte[] { 0x43, 0x43, 0x56, 0x46, 0x53, 0x31, 0x30, 0x30 };
    public ushort Version = 1;
    public byte[] Salt = new byte[16];
    public int TotalBlockCount;
    public byte[] Hmac = new byte[32];
    public int DirectoryCapacity = 0;

    public void Write(BinaryWriter w)
    {
        w.Write(Magic);
        w.Write(Version);
        w.Write(Salt);
        w.Write(TotalBlockCount);
        w.Write(Hmac);
        w.Write(DirectoryCapacity);
    }
    public static Header Read(BinaryReader r)
    {
        var h = new Header();
        h.Magic = r.ReadBytes(8);
        h.Version = r.ReadUInt16();
        h.Salt = r.ReadBytes(16);
        h.TotalBlockCount = r.ReadInt32();
        h.Hmac = r.ReadBytes(32);
        h.DirectoryCapacity = r.ReadInt32();
        return h;
    }
    public static bool Valid(byte[] magic)
    {
        byte[] expectedMagic = new byte[] { 0x43, 0x43, 0x56, 0x46, 0x53, 0x31, 0x30, 0x30 };

        return magic.SequenceEqual(expectedMagic);

    }
    public Header() 
{}	

}    
