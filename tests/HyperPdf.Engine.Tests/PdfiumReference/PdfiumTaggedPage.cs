// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Reading;

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>A page's tagged blocks as PDFium's structure tree and text page give them.</summary>
/// <param name="IsTrusted">Whether the document is tagged and its tags cover the page's text well enough to be used.</param>
/// <param name="Blocks">The block-level elements in logical order; empty when <paramref name="IsTrusted"/> is <see langword="false"/>.</param>
/// <param name="Unicodes">Every text page character's Unicode value, by character index.</param>
internal sealed record PdfiumTaggedPage(bool IsTrusted, TaggedBlock[] Blocks, uint[] Unicodes);
