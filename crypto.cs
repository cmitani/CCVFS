using System;
using System.Security.Cryptography;
using System.Text;


public static class Crypto
{
    public const int DefaultBlockSize = 64 * 1024;

	public static byte[] DeriveKey(string password, byte[] salt)
	{
        const int kdfIterations = 100_000;
		Rfc2898DeriveBytes keyDerivation = new Rfc2898DeriveBytes(password, salt, kdfIterations, HashAlgorithmName.SHA256);
		byte[] derivedKey = keyDerivation.GetBytes(32);
		return derivedKey;
    }

	public static byte [] DeriveKeyFromFile(string password, byte[] salt, byte [] fileBytes)
	{
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
		byte[] key = new byte[passwordBytes.Length];
        const int kdfIterations = 100_000;
        byte[] hash = SHA256.HashData(fileBytes);
        for (int i = 0; i < passwordBytes.Length; i++)
        {
            key[i] = (byte)(passwordBytes[i] ^ hash[i % hash.Length]);
        }
        
        Rfc2898DeriveBytes keyDerivation = new Rfc2898DeriveBytes(key, salt, kdfIterations, HashAlgorithmName.SHA256);
        byte[] derivedKey = keyDerivation.GetBytes(32);
        return derivedKey;
    }

    public static byte[] PadBlock(byte[] data, int blockSize = DefaultBlockSize)
    {
        if (data.Length > blockSize)
        {
            throw new ArgumentException("Размер блока больше допустимого.", nameof(data));
        }

        if (data.Length == blockSize)
        {
            return data;
        }

        byte[] padded = new byte[blockSize];
        Buffer.BlockCopy(data, 0, padded, 0, data.Length);
        return padded;
    }

    public static (byte[],byte[], byte[]) Encrypt (byte[] key, byte[] data)
    {
        using var aes = new AesGcm(key, tagSizeInBytes: 16);
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] ciphertext = new byte[data.Length];
        byte[] tag = new byte[16];
        aes.Encrypt(nonce, data, ciphertext, tag);
        return (ciphertext, nonce, tag);

    }

    public static byte[] Decrypt(byte[] key, byte[] nonce, byte[] tag, byte[] ciphertext)
    {
        using var aes = new AesGcm(key, tagSizeInBytes: 16);
        byte[] data = new byte[ciphertext.Length];
        aes.Decrypt(nonce, ciphertext, tag, data);
        return data;
    }

	public static byte[] CreateHmac(byte[] key, byte[] data)
    {

        using (var hmac = new HMACSHA256(key))
        {
            byte[] hash = hmac.ComputeHash(data);

            return hash;
        }

    }
    
	public static bool ValidateHmac(byte[] key, byte[] data, byte[] expectedHmac)
    {
        using (var hmac = new HMACSHA256(key))
        {
            byte[] hash = hmac.ComputeHash(data);

            return CryptographicOperations.FixedTimeEquals(hash, expectedHmac); 
        }

        
    }
	
}
