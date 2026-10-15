// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// Gives the alternate description of a figure that layout inference found, for example from a person or from an image
/// description service the host runs. It is called once for each figure.
/// </summary>
/// <param name="pageIndex">The zero based page index.</param>
/// <param name="bounds">The figure's box in viewer space.</param>
/// <param name="caption">The text of the caption linked to the figure, or <see langword="null"/> when it has none.</param>
/// <returns>The description, or <see langword="null"/> when the host has none yet.</returns>
public delegate string? PdfFigureAltTextProvider(int pageIndex, PdfViewerRect bounds, string? caption);
