// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Printing;

namespace HyperPdf.Core.Platform;

/// <summary>How a print job is printed.</summary>
/// <param name="Printer">The printer's queue name.</param>
/// <param name="Copies">The number of copies.</param>
/// <param name="Colour">Whether to print in colour rather than black and white.</param>
/// <param name="TwoSided">Whether to print on both sides.</param>
/// <param name="Paper">The paper.</param>
[DebuggerDisplay("PrintJobOptions: {Printer} x{Copies}")]
public readonly record struct PrintJobOptions(string Printer, int Copies, bool Colour, bool TwoSided, PaperSize Paper)
{
    /// <summary>Gets the edge used to turn two-sided sheets.</summary>
    public DuplexBinding Binding { get; init; }
}
