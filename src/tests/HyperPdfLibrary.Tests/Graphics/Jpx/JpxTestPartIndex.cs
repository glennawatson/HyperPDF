// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>A tile-part's index within its tile, and the tile's tile-part count.</summary>
/// <param name="Index">The tile-part index.</param>
/// <param name="Count">The tile-parts of the tile.</param>
internal readonly record struct JpxTestPartIndex(int Index, int Count);
