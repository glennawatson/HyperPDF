// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace HyperPdf.TestAssets;

/// <summary>Builds hand-made preseparated documents (ISO 32000-2 §14.11.4) for tests and benchmarks.</summary>
public static class SeparationPdf
{
    /// <summary>The pages of the preseparated document: two printed pages of four colourants, then an ordinary page.</summary>
    public static readonly int PreseparatedPages = 9;

    /// <summary>The separation pages of each printed page.</summary>
    public static readonly int ColorantsPerPage = 4;

    /// <summary>The index of the ordinary page.</summary>
    public static readonly int OrdinaryPage = 8;

    /// <summary>The printed pages of the preseparated document.</summary>
    public static readonly int PrintedPages = 2;

    /// <summary>The first page of the second printed page.</summary>
    public static readonly int SecondGroupStart = 4;

    /// <summary>The number of the first page object.</summary>
    private const int FirstPageNumber = 3;

    /// <summary>The catalog object every document here starts with.</summary>
    private const string Catalog = "<< /Type /Catalog /Pages 2 0 R >>";

    /// <summary>Gets the pages of the first printed page.</summary>
    public static int[] FirstGroup => [.. Enumerable.Range(0, ColorantsPerPage)];

    /// <summary>Gets the pages of the second printed page.</summary>
    public static int[] SecondGroup => [.. Enumerable.Range(SecondGroupStart, ColorantsPerPage)];

    /// <summary>Gets every page in order.</summary>
    public static int[] AllPages => [.. Enumerable.Range(0, PreseparatedPages)];

    /// <summary>Gets the colourants of the first printed page, in its <c>/Pages</c> order.</summary>
    public static string[] FirstColorants => ["Cyan", "Magenta", "Yellow", "Black"];

    /// <summary>Gets the colourants of the second printed page, in its <c>/Pages</c> order.</summary>
    public static string[] SecondColorants => ["Cyan", "Magenta", "Yellow", "PANTONE 35 CV"];

    /// <summary>
    /// Creates the preseparated document. Printed page 1 (pages 0 to 3) shares one indirect <c>/Pages</c> array and names
    /// its colourants with Separation colour spaces. Printed page 2 (pages 4 to 7) gives each page an identical direct
    /// array; its spot colourant is a string with a DeviceN colour space. Page 8 is an ordinary composite page.
    /// </summary>
    /// <returns>The file bytes.</returns>
    public static byte[] CreatePreseparated()
    {
        const string Tint = "13 0 R";
        string[] first = ["/Cyan", "/Magenta", "/Yellow", "/Black"];
        List<string> objects = [Catalog, Tree(PreseparatedPages)];
        for (var i = 0; i < ColorantsPerPage; i++)
        {
            objects.Add(Page($"/Pages 12 0 R /DeviceColorant {first[i]} /ColorSpace [/Separation {first[i]} /DeviceCMYK {Tint}]"));
        }

        var second = $"[{Kids(FirstPageNumber + ColorantsPerPage, ColorantsPerPage)}]";
        objects.Add(Page($"/Pages {second} /DeviceColorant /Cyan"));
        objects.Add(Page($"/Pages {second} /DeviceColorant /Magenta"));
        objects.Add(Page($"/Pages {second} /DeviceColorant /Yellow"));
        objects.Add(Page($"/Pages {second} /DeviceColorant (PANTONE 35 CV) /ColorSpace [/DeviceN [/PANTONE#2035#20CV] /DeviceCMYK {Tint}]"));
        objects.Add("<< /Type /Page /Parent 2 0 R >>");
        objects.Add($"[{Kids(FirstPageNumber, ColorantsPerPage)}]");
        objects.Add("<< /FunctionType 2 /Domain [0 1] /C0 [0 0 0 0] /C1 [1 0 0 0] /N 1 >>");
        return MiniPdf.Build([.. objects]);
    }

