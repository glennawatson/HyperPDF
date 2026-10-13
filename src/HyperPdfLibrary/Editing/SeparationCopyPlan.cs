// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Features;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>The separation dictionary each copied page gets, by output position.</summary>
/// <param name="Links">For each copied page, the output positions its <c>/Pages</c> array lists, or <see langword="null"/> to leave the dictionary out.</param>
/// <param name="Pages">For each linked page, its colourant and colour space.</param>
/// <param name="Sources">For each linked page, its source separation dictionary.</param>
/// <param name="ColorantKey">The source's <c>/DeviceColorant</c> name.</param>
[DebuggerDisplay("SeparationCopyPlan: {Links.Length} pages")]
internal sealed record SeparationCopyPlan(int[]?[] Links, PdfSeparationPage?[] Pages, PdfDictionary?[] Sources, PdfName ColorantKey);
