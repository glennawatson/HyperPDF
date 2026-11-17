// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Engine.Tests;

/// <summary>
/// Finds and retypes paragraphs of real documents, typeset by other programs in other fonts, scripts and angles, which the generated pages
/// of the other tests do not cover. The files come from src/tests/reference-pdfs/manifest.json.
/// </summary>
public sealed class ParagraphEditRealDocumentTests
{
    /// <summary>The file of Arabic text that is set turned.</summary>
    private const string TurnedArabic = "pypdf-arabic-rotated";

    /// <summary>The most pages looked at for each file.</summary>
    private const int MaxPages = 3;

    /// <summary>The most lines picked from a page to find a paragraph from.</summary>
    private const int MostSamples = 80;

    /// <summary>The fewest lines of a paragraph that is retyped.</summary>
    private const int FewestLines = 3;

    /// <summary>The most paragraphs of a file that are retyped, each in a document of its own.</summary>
    private const int MostParagraphs = 4;

    /// <summary>How far the places of the new lines may be from the old ones, as a multiple of the height of the first old line.</summary>
    private const float Tolerance = 1.5F;

    /// <summary>How much further down than the old paragraph the new one may reach, as a share of the old paragraph's height, for a font that is wider than the old one.</summary>
    private const float ExtraHeight = 0.6F;

    /// <summary>The narrowest a paragraph is set, as a multiple of the height of its first line.</summary>
    private const float NarrowestWidth = 4F;

    /// <summary>One half.</summary>
    private const float Half = 0.5F;

    /// <summary>The least the dot product of the directions of two lines may be for them to read the same way.</summary>
    private const float SameDirection = 0.99F;

    /// <summary>The way a missing text object is told in a failure.</summary>
    private const string Missing = "missing";

    /// <summary>Gets the ids of the real documents with text, for <c>[MethodDataSource]</c>: court opinions, a tax form, survey reports, Arabic text set upright and turned, and mixed files.</summary>
    /// <returns>The ids.</returns>
    public static IEnumerable<string> Ids() =>
    [
        "loc-brown-v-board", "loc-gideon-v-wainwright", "loc-korematsu-v-united-states", "loc-marbury-v-madison", "loc-plessy-v-ferguson", "irs-w9-2024",
        "usgs-lost-city-2023", "pypdf-arabic", TurnedArabic, "pypdf-arabic-oneline-cmap", "commons-apna-aap", "unaa-udhr-arabic",
        "pypdf-outline", "verapdf-ua-headings", "verapdf-ua-notes",
    ];

    /// <summary>Gets the ids of the real documents whose paragraphs are short, for <c>[MethodDataSource]</c>: Arabic text, set upright and turned, and reports.</summary>
    /// <returns>The ids.</returns>
    public static IEnumerable<string> ShortIds() => ["pypdf-arabic", TurnedArabic, "pypdf-arabic-oneline-cmap", "verapdf-ua-notes"];

    /// <summary>Every paragraph found on a real page holds the line it was found from, each line once, and only lines of text that read the same way.</summary>
    /// <param name="id">The file.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(Ids))]
    public async Task ParagraphsFoundOnRealPagesAreConsistent(string id)
    {
        using var test = new HyperPdfTestDocument(ReferencePdfFile.Read(id));
        var problems = new List<string>();
        var searched = 0;
        for (var page = 0; page < Math.Min(test.Document.PageCount, MaxPages); page++)
        {
            var objects = await HyperPdfDocumentPageContentEditing.ReadObjectsAsync(test.Document, page, CancellationToken.None);
            var byIndex = objects.ToDictionary(static o => o.Index);
            var texts = objects.Where(IsText).ToArray();
            var step = Math.Max(1, texts.Length / MostSamples);
            for (var i = 0; i < texts.Length; i += step)
            {
                searched++;
                Check(ParagraphFinder.Find(objects, texts[i]), texts[i], byIndex, $"page {page} line {texts[i].Index}", problems);
            }
        }

        await Assert.That(searched).IsGreaterThan(0);
        await Assert.That(problems).IsEmpty().Because(string.Join("; ", problems.Take(MostParagraphs)));
    }

