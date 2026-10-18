using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

public class ContainerManager
{
    private long BlockPosition(Header header, int blockIndex)
    {
        return 66 + header.TotalBlockCount + 12 + 16 + header.DirectoryCapacity + blockIndex * (64 * 1024 + 12 + 16);
    }

    private List<Entry> ReadDirectory(FileStream fs, byte[] key, Header header)
    {

        fs.Position = 66 + header.TotalBlockCount;

        byte[] nonce = new byte[12];
        fs.Read(nonce, 0, 12);

        byte[] tag = new byte[16];
        fs.Read(tag, 0, 16);

        byte[] directory = new byte[header.DirectoryCapacity];
        fs.Read(directory, 0, header.DirectoryCapacity);

        byte[] ndirectory = Crypto.Decrypt(key, nonce, tag, directory);


        var ms = new MemoryStream(ndirectory);
        using var reader = new BinaryReader(ms);
        int count = reader.ReadInt32();

        List<Entry> decode = new List<Entry>();
        for (int i = 0; i < count; i++)
        {
            decode.Add(Entry.Read(reader));
        }

        return decode;
    }

    private void WriteDirectory(FileStream fs, byte[] key, Header header, List<Entry> files)
    {

        fs.Position = 66 + header.TotalBlockCount;
        var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(files.Count);

        foreach (var f in files)
        {
            f.Write(writer);
        }

        byte[] bytefl = ms.ToArray();
        (byte[] ciphertext, byte[] nonce, byte[] tag) data;
        if (bytefl.Length < header.DirectoryCapacity)
        {
            byte[] padded = new byte[header.DirectoryCapacity];
            Buffer.BlockCopy(bytefl, 0, padded, 0, bytefl.Length);
            data = Crypto.Encrypt(key, padded);
        }

        else if (bytefl.Length == header.DirectoryCapacity)
        { data = Crypto.Encrypt(key, bytefl); }

        else { throw new Exception("директория переполнена"); }
        
        fs.Position = 66 + header.TotalBlockCount;
        fs.Write(data.nonce);
        fs.Write(data.tag);
        fs.Write(data.ciphertext);
        fs.Position = 0;
        using var wr = new BinaryWriter(fs, System.Text.Encoding.UTF8, leaveOpen: true);
        header.Write(wr);
    }

    private void UpdateHmac(FileStream fs, byte[] key)
    {
        fs.Position = 30;
        fs.Write(new byte[32], 0, 32);

        fs.Position = 0;
        byte[] data = new byte[fs.Length];
        fs.ReadExactly(data, 0, data.Length);

        byte[] hash = Crypto.CreateHmac(key, data);
        fs.Position = 30;
        fs.Write(hash);
    }

    private (byte[], byte[], byte[]) ReadBlock(FileStream fs, long offset)
    {
        const int blocksize = 64 * 1024;

        fs.Position = offset;

        byte[] nonce = new byte[12];
        fs.Read(nonce, 0, 12);

        byte[] tag = new byte[16];
        fs.Read(tag, 0, 16);

        byte[] ciphertext = new byte[blocksize];
        fs.Read(ciphertext, 0, ciphertext.Length);

        return (ciphertext, nonce, tag);
    }

    private void WriteBlock(FileStream fs, long offset, byte[] nonce, byte[] tag, byte[] ciphertext)
    {
        fs.Position = offset;
        fs.Write(nonce, 0, nonce.Length);
        fs.Write(tag, 0, tag.Length);
        fs.Write(ciphertext, 0, ciphertext.Length);
    }

