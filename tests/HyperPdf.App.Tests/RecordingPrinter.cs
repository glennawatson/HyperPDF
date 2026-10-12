// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Platform;
using HyperPdf.Engine;

namespace HyperPdf.App.Tests;

/// <summary>A print service that opens the file it is given and counts its annotations.</summary>
internal sealed class RecordingPrinter : IPrintService
{
    /// <summary>The test printer's queue name.</summary>
    internal const string PrinterQueue = "office";

    /// <summary>The test printer's name.</summary>
    internal const string PrinterName = "Office Printer";

    /// <summary>Gets the file handed over.</summary>
    public string? Path { get; private set; }

    /// <summary>Gets the job title.</summary>
    public string? Title { get; private set; }

    /// <summary>Gets the page count of the handed over file.</summary>
    public int PageCount { get; private set; }

    /// <summary>Gets the annotations found on the first page of the handed over file.</summary>
    public int AnnotationCount { get; private set; }

    /// <summary>Gets text present in the first printed page's content.</summary>
    public string PageText { get; private set; } = string.Empty;

    /// <summary>Gets or sets an area of the first page to check for ink when the file is handed over.</summary>
    public PageRect InkArea { get; set; }

    /// <summary>Gets the inked pixels found in <see cref="InkArea"/> when the first page is rendered for printing.</summary>
    public int InkPixels { get; private set; }

    /// <summary>Gets the job settings sent.</summary>
    public PrintJobOptions Options { get; private set; }

    /// <summary>Gets a value indicating whether the system dialog was used.</summary>
    public bool UsedDialog { get; private set; }

    /// <inheritdoc/>
    public bool IsAvailable => true;

    /// <inheritdoc/>
    public IReadOnlyList<PrinterInfo> GetPrinters() => [new(PrinterQueue, PrinterName, true)];

    /// <inheritdoc/>
    public Task<bool> PrintAsync(string filePath, string title, CancellationToken cancellationToken)
    {
        UsedDialog = true;
        Inspect(filePath, title);
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<PrintOutcome> SubmitAsync(string filePath, string title, PrintJobOptions options, CancellationToken cancellationToken)
    {
        Options = options;
        Inspect(filePath, title);
        return Task.FromResult(new PrintOutcome(true, string.Empty));
    }

    /// <summary>Opens the file handed over and records what is in it.</summary>
    /// <param name="filePath">The file.</param>
    /// <param name="title">The job title.</param>
    private void Inspect(string filePath, string title)
    {
        Path = filePath;
        Title = title;
        using var document = new HyperPdfEngine().Open(filePath, null);
        PageCount = document.PageCount;
        PageText = document.GetText(0, 0, document.GetCharacterCount(0));
        List<PageAnnotation> annotations = [];
        DocumentFeature<IAnnotationEditor>.Cast(document)!.GetAnnotations(0, annotations);
        AnnotationCount = annotations.Count;
        if (InkArea.Width > 0)
        {
            InkPixels = PageInk.Count(document, 0, InkArea, RenderFlags.Annotations | RenderFlags.Printing);
        }
    }
}
