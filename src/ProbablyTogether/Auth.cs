using System;
using System.Security.Cryptography;
using System.Text;

namespace ProbablyTogether;

internal static class Auth
{
	internal const int Rounds = 20000;

	internal static string Random16()
	{
		byte[] array = new byte[16];
		using (RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create())
		{
			randomNumberGenerator.GetBytes(array);
		}
		return Hex(array);
	}

	internal static byte[] DeriveKey(string password, string salt)
	{
		if (string.IsNullOrEmpty(password))
		{
			return null;
		}
		return Pbkdf2Sha256(Encoding.UTF8.GetBytes(password), Encoding.UTF8.GetBytes(salt ?? ""), 20000);
	}

	internal static string Proof(byte[] key, string nonce)
	{
		if (key == null)
		{
			return "";
		}
		using HMACSHA256 hMACSHA = new HMACSHA256(key);
		return Hex(hMACSHA.ComputeHash(Encoding.UTF8.GetBytes(nonce ?? "")));
	}

	internal static bool Matches(byte[] key, string nonce, string auth)
	{
		string text = Proof(key, nonce);
		if (text.Length == 0 || auth == null || auth.Length != text.Length)
		{
			return false;
		}
		int num = 0;
		for (int i = 0; i < text.Length; i++)
		{
			num |= char.ToLowerInvariant(auth[i]) ^ text[i];
		}
		return num == 0;
	}

	internal static byte[] Pbkdf2Sha256(byte[] password, byte[] salt, int rounds)
	{
		using HMACSHA256 hMACSHA = new HMACSHA256(password);
		byte[] array = new byte[salt.Length + 4];
		Buffer.BlockCopy(salt, 0, array, 0, salt.Length);
		array[^1] = 1;
		byte[] array2 = hMACSHA.ComputeHash(array);
		byte[] array3 = (byte[])array2.Clone();
		for (int i = 1; i < rounds; i++)
		{
			array2 = hMACSHA.ComputeHash(array2);
			for (int j = 0; j < array3.Length; j++)
			{
				array3[j] ^= array2[j];
			}
		}
		return array3;
	}

	internal static string Hex(byte[] b)
	{
		StringBuilder stringBuilder = new StringBuilder(b.Length * 2);
		foreach (byte b2 in b)
		{
			stringBuilder.Append(b2.ToString("x2"));
		}
		return stringBuilder.ToString();
	}
}
