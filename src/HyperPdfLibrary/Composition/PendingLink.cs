// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>A link drawn on the page being written, waiting for its annotation to be added when the page ends.</summary>
/// <param name="Box">The clickable area.</param>
/// <param name="Target">Where the link goes.</param>
/// <param name="Node">The structure node of the link, or <see langword="null"/> when the document is not tagged.</param>
[DebuggerDisplay("PendingLink: {Target}")]
internal readonly record struct PendingLink(PdfCanvasBox Box, PdfLinkTarget Target, StructNode? Node);
