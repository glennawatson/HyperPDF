// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdfLibrary.Content;

/// <summary>A reader's view of a page's decoded content. It returns the array it owns, if any, when disposed; content a <see cref="PdfPageContentScope"/> keeps is returned by the scope.</summary>
/// <param name="Array">The rented array, or <see langword="null"/> for empty content.</param>
/// <param name="Length">The bytes of content.</param>
/// <param name="Owns">Whether disposing the lease returns the array.</param>
internal readonly record struct PageContentLease(byte[]? Array, int Length, bool Owns) : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="PageContentLease"/> struct that owns its array.</summary>
    /// <param name="array">The rented array, or <see langword="null"/> for empty content.</param>
    /// <param name="length">The bytes of content.</param>
    internal PageContentLease(byte[]? array, int length)
        : this(array, length, true)
    {
    }

    /// <summary>Gets the decoded content; valid until the lease, or the scope that kept it, is disposed.</summary>
    internal ReadOnlySpan<byte> Content => Array is null ? [] : Array.AsSpan(0, Length);

    /// <summary>Returns the array to the pool when the lease owns it.</summary>
    public void Dispose()
    {
        if (Owns && Array is not null)
        {
            ArrayPool<byte>.Shared.Return(Array);
        }
    }
}
