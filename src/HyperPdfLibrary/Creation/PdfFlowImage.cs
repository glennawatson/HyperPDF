// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>An image on a line of its own.</summary>
/// <param name="Image">The image, added to the document first.</param>
/// <param name="AlternativeText">What the image shows, for people who cannot see it. An image with no text is treated as decoration and left out of the structure.</param>
/// <param name="WidthPoints">The width to draw it at, or zero for its pixel width at 96 pixels an inch; it is never wider than the page's text.</param>
[DebuggerDisplay("PdfFlowImage: {Image}, {AlternativeText}")]
public sealed record PdfFlowImage(PdfCreatedImage Image, string AlternativeText, float WidthPoints) : PdfFlowBlock;
