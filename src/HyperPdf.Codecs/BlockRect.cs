// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs;

/// <summary>A rectangle of samples in a plane.</summary>
/// <param name="X">The first column.</param>
/// <param name="Y">The first row.</param>
/// <param name="Width">The width in samples.</param>
/// <param name="Height">The height in samples.</param>
internal readonly record struct BlockRect(int X, int Y, int Width, int Height);
