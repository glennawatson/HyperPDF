// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>
/// A COM <c>VARIANT</c> as UI Automation passes it. The layout is the native one: a type in the first two bytes, and the value in
/// the eight bytes that start at offset 8. UI Automation frees the string or array a variant holds when it has copied the value.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 24)]
[DebuggerDisplay("ComVariant: type {_type}")]
internal readonly struct ComVariant : IEquatable<ComVariant>
{
    /// <summary>The type of a 32 bit integer.</summary>
    private const ushort Int32Type = 3;

    /// <summary>The type of a 64 bit float.</summary>
    private const ushort DoubleType = 5;

    /// <summary>The type of a string.</summary>
    private const ushort StringType = 8;

    /// <summary>The type of a Boolean.</summary>
    private const ushort BooleanType = 11;

    /// <summary>The type of an object.</summary>
    private const ushort UnknownType = 13;

    /// <summary>The value of a true Boolean, all bits set.</summary>
    private const long True = -1;

    /// <summary>The variant's type.</summary>
    [FieldOffset(0)]
    private readonly ushort _type;

    /// <summary>The variant's value, or a pointer to it.</summary>
    [FieldOffset(8)]
    private readonly long _data;

    /// <summary>Initializes a new instance of the <see cref="ComVariant"/> struct.</summary>
    /// <param name="type">The type.</param>
    /// <param name="data">The value or the pointer.</param>
    private ComVariant(ushort type, long data)
    {
        _type = type;
        _data = data;
    }

    /// <summary>Checks whether two variants hold the same type and value.</summary>
    /// <param name="left">The first variant.</param>
    /// <param name="right">The second variant.</param>
    /// <returns>Whether they are equal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(ComVariant left, ComVariant right) => left.Equals(right);

    /// <summary>Checks whether two variants differ.</summary>
    /// <param name="left">The first variant.</param>
    /// <param name="right">The second variant.</param>
    /// <returns>Whether they differ.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(ComVariant left, ComVariant right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(ComVariant other) => _type == other._type && _data == other._data;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ComVariant other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_type, _data);

    /// <summary>Makes a variant that holds a 32 bit integer.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The variant.</returns>
    internal static ComVariant FromInt32(int value) => new(Int32Type, value);

    /// <summary>Makes a variant that holds a Boolean.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The variant.</returns>
    internal static ComVariant FromBoolean(bool value) => new(BooleanType, value ? True : 0);

    /// <summary>Makes a variant that holds a 64 bit float.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The variant.</returns>
    internal static ComVariant FromDouble(double value) => new(DoubleType, BitConverter.DoubleToInt64Bits(value));

    /// <summary>Makes a variant that holds a string; the receiver frees it.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The variant.</returns>
    internal static ComVariant FromString(string value) => new(StringType, Marshal.StringToBSTR(value));

    /// <summary>Makes a variant that holds an object; the receiver releases it.</summary>
    /// <param name="pointer">The object's <c>IUnknown</c>, already counted for this variant.</param>
    /// <returns>The variant.</returns>
    internal static ComVariant FromUnknown(nint pointer) => new(UnknownType, pointer);

    /// <summary>Frees the string a variant holds, for a variant the caller kept ownership of. Other kinds hold nothing to free.</summary>
    internal void Free()
    {
        if (_type == StringType)
        {
            Marshal.FreeBSTR((nint)_data);
        }
    }
}
