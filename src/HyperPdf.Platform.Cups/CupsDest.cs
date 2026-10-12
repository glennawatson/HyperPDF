// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdf.Platform.Cups;

/// <summary>Native <c>cups_dest_t</c>: a printer or class, with its options.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct CupsDest : IEquatable<CupsDest>
{
    /// <summary>The UTF-8 queue name.</summary>
    private readonly nint _name;

    /// <summary>The UTF-8 instance name, or null.</summary>
    private readonly nint _instance;

    /// <summary>Non-zero for the default destination.</summary>
    private readonly int _isDefault;

    /// <summary>The number of options.</summary>
    private readonly int _optionCount;

    /// <summary>The options.</summary>
    private readonly nint _options;

    /// <summary>Gets the queue name.</summary>
    internal string? Name => Marshal.PtrToStringUTF8(_name);

    /// <summary>Gets a value indicating whether this is an instance (a saved set of options) rather than the printer itself.</summary>
    internal bool IsInstance => _instance != 0;

    /// <summary>Gets a value indicating whether this is the default printer.</summary>
    internal bool IsDefault => _isDefault != 0;

    /// <summary>Compares two destinations.</summary>
    /// <param name="left">The left destination.</param>
    /// <param name="right">The right destination.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(CupsDest left, CupsDest right) => left.Equals(right);

    /// <summary>Compares two destinations.</summary>
    /// <param name="left">The left destination.</param>
    /// <param name="right">The right destination.</param>
    /// <returns><see langword="true"/> when different.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(CupsDest left, CupsDest right) => !left.Equals(right);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(CupsDest other) => _name == other._name && _instance == other._instance;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => obj is CupsDest other && Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(_name, _instance);

    /// <summary>Gets an option's value.</summary>
    /// <param name="name">The option name.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    internal unsafe string? GetOption(string name)
    {
        var options = (CupsOption*)_options;
        for (var i = 0; i < _optionCount; i++)
        {
            if (options[i].Name == name)
            {
                return options[i].Value;
            }
        }

        return null;
    }
}