    /// <summary>A real page of Arabic that was set turned reads at an angle, so its lines are found by the way they read and not by the box round them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ARealTurnedPageReadsAtItsAngle()
    {
        using var test = new HyperPdfTestDocument(ReferencePdfFile.Read(TurnedArabic));
        var turned = 0;
        for (var page = 0; page < test.Document.PageCount; page++)
        {
            var objects = await HyperPdfDocumentPageContentEditing.ReadObjectsAsync(test.Document, page, CancellationToken.None);
            turned += objects.Count(static o => IsText(o) && !o.Reading.IsUpright);
        }

        await Assert.That(turned).IsGreaterThan(0);
    }

    /// <summary>Retyping a paragraph of a real page with its own words keeps the new lines in the old paragraph's place, lined up on the left, the right or the middle as it was.</summary>
    /// <param name="id">The file.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(Ids))]
    public Task RetypingARealParagraphKeepsItsPlace(string id) => RetypeParagraphsAsync(id, FewestLines);

    /// <summary>Retyping the lines of a real page of Arabic, set upright and turned, or of a short report keeps them where they were, lined up on the side the text starts on.</summary>
    /// <param name="id">The file.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(ShortIds))]
    public Task RetypingARealShortParagraphKeepsItsPlace(string id) => RetypeParagraphsAsync(id, 1);

    /// <summary>Retypes some paragraphs of a file with their own words and checks where the new lines are.</summary>
    /// <param name="id">The file.</param>
    /// <param name="fewest">The fewest lines of a paragraph that is retyped.</param>
    /// <returns>A task.</returns>
    private static async Task RetypeParagraphsAsync(string id, int fewest)
    {
        var bytes = ReferencePdfFile.Read(id);
        var problems = new List<string>();
        var retyped = 0;
        for (var number = 0; number < MostParagraphs; number++)
        {
            retyped += await RetypeAsync(bytes, number, fewest, problems);
        }

        // A file may have no paragraph of that many lines, as a form has none of three.
        if (retyped == 0)
        {
            Skip.Test($"'{id}' has no paragraph of {fewest} lines to retype.");
        }

        await Assert.That(problems).IsEmpty().Because(string.Join("; ", problems));
    }

    /// <summary>Tells whether an object is a text with words, and not only spaces as a scan's blank text layer has.</summary>
    /// <param name="item">The object.</param>
    /// <returns><see langword="true"/> for text that has a letter.</returns>
    private static bool IsText(PageObjectInfo item) => item.Kind == PageObjectKind.Text && !string.IsNullOrWhiteSpace(item.Text);

    /// <summary>Checks a paragraph a search found, and notes what is wrong with it.</summary>
    /// <param name="lines">The numbers of the lines found.</param>
    /// <param name="picked">The line the search began from.</param>
    /// <param name="byIndex">The objects of the page by number.</param>
    /// <param name="where">Where the line is, for a note.</param>
    /// <param name="problems">Receives a note for each thing that is wrong.</param>
    private static void Check(int[] lines, in PageObjectInfo picked, Dictionary<int, PageObjectInfo> byIndex, string where, List<string> problems)
    {
        if (!lines.Contains(picked.Index))
        {
            problems.Add($"{where}: the paragraph does not hold the line it was found from");
        }

        if (lines.Distinct().Count() != lines.Length)
        {
            problems.Add($"{where}: a line is found twice");
        }

        foreach (var number in lines)
        {
            if (!byIndex.TryGetValue(number, out var line) || line.Kind != PageObjectKind.Text)
            {
                problems.Add($"{where}: line {number} is {Missing} or is not text");
            }
            else if (!SameWay(line, picked))
            {
                problems.Add($"{where}: line {number} reads another way");
            }
        }
    }

    /// <summary>Tells whether two lines read the same way, to within a few degrees.</summary>
    /// <param name="first">The first line.</param>
    /// <param name="second">The second line.</param>
    /// <returns><see langword="true"/> when they do.</returns>
    private static bool SameWay(in PageObjectInfo first, in PageObjectInfo second)
    {
        var a = first.Reading;
        var b = second.Reading;
        return a.IsUpright || b.IsUpright
            ? a.IsUpright && b.IsUpright
            : (a.DirectionX * b.DirectionX) + (a.DirectionY * b.DirectionY) >= SameDirection;
    }

