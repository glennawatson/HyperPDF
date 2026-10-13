// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Text;

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>Reads PDFium's measure-then-copy string outputs. Call these while holding the PDFium lock.</summary>
internal static unsafe class PdfiumNativeStrings
{
    /// <summary>The bytes in a UTF-16 terminator.</summary>
    private const int Utf16TerminatorBytes = 2;

    /// <summary>Reads a UTF-16LE string that a one-handle function copies.</summary>
    /// <param name="handle">The native object.</param>
    /// <param name="read">The PDFium function.</param>
    /// <returns>The text, or <see langword="null"/> when PDFium reports nothing.</returns>
    internal static string? ReadUtf16(nint handle, delegate*<nint, void*, CULong, CULong> read)
    {
        var length = checked((int)read(handle, null, default).Value);
        if (length < Utf16TerminatorBytes)
        {
            return null;
        }

        var buffer = new byte[length];
        fixed (byte* data = buffer)
        {
            _ = read(handle, data, new((nuint)length));
        }

        return Encoding.Unicode.GetString(buffer, 0, length - Utf16TerminatorBytes);
    }

    /// <summary>Reads a UTF-16LE string that a two-handle function copies.</summary>
    /// <param name="first">The first native object, such as the form.</param>
    /// <param name="second">The second native object, such as the annotation.</param>
    /// <param name="read">The PDFium function.</param>
    /// <returns>The text, or <see langword="null"/> when PDFium reports nothing.</returns>
    internal static string? ReadUtf16(nint first, nint second, delegate*<nint, nint, void*, CULong, CULong> read)
    {
        var length = checked((int)read(first, second, null, default).Value);
        if (length < Utf16TerminatorBytes)
        {
            return null;
        }

        var buffer = new byte[length];
        fixed (byte* data = buffer)
        {
            _ = read(first, second, data, new((nuint)length));
        }

        return Encoding.Unicode.GetString(buffer, 0, length - Utf16TerminatorBytes);
    }

    /// <summary>Reads a NUL-terminated byte string that a one-handle function copies.</summary>
    /// <param name="handle">The native object.</param>
    /// <param name="read">The PDFium function.</param>
    /// <returns>The bytes without the terminator, or <see langword="null"/> when PDFium reports nothing.</returns>
    internal static byte[]? ReadBytes(nint handle, delegate*<nint, void*, CULong, CULong> read)
    {
        var length = checked((int)read(handle, null, default).Value);
        if (length < 1)
        {
            return null;
        }

        var buffer = new byte[length];
        fixed (byte* data = buffer)
        {
            _ = read(handle, data, new((nuint)length));
        }

        return buffer.AsSpan(0, length - 1).ToArray();
    }
}
