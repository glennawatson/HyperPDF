// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>A paragraph. Line breaks in the text start new lines; the text wraps to the page width.</summary>
/// <param name="Text">The paragraph's text.</param>
[DebuggerDisplay("PdfFlowParagraph: {Text}")]
public sealed record PdfFlowParagraph(string Text) : PdfFlowBlock;
