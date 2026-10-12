// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;

namespace HyperPdf.Engine;

/// <summary>Provides IRepairReport through the owning document.</summary>
internal sealed class HyperPdfRepairReportService : IRepairReport
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfRepairReportService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfRepairReportService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    public bool WasRepaired { get => HyperPdfDocumentRepairs.GetWasRepaired(_owner); }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IReadOnlyList<RepairNote> GetRepairs() => HyperPdfDocumentRepairs.GetRepairs(_owner);
}
