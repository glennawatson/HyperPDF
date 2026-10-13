// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Creation;

/// <summary>Keeps created documents for outside checks and reads their text; shared with the engine tests.</summary>
internal static class CreatedFiles
{
    /// <summary>The environment variable naming a folder where the created documents are kept for outside checks.</summary>
    internal const string OutputVariable = "HYPERPDF_CREATION_OUTPUT";

    /// <summary>Keeps a document for outside checks such as <c>qpdf --check</c> when the output variable names a folder.</summary>
    /// <param name="name">The file name.</param>
    /// <param name="pdf">The document.</param>
    internal static void Keep(string name, byte[] pdf)
    {
        if (Environment.GetEnvironmentVariable(OutputVariable) is not { Length: > 0 } folder)
        {
            return;
        }

        _ = Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, name), pdf);
    }

    /// <summary>Gets a page's text.</summary>
    /// <param name="pdf">The document.</param>
    /// <param name="page">The zero-based page.</param>
    /// <returns>The text.</returns>
    internal static string TextOf(byte[] pdf, int page)
    {
        using var document = PdfDocumentReader.Open(pdf, null);
        return PdfDocumentText.GetTextPage(document, page).Text;
    }
}
