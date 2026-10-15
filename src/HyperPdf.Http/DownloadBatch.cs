// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdf.Http;

/// <summary>A snapshot of missing downloads held in a rented array until the operation finishes.</summary>
/// <typeparam name="T">The catalogue entry type.</typeparam>
/// <param name="Items">The rented entries, or an empty array.</param>
/// <param name="Count">The entries used.</param>
/// <param name="Bytes">The bytes expected in the entries.</param>
internal readonly record struct DownloadBatch<T>(T[] Items, int Count, long Bytes) : IDisposable
{
    /// <inheritdoc/>
    public void Dispose()
    {
        if (Items.Length > 0)
        {
            ArrayPool<T>.Shared.Return(Items, true);
        }
    }
}
