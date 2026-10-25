// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Writing;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Saves edited documents both ways and checks the saved files, for the edit tests.</summary>
internal static class EditChecks
{
    /// <summary>Saves the document as an incremental update.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The file.</returns>
    internal static byte[] SaveIncremental(PdfDocument document) => PdfIncrementalWriter.Save(document.Objects);

    /// <summary>Saves the document as a full, compact rewrite.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The file.</returns>
    internal static byte[] SaveCompact(PdfDocument document) => PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default);

    /// <summary>Saves the document as a full rewrite that leaves every stream uncompressed, so a test can look for bytes in the file.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The file.</returns>
    internal static byte[] SaveReadable(PdfDocument document) =>
        PdfCompactWriter.Save(document.Objects, new(false, false) { StreamMode = PdfStreamMode.Uncompressed });

    /// <summary>Checks a saved file with <c>qpdf --check</c>, skipping the test when qpdf is not installed.</summary>
    /// <param name="name">A name for the file, used when the files are kept.</param>
    /// <param name="file">The saved file.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task.</returns>
    internal static async Task AssertQpdfCleanAsync(string name, byte[] file, CancellationToken cancellationToken)
    {
        var outcome = await QpdfCheck.RunAsync(name, file, cancellationToken);
        if (outcome is not { } result)
        {
            Skip.Test("qpdf is not installed.");
            return;
        }

        await Assert.That(result.IsClean).IsTrue().Because(result.Output);
    }
}
