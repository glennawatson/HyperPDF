// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>One font program and its Flate-compressed form.</summary>
/// <param name="Hash">The hash of <paramref name="Source"/>, compared before the bytes.</param>
/// <param name="Source">The uncompressed program, compared byte for byte so a hash collision cannot return the wrong stream.</param>
/// <param name="Compressed">The compressed program, shared by every stream made from it and never changed.</param>
[DebuggerDisplay("FontStreamCacheEntry: {Source.Length} to {Compressed.Length} bytes")]
internal sealed record FontStreamCacheEntry(int Hash, byte[] Source, byte[] Compressed);
