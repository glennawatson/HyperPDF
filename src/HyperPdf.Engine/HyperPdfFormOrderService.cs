// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;

namespace HyperPdf.Engine;

/// <summary>Provides IFormOrder through the owning document.</summary>
internal sealed class HyperPdfFormOrderService : IFormOrder
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfFormOrderService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfFormOrderService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetCalculationOrder(List<string> output) => HyperPdfDocumentFormOrder.GetCalculationOrder(
            _owner,
            output);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetTabOrder(int pageIndex, List<int> output) => HyperPdfDocumentFormOrder.GetTabOrder(
            _owner,
            pageIndex,
            output);
}