    /// <summary>Retypes the paragraph of a number among the paragraphs of three lines or more of the file, in a document opened for it.</summary>
    /// <param name="bytes">The file.</param>
    /// <param name="number">Which of those paragraphs, from the first page on.</param>
    /// <param name="fewest">The fewest lines of a paragraph that counts.</param>
    /// <param name="problems">Receives a note for each thing that is wrong.</param>
    /// <returns>1 when the paragraph was retyped, or 0 when the file has no such paragraph.</returns>
    private static async Task<int> RetypeAsync(byte[] bytes, int number, int fewest, List<string> problems)
    {
        using var test = new HyperPdfTestDocument(bytes);
        for (var page = 0; page < Math.Min(test.Document.PageCount, MaxPages); page++)
        {
            var before = await HyperPdfDocumentPageContentEditing.ReadObjectsAsync(test.Document, page, CancellationToken.None);
            if (FindParagraph(before, number, fewest) is not { } lines)
            {
                number -= Paragraphs(before, fewest).Count;
                continue;
            }

            var words = string.Join(' ', lines.Select(static item => item.Text));
            var area = ParagraphPictures.AreaAround(Union(lines), test.Document.GetPageSize(page));
            var pictureBefore = ParagraphPictures.Render(test.Document, page, area);
            await HyperPdfDocumentPageContentEditing.ReflowParagraphAsync(test.Document, page, [.. lines.Select(static item => item.Index)], words, CancellationToken.None);
            var after = await HyperPdfDocumentPageContentEditing.ReadObjectsAsync(test.Document, page, CancellationToken.None);
            var where = $"page {page} paragraph from line {lines[0].Index}";
            CheckRetyped(before, after, lines, where, problems);
            var written = WrittenLines(before, after, lines);
            if (written.Length > 0)
            {
                CheckPicture(area, pictureBefore, ParagraphPictures.Render(test.Document, page, area), new(Union(lines), Union(written)), where, problems);
            }

            return 1;
        }

        return 0;
    }

    /// <summary>Checks how a retyped paragraph looks: the picture changed where the paragraph is and nowhere else.</summary>
    /// <param name="area">The area that was drawn.</param>
    /// <param name="before">The pixels before the edit.</param>
    /// <param name="after">The pixels after it.</param>
    /// <param name="boxes">The boxes of the old and the new paragraph.</param>
    /// <param name="where">Where the paragraph is, for a note.</param>
    /// <param name="problems">Receives a note for each thing that is wrong.</param>
    private static void CheckPicture(in ParagraphPictures.PictureArea area, byte[] before, byte[] after, in ParagraphPictures.EditedBoxes boxes, string where, List<string> problems)
    {
        if (ParagraphPictures.Check(area, before, after, boxes) is { } problem)
        {
            problems.Add($"{where}: {problem}");
        }
    }

    /// <summary>Finds the paragraphs of three lines or more of a page, in the order of their first lines.</summary>
    /// <param name="objects">The objects of the page.</param>
    /// <param name="fewest">The fewest lines of a paragraph that counts.</param>
    /// <returns>The lines of each paragraph, from its first line.</returns>
    private static List<PageObjectInfo[]> Paragraphs(PageObjectInfo[] objects, int fewest)
    {
        var byIndex = objects.ToDictionary(static o => o.Index);
        var taken = new HashSet<int>();
        var paragraphs = new List<PageObjectInfo[]>();
        foreach (var item in objects.Where(IsText))
        {
            if (taken.Contains(item.Index))
            {
                continue;
            }

            var found = ParagraphFinder.Find(objects, item);
            taken.UnionWith(found);
            if (found.Length >= fewest && found.All(byIndex.ContainsKey))
            {
                paragraphs.Add([.. found.Select(i => byIndex[i])]);
            }
        }

        return paragraphs;
    }

    /// <summary>Finds one of the paragraphs of three lines or more of a page.</summary>
    /// <param name="objects">The objects of the page.</param>
    /// <param name="number">Which paragraph, from the first.</param>
    /// <param name="fewest">The fewest lines of a paragraph that counts.</param>
    /// <returns>The lines of the paragraph; <see langword="null"/> when the page has fewer.</returns>
    private static PageObjectInfo[]? FindParagraph(PageObjectInfo[] objects, int number, int fewest)
    {
        var paragraphs = Paragraphs(objects, fewest);
        return number >= 0 && number < paragraphs.Count ? paragraphs[number] : null;
    }

