// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics;

namespace HyperPdfLibrary.Portfolio;

/// <summary>The colours a portfolio asks for. Each is an RGB triple with components from 0 to 1, or null when not set.</summary>
/// <param name="Background">The background.</param>
/// <param name="CardBackground">The background of a file card.</param>
/// <param name="CardBorder">The border of a file card.</param>
/// <param name="PrimaryText">The main text.</param>
/// <param name="SecondaryText">The secondary text.</param>
[DebuggerDisplay("PdfPortfolioColors")]
public sealed record PdfPortfolioColors(
    ImmutableArray<double>? Background,
    ImmutableArray<double>? CardBackground,
    ImmutableArray<double>? CardBorder,
    ImmutableArray<double>? PrimaryText,
    ImmutableArray<double>? SecondaryText);
