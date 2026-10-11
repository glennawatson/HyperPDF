// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Forms.Scripting;

namespace PdfViewerLite.HyperPdf;

/// <summary>Provides IFormScriptSource through the owning document.</summary>
internal sealed class HyperPdfFormScriptSourceService : IFormScriptSource
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfFormScriptSourceService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfFormScriptSourceService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetScripts(int pageIndex, List<FieldScripts> output) => HyperPdfDocumentFormScripts.GetScripts(
            _owner,
            pageIndex,
            output);
}
