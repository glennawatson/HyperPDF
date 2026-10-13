// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Features;

/// <summary>One page of a preseparated document: the page that paints a single colourant of a printed page.</summary>
/// <param name="PageIndex">The zero based page index.</param>
/// <param name="Colorant">The device colourant (<c>/DeviceColorant</c>), for example Cyan; empty when the entry is missing.</param>
/// <param name="SpaceKind">The kind of colour space <c>/ColorSpace</c> gives for previewing the colourant.</param>
/// <param name="SpaceColorants">The colourant names of that colour space, in order; empty when it has none.</param>
[DebuggerDisplay("PdfSeparationPage: {PageIndex} {Colorant}")]
public sealed record PdfSeparationPage(int PageIndex, string Colorant, PdfSeparationSpaceKind SpaceKind, string[] SpaceColorants);
