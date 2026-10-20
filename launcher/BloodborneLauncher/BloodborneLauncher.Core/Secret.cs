using System;
using System.Runtime.InteropServices;
using System.Text;

namespace BloodborneLauncher.Core;

public static class Secret
{
	private struct Blob
	{
		public int Size;

		public nint Data;
	}

	private const string Prefix = "dpapi:";

	private const int UiForbidden = 1;

	[DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool CryptProtectData(ref Blob input, string? description, nint entropy, nint reserved, nint prompt, int flags, out Blob output);

	[DllImport("crypt32.dll", SetLastError = true)]
	private static extern bool CryptUnprotectData(ref Blob input, nint description, nint entropy, nint reserved, nint prompt, int flags, out Blob output);

	[DllImport("kernel32.dll")]
	private static extern nint LocalFree(nint memory);

	public static bool IsProtected(string stored)
	{
		return stored.StartsWith("dpapi:", StringComparison.Ordinal);
	}

	public static string Protect(string plain)
	{
		if (plain.Length == 0)
		{
			return "";
		}
		byte[] array = Transform(Encoding.UTF8.GetBytes(plain), encrypt: true);
		if (array != null)
		{
			return "dpapi:" + Convert.ToBase64String(array);
		}
		return "";
	}

	public static string Unprotect(string stored)
	{
		if (!IsProtected(stored))
		{
			return stored;
		}
		try
		{
			string text = stored;
			int length = "dpapi:".Length;
			byte[] array = Transform(Convert.FromBase64String(text.Substring(length, text.Length - length)), encrypt: false);
			return (array == null) ? "" : Encoding.UTF8.GetString(array);
		}
		catch (FormatException)
		{
			return "";
		}
	}

	private static byte[]? Transform(byte[] bytes, bool encrypt)
	{
		Blob input = new Blob
		{
			Size = bytes.Length,
			Data = Marshal.AllocHGlobal(Math.Max(bytes.Length, 1))
		};
		Blob output = default(Blob);
		try
		{
			Marshal.Copy(bytes, 0, input.Data, bytes.Length);
			if (!(encrypt ? CryptProtectData(ref input, "Bloodborne launcher", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output) : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)) || output.Size < 0 || output.Size > 65536)
			{
				return null;
			}
			byte[] array = new byte[output.Size];
			Marshal.Copy(output.Data, array, 0, array.Length);
			return array;
		}
		finally
		{
			Marshal.Copy(new byte[bytes.Length], 0, input.Data, bytes.Length);
			Marshal.FreeHGlobal(input.Data);
			if (output.Data != IntPtr.Zero)
			{
				LocalFree(output.Data);
			}
		}
	}
}
