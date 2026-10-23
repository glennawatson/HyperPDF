// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>A named place in the text that a <see cref="PdfHyperlink"/> or an outline entry can jump to. It takes no space.</summary>
/// <param name="Name">The bookmark's name, which must be unique in the document.</param>
[DebuggerDisplay("PdfBookmark: {Name}")]
public sealed record PdfBookmark(string Name) : PdfInline;
