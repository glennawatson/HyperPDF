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

    /// <summary>The cached rich table grid built during keep-with-next lookahead, or <see langword="null"/>.</summary>
    private RichTableGrid? _cachedRichGrid;

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

        var bodyFont = FontFallbackResolver.ApplyFallbacks(options.BodyFont, options.FallbackFonts);
        var headingFont = options.HeadingFont is { } heading
            ? FontFallbackResolver.ResolveScopedFont(heading, options.FallbackFonts, bodyFont)
            : null;
        _creator = creator;
        _options = ReferenceEquals(bodyFont, options.BodyFont) && ReferenceEquals(headingFont, options.HeadingFont)
            ? options
            : options with { BodyFont = bodyFont, HeadingFont = headingFont };
        _cancellationToken = cancellationToken;
        _lines = default;
        _canvas = default;
        _hasPage = false;
        _top = 0;
        _pages = 0;
        _headingLevel = 0;
        _open = null;
        _cachedRichGrid = null;
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

    /// <summary>Validates a <see cref="PdfFlowRule"/> configuration.</summary>
    /// <param name="rule">The rule to validate.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rule"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A numeric property on <paramref name="rule"/> is invalid.</exception>
    internal static void ValidateRule(PdfFlowRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ValidatePositiveFinite(rule.Thickness, nameof(rule));
        rule.Color.Validate();
        if (rule.Width is { } width)
        {
            ValidatePositiveFinite(width, nameof(rule));
        }

        if (rule.SpaceBefore is { } before)
        {
            ValidateNonNegativeFinite(before, nameof(rule));
        }

        if (rule.SpaceAfter is { } after)
        {
            ValidateNonNegativeFinite(after, nameof(rule));
        }
    }

    /// <summary>Abandons a page that was not finished and returns the pooled line storage.</summary>
    internal void Abandon()
    {
        _canvas.Dispose();
        _lines.Dispose();
        _cachedRichGrid = null;
    }

    /// <summary>Gets a cached <see cref="RichTableGrid"/> for <paramref name="table"/> or builds and caches it.</summary>
    /// <param name="table">The table to resolve.</param>
    /// <returns>The resolved grid, or <see langword="null"/> when the table is empty.</returns>
    internal RichTableGrid? GetOrBuildRichGrid(PdfFlowTable table)
    {
        if (_cachedRichGrid is { } cached && ReferenceEquals(cached.Table, table))
        {
            return cached;
        }

        _cachedRichGrid = RichTableGrid.Build(table, _options, TextWidth);
        return _cachedRichGrid;
    }

    /// <summary>Consumes a cached <see cref="RichTableGrid"/> for <paramref name="table"/> or builds a new one.</summary>
    /// <param name="table">The table to resolve.</param>
    /// <returns>The resolved grid, or <see langword="null"/> when the table is empty.</returns>
    internal RichTableGrid? ConsumeRichGrid(PdfFlowTable table)
    {
        if (_cachedRichGrid is { } cached && ReferenceEquals(cached.Table, table))
        {
            _cachedRichGrid = null;
            return cached;
        }

        return RichTableGrid.Build(table, _options, TextWidth);
    }

    /// <summary>Lays out the blocks.</summary>
    /// <param name="blocks">The blocks.</param>
    /// <returns>The number of pages added.</returns>
    internal int Run(IReadOnlyList<PdfFlowBlock> blocks)
    {
        for (var i = 0; i < blocks.Count; i++)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            var block = blocks[i];
            ArgumentNullException.ThrowIfNull(block);
            var nextKeepHeight = block.KeepWithNext && i + 1 < blocks.Count && blocks[i + 1] is not PdfFlowPageBreak
                ? EnsureKeepWithNextFits(blocks, i)
                : 0F;
            var keptWithPrevious = i > 0 && blocks[i - 1] is { KeepWithNext: true } and not PdfFlowPageBreak;
            WriteBlock(block, keptWithPrevious, nextKeepHeight);
        }

        EndPage();
        return _pages;
    }

    /// <summary>Gets the space that will be added before a block on the currently open page.</summary>
    /// <param name="size">The block's text size.</param>
    /// <returns>The space in points, or zero when at the top of a page.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly float GetPendingSpaceBefore(float size) =>
        _hasPage && _top < PageTop ? size * SpaceBefore : 0F;

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

        if (_open is not null)
        {
            _canvas.EndContent();
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

    /// <summary>Validates that a float value is finite and strictly positive.</summary>
    /// <param name="value">The value to check.</param>
    /// <param name="paramName">The parameter name.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not finite and positive.</exception>
    private static void ValidatePositiveFinite(float value, string paramName)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0F)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "The value must be a finite positive number.");
        }
    }

    /// <summary>Validates that a float value is finite and non-negative.</summary>
    /// <param name="value">The value to check.</param>
    /// <param name="paramName">The parameter name.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not finite and non-negative.</exception>
    private static void ValidateNonNegativeFinite(float value, string paramName)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0F)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "The value must be a finite non-negative number.");
        }
    }

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
    /// <param name="keptWithPrevious">Whether the immediately preceding block requested <see cref="PdfFlowBlock.KeepWithNext"/>.</param>
    /// <param name="nextKeepHeight">The height required after this block when this block has <see cref="PdfFlowBlock.KeepWithNext"/>.</param>
    private void WriteBlock(PdfFlowBlock block, bool keptWithPrevious, float nextKeepHeight)
    {
        switch (block)
        {
            case PdfFlowHeading heading:
            {
                WriteHeading(heading, nextKeepHeight);
                break;
            }

            case PdfFlowParagraph paragraph:
            {
                WriteText(_options.BodyFont, _options.BodySize, paragraph.Text, "P", nextKeepHeight);
                break;
            }

            case PdfFlowImage image:
            {
                WriteImage(image, keptWithPrevious);
                break;
            }

            case PdfFlowRule rule:
            {
                WriteRule(rule);
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

    /// <summary>
    /// Ensures that a <see cref="PdfFlowBlock.KeepWithNext"/> chain starting at <paramref name="startIndex"/> fits on the
    /// current page, or starts a new page before it, and returns the trailing keep height required for oversized text blocks.
    /// </summary>
    /// <param name="blocks">The flow blocks.</param>
    /// <param name="startIndex">The index of the block with <see cref="PdfFlowBlock.KeepWithNext"/>.</param>
    /// <returns>The height to reserve after the last line when the block at <paramref name="startIndex"/> exceeds a single page, or zero otherwise.</returns>
    private float EnsureKeepWithNextFits(IReadOnlyList<PdfFlowBlock> blocks, int startIndex)
    {
        var simHeading = _headingLevel;
        var firstFull = MeasureBlockHeight(blocks[startIndex], ref simHeading, fullBlock: true);
        if (firstFull > TextHeight)
        {
            return MeasureNextBlockOpeningWithSpacing(blocks[startIndex + 1]);
        }

        if (_hasPage && _top < PageTop && NeedsPageBreakForChain(blocks, startIndex, simHeading, firstFull))
        {
            StartNewPage();
        }

        return 0F;
    }

    /// <summary>Evaluates whether the keep-with-next chain starting after <paramref name="startIndex"/> overflows the open page.</summary>
    /// <param name="blocks">The flow blocks.</param>
    /// <param name="startIndex">The starting block index.</param>
    /// <param name="simHeading">The simulated heading level cursor.</param>
    /// <param name="firstFull">The full height of the block at <paramref name="startIndex"/>.</param>
    /// <returns><see langword="true"/> when a new page should be started before <paramref name="startIndex"/>.</returns>
    private bool NeedsPageBreakForChain(
        IReadOnlyList<PdfFlowBlock> blocks,
        int startIndex,
        int simHeading,
        float firstFull)
    {
        var firstBlock = blocks[startIndex];
        var freshRequired = firstFull + GetBlockSpaceAfter(firstBlock, simHeading);
        var currentRequired = GetBlockSpaceBefore(firstBlock, simHeading) + freshRequired;
        var available = _top - _options.Margin;
        for (var j = startIndex + 1; j < blocks.Count; j++)
        {
            var next = blocks[j];
            ArgumentNullException.ThrowIfNull(next);
            if (next is PdfFlowPageBreak)
            {
                return false;
            }

            var targetHeading = simHeading;
            var nextOpening = MeasureBlockHeight(next, ref targetHeading, fullBlock: false);
            var nextBefore = GetBlockSpaceBefore(next, targetHeading);
            if (currentRequired + nextBefore + nextOpening > available
                && (j == startIndex + 1 || freshRequired + nextBefore + nextOpening <= TextHeight))
            {
                return true;
            }

            if (!next.KeepWithNext)
            {
                return false;
            }

            var step = nextBefore + MeasureBlockHeight(next, ref simHeading, fullBlock: true) + GetBlockSpaceAfter(next, simHeading);
            if (freshRequired + step > TextHeight)
            {
                return false;
            }

            currentRequired += step;
            freshRequired += step;
        }

        return false;
    }

    /// <summary>Measures the opening height (including space before) of the block following a keep-with-next block.</summary>
    /// <param name="next">The following block.</param>
    /// <returns>The height in points.</returns>
    private float MeasureNextBlockOpeningWithSpacing(PdfFlowBlock next)
    {
        var simHeading = _headingLevel;
        var opening = MeasureBlockHeight(next, ref simHeading, fullBlock: false);
        var before = GetBlockSpaceBefore(next, simHeading);
        return Math.Min(before + opening, TextHeight);
    }

    /// <summary>Measures the vertical height of a block (either its full content height or its opening slice height).</summary>
    /// <param name="block">The block to measure.</param>
    /// <param name="headingLevel">The simulated heading level cursor.</param>
    /// <param name="fullBlock">Whether to measure the full block or only its opening slice.</param>
    /// <returns>The block height in points.</returns>
    private float MeasureBlockHeight(PdfFlowBlock block, ref int headingLevel, bool fullBlock)
    {
        switch (block)
        {
            case PdfFlowHeading heading:
            {
                headingLevel = Math.Clamp(heading.Level, 1, Math.Min(headingLevel + 1, MaxHeadingLevel));
                var font = _options.HeadingFont ?? _options.BodyFont;
                var size = _options.BodySize * GetHeadingScale(headingLevel);
                return MeasureTextHeight(font, size, heading.Text, fullBlock);
            }

            case PdfFlowParagraph paragraph:
            {
                return MeasureTextHeight(_options.BodyFont, _options.BodySize, paragraph.Text, fullBlock);
            }

            case PdfFlowRule rule:
            {
                ValidateRule(rule);
                return Math.Min(rule.Thickness, TextHeight);
            }

            case PdfFlowImage image:
            {
                return MeasureImageOpeningHeight(image);
            }

            case PdfFlowCodeBlock code:
            {
                return PdfCodeFlow.MeasureHeight(code, _options, TextWidth, TextHeight, fullBlock);
            }

            case PdfFlowTable table:
            {
                return PdfTableFlow.MeasureOpeningHeight(ref this, table);
            }

            default:
            {
                return 0F;
            }
        }
    }

    /// <summary>Measures the height of a heading or paragraph.</summary>
    /// <param name="font">The font.</param>
    /// <param name="size">The font size.</param>
    /// <param name="text">The text.</param>
    /// <param name="fullBlock">Whether to measure all wrapped lines or only the first line.</param>
    /// <returns>The height in points.</returns>
    private float MeasureTextHeight(PdfCanvasFont font, float size, string text, bool fullBlock)
    {
        ArgumentNullException.ThrowIfNull(text);
        var leading = size * _options.LineSpacing;
        if (!fullBlock)
        {
            return leading;
        }

        _lines.Clear();
        PdfTextWrapper.Wrap(font, size, text, TextWidth, ref _lines);
        return Math.Max(_lines.Count, 1) * leading;
    }

    /// <summary>Measures the height of an image block for keep-with-next pagination.</summary>
    /// <param name="block">The image block.</param>
    /// <returns>The height in points.</returns>
    private readonly float MeasureImageOpeningHeight(PdfFlowImage block)
    {
        ArgumentNullException.ThrowIfNull(block);
        var image = block.Image;
        var desired = block.WidthPoints > 0F ? block.WidthPoints : image.Width * PointsPerPixel;
        var width = block.Fit == PdfFlowImageFit.ClipOverflow ? desired : Math.Min(desired, TextWidth);
        return Math.Min(width * image.AspectRatio, TextHeight);
    }

    /// <summary>Gets the vertical space added before a block when it is not at the top of a page.</summary>
    /// <param name="block">The block.</param>
    /// <param name="headingLevel">The resolved heading level when <paramref name="block"/> is a heading.</param>
    /// <returns>The space before in points.</returns>
    private readonly float GetBlockSpaceBefore(PdfFlowBlock block, int headingLevel) =>
        block switch
        {
            PdfFlowHeading => _options.BodySize * GetHeadingScale(headingLevel) * SpaceBefore,
            PdfFlowRule rule => rule.SpaceBefore ?? (_options.BodySize * SpaceBefore),
            PdfFlowCodeBlock code => code.FontSize * SpaceBefore,
            PdfFlowTable table => (table.FontSize ?? _options.BodySize) * SpaceBefore,
            _ => _options.BodySize * SpaceBefore,
        };

    /// <summary>Gets the vertical space added after a block.</summary>
    /// <param name="block">The block.</param>
    /// <param name="headingLevel">The resolved heading level when <paramref name="block"/> is a heading.</param>
    /// <returns>The space after in points.</returns>
    private readonly float GetBlockSpaceAfter(PdfFlowBlock block, int headingLevel) =>
        block switch
        {
            PdfFlowHeading => _options.BodySize * GetHeadingScale(headingLevel) * SpaceAfter,
            PdfFlowRule rule => rule.SpaceAfter ?? (_options.BodySize * SpaceAfter),
            PdfFlowCodeBlock code => code.FontSize * SpaceAfter,
            PdfFlowTable table => (table.FontSize ?? _options.BodySize) * SpaceAfter,
            _ => _options.BodySize * SpaceAfter,
        };

    /// <summary>Writes a heading, keeping its level no more than one below the heading before it.</summary>
    /// <param name="heading">The heading.</param>
    /// <param name="nextKeepHeight">The height required after this heading when <see cref="PdfFlowBlock.KeepWithNext"/> is enabled.</param>
    private void WriteHeading(PdfFlowHeading heading, float nextKeepHeight)
    {
        _headingLevel = Math.Clamp(heading.Level, 1, Math.Min(_headingLevel + 1, MaxHeadingLevel));
        var font = _options.HeadingFont ?? _options.BodyFont;
        var type = HeadingTypes[_headingLevel - 1];
        WriteText(font, _options.BodySize * GetHeadingScale(_headingLevel), heading.Text, type, nextKeepHeight);
    }

    /// <summary>Writes wrapped text as one structure element, starting a new page when the page is full.</summary>
    /// <param name="font">The font.</param>
    /// <param name="size">The font size.</param>
    /// <param name="text">The text.</param>
    /// <param name="type">The structure type.</param>
    /// <param name="nextKeepHeight">The vertical space required after the last line when keeping with the next block.</param>
    private void WriteText(PdfCanvasFont font, float size, string text, string type, float nextKeepHeight)
    {
        var leading = size * _options.LineSpacing;
        _lines.Clear();
        PdfTextWrapper.Wrap(font, size, text, TextWidth, ref _lines);
        var element = _creator.Tags is { } tags ? tags.AddElement(tags.Root, type) : null;
        for (var i = 0; i < _lines.Count; i++)
        {
            _ = Reserve(GetLineReservation(i, _lines.Count, size, leading, nextKeepHeight));
            if (i == 0 && _top < PageTop)
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

    /// <summary>Computes the vertical space to reserve before drawing line <paramref name="lineIndex"/> of a text block.</summary>
    /// <param name="lineIndex">The zero-based line index.</param>
    /// <param name="lineCount">The total number of lines in the block.</param>
    /// <param name="size">The font size.</param>
    /// <param name="leading">The line pitch.</param>
    /// <param name="nextKeepHeight">The vertical space required after the last line when keeping with the next block.</param>
    /// <returns>The height to reserve in points.</returns>
    private readonly float GetLineReservation(int lineIndex, int lineCount, float size, float leading, float nextKeepHeight)
    {
        var before = lineIndex == 0 ? GetPendingSpaceBefore(size) : 0F;
        var tailKeep = lineIndex == lineCount - 1 && nextKeepHeight > 0F ? (size * SpaceAfter) + nextKeepHeight : 0F;
        return Math.Min(leading + before + tailKeep, TextHeight);
    }

    /// <summary>Writes a horizontal rule block, wrapping it in a layout artifact when the document is tagged.</summary>
    /// <param name="rule">The rule block.</param>
    private void WriteRule(PdfFlowRule rule)
    {
        ValidateRule(rule);
        var thickness = Math.Min(rule.Thickness, TextHeight);
        var spaceBefore = rule.SpaceBefore ?? (_options.BodySize * SpaceBefore);
        var spaceAfter = rule.SpaceAfter ?? (_options.BodySize * SpaceAfter);
        var pendingBefore = _hasPage && _top < PageTop ? spaceBefore : 0F;
        _ = Reserve(Math.Min(thickness + pendingBefore, TextHeight));
        if (_top < PageTop)
        {
            _top -= spaceBefore;
        }

        CloseContent();
        var tagged = _creator.Tags is not null;
        if (tagged)
        {
            _canvas.BeginArtifact(PdfArtifactKind.Layout);
        }

        var width = rule.Width is { } requested ? Math.Min(requested, TextWidth) : TextWidth;
        var x = GetAlignedX(width, rule.Alignment);
        var lineY = _top - (thickness * Half);
        var stroke = new PdfStroke(rule.Color.Red, rule.Color.Green, rule.Color.Blue, thickness);
        _canvas.DrawLine(stroke, x, lineY, x + width, lineY);

        if (tagged)
        {
            _canvas.EndArtifact();
        }

        _top -= thickness + spaceAfter;
    }

    /// <summary>Writes an image on a line of its own according to its <see cref="PdfFlowImage.Fit"/> mode.</summary>
    /// <param name="block">The image block.</param>
    /// <param name="keptWithPrevious">Whether the immediately preceding block requested <see cref="PdfFlowBlock.KeepWithNext"/>.</param>
    private void WriteImage(PdfFlowImage block, bool keptWithPrevious)
    {
        ArgumentNullException.ThrowIfNull(block);
        var desired = block.WidthPoints > 0F ? block.WidthPoints : block.Image.Width * PointsPerPixel;
        if (block.Fit is PdfFlowImageFit.ScaleToWidth or PdfFlowImageFit.ClipOverflow)
        {
            WriteClippedOrWidthFitImage(block, desired, keptWithPrevious);
            return;
        }

        WriteScaledImage(block, desired, keptWithPrevious);
    }

    /// <summary>Writes an image using <see cref="PdfFlowImageFit.ScaleToPage"/> or <see cref="PdfFlowImageFit.ScaleToRemaining"/>.</summary>
    /// <param name="block">The image block.</param>
    /// <param name="desiredWidth">The desired width in points.</param>
    /// <param name="keptWithPrevious">Whether the immediately preceding block requested <see cref="PdfFlowBlock.KeepWithNext"/>.</param>
    private void WriteScaledImage(PdfFlowImage block, float desiredWidth, bool keptWithPrevious)
    {
        var image = block.Image;
        var width = Math.Min(desiredWidth, TextWidth);
        var height = width * image.AspectRatio;
        var spaceBefore = _options.BodySize * SpaceBefore;
        if (block.Fit == PdfFlowImageFit.ScaleToRemaining || (keptWithPrevious && _hasPage && _top < PageTop))
        {
            PrepareRemainingPageForImage(spaceBefore, _options.BodySize * _options.LineSpacing);
            var availableHeight = _top - _options.Margin;
            if (height > availableHeight)
            {
                height = availableHeight;
                width = height / image.AspectRatio;
            }
        }
        else
        {
            if (height > TextHeight)
            {
                height = TextHeight;
                width = height / image.AspectRatio;
            }

            var pendingBefore = _hasPage && _top < PageTop ? spaceBefore : 0F;
            _ = Reserve(Math.Min(height + pendingBefore, TextHeight));
            if (_top < PageTop)
            {
                _top -= spaceBefore;
            }
        }

        var x = GetAlignedX(width, block.Alignment);
        EmitImageWithTags(block, x, _top - height, width, height, width, height);
        _top -= height + (_options.BodySize * SpaceAfter);
    }

    /// <summary>Writes an image using <see cref="PdfFlowImageFit.ScaleToWidth"/> or <see cref="PdfFlowImageFit.ClipOverflow"/>.</summary>
    /// <param name="block">The image block.</param>
    /// <param name="desiredWidth">The desired width in points.</param>
    /// <param name="keptWithPrevious">Whether the immediately preceding block requested <see cref="PdfFlowBlock.KeepWithNext"/>.</param>
    private void WriteClippedOrWidthFitImage(PdfFlowImage block, float desiredWidth, bool keptWithPrevious)
    {
        var image = block.Image;
        var width = block.Fit == PdfFlowImageFit.ScaleToWidth ? Math.Min(desiredWidth, TextWidth) : desiredWidth;
        var height = width * image.AspectRatio;
        var spaceBefore = _options.BodySize * SpaceBefore;
        if (!_hasPage)
        {
            StartPage();
        }
        else if (_top < PageTop)
        {
            var fitsRemaining = _top - (height + spaceBefore) >= _options.Margin;
            var stayWithPrevious = keptWithPrevious && _top - spaceBefore - _options.Margin >= _options.BodySize * _options.LineSpacing;
            if (!fitsRemaining && !stayWithPrevious)
            {
                StartNewPage();
            }
            else
            {
                _top -= spaceBefore;
            }
        }

        var availHeight = Math.Max(0F, _top - _options.Margin);
        var boxWidth = Math.Min(width, TextWidth);
        var boxHeight = Math.Min(height, availHeight);
        var x = GetAlignedX(boxWidth, block.Alignment);
        EmitImageWithTags(block, x, _top - height, width, height, boxWidth, boxHeight);
        _top -= boxHeight + (_options.BodySize * SpaceAfter);
    }

    /// <summary>Prepares the open page for an image that scales to the remaining vertical space.</summary>
    /// <param name="spaceBefore">The vertical space before the image.</param>
    /// <param name="minLine">The minimum remaining space required to stay on the open page.</param>
    private void PrepareRemainingPageForImage(float spaceBefore, float minLine)
    {
        if (!_hasPage)
        {
            StartPage();
            return;
        }

        if (_top >= PageTop)
        {
            return;
        }

        if (_top - spaceBefore - _options.Margin < minLine)
        {
            StartNewPage();
            return;
        }

        _top -= spaceBefore;
    }

    /// <summary>Emits an image to the canvas wrapped in the appropriate <c>Figure</c> tag or layout artifact, clipping when needed.</summary>
    /// <param name="block">The flow image block.</param>
    /// <param name="x">The image left X.</param>
    /// <param name="y">The image bottom Y.</param>
    /// <param name="width">The unclipped image width.</param>
    /// <param name="height">The unclipped image height.</param>
    /// <param name="boxWidth">The visible content box width.</param>
    /// <param name="boxHeight">The visible content box height.</param>
    private void EmitImageWithTags(
        PdfFlowImage block,
        float x,
        float y,
        float width,
        float height,
        float boxWidth,
        float boxHeight)
    {
        var tags = _creator.Tags;
        var decorative = string.IsNullOrWhiteSpace(block.AlternativeText);
        if (tags is not null && decorative)
        {
            CloseContent();
            _canvas.BeginArtifact(PdfArtifactKind.Layout);
        }
        else if (tags is not null)
        {
            OpenContent(tags.AddElement(tags.Root, "Figure", new PdfTagOptions { AlternativeText = block.AlternativeText }));
        }

        if (width > boxWidth || height > boxHeight)
        {
            var clipBox = new ClippedImageBox(x, y, width, height, _options.Margin, _top - boxHeight, TextWidth, boxHeight);
            _canvas.DrawImageClipped(block.Image, in clipBox);
        }
        else
        {
            _canvas.DrawImage(block.Image, x, y, width, height);
        }

        if (tags is not null && decorative)
        {
            _canvas.EndArtifact();
        }

        CloseContent();
    }

    /// <summary>Computes the horizontal X coordinate of an element of width <paramref name="width"/> within the page margins.</summary>
    /// <param name="width">The element width in points.</param>
    /// <param name="alignment">The requested horizontal alignment.</param>
    /// <returns>The left X coordinate in points.</returns>
    private readonly float GetAlignedX(float width, PdfTableAlignment alignment)
    {
        var slack = Math.Max(0F, TextWidth - width);
        return alignment switch
        {
            PdfTableAlignment.Right => _options.Margin + slack,
            PdfTableAlignment.Center => _options.Margin + (slack * Half),
            _ => _options.Margin,
        };
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