    /// <summary>
    /// Creates a document of single pages with the given separation dictionary entries. Page <c>i</c> is object
    /// <c>3 + i</c>; extra objects follow the pages.
    /// </summary>
    /// <param name="entries">For each page, the separation dictionary's entries, or <see langword="null"/> for an ordinary page.</param>
    /// <param name="extra">Objects numbered after the pages.</param>
    /// <returns>The file bytes.</returns>
    public static byte[] CreatePages(string?[] entries, params string[] extra)
    {
        List<string> objects = [Catalog, Tree(entries.Length)];
        foreach (var entry in entries)
        {
            objects.Add(entry is null ? "<< /Type /Page /Parent 2 0 R >>" : Page(entry));
        }

        objects.AddRange(extra);
        return MiniPdf.Build([.. objects]);
    }

    /// <summary>Creates a document with more separation pages than are read, each listing only itself.</summary>
    /// <param name="pageCount">The number of pages.</param>
    /// <returns>The file bytes.</returns>
    public static byte[] CreateMany(int pageCount) =>
        BuildLarge(pageCount, static i => string.Create(CultureInfo.InvariantCulture, $"/Pages [{FirstPageNumber + i} 0 R] /DeviceColorant /Black"));

    /// <summary>Creates a large preseparated document: printed pages of cyan, magenta, yellow and black separations.</summary>
    /// <param name="printedPages">The number of printed pages.</param>
    /// <returns>The file bytes.</returns>
    public static byte[] CreatePrintedPages(int printedPages) =>
        BuildLarge(printedPages * ColorantsPerPage, static i =>
        {
            var first = FirstPageNumber + (i - (i % ColorantsPerPage));
            return string.Create(CultureInfo.InvariantCulture, $"/Pages [{Kids(first, ColorantsPerPage)}] /DeviceColorant /{FirstColorants[i % ColorantsPerPage]}");
        });

    /// <summary>Lists page references.</summary>
    /// <param name="first">The first object number.</param>
    /// <param name="count">The number of references.</param>
    /// <returns>The references, separated by spaces.</returns>
    public static string Kids(int first, int count)
    {
        var kids = new StringBuilder();
        for (var i = 0; i < count; i++)
        {
            _ = kids.Append(CultureInfo.InvariantCulture, $"{first + i} 0 R ");
        }

        return kids.ToString().TrimEnd();
    }

    /// <summary>Builds a document of separation pages, counting offsets as the file grows, as MiniPdf measures the whole text for every object.</summary>
    /// <param name="pageCount">The number of pages.</param>
    /// <param name="entries">The separation dictionary entries of each page.</param>
    /// <returns>The file bytes.</returns>
    private static byte[] BuildLarge(int pageCount, Func<int, string> entries)
    {
        var output = new StringBuilder("%PDF-1.7\n");
        var offsets = new List<int>();
        Append(output, offsets, Catalog);
        Append(output, offsets, Tree(pageCount));
        for (var i = 0; i < pageCount; i++)
        {
            Append(output, offsets, Page(entries(i)));
        }

        var xref = output.Length;
        _ = output.Append(CultureInfo.InvariantCulture, $"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            _ = output.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        }

        _ = output.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.Latin1.GetBytes(output.ToString());
    }

    /// <summary>Writes the page tree root listing every page in order.</summary>
    /// <param name="pageCount">The number of pages.</param>
    /// <returns>The object body.</returns>
    private static string Tree(int pageCount) =>
        $"<< /Type /Pages /Kids [{Kids(FirstPageNumber, pageCount)}] /Count {pageCount} /Resources << >> /MediaBox [0 0 200 200] >>";

    /// <summary>Writes a page object with a separation dictionary.</summary>
    /// <param name="entries">The separation dictionary's entries.</param>
    /// <returns>The object body.</returns>
    private static string Page(string entries) => $"<< /Type /Page /Parent 2 0 R /SeparationInfo << {entries} >> >>";

    /// <summary>Appends one object, recording its offset.</summary>
    /// <param name="output">The file text.</param>
    /// <param name="offsets">The object offsets.</param>
    /// <param name="body">The object body.</param>
    private static void Append(StringBuilder output, List<int> offsets, string body)
    {
        offsets.Add(output.Length);
        _ = output.Append(CultureInfo.InvariantCulture, $"{offsets.Count} 0 obj\n{body}\nendobj\n");
    }
}
