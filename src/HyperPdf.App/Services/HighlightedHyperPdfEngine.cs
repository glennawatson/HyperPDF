// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.Engine;

namespace HyperPdf.App.Services;

/// <summary>Opens documents with HyperPDF and applies the current fillable field tint.</summary>
[DebuggerDisplay("HighlightedHyperPdfEngine")]
internal sealed class HighlightedHyperPdfEngine : IDocumentEngine
{
    /// <summary>The engine that opens every document.</summary>
    private readonly HyperPdfEngine _engine = new();

    /// <summary>Reads the tint over fillable form fields that each document opens with.</summary>
    private readonly Func<FormHighlight> _highlight;

    /// <summary>Initializes a new instance of the <see cref="HighlightedHyperPdfEngine"/> class.</summary>
    /// <param name="highlight">Reads the tint over fillable form fields.</param>
    internal HighlightedHyperPdfEngine(Func<FormHighlight> highlight)
    {
        ArgumentNullException.ThrowIfNull(highlight);
        _highlight = highlight;
    }

    /// <inheritdoc/>
    public string Name => _engine.Name;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CanOpen(string path) => _engine.CanOpen(path);

    /// <inheritdoc/>
    public IDocument Open(string path, string? password)
    {
        var document = _engine.Open(path, password);
        if (DocumentFeature<IFormHighlight>.Get(document) is { } tinted)
        {
            tinted.Highlight = _highlight();
        }

        return document;
    }
}
