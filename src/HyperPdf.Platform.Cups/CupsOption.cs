// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdf.Platform.Cups;

/// <summary>Native <c>cups_option_t</c>: a name and value, both UTF-8 strings.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct CupsOption : IEquatable<CupsOption>
{
    /// <summary>The UTF-8 name.</summary>
    private readonly nint _name;

    /// <summary>The UTF-8 value.</summary>
    private readonly nint _value;

    /// <summary>Initializes a new instance of the <see cref="CupsOption"/> struct.</summary>
    /// <param name="name">The UTF-8 name, owned by the caller.</param>
    /// <param name="value">The UTF-8 value, owned by the caller.</param>
    internal CupsOption(nint name, nint value)
    {
        _name = name;
        _value = value;
    }

    /// <summary>Gets the name.</summary>
    internal string? Name => Marshal.PtrToStringUTF8(_name);

    /// <summary>Gets the value.</summary>
    internal string? Value => Marshal.PtrToStringUTF8(_value);

    /// <summary>Gets the native name pointer, for freeing options the app allocated.</summary>
    internal nint NamePointer => _name;

    /// <summary>Gets the native value pointer, for freeing options the app allocated.</summary>
    internal nint ValuePointer => _value;

    /// <summary>Compares two options.</summary>
    /// <param name="left">The left option.</param>
    /// <param name="right">The right option.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(CupsOption left, CupsOption right) => left.Equals(right);

    /// <summary>Compares two options.</summary>
    /// <param name="left">The left option.</param>
    /// <param name="right">The right option.</param>
    /// <returns><see langword="true"/> when different.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(CupsOption left, CupsOption right) => !left.Equals(right);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(CupsOption other) => _name == other._name && _value == other._value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => obj is CupsOption other && Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(_name, _value);
}
