// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Text;

namespace HyperPdf.Engine;

/// <summary>Provides ITextBoxEditor through the owning document.</summary>
internal sealed class HyperPdfTextBoxEditorService : ITextBoxEditor
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfTextBoxEditorService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfTextBoxEditorService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int AddTextBox(int pageIndex, PagePoint location, float wrapWidth, string text, TextFormat format) => HyperPdfDocumentTextBoxes.AddTextBox(
            _owner,
            pageIndex,
            location,
            wrapWidth,
            text,
            format);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TextBoxContent? GetTextBox(int pageIndex, int index) => HyperPdfDocumentTextBoxes.GetTextBox(
            _owner,
            pageIndex,
            index);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetFirstBaseline(string text, TextFormat format) => HyperPdfDocumentTextBoxes.GetFirstBaseline(
            _owner,
            text,
            format);
}
