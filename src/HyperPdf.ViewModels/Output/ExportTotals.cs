// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Output;

/// <summary>What the files of one page export hold so far, and why the export stopped when a file was refused.</summary>
internal sealed class ExportTotals
{
    /// <summary>Gets or sets the number of pieces the export left out.</summary>
    internal int Skipped { get; set; }

    /// <summary>Gets or sets the number of pieces the export approximated.</summary>
    internal int Approximated { get; set; }

    /// <summary>Gets or sets why the export stopped, or <see langword="null"/> when no file was refused.</summary>
    internal ExportPagesSummary? Refusal { get; set; }
}
