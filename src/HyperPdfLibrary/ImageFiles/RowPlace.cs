// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>One stored row of an interlace pass and the picture row it belongs to.</summary>
/// <param name="Pixels">The pixels in the stored row.</param>
/// <param name="Row">The picture row.</param>
[DebuggerDisplay("RowPlace: {Pixels} pixels on row {Row}")]
internal readonly record struct RowPlace(int Pixels, int Row);
