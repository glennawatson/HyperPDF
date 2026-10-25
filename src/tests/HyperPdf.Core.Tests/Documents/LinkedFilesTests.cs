// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Tests.Documents;

/// <summary>Tests for <see cref="LinkedFiles"/>, which finds and judges the files links point to.</summary>
public sealed class LinkedFilesTests
{
    /// <summary>A document in a folder of its own.</summary>
    private static readonly string Document = Path.Combine(Path.GetTempPath(), "reports", "annual.pdf");

    /// <summary>Paths written relative to the document are found beside it, with either separator.</summary>
    /// <param name="link">The path as written.</param>
    /// <param name="expected">The path from the document's folder.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("other.pdf", "other.pdf")]
    [Arguments("appendix/a.pdf", "appendix/a.pdf")]
    [Arguments("appendix\\a.pdf", "appendix/a.pdf")]
    [Arguments("../notes.txt", "../notes.txt")]
    public async Task ResolvesRelativePaths(string link, string expected)
    {
        var folder = Path.GetDirectoryName(Document)!;
        var full = Path.GetFullPath(Path.Combine(folder, expected.Replace('/', Path.DirectorySeparatorChar)));

        await Assert.That(LinkedFiles.Resolve(Document, link)).IsEqualTo(full);
    }

    /// <summary>File URIs and absolute paths are used as they are; web addresses and blanks are not files.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ResolvesAbsolutePathsAndRejectsOthers()
    {
        var absolute = Path.Combine(Path.GetTempPath(), "elsewhere.pdf");

        await Assert.That(LinkedFiles.Resolve(Document, new Uri(absolute).AbsoluteUri)).IsEqualTo(absolute);
        await Assert.That(LinkedFiles.Resolve(Document, absolute)).IsEqualTo(absolute);
        await Assert.That(LinkedFiles.Resolve(Document, "https://example.com/a.pdf")).IsNull();
        await Assert.That(LinkedFiles.Resolve(Document, " ")).IsNull();
    }

    /// <summary>PDFs open in a tab; programs, scripts and files without a type never start; other files may.</summary>
    /// <param name="name">The file.</param>
    /// <param name="pdf">Whether it is a PDF.</param>
    /// <param name="runnable">Whether it runs code.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("a.PDF", true, false)]
    [Arguments("notes.txt", false, false)]
    [Arguments("photo.jpg", false, false)]
    [Arguments("setup.exe", false, true)]
    [Arguments("run.SH", false, true)]
    [Arguments("macro.vbs", false, true)]
    [Arguments("tool", false, true)]
    public async Task JudgesFiles(string name, bool pdf, bool runnable)
    {
        await Assert.That(LinkedFiles.IsPdf(name)).IsEqualTo(pdf);
        await Assert.That(LinkedFiles.IsRunnable(name)).IsEqualTo(runnable);
    }
}
