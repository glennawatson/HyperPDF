// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Accessibility;

namespace HyperPdfLibrary.Document;

/// <summary>Reads the suggestions of the Tagged PDF Best Practice Guide: Syntax (PDF Association, version 1.0.1). Advice never blocks a save and is not a conformance claim.</summary>
public static class PdfDocumentTagAdvice
{
    /// <summary>Lists what a tagged document could do better.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The advice; empty when the file is untagged or has none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfTagAdvice[] Get(PdfDocument document) => Get(document, CancellationToken.None);

    /// <summary>Lists what a tagged document could do better.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    /// <returns>The advice; empty when the file is untagged or has none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static PdfTagAdvice[] Get(PdfDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        return BestPracticeScan.Check(document, cancellationToken);
    }
}
