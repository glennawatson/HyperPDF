// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Creation;

/// <summary>The state of one <see cref="PdfTextFlow.Add"/> call: the open page, where the next block starts, and the marked content that is open.</summary>
internal ref struct FlowRun
{
    /// <summary>The factor from pixels at 96 to the inch to points.</summary>
    private const float PointsPerPixel = 0.75F;

    /// <summary>The page number's size as a fraction of the body size.</summary>
    private const float PageNumberScale = 0.9F;

    /// <summary>The space before a block as a fraction of its size.</summary>
    private const float SpaceBefore = 0.4F;

    /// <summary>The space after a block as a fraction of its size.</summary>
    private const float SpaceAfter = 0.6F;

    /// <summary>The size of a level 1 heading as a multiple of the body size.</summary>
    private const float LevelOneScale = 1.9F;

    /// <summary>The size of a level 2 heading as a multiple of the body size.</summary>
    private const float LevelTwoScale = 1.55F;

    /// <summary>The size of a level 3 heading as a multiple of the body size.</summary>
    private const float LevelThreeScale = 1.3F;

    /// <summary>The size of a level 4 heading as a multiple of the body size.</summary>
    private const float LevelFourScale = 1.15F;

    /// <summary>The size of a level 5 heading as a multiple of the body size.</summary>
    private const float LevelFiveScale = 1.05F;

    /// <summary>One half, for centring.</summary>
    private const float Half = 0.5F;

    /// <summary>The number of margins across a page: one on each side.</summary>
    private const float MarginSides = 2F;

    /// <summary>The deepest heading level.</summary>
    private const int MaxHeadingLevel = 6;

    /// <summary>The maximum digits of a 32-bit page number.</summary>
    private const int MaxPageDigits = 11;

    /// <summary>The structure type of each heading level, so a heading does not build its spelling.</summary>
    private static readonly string[] HeadingTypes = ["H1", "H2", "H3", "H4", "H5", "H6"];

    /// <summary>The document.</summary>
    private readonly PdfDocumentCreator _creator;

    /// <summary>The options.</summary>
    private readonly PdfFlowOptions _options;

    /// <summary>Cancels the layout.</summary>
    private readonly CancellationToken _cancellationToken;

    /// <summary>The wrapped lines of the block being written, reused for every block.</summary>
    private PooledLines _lines;

    /// <summary>The open page.</summary>
    private PdfPageCanvas _canvas;

    /// <summary>Whether a page is open.</summary>
    private bool _hasPage;

    /// <summary>The top of the space left on the page.</summary>
    private float _top;

    /// <summary>The number of pages finished.</summary>
    private int _pages;

    /// <summary>The level of the last heading, for keeping levels in order.</summary>
    private int _headingLevel;

    /// <summary>The element whose marked content is open, or <see langword="null"/>.</summary>
    private PdfTagElement? _open;

    /// <summary>Initializes a new instance of the <see cref="FlowRun"/> struct.</summary>
    /// <param name="creator">The document.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">Cancels the layout.</param>
    internal FlowRun(PdfDocumentCreator creator, PdfFlowOptions options, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.PageWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.PageHeight);
        ArgumentOutOfRangeException.ThrowIfNegative(options.Margin);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.BodySize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.LineSpacing);
        ArgumentNullException.ThrowIfNull(options.BodyFont);
        ArgumentNullException.ThrowIfNull(options.PageNumberPrefix);
        ArgumentNullException.ThrowIfNull(options.PageNumberSuffix);
        if (options.PageNumberOffset is { } offset)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(offset, options.PageHeight);
        }

        _creator = creator;
        _options = options;
        _cancellationToken = cancellationToken;
        _lines = default;
        _canvas = default;
        _hasPage = false;
        _top = 0;
        _pages = 0;
        _headingLevel = 0;
        _open = null;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(TextWidth, 0F);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(TextHeight, 0F);
    }

    /// <summary>Gets the options.</summary>
    internal readonly PdfFlowOptions Options => _options;

    /// <summary>Gets the document.</summary>
    internal readonly PdfDocumentCreator Creator => _creator;

    /// <summary>Gets the token that cancels the layout.</summary>
    internal readonly CancellationToken CancellationToken => _cancellationToken;

    /// <summary>Gets the width available for text.</summary>
    internal readonly float TextWidth => _options.PageWidth - (_options.Margin * MarginSides);

    /// <summary>Gets the height available for text.</summary>
    internal readonly float TextHeight => _options.PageHeight - (_options.Margin * MarginSides);

    /// <summary>Gets the top of the text area of a page.</summary>
    internal readonly float PageTop => _options.PageHeight - _options.Margin;

    /// <summary>Gets or sets the top of the space left on the open page.</summary>
    internal float Top
    {
        readonly get => _top;
        set => _top = value;
    }

    /// <summary>Gets the zero-based index of the open page.</summary>
    internal readonly int PageNumber => _pages;

    /// <summary>Gets the open page. Only valid after a page is started.</summary>
    [UnscopedRef]
    internal ref PdfPageCanvas Canvas => ref _canvas;

    /// <summary>Gets the size of each heading level as a multiple of the body size.</summary>
    private static ReadOnlySpan<float> HeadingScales => [LevelOneScale, LevelTwoScale, LevelThreeScale, LevelFourScale, LevelFiveScale, 1F];

    /// <summary>Abandons a page that was not finished and returns the pooled line storage.</summary>
    internal void Abandon()
    {
        _canvas.Dispose();
        _lines.Dispose();
    }

    /// <summary>Lays out the blocks.</summary>
    /// <param name="blocks">The blocks.</param>
    /// <returns>The number of pages added.</returns>
    internal int Run(IReadOnlyList<PdfFlowBlock> blocks)
    {
        for (var i = 0; i < blocks.Count; i++)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            WriteBlock(blocks[i]);
        }

        EndPage();
        return _pages;
    }

    /// <summary>Adds the space before a block, unless the block starts a page.</summary>
    /// <param name="size">The block's text size.</param>
    internal void AddSpaceBefore(float size)
    {
        if (_hasPage && _top < PageTop)
        {
            _top -= size * SpaceBefore;
        }
    }

    /// <summary>Adds the space after a block.</summary>
    /// <param name="size">The block's text size.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddSpaceAfter(float size) => _top -= size * SpaceAfter;

    /// <summary>Ends the open page, if any, and starts another.</summary>
    internal void StartNewPage()
    {
        CloseContent();
        EndPage();
        StartPage();
    }

    /// <summary>Makes room for a block, starting a new page when the open one cannot hold it.</summary>
    /// <param name="height">The height the block needs.</param>
    /// <returns><see langword="true"/> when a new page was started.</returns>
    internal bool Reserve(float height)
    {
        if (_hasPage && _top - height >= _options.Margin)
        {
            return false;
        }

        var wasOpen = _hasPage;
        EndPage();
        StartPage();
        return wasOpen;
    }

    /// <summary>Opens marked content for an element, when the document is tagged and it is not open yet.</summary>
    /// <param name="element">The element, or <see langword="null"/> for an untagged document.</param>
    internal void OpenContent(PdfTagElement? element)
    {
        if (element is null || ReferenceEquals(_open, element))
        {
            return;
        }

        _canvas.BeginContent(element);
        _open = element;
    }

    /// <summary>Closes the open marked content.</summary>
    internal void CloseContent()
    {
        if (_open is null)
        {
            return;
        }

        _canvas.EndContent();
        _open = null;
    }

    /// <summary>Gets the size of a heading.</summary>
    /// <param name="level">The level, 1 to 6.</param>
    /// <returns>The size as a multiple of the body size.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float GetHeadingScale(int level) => HeadingScales[Math.Clamp(level, 1, MaxHeadingLevel) - 1];

    /// <summary>Formats the page number with its prefix and suffix.</summary>
    /// <param name="pageNumber">The 1-based page number.</param>
    /// <param name="prefix">The prefix string.</param>
    /// <param name="suffix">The suffix string.</param>
    /// <param name="buffer">A caller-supplied rented buffer.</param>
    /// <returns>The formatted characters.</returns>
    private static ReadOnlySpan<char> FormatPageLabel(int pageNumber, string prefix, string suffix, char[] buffer)
    {
        var span = buffer.AsSpan();
        prefix.AsSpan().CopyTo(span);
        _ = pageNumber.TryFormat(span[prefix.Length..], out var digits, default, CultureInfo.InvariantCulture);
        suffix.AsSpan().CopyTo(span[(prefix.Length + digits)..]);
        return span[..(prefix.Length + digits + suffix.Length)];
    }

    /// <summary>Writes one block.</summary>
    /// <param name="block">The block.</param>
    private void WriteBlock(PdfFlowBlock block)
    {
        switch (block)
        {
            case PdfFlowHeading heading:
            {
                WriteHeading(heading);
                break;
            }

            case PdfFlowParagraph paragraph:
            {
                WriteText(_options.BodyFont, _options.BodySize, paragraph.Text, "P", 0);
                break;
            }

            case PdfFlowImage image:
            {
                WriteImage(image);
                break;
            }

            case PdfFlowTable table:
            {
                PdfTableFlow.Write(ref this, table);
                break;
            }

            case PdfFlowCodeBlock codeBlock:
            {
                PdfCodeFlow.Write(ref this, codeBlock);
                break;
            }

            default:
            {
                EndPage();
                break;
            }
        }
    }

    /// <summary>Writes a heading, keeping its level no more than one below the heading before it.</summary>
    /// <param name="heading">The heading.</param>
    private void WriteHeading(PdfFlowHeading heading)
    {
        _headingLevel = Math.Clamp(heading.Level, 1, Math.Min(_headingLevel + 1, MaxHeadingLevel));
        var font = _options.HeadingFont ?? _options.BodyFont;
        var type = HeadingTypes[_headingLevel - 1];
        WriteText(font, _options.BodySize * GetHeadingScale(_headingLevel), heading.Text, type, 1);
    }

    /// <summary>Writes wrapped text as one structure element, starting a new page when the page is full.</summary>
    /// <param name="font">The font.</param>
    /// <param name="size">The font size.</param>
    /// <param name="text">The text.</param>
    /// <param name="type">The structure type.</param>
    /// <param name="keepWith">The number of lines that must fit with the first, so a heading is not left alone at the foot of a page.</param>
    private void WriteText(PdfCanvasFont font, float size, string text, string type, int keepWith)
    {
        var leading = size * _options.LineSpacing;
        _lines.Clear();
        PdfTextWrapper.Wrap(font, size, text, TextWidth, ref _lines);
        var element = _creator.Tags is { } tags ? tags.AddElement(tags.Root, type) : null;
        for (var i = 0; i < _lines.Count; i++)
        {
            var needed = i == 0 ? leading + (size * SpaceBefore) + (leading * keepWith) : leading;
            _ = Reserve(needed);
            if (i == 0 && _top < _options.PageHeight - _options.Margin)
            {
                _top -= size * SpaceBefore;
            }

            OpenContent(element);
            var line = _lines[i];
            if (line.Length > 0)
            {
                var baseline = _top - ((leading - (font.GetAscent(size) - font.GetDescent(size))) * Half) - font.GetAscent(size);
                _canvas.DrawText(font, size, _options.Margin, baseline, text.AsSpan(line.Start, line.Length));
            }

            _top -= leading;
        }

        CloseContent();
        _top -= size * SpaceAfter;
    }

    /// <summary>Writes an image on a line of its own.</summary>
    /// <param name="block">The image block.</param>
    private void WriteImage(PdfFlowImage block)
    {
        var image = block.Image;
        var width = Math.Min(block.WidthPoints > 0F ? block.WidthPoints : image.Width * PointsPerPixel, TextWidth);
        var height = width * image.AspectRatio;
        if (height > TextHeight)
        {
            height = TextHeight;
            width = height / image.AspectRatio;
        }

        _ = Reserve(height + (_options.BodySize * SpaceBefore));
        var tags = _creator.Tags;
        var decorative = string.IsNullOrWhiteSpace(block.AlternativeText);
        if (tags is not null && decorative)
        {
            _canvas.BeginArtifact(PdfArtifactKind.Layout);
        }
        else if (tags is not null)
        {
            OpenContent(tags.AddElement(tags.Root, "Figure", new PdfTagOptions { AlternativeText = block.AlternativeText }));
        }

        _canvas.DrawImage(image, _options.Margin, _top - height, width, height);
        if (tags is not null && decorative)
        {
            _canvas.EndArtifact();
        }

        CloseContent();
        _top -= height + (_options.BodySize * SpaceAfter);
    }

    /// <summary>Starts a page.</summary>
    private void StartPage()
    {
        _canvas = _creator.BeginPage(_options.PageWidth, _options.PageHeight);
        _hasPage = true;
        _top = _options.PageHeight - _options.Margin;
    }

    /// <summary>Finishes the open page, adding its number first when asked.</summary>
    private void EndPage()
    {
        if (!_hasPage)
        {
            return;
        }

        CloseContent();
        if (_options.PageNumbers)
        {
            WritePageNumber();
        }

        _ = _canvas.Finish();
        _hasPage = false;
        _pages++;
    }

    /// <summary>Writes the page number at the foot of the page, as an artifact in a tagged document.</summary>
    private void WritePageNumber()
    {
        var size = _options.BodySize * PageNumberScale;
        var prefix = _options.PageNumberPrefix;
        var suffix = _options.PageNumberSuffix;
        var rented = ArrayPool<char>.Shared.Rent(prefix.Length + suffix.Length + MaxPageDigits);
        try
        {
            var label = FormatPageLabel(_pages + 1, prefix, suffix, rented);
            var tagged = _creator.Tags is not null;
            if (tagged)
            {
                _canvas.BeginArtifact(PdfArtifactKind.Pagination);
            }

            var width = _options.BodyFont.Measure(label, size);
            var x = GetPageNumberX(width);
            var y = Math.Clamp(_options.PageNumberOffset ?? (_options.Margin * Half), 0F, Math.Max(0F, _options.PageHeight - size));
            _canvas.DrawText(_options.BodyFont, size, x, y, label);
            if (tagged)
            {
                _canvas.EndArtifact();
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(rented);
        }
    }

    /// <summary>Calculates the horizontal start position of a page number within the content margins.</summary>
    /// <param name="width">The measured advance of the page number label.</param>
    /// <returns>The baseline start x in points.</returns>
    private readonly float GetPageNumberX(float width)
    {
        var left = _options.Margin;
        var right = Math.Max(left, _options.PageWidth - _options.Margin - width);
        var raw = _options.PageNumberAlignment switch
        {
            PdfTableAlignment.Left => left,
            PdfTableAlignment.Right => right,
            _ => (_options.PageWidth - width) * Half,
        };
        return Math.Clamp(raw, left, right);
    }
}
