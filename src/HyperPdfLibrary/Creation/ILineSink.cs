// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Creation;

/// <summary>
/// Receives the lines <see cref="PdfTextWrapper"/> breaks text into. Implementations are structs, which may be ref structs,
/// so the wrapper is specialised for each and nothing is boxed.
/// </summary>
internal interface ILineSink
{
    /// <summary>Adds a line.</summary>
    /// <param name="line">The line.</param>
    void Add(PdfTextLine line);
}
