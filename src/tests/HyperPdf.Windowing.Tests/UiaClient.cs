// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace HyperPdf.Windowing.Tests;

/// <summary>A minimal UI Automation client built on the C API of the operating system, to read a window the way a screen reader does.</summary>
[SupportedOSPlatform("windows")]
internal static unsafe class UiaClient
{
    /// <summary>The size of a VARIANT in bytes on 64-bit Windows.</summary>
    private const int VariantSize = 24;

    /// <summary>The offset of the value in a VARIANT.</summary>
    private const int VariantDataOffset = 8;

    /// <summary>The VT_I4 type tag.</summary>
    private const ushort Int32Type = 3;

    /// <summary>The VT_BSTR type tag.</summary>
    private const ushort StringType = 8;

    /// <summary>Reads a string property of the element at the root of a window.</summary>
    /// <param name="window">The window handle.</param>
    /// <param name="propertyId">The UI Automation property id.</param>
    /// <returns>The value, or <see langword="null"/> when the element or the property is not there.</returns>
    internal static string? ReadString(long window, int propertyId)
    {
        byte* variant = stackalloc byte[VariantSize];
        return TryRead((nint)window, propertyId, variant, out var type) && type == StringType
            ? Marshal.PtrToStringBSTR(*(nint*)(variant + VariantDataOffset))
            : null;
    }

    /// <summary>Reads an integer property of the element at the root of a window.</summary>
    /// <param name="window">The window handle.</param>
    /// <param name="propertyId">The UI Automation property id.</param>
    /// <returns>The value, or <see langword="null"/> when the element or the property is not there.</returns>
    internal static int? ReadInt32(long window, int propertyId)
    {
        byte* variant = stackalloc byte[VariantSize];
        return TryRead((nint)window, propertyId, variant, out var type) && type == Int32Type
            ? *(int*)(variant + VariantDataOffset)
            : null;
    }

    /// <summary>Reads a property into a VARIANT buffer.</summary>
    /// <param name="window">The window.</param>
    /// <param name="propertyId">The property id.</param>
    /// <param name="variant">The 24 byte VARIANT buffer.</param>
    /// <param name="type">Receives the VARIANT type tag.</param>
    /// <returns><see langword="true"/> when UI Automation answered.</returns>
    private static bool TryRead(nint window, int propertyId, byte* variant, out ushort type)
    {
        type = 0;
        new Span<byte>(variant, VariantSize).Clear();
        if (NativeMethods.NodeFromHandle(window, out var node) < 0 || node == 0)
        {
            return false;
        }

        try
        {
            if (NativeMethods.GetPropertyValue(node, propertyId, variant) < 0)
            {
                return false;
            }

            type = *(ushort*)variant;
            return true;
        }
        finally
        {
            _ = NativeMethods.NodeRelease(node);
        }
    }
}
