// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Platform;

/// <summary>
/// Prints PDF files: straight to a printer's queue with chosen settings, like a browser, or through the desktop's own
/// print dialog when people want its extra settings.
/// </summary>
public interface IPrintService
{
    /// <summary>Gets a value indicating whether printing is available on this desktop.</summary>
    bool IsAvailable { get; }

    /// <summary>Hands a PDF file to the desktop's own print dialog.</summary>
    /// <param name="filePath">The PDF to print; it must stay on disk until the task completes.</param>
    /// <param name="title">The job title, normally the document's file name.</param>
    /// <param name="cancellationToken">Cancels waiting for the desktop.</param>
    /// <returns><see langword="true"/> when the desktop accepted the file.</returns>
    Task<bool> PrintAsync(string filePath, string title, CancellationToken cancellationToken);

    /// <summary>Gets the printers that jobs can be sent to directly.</summary>
    /// <returns>The printers; empty when the print system is not available.</returns>
    IReadOnlyList<PrinterInfo> GetPrinters();

    /// <summary>Sends a PDF straight to a printer's queue.</summary>
    /// <param name="filePath">The PDF; it may be deleted once the task completes.</param>
    /// <param name="title">The job title, normally the document's file name.</param>
    /// <param name="options">The printer and settings.</param>
    /// <param name="cancellationToken">Cancels waiting for the print system.</param>
    /// <returns>Whether the queue accepted the job.</returns>
    Task<PrintOutcome> SubmitAsync(string filePath, string title, PrintJobOptions options, CancellationToken cancellationToken);
}
