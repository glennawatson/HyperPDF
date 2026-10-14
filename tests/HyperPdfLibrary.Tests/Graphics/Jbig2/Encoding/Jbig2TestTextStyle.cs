// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Graphics.Jbig2.Encoding;

/// <summary>How <see cref="Jbig2TestPages"/> draws a text page.</summary>
/// <param name="Scale">The pixels per glyph pixel.</param>
/// <param name="Seed">The random seed.</param>
/// <param name="StrayPixels">The number of stray black pixels added.</param>
internal readonly record struct Jbig2TestTextStyle(int Scale, int Seed, int StrayPixels);
