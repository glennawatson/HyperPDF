// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.TestAssets;

/// <summary>A JBIG2 stream as a PDF holds it, with the hash of its decoded image.</summary>
/// <param name="Name">The sample's name.</param>
/// <param name="Width">The image width from the PDF image dictionary.</param>
/// <param name="Height">The image height from the PDF image dictionary.</param>
/// <param name="Data">The page segments.</param>
/// <param name="Globals">The /JBIG2Globals segments, or an empty array.</param>
/// <param name="Sha256">The lowercase hex SHA-256 of the decoded rows: <c>(width + 7) / 8</c> bytes each, 1 for white.</param>
[DebuggerDisplay("Jbig2Sample: {Name} {Width}x{Height}")]
public sealed record Jbig2Sample(string Name, int Width, int Height, byte[] Data, byte[] Globals, string Sha256);
