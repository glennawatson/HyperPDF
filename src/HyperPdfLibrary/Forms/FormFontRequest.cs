// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>What is needed to make a <see cref="FormFont"/> from a font dictionary.</summary>
/// <param name="Store">The document's objects.</param>
/// <param name="Key">The font's name in the resources.</param>
/// <param name="Resource">The resources entry: a reference or the font dictionary.</param>
/// <param name="Fonts">The document's font cache, or <see langword="null"/>.</param>
[DebuggerDisplay("FormFontRequest: {Key}")]
internal readonly record struct FormFontRequest(PdfObjectStore Store, PdfName Key, PdfValue Resource, PdfFontCache? Fonts);
