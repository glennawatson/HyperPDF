// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>An immutable snapshot of the compressed font programs that <see cref="FontStreamCache"/> keeps, newest first.</summary>
/// <param name="Entries">The kept programs.</param>
/// <param name="Bytes">The bytes the entries hold, counting each source and its compressed form.</param>
[DebuggerDisplay("FontStreamCacheState: {Entries.Length} entries, {Bytes} bytes")]
internal sealed record FontStreamCacheState(FontStreamCacheEntry[] Entries, long Bytes)
{
    /// <summary>Gets a snapshot with no entries.</summary>
    internal static FontStreamCacheState Empty { get; } = new([], 0);
}
