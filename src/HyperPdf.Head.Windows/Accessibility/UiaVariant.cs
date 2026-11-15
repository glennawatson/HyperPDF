// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HyperPdf.Head.Windows.Accessibility;

/// <summary>The 24 byte VARIANT UI Automation reads property values from. Strings are BSTRs the caller frees.</summary>
[StructLayout(LayoutKind.Sequential)]
[DebuggerDisplay("UiaVariant type {Type}")]
internal readonly record struct UiaVariant
{
    /// <summary>The VT_I4 type tag.</summary>
    private const ushort Int32Type = 3;

    /// <summary>The VT_BSTR type tag.</summary>
    private const ushort StringType = 8;

    /// <summary>The VT_BOOL type tag.</summary>
    private const ushort BooleanType = 11;

    /// <summary>The VARIANT_TRUE value.</summary>
    private const long VariantTrue = -1;

    /// <summary>Gets the type tag, where 0 means empty.</summary>
    internal ushort Type { get; private init; }

    /// <summary>Gets the first reserved word.</summary>
    internal ushort Reserved1 { get; private init; }

    /// <summary>Gets the second reserved word.</summary>
    internal ushort Reserved2 { get; private init; }

    /// <summary>Gets the third reserved word.</summary>
    internal ushort Reserved3 { get; private init; }

    /// <summary>Gets the first half of the value.</summary>
    internal long Data { get; private init; }

    /// <summary>Gets the second half of the value, which only structures use.</summary>
    internal long Data2 { get; private init; }

    /// <summary>Makes an integer variant.</summary>
    /// <param name="value">The integer.</param>
    /// <returns>The variant.</returns>
    internal static UiaVariant FromInt32(int value) => new() { Type = Int32Type, Data = value };

    /// <summary>Makes a boolean variant.</summary>
    /// <param name="value">The boolean.</param>
    /// <returns>The variant.</returns>
    internal static UiaVariant FromBoolean(bool value) => new() { Type = BooleanType, Data = value ? VariantTrue : 0 };

    /// <summary>Makes a string variant holding a new BSTR, which UI Automation frees.</summary>
    /// <param name="value">The string.</param>
    /// <returns>The variant.</returns>
    internal static UiaVariant FromString(string value) => new() { Type = StringType, Data = Marshal.StringToBSTR(value) };
}
