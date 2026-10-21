// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Rendering;
using HyperPdf.Core.Text.Fonts;
using HyperPdf.Engine;

namespace HyperPdf.ViewModels.Services;

/// <summary>Opens documents with HyperPDF and applies the current fillable field tint.</summary>
[DebuggerDisplay("HighlightedHyperPdfEngine")]
internal sealed class HighlightedHyperPdfEngine : IDocumentEngine
{
    /// <summary>The engine that opens every document.</summary>
    private readonly HyperPdfEngine _engine;

    /// <summary>Reads the tint over fillable form fields that each document opens with.</summary>
    private readonly Func<FormHighlight> _highlight;

    /// <summary>Reads the page sharpness choices that each document opens with.</summary>
    private readonly Func<RenderSharpness> _sharpness;

    /// <summary>Initializes a new instance of the <see cref="HighlightedHyperPdfEngine"/> class.</summary>
    /// <param name="highlight">Reads the tint over fillable form fields.</param>
    /// <param name="sharpness">Reads the page sharpness choices.</param>
    internal HighlightedHyperPdfEngine(Func<FormHighlight> highlight, Func<RenderSharpness> sharpness)
        : this(highlight, sharpness, new HyperPdfEngine())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HighlightedHyperPdfEngine"/> class with a shared font catalog.</summary>
    /// <param name="highlight">Reads the tint over fillable form fields.</param>
    /// <param name="sharpness">Reads the page sharpness choices.</param>
    /// <param name="catalog">The installed fonts, scanned on first use.</param>
    internal HighlightedHyperPdfEngine(Func<FormHighlight> highlight, Func<RenderSharpness> sharpness, Lazy<FontCatalog> catalog)
        : this(highlight, sharpness, HyperPdfEngine.WithFontCatalog(catalog))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HighlightedHyperPdfEngine"/> class around one engine.</summary>
    /// <param name="highlight">Reads the tint over fillable form fields.</param>
    /// <param name="sharpness">Reads the page sharpness choices.</param>
    /// <param name="engine">The document engine.</param>
    private HighlightedHyperPdfEngine(Func<FormHighlight> highlight, Func<RenderSharpness> sharpness, HyperPdfEngine engine)
    {
        ArgumentNullException.ThrowIfNull(highlight);
        ArgumentNullException.ThrowIfNull(sharpness);
        _engine = engine;
        _highlight = highlight;
        _sharpness = sharpness;
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

        if (DocumentFeature<IRenderSharpness>.Get(document) is { } sharp)
        {
            sharp.Sharpness = _sharpness();
        }

        return document;
    }
}
