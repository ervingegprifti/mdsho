using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace MDSHO.Data {

  /// <summary>
  /// Windows data protection (DPAPI, CryptProtectData) for the current user: only this Windows user on this PC can open
  /// what it protects. Used for the Google Drive and OneDrive refresh tokens, which never leave this PC.
  /// </summary>
  internal static class Dpapi {
    private const int UiForbidden = 0x1;
    private static readonly byte[] Entropy = Encoding.ASCII.GetBytes("MDSHO sync token");

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob {
      public int Size;
      public IntPtr Data;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref DataBlob data, string description, ref DataBlob entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob result);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref DataBlob data, IntPtr description, ref DataBlob entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob result);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);

    /// <summary>The text protected for this Windows user, as base64.</summary>
    public static string Protect(string text) {
      byte[] result = Run(Encoding.UTF8.GetBytes(text), protect: true);
      return Convert.ToBase64String(result);
    }

    /// <summary>The protected text, or null when it isn't protected data of this Windows user.</summary>
    public static string Unprotect(string base64) {
      try {
        return Encoding.UTF8.GetString(Run(Convert.FromBase64String(base64), protect: false));
      } catch (FormatException) {
        return null;
      } catch (Win32Exception) {
        return null;
      }
    }

    private static byte[] Run(byte[] input, bool protect) {
      GCHandle inHandle = GCHandle.Alloc(input, GCHandleType.Pinned);
      GCHandle entropyHandle = GCHandle.Alloc(Entropy, GCHandleType.Pinned);
      DataBlob output = default;
      try {
        DataBlob data = new DataBlob { Size = input.Length, Data = inHandle.AddrOfPinnedObject() };
        DataBlob entropy = new DataBlob { Size = Entropy.Length, Data = entropyHandle.AddrOfPinnedObject() };
        bool ok = protect
          ? CryptProtectData(ref data, "MDSHO", ref entropy, IntPtr.Zero, IntPtr.Zero, UiForbidden, out output)
          : CryptUnprotectData(ref data, IntPtr.Zero, ref entropy, IntPtr.Zero, IntPtr.Zero, UiForbidden, out output);
        if (!ok) {
          throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        byte[] result = new byte[output.Size];
        Marshal.Copy(output.Data, result, 0, output.Size);
        return result;
      } finally {
        if (output.Data != IntPtr.Zero) {
          LocalFree(output.Data);
        }
        inHandle.Free();
        entropyHandle.Free();
      }
    }
  }
}
