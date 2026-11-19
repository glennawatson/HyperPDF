// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdfLibrary.Document;

namespace HyperPdf.Engine;

/// <summary>Reads and sets the ranges of page labels of a document.</summary>
internal sealed class HyperPdfPageLabelerService : IPageLabeler
{
    /// <summary>The document.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfPageLabelerService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfPageLabelerService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    public PageLabelRun[] GetRanges()
    {
        lock (_owner.EditGate)
        {
            ObjectDisposedException.ThrowIf(_owner.IsDisposed, _owner);
            var found = PdfDocumentPageLabels.GetRanges(_owner.Document);
            var result = new PageLabelRun[found.Length];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = new(found[i].StartPage, (PageLabelStyle)(int)found[i].Style, found[i].Prefix, found[i].FirstNumber);
            }

            return result;
        }
    }

    /// <inheritdoc/>
    public void SetRanges(PageLabelRun[] ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        var converted = new PdfPageLabelRange[ranges.Length];
        for (var i = 0; i < converted.Length; i++)
        {
            converted[i] = new(ranges[i].StartPage, (PdfPageLabelStyle)(int)ranges[i].Style, ranges[i].Prefix, ranges[i].FirstNumber);
        }

        lock (_owner.EditGate)
        {
            ObjectDisposedException.ThrowIf(_owner.IsDisposed, _owner);
            using var access = HyperPdfNavigation.EnterPageWrite(_owner);
            PdfDocumentPageLabels.SetRanges(_owner.Document, converted);
            HyperPdfNavigation.PagesEdited(_owner);
        }
    }
}
