// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdf.TestAssets;

namespace HyperPdfLibrary.Tests.Robustness;

/// <summary>Hand-built files with loops, huge counts and deep nesting, which must open or fail with a PdfException, quickly.</summary>
public sealed class HostileStructureTests
{
    /// <summary>The page object every file shares.</summary>
    private const string Page = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>";

    /// <summary>The catalog every file shares.</summary>
    private const string Catalog = "<< /Type /Catalog /Pages 2 0 R >>";

    /// <summary>The page tree node every file shares.</summary>
    private const string Pages = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>";

    /// <summary>A page whose content stream is object 4.</summary>
    private const string ContentPage = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 9 9] /Contents 4 0 R >>";

    /// <summary>A page with an odd entry in object 4.</summary>
    private const string DeepPage = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 9 9] /Deep 4 0 R >>";

    /// <summary>A catalog with an outline at object 4.</summary>
    private const string OutlineCatalog = "<< /Type /Catalog /Pages 2 0 R /Outlines 4 0 R >>";

    /// <summary>A catalog with an embedded file name tree at object 4.</summary>
    private const string NamesCatalog = "<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles 4 0 R >> >>";

    /// <summary>An outline item that is its own next sibling and first child.</summary>
    private const string OutlineLoopItem = "<< /Title (x) /Next 5 0 R /First 5 0 R /Dest [3 0 R /Fit] >>";

    /// <summary>A filter chain longer than the limit, ending in an unknown filter.</summary>
    private const string FilterChain =
        "/Filter [/ASCIIHexDecode /ASCII85Decode /LZWDecode /RunLengthDecode /ASCIIHexDecode /ASCII85Decode /LZWDecode /RunLengthDecode /ASCIIHexDecode /Unknown]";

    /// <summary>The nesting depth of the deep array.</summary>
    private const int DeepNesting = 50_000;

    /// <summary>The number of kids in the wide page tree.</summary>
    private const int WideKids = 20_000;

    /// <summary>The size a hostile trailer declares.</summary>
    private const string HugeCount = "2000000000";

    /// <summary>Every hostile file opens and reads, or fails with a <see cref="PdfException"/>, within the time limit.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HostileFilesNeverCrashOrHang()
    {
        var failures = new List<string>();
        foreach (var (name, file) in Files())
        {
            var result = await MutantRunner.RunAsync(file);
            if (result.Problem is not null)
            {
                failures.Add(string.Create(CultureInfo.InvariantCulture, $"{name}: {result.Problem}"));
            }
        }

        await Assert.That(failures).IsEmpty();
    }

    /// <summary>A page tree whose kid points back at its parent still yields its page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageTreeLoopKeepsTheRealPage()
    {
        var result = await MutantRunner.RunAsync(MiniPdf.Build("<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [2 0 R 3 0 R] /Count 1 >>", Page));

        await Assert.That(result.Opened).IsTrue();
        await Assert.That(result.Problem).IsNull();
    }

    /// <summary>Builds the hostile files.</summary>
    /// <returns>The names and files.</returns>
    private static List<NamedFile> Files()
    {
        var plain = MiniPdf.Build(Catalog, Pages, Page);
        return
        [
            new("page-tree-loop", MiniPdf.Build(Catalog, "<< /Type /Pages /Kids [2 0 R 3 0 R] /Count 1 >>", Page)),
            new("reference-loop", MiniPdf.Build("<< /Type /Catalog /Pages 4 0 R >>", Pages, Page, "5 0 R", "4 0 R")),
            new("self-length", MiniPdf.Build(Catalog, Pages, ContentPage, "<< /Length 4 0 R >>\nstream\nabc\nendstream")),
            new("outline-loop", MiniPdf.Build(OutlineCatalog, Pages, Page, "<< /First 5 0 R /Last 5 0 R >>", OutlineLoopItem)),
            new("name-tree-loop", MiniPdf.Build(NamesCatalog, Pages, Page, "<< /Kids [4 0 R] >>")),
            new("deep-array", MiniPdf.Build(Catalog, Pages, DeepPage, new string('[', DeepNesting))),
            new("deep-dictionary", MiniPdf.Build(Catalog, Pages, DeepPage, string.Concat(Enumerable.Repeat("<< /A ", DeepNesting)))),
            new("wide-page-tree", MiniPdf.Build(Catalog, $"<< /Type /Pages /Kids [{string.Concat(Enumerable.Repeat("3 0 R ", WideKids))}] /Count {WideKids} >>", Page)),
            new("orphan-parent-loop", MiniPdf.Build("<< /Type /Catalog >>", "<< /Type /Pages /Kids [3 0 R] >>", "<< /Type /Page /Parent 3 0 R /MediaBox [0 0 9 9] >>")),
            new("huge-size", Replace(plain, "/Size 4", $"/Size {HugeCount}")),
            new("huge-subsection", Replace(plain, "xref\n0 4", $"xref\n0 {HugeCount}")),
            new("prev-loop", PrevLoop(plain)),
            new("objstm-huge-count", WithoutXref(MiniPdf.Build(Catalog, Pages, Page, MiniPdf.Stream("/Type /ObjStm /N 1000000000 /First 3", "5 0 6 0 << >> << >>")))),
            new("objstm-bad-first", WithoutXref(MiniPdf.Build(Catalog, Pages, Page, MiniPdf.Stream("/Type /ObjStm /N 2 /First 999999", "5 0 6 5 << >> << >>")))),
            new("flate-garbage", MiniPdf.Build(Catalog, Pages, ContentPage, MiniPdf.Stream("/Filter /FlateDecode", "not really flate data at all"))),
            new("filter-chain", MiniPdf.Build(Catalog, Pages, ContentPage, MiniPdf.Stream(FilterChain, "4142>"))),
        ];
    }

    /// <summary>Replaces text in a file.</summary>
    /// <param name="file">The file.</param>
    /// <param name="from">The text to find.</param>
    /// <param name="to">The replacement.</param>
    /// <returns>The new file.</returns>
    private static byte[] Replace(byte[] file, string from, string to) =>
        Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(file).Replace(from, to, StringComparison.Ordinal));

    /// <summary>Makes the trailer's /Prev point at its own cross-reference section.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The new file.</returns>
    private static byte[] PrevLoop(byte[] file)
    {
        var text = Encoding.Latin1.GetString(file);
        var offset = text[(text.LastIndexOf("startxref\n", StringComparison.Ordinal) + "startxref\n".Length)..].Split('\n')[0];
        return Replace(file, "/Root 1 0 R", $"/Root 1 0 R /Prev {offset}");
    }

    /// <summary>Breaks the cross-reference table so that opening scans the file instead.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The new file.</returns>
    private static byte[] WithoutXref(byte[] file) => Replace(file, "startxref", "startxxxx");

    /// <summary>A named hostile file.</summary>
    /// <param name="Name">The name.</param>
    /// <param name="File">The file.</param>
    private readonly record struct NamedFile(string Name, byte[] File);
}
