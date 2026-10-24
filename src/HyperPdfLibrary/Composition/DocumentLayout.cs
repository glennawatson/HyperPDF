// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>Lays a whole document out on pages: sections in turn, each starting a new page, with the room headers, footers and footnotes take.</summary>
[DebuggerDisplay("DocumentLayout: {Pages.Count} pages")]
internal sealed class DocumentLayout : IPager
{
    /// <summary>The share of the text width a footnote separator takes.</summary>
    private const float SeparatorShare = 0.33F;

    /// <summary>The width of a footnote separator line, in points.</summary>
    private const float SeparatorWidth = 0.5F;

    /// <summary>The gap in points between a footnote separator and the notes below it.</summary>
    private const float SeparatorGap = 2F;

    /// <summary>The grey level of a footnote separator.</summary>
    private const float SeparatorGray = 0.4F;

    /// <summary>The document and its styles.</summary>
    private readonly LayoutEnv _env;

    /// <summary>Cancels the layout.</summary>
    private readonly CancellationToken _cancellationToken;

    /// <summary>The sections, as laid out.</summary>
    private readonly List<SectionInfo> _sections = [];

    /// <summary>The section being laid out.</summary>
    private SectionInfo? _section;

    /// <summary>The number of pages in the section so far.</summary>
    private int _sectionPage;

    /// <summary>The number the last page showed.</summary>
    private int _display;

    /// <summary>Initializes a new instance of the <see cref="DocumentLayout"/> class.</summary>
    /// <param name="env">The document and its styles.</param>
    /// <param name="cancellationToken">Cancels the layout.</param>
    internal DocumentLayout(LayoutEnv env, CancellationToken cancellationToken)
    {
        _env = env;
        _cancellationToken = cancellationToken;
    }

    /// <summary>Gets the pages, in order.</summary>
    internal List<PagePlan> Pages { get; } = [];

    /// <summary>Gets the sections, in order.</summary>
    internal IReadOnlyList<SectionInfo> Sections => _sections;

    /// <inheritdoc/>
    public void StartPage(FlowCursor cursor)
    {
        FinishPage(cursor);
        var info = _section ?? throw new InvalidOperationException("No section is being laid out.");
        _sectionPage++;
        _display++;
        var number = Pages.Count + 1;
        var page = new PagePlan(number, info, _sectionPage, _display);
        var sizes = GetSizes(info, _sectionPage, _display);
        Pages.Add(page);
        cursor.Page = page;
        cursor.Reset(PageGeometry.For(info.Setup, number, sizes.HeaderHeight, sizes.FooterHeight));
        _env.Fields = new(_display, info.Setup.PageNumberFormat, number, info.Number, _sectionPage, _env.Now, _env.Document.Title, _env.Document.Author);
    }

    /// <summary>Lays out every section.</summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal void Run()
    {
        var number = 0;
        foreach (var section in _env.Document.Sections)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            number++;
            LayoutSection(new(number, section));
        }
    }

    /// <summary>Works out which header and footer a page has and how tall they are.</summary>
    /// <param name="info">The section.</param>
    /// <param name="sectionPage">The page's place in the section.</param>
    /// <param name="display">The number the page shows.</param>
    /// <returns>The header and footer and their heights.</returns>
    internal RunningSizes GetSizes(SectionInfo info, int sectionPage, int display)
    {
        var header = RunningContentSelector.SelectHeader(info.Section, sectionPage, display);
        var footer = RunningContentSelector.SelectFooter(info.Section, sectionPage, display);
        var width = info.Setup.PageWidth - info.Setup.MarginLeft - info.Setup.MarginRight - info.Setup.Gutter;
        return new(header, footer, MeasureRunning(header, width), MeasureRunning(footer, width));
    }

    /// <summary>Measures a header or footer.</summary>
    /// <param name="content">The content, or <see langword="null"/> for none.</param>
    /// <param name="width">The width of the text area.</param>
    /// <returns>The height in points, zero for none.</returns>
    private float MeasureRunning(PdfRunningContent? content, float width) =>
        content is null ? 0F : StackLayout.Build(_env.WithoutTags(), content.Blocks, width, PdfArtifactKind.Pagination, new(0F, null, null), _cancellationToken).Height;

    /// <summary>Lays out one section: starts its first page and flows its blocks.</summary>
    /// <param name="info">The section.</param>
    private void LayoutSection(SectionInfo info)
    {
        _section = info;
        _sections.Add(info);
        _sectionPage = 1;
        _display = info.Setup.StartPageNumber is { } start ? start : _display + 1;
        var number = Pages.Count + 1;
        var page = new PagePlan(number, info, _sectionPage, _display);
        Pages.Add(page);
        var sizes = GetSizes(info, _sectionPage, _display);
        var geometry = PageGeometry.For(info.Setup, number, sizes.HeaderHeight, sizes.FooterHeight);
        _env.Fields = new(_display, info.Setup.PageNumberFormat, number, info.Number, _sectionPage, _env.Now, _env.Document.Title, _env.Document.Author);
        var cursor = new FlowCursor(_env, page, geometry, this);
        var items = new Flattener(_env).Flatten(info.Section.Blocks, new(0F, null, null));
        FlowEngine.Run(items, cursor, _cancellationToken);
        FinishPage(cursor);
        info.PageCount = _sectionPage;
    }

    /// <summary>Draws the footnotes at the foot of the page the cursor is on.</summary>
    /// <param name="cursor">The cursor.</param>
    private void FinishPage(FlowCursor cursor)
    {
        var page = cursor.Page;
        if (page.Notes.Count == 0)
        {
            return;
        }

        var top = cursor.BottomBase + cursor.NotesHeight;
        var separatorY = top - SeparatorGap;
        var pen = new PdfPen(PdfCanvasColor.Gray(SeparatorGray), SeparatorWidth);
        page.Commands.Add(new StrokeCommand(null, PdfArtifactKind.Layout, pen, new(cursor.Left, separatorY), new(cursor.Left + (cursor.Width * SeparatorShare), separatorY)));
        var y = top - FlowCursor.NoteSeparatorHeight;
        foreach (var note in page.Notes)
        {
            y = DrawNote(page, note, cursor, y);
        }
    }

    /// <summary>Draws one footnote.</summary>
    /// <param name="page">The page.</param>
    /// <param name="note">The footnote.</param>
    /// <param name="cursor">The cursor, for the text area.</param>
    /// <param name="top">The y of the top of the footnote.</param>
    /// <returns>The y below the footnote.</returns>
    private float DrawNote(PagePlan page, LaidOutNote note, FlowCursor cursor, float top)
    {
        var noteNode = _env.CreateNode("Note", null);
        var y = top;
        foreach (var paragraph in note.Paragraphs)
        {
            paragraph.Node = _env.CreateNode("P", noteNode);
            foreach (var line in paragraph.Lines)
            {
                new LineDrawer(paragraph, line, cursor.Left, y, paragraph.Node, PdfArtifactKind.Layout, page.Commands).Draw();
                y -= line.Height;
            }

            y -= paragraph.Style.SpaceAfter;
        }

        return y;
    }
}
