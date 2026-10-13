// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>Receives what a page edit or extraction changed that the user should know. One report is for one operation.</summary>
[DebuggerDisplay("PageEditReport: {SeparationChanges.Count} separation changes")]
public sealed class PageEditReport
{
    /// <summary>Gets or sets the printed pages whose separation pages were not all kept together; empty when none.</summary>
    public IReadOnlyList<SeparationChange> SeparationChanges { get; set; } = [];

    /// <summary>
    /// Gets or sets the warnings, in plain words, about trap networks the copy left out because the pages they were made
    /// for had changed; empty when none were. HyperPDF cannot make new traps.
    /// </summary>
    public IReadOnlyList<string> TrapNetworkWarnings { get; set; } = [];
}
