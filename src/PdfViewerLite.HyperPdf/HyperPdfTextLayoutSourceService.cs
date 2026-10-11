// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Reading;

namespace PdfViewerLite.HyperPdf;

/// <summary>Provides ITextLayoutSource through the owning document.</summary>
internal sealed class HyperPdfTextLayoutSourceService : ITextLayoutSource
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfTextLayoutSourceService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfTextLayoutSourceService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetCharacters(int pageIndex, List<PageCharacter> output) => HyperPdfDocumentCharacterLayout.GetCharacters(
            _owner,
            pageIndex,
            output);
}
