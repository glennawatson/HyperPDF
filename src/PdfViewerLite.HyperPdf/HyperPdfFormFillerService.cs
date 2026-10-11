// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Forms;

namespace PdfViewerLite.HyperPdf;

/// <summary>Provides IFormFiller through the owning document.</summary>
internal sealed class HyperPdfFormFillerService : IFormFiller
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfFormFillerService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfFormFillerService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    public bool HasForm { get => HyperPdfDocumentFormFilling.GetHasForm(_owner); }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetFields(int pageIndex, List<FormField> output) => HyperPdfDocumentFormFilling.GetFields(
            _owner,
            pageIndex,
            output);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SetText(int pageIndex, int index, string text) => HyperPdfDocumentFormFilling.SetText(
            _owner,
            pageIndex,
            index,
            text);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SetChecked(int pageIndex, int index, bool isChecked) => HyperPdfDocumentFormFilling.SetChecked(
            _owner,
            pageIndex,
            index,
            isChecked);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SelectOption(int pageIndex, int index, int option) => HyperPdfDocumentFormFilling.SelectOption(
            _owner,
            pageIndex,
            index,
            option);
}
