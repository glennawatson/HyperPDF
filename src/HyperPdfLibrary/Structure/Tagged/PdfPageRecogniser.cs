// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// A host's text recogniser. It is called for a page that draws images and no text, and returns the words it found in
/// viewer space, or <see langword="null"/> when it found none.
/// </summary>
/// <param name="pageIndex">The zero based page index.</param>
/// <param name="cancellationToken">Cancels the recognition.</param>
/// <returns>The words.</returns>
public delegate ValueTask<IReadOnlyList<PdfOcrWord>?> PdfPageRecogniser(int pageIndex, CancellationToken cancellationToken);
