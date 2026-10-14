// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>A position on a page.</summary>
/// <param name="X">The column.</param>
/// <param name="Y">The row.</param>
internal readonly record struct Jbig2Point(int X, int Y);
