// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdf.Platform.Windows;

/// <summary>Native <c>DOCINFOW</c>: a print job's name.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct DocumentInfo : IEquatable<DocumentInfo>
{
    /// <summary>The structure size.</summary>
    private readonly int _size;

    /// <summary>The UTF-16 job name, owned by the caller.</summary>
    private readonly nint _documentName;

    /// <summary>The output file, null for the printer.</summary>
    private readonly nint _output;

    /// <summary>The data type, null.</summary>
    private readonly nint _dataType;

    /// <summary>Extra flags, zero.</summary>
    private readonly uint _type;

    /// <summary>Initializes a new instance of the <see cref="DocumentInfo"/> struct.</summary>
    /// <param name="documentName">The UTF-16, null terminated job name, pinned by the caller.</param>
    internal unsafe DocumentInfo(nint documentName)
    {
        _size = sizeof(DocumentInfo);
        _documentName = documentName;
        _output = 0;
        _dataType = 0;
        _type = 0;
    }

    /// <summary>Compares two values.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(DocumentInfo left, DocumentInfo right) => left.Equals(right);

    /// <summary>Compares two values.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns><see langword="true"/> when different.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(DocumentInfo left, DocumentInfo right) => !left.Equals(right);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(DocumentInfo other) => _size == other._size && _documentName == other._documentName && _output == other._output && _dataType == other._dataType && _type == other._type;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => obj is DocumentInfo other && Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(_size, _documentName, _output, _dataType, _type);
}
