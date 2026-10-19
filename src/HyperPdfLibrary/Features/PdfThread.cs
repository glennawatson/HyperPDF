// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Features;

/// <summary>An article thread: a chain of beads that reading follows across pages.</summary>
/// <param name="Title">The thread's title (<c>/I /Title</c>), or null.</param>
/// <param name="Beads">The beads in reading order.</param>
/// <param name="Dictionary">The thread dictionary.</param>
[DebuggerDisplay("PdfThread: {Title} {Beads.Length} beads")]
public sealed record PdfThread(string? Title, PdfBead[] Beads, PdfDictionary Dictionary);
