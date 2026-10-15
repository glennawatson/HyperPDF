// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Writing;

namespace HyperPdf.Engine;

/// <summary>
/// Hands out the writer's report for a save and takes it back after the save has been copied to the caller's report, so
/// saving does not make a report each time. One spare is kept; a save that finds it taken makes its own.
/// </summary>
internal static class SaveReportPool
{
    /// <summary>Takes a clear report.</summary>
    /// <param name="spare">The owning document's spare report.</param>
    /// <returns>The spare, or a new report when the spare is in use.</returns>
    internal static PdfSaveReport Rent(ref PdfSaveReport? spare) => Interlocked.Exchange(ref spare, null) ?? new();

    /// <summary>Clears a report and keeps it for the next save.</summary>
    /// <param name="spare">The owning document's spare report.</param>
    /// <param name="report">The report, which nothing may read afterwards.</param>
    internal static void Return(ref PdfSaveReport? spare, PdfSaveReport report)
    {
        report.Reset();
        Volatile.Write(ref spare, report);
    }
}
