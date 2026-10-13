// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Creation;

/// <summary>The name a page's resource dictionary gives a font, image, shading, pattern or other resource, as content operators use it.</summary>
[DebuggerDisplay("PdfResourceName: {Value}")]
public readonly struct PdfResourceName : IEquatable<PdfResourceName>
{
    /// <summary>The name as UTF-8 bytes, without the slash.</summary>
    private readonly byte[]? _utf8;

    /// <summary>Initializes a new instance of the <see cref="PdfResourceName"/> struct.</summary>
    /// <param name="value">The name without the slash.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is <see langword="null"/> or empty.</exception>
    public PdfResourceName(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        Value = value;
        _utf8 = Encoding.UTF8.GetBytes(value);
    }

    /// <summary>Gets the name without the slash, or <see langword="null"/> for a default value.</summary>
    public string? Value { get; }

    /// <summary>Gets the name as UTF-8 bytes, which is what <see cref="PdfContentBuilder"/> takes.</summary>
    public ReadOnlySpan<byte> Utf8 => _utf8;

    /// <summary>Determines whether two names are the same.</summary>
    /// <param name="left">The first name.</param>
    /// <param name="right">The second name.</param>
    /// <returns><see langword="true"/> when they spell the same name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(PdfResourceName left, PdfResourceName right) => left.Equals(right);

    /// <summary>Determines whether two names differ.</summary>
    /// <param name="left">The first name.</param>
    /// <param name="right">The second name.</param>
    /// <returns><see langword="true"/> when they spell different names.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(PdfResourceName left, PdfResourceName right) => !left.Equals(right);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(PdfResourceName other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PdfResourceName other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Value?.GetHashCode(StringComparison.Ordinal) ?? 0;

    /// <inheritdoc/>
    public override string ToString() => Value ?? string.Empty;
}
