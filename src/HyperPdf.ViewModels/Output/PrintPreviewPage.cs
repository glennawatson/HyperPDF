// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;

namespace HyperPdf.ViewModels.Output;

/// <summary>One sheet of a print preview: a page of the file that will print.</summary>
/// <param name="Document">The preview document.</param>
/// <param name="PageIndex">The page in the preview document.</param>
/// <param name="Size">The page size in points.</param>
/// <param name="CaptionMessage">The caption under the sheet, such as "2 of 5".</param>
[DebuggerDisplay("PrintPreviewPage: {Caption}")]
public sealed record PrintPreviewPage(IDocument Document, int PageIndex, PageSize Size, LocalizedMessage CaptionMessage) : INotifyPropertyChanged, ILocalisationChildren
{
    /// <summary>Initializes a new instance of the <see cref="PrintPreviewPage"/> class.</summary>
    /// <param name="document">The preview document.</param>
    /// <param name="pageIndex">The zero based page.</param>
    /// <param name="size">The page size.</param>
    /// <param name="caption">The caption.</param>
    public PrintPreviewPage(IDocument document, int pageIndex, PageSize size, string caption)
        : this(document, pageIndex, size, LocalizedMessage.Literal(caption))
    {
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets or initializes the sheet caption; assigned text remains literal.</summary>
    public string Caption
    {
        get => CaptionMessage.Value ?? string.Empty;
        init => CaptionMessage = LocalizedMessage.Literal(value);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ILocalisationChildren.Refresh(HashSet<object> visited) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Caption)));
}
