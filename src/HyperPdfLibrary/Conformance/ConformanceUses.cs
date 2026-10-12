// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// The resources a document's content uses for rendering. ISO 19005-1 Cor.2 §6.2.10 exempts named resources that the
/// associated content does not use, so the conformance reading report counts only these.
/// </summary>
/// <param name="Fonts">The font dictionaries selected with <c>Tf</c> or a graphics state's <c>/Font</c>.</param>
/// <param name="Forms">The form XObjects drawn with <c>Do</c>.</param>
/// <param name="Images">The image XObject dictionaries drawn with <c>Do</c>.</param>
/// <param name="GraphicsStates">The graphics state dictionaries set with <c>gs</c>.</param>
/// <param name="TilingPatterns">The tiling pattern streams painted with <c>scn</c> or <c>SCN</c>.</param>
/// <param name="ColorOperators">Whether drawn content sets a device colour or selects a device colour space.</param>
[DebuggerDisplay("ConformanceUses: {Fonts.Count} fonts, {Forms.Count} forms, {Images.Count} images")]
internal sealed record ConformanceUses(
    List<PdfDictionary> Fonts,
    List<PdfStream> Forms,
    List<PdfDictionary> Images,
    List<PdfDictionary> GraphicsStates,
    List<PdfStream> TilingPatterns,
    bool ColorOperators);
