using System;

public class ContainerSession
{
    public required string ContainerPath;
    public required Header Header;
    public required byte[] Key;
    public required Bitmap Bitmap;
    public required List<Entry> Files;
}