    public void Create(string path, int sizeMb, string password, string? keyfilePath)
    {
        if (sizeMb > 0 && !string.IsNullOrEmpty(password))
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            fs.SetLength((long)sizeMb * 1024 * 1024);
            using var w = new BinaryWriter(fs);
            Header header = new Header();
            List<Entry> files = new List<Entry>();
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            if (string.IsNullOrEmpty(keyfilePath))
            {
                byte[] key = Crypto.DeriveKey(password, salt);
                header.Salt = salt;
                header.TotalBlockCount = (sizeMb * 1024 * 1024 - 66 - 12 - 16) / (Crypto.DefaultBlockSize + 12 + 16 + 1 + 256);
                header.DirectoryCapacity = header.TotalBlockCount * 256;
                if (header.TotalBlockCount <= 0)
                {
                    throw new Exception("слишком маленький размер для контейнера");
                }
                Bitmap bitmap = new Bitmap(header.TotalBlockCount);
                fs.Position = 0;
                header.Write(w);
                bitmap.Write(w);
                WriteDirectory(fs, key, header, files);
                UpdateHmac(fs, key);
            }
            else if (File.Exists(keyfilePath))
            {
                byte[] data = File.ReadAllBytes(keyfilePath);
                byte[] key = Crypto.DeriveKeyFromFile(password, salt, data);
                header.Salt = salt;
                header.TotalBlockCount = (sizeMb * 1024 * 1024 - 66 - 12 - 16) / (Crypto.DefaultBlockSize + 12 + 16 + 1 + 256);
                header.DirectoryCapacity = header.TotalBlockCount * 256;
                if (header.TotalBlockCount <= 0)
                {
                    throw new Exception("слишком маленький размер для контейнера");
                }
                Bitmap bitmap = new Bitmap(header.TotalBlockCount);
                fs.Position = 0;
                header.Write(w);
                bitmap.Write(w);
                WriteDirectory(fs, key, header, files);
                UpdateHmac(fs, key);
            }
            else
            {
                throw new Exception("прикрепляемый keyfile не существует");
            }
        }
        else throw new Exception("некорректные данные для создания контейнера(размер файла должен быть больше 0, пароль не должен быть пустым)");
    }

    public ContainerSession Open(string path, string password, string? keyfilePath)
    {

        if (File.Exists(path) && !string.IsNullOrEmpty(password))
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var b = new BinaryReader(fs);
            Header header = Header.Read(b);

            if (!Header.Valid(header.Magic))
            {
                throw new Exception("Выбранный файл не является контейнером");
            }

            byte[] key;
            if (string.IsNullOrEmpty(keyfilePath))
            {
                key = Crypto.DeriveKey(password, header.Salt);
            }
            else if (File.Exists(keyfilePath))
            {
                byte[] keyfileData = File.ReadAllBytes(keyfilePath);
                key = Crypto.DeriveKeyFromFile(password, header.Salt, keyfileData);
            }
            else
            {
                throw new Exception("прикрепляемый keyfile не существует");
            }

            fs.Position = 0;
            byte[] containerData = new byte[fs.Length];
            fs.ReadExactly(containerData, 0, containerData.Length);
            Array.Clear(containerData, 30, 32);
            if (!Crypto.ValidateHmac(key, containerData, header.Hmac))
            {
                throw new Exception("Неверный пароль, keyfile или контейнер поврежден");
            }

            fs.Position = 66;
            Bitmap bitmap = Bitmap.Read(b, header.TotalBlockCount);
            List<Entry> entries = ReadDirectory(fs, key, header);
            return new ContainerSession
            {
                ContainerPath = path,
                Header = header,
                Key = key,
                Bitmap = bitmap,
                Files = entries
            };
        }

        else throw new Exception("Такого файла не существует");
    }

    public void AddFile(string containerPath, string password, string? keyfilePath, string sourceFilePath)
    {
        if (string.IsNullOrEmpty(sourceFilePath) || !File.Exists(sourceFilePath))
        {
            throw new Exception("выбранный файл не существует");
        }
        ContainerSession containerSession = Open(containerPath, password, keyfilePath);
        using var fs1 = new FileStream(containerPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);

        if (containerSession.Files.Any(entry => entry.Name.Equals(Path.GetFileName(sourceFilePath), StringComparison.OrdinalIgnoreCase)))
        {
            throw new Exception("файл с таким именем уже существует");
        }

        using var fs = new FileStream(sourceFilePath, FileMode.Open);

        int bytesRead;
        int fileBlockCount = (int)((fs.Length + 64 * 1024 - 1) / (64 * 1024));
        Entry file = new Entry();
        file.Name = Path.GetFileName(sourceFilePath);
        file.OriginalSize = fs.Length;

        List<Entry> check = new List<Entry>(containerSession.Files);
        check.Add(file);
        var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(check.Count);

        foreach (var f in check)
        {
            f.Write(writer);
        }

        if (ms.Length >= containerSession.Header.DirectoryCapacity)
        {
            throw new Exception("директория переполнена");
        }

        byte[][] blocks = new byte[fileBlockCount][];
        for (int i = 0; i < fileBlockCount; i++)
        {
            blocks[i] = new byte[Crypto.DefaultBlockSize];
        }

        for (int i = 0; i < fileBlockCount; i++)
        {

            bytesRead = fs.Read(blocks[i], 0, Crypto.DefaultBlockSize);

        }


        int[] indexes = new int[fileBlockCount];
        for (int i = 0; i < fileBlockCount; i++)
        {
            int index = containerSession.Bitmap.FindFree();
            containerSession.Bitmap.SetUsed(index);

            indexes[i] = index;

        }

        using var br = new BinaryWriter(fs1);
        fs1.Position = 66;
        containerSession.Bitmap.Write(br);
        fs1.Position = 0;
        (byte[] ciphertext, byte[] nonce, byte[] tag)[] encryptedBlocks = new (byte[] ciphertext, byte[] nonce, byte[] tag)[fileBlockCount];

        Parallel.For(0, indexes.Length, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) }, i =>
        {
            encryptedBlocks[i] = Crypto.Encrypt(containerSession.Key, blocks[i]);

        });

        for (int i = 0; i < fileBlockCount; i++)
        {
            WriteBlock(fs1, BlockPosition(containerSession.Header, indexes[i]), encryptedBlocks[i].nonce, encryptedBlocks[i].tag, encryptedBlocks[i].ciphertext);

            file.Blocks.Add(indexes[i]);

        }

        containerSession.Files.Add(file);

        WriteDirectory(fs1, containerSession.Key, containerSession.Header, containerSession.Files);
        UpdateHmac(fs1, containerSession.Key);
    }

    public void ExtractFile(string containerPath, string password, string? keyfilePath, string fileName, string destPath)
    {
        if (string.IsNullOrEmpty(destPath))
        {
            throw new Exception("неверный путь для извлечения файла");
        }

        ContainerSession containerSession = Open(containerPath, password, keyfilePath);
        int index = -1;
        for (int i = 0; i < containerSession.Files.Count; i++)
        {
            if (containerSession.Files[i].Name.Equals(fileName, StringComparison.OrdinalIgnoreCase))
            {
                index = i; break;
            }
        }
        if (index == -1)
        {
            throw new Exception("файла с таким именем не существует");
        }

        using var fs = new FileStream(containerPath, FileMode.Open, FileAccess.Read);

        List<byte[]> blocks = new List<byte[]>();
        foreach (int i in containerSession.Files[index].Blocks) 
        {
           
            
            (byte[] ciphertext, byte[] nonce, byte[] tag) = ReadBlock(fs,BlockPosition(containerSession.Header,i));
            blocks.Add(Crypto.Decrypt(containerSession.Key, nonce, tag, ciphertext));


        }

        var ms = new MemoryStream();


        long remaining = containerSession.Files[index].OriginalSize;

        foreach (var f in blocks)
        {
            if (remaining >= Crypto.DefaultBlockSize)
            {
                ms.Write(f, 0, f.Length);
                remaining -= Crypto.DefaultBlockSize;
            }
            else
            {
                ms.Write(f, 0, (int)remaining);
                break;
            }
        }

        File.WriteAllBytes(destPath,ms.ToArray());







    }
    public void DeleteFile(string containerPath, string password, string? keyfilePath, string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            throw new Exception("введите корректное имя файла");
        }
        ContainerSession containerSession = Open(containerPath, password, keyfilePath);
        using var fs = new FileStream(containerPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        int index = -1;
        for (int i = 0; i < containerSession.Files.Count; i++)
        {
            if (containerSession.Files[i].Name.Equals(fileName, StringComparison.OrdinalIgnoreCase))
            {
                index = i; break;
            }


        }

        if (index == -1)
        {
            throw new Exception("файла с таким именем не существует либо контейнер поврежден");
        }

        foreach (int i in containerSession.Files[index].Blocks)
        {
            containerSession.Bitmap.SetFree(i);
        }

        containerSession.Files.RemoveAt(index);
        WriteDirectory(fs, containerSession.Key, containerSession.Header, containerSession.Files);

        using var br = new BinaryWriter(fs);
        fs.Position = 66;
        containerSession.Bitmap.Write(br);
        fs.Position = 0;

        UpdateHmac(fs, containerSession.Key);

    }




}