    /// <summary>Checks that the lines written for a paragraph are where the old ones were.</summary>
    /// <param name="before">The objects of the page before the edit.</param>
    /// <param name="after">The objects of the page after it.</param>
    /// <param name="lines">The old lines of the paragraph.</param>
    /// <param name="where">Where the paragraph is, for a note.</param>
    /// <param name="problems">Receives a note for each thing that is wrong.</param>
    private static void CheckRetyped(PageObjectInfo[] before, PageObjectInfo[] after, PageObjectInfo[] lines, string where, List<string> problems)
    {
        var written = WrittenLines(before, after, lines);
        if (written.Length == 0)
        {
            problems.Add($"{where}: nothing was written for {lines.Length} old lines");
            return;
        }

        var oldBox = Union(lines);
        var newBox = Union(written);
        var margin = lines[0].Bounds.Height * Tolerance;
        var keepsEdge = Math.Abs(newBox.Left - oldBox.Left) <= margin
            || Math.Abs(newBox.Right - oldBox.Right) <= margin
            || Math.Abs(((newBox.Left + newBox.Right) * Half) - ((oldBox.Left + oldBox.Right) * Half)) <= margin;

        // A paragraph is never set narrower than a few letters wide, so a narrow old one may be reached past by the difference.
        var reach = Math.Max(margin, lines[0].Bounds.Height * NarrowestWidth);
        var inside = newBox.Left >= oldBox.Left - reach && newBox.Right <= oldBox.Right + reach;
        var down = newBox.Top >= oldBox.Top - margin && newBox.Bottom <= oldBox.Bottom + (oldBox.Height * ExtraHeight) + margin;
        if (!keepsEdge || !inside || !down)
        {
            problems.Add($"{where}: old {oldBox} new {newBox} (edge {keepsEdge}, inside {inside}, down {down})");
        }
    }

    /// <summary>Finds the lines an edit wrote: what the page has now that it did not have before the paragraph's own lines were taken out, counting a text written where it was.</summary>
    /// <param name="before">The objects of the page before the edit.</param>
    /// <param name="after">The objects of the page after it.</param>
    /// <param name="lines">The old lines of the paragraph.</param>
    /// <returns>The new lines.</returns>
    private static PageObjectInfo[] WrittenLines(PageObjectInfo[] before, PageObjectInfo[] after, PageObjectInfo[] lines)
    {
        var kept = new Dictionary<string, int>();
        var oldKeys = new HashSet<int>(lines.Select(static item => item.Index));
        foreach (var item in before)
        {
            if (IsText(item) && !oldKeys.Contains(item.Index))
            {
                var key = Place(item);
                kept[key] = kept.GetValueOrDefault(key) + 1;
            }
        }

        var written = new List<PageObjectInfo>();
        foreach (var item in after)
        {
            var key = Place(item);
            if (!IsText(item))
            {
                continue;
            }

            if (kept.GetValueOrDefault(key) > 0)
            {
                kept[key]--;
            }
            else
            {
                written.Add(item);
            }
        }

        return [.. written];
    }

    /// <summary>Tells where a text is, as a key that tells it from the other texts of a page.</summary>
    /// <param name="item">The text.</param>
    /// <returns>The words and the place, to the nearest point.</returns>
    private static string Place(in PageObjectInfo item) => $"{item.Text}@{MathF.Round(item.Bounds.Left)},{MathF.Round(item.Bounds.Top)}";

    /// <summary>Finds the box that holds some lines.</summary>
    /// <param name="lines">The lines.</param>
    /// <returns>The box.</returns>
    private static PageRect Union(PageObjectInfo[] lines)
    {
        var left = lines.Min(static item => item.Bounds.Left);
        var top = lines.Min(static item => item.Bounds.Top);
        return new(left, top, lines.Max(static item => item.Bounds.Right) - left, lines.Max(static item => item.Bounds.Bottom) - top);
    }
}
