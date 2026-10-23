// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using HyperPdf.Core.Accessibility;

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>
/// A stretch of an element's text that a screen reader moves and reads. The range holds the offsets only and reads the element's
/// current text on every call, clamping the offsets to it, so a range stays valid when the page text changes under it.
/// </summary>
[GeneratedComClass]
[DebuggerDisplay("UiaTextRange: {_element.Id} {_start}..{_end}")]
internal sealed partial class UiaTextRange : ITextRangeProvider
{
    /// <summary>The element the text belongs to.</summary>
    private readonly UiaElement _element;

    /// <summary>The offset where the range starts.</summary>
    private int _start;

    /// <summary>The offset just after the range.</summary>
    private int _end;

    /// <summary>Initializes a new instance of the <see cref="UiaTextRange"/> class.</summary>
    /// <param name="element">The element the text belongs to.</param>
    /// <param name="start">The offset where the range starts.</param>
    /// <param name="end">The offset just after the range.</param>
    internal UiaTextRange(UiaElement element, int start, int end)
    {
        _element = element;
        _start = start;
        _end = end;
    }

    /// <inheritdoc/>
    public ITextRangeProvider Clone()
    {
        var (start, end) = Current(_element.Text.Value);
        return new UiaTextRange(_element, start, end);
    }

    /// <inheritdoc/>
    public bool Compare(ITextRangeProvider range)
    {
        var other = Other(range);
        var text = _element.Text.Value;
        return other._element.Id == _element.Id && Current(text) == other.Current(text);
    }

    /// <inheritdoc/>
    public void CompareEndpoints(TextPatternRangeEndpoint endpoint, ITextRangeProvider targetRange, TextPatternRangeEndpoint targetEndpoint, out int order)
    {
        var other = Other(targetRange);
        var text = _element.Text.Value;
        order = Endpoint(Current(text), endpoint).CompareTo(Endpoint(other.Current(text), targetEndpoint));
    }

    /// <inheritdoc/>
    public void ExpandToEnclosingUnit(TextRangeUnit unit)
    {
        var text = _element.Text.Value;
        var range = Current(text);
        var first = TextRanges.Expand(text, range.Start, unit);
        var last = range.IsEmpty ? first : TextRanges.Expand(text, range.End - 1, unit);
        Set(new(first.Start, Math.Max(first.End, last.End)));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ITextRangeProvider? FindAttribute(int attribute, ComVariant value, bool backward) => null;

    /// <inheritdoc/>
    public ITextRangeProvider? FindText(string text, bool backward, bool ignoreCase)
    {
        var all = _element.Text.Value;
        var range = Current(all);
        var inside = all.AsSpan(range.Start, range.Length);
        var comparison = ignoreCase ? StringComparison.CurrentCultureIgnoreCase : StringComparison.CurrentCulture;
        var found = backward ? inside.LastIndexOf(text, comparison) : inside.IndexOf(text, comparison);
        return found < 0 ? null : new UiaTextRange(_element, range.Start + found, range.Start + found + text.Length);
    }

    /// <inheritdoc/>
    public ComVariant GetAttributeValue(int attribute)
    {
        Marshal.ThrowExceptionForHR(NativeMethods.UiaGetReservedNotSupportedValue(out var notSupported));
        return ComVariant.FromUnknown(notSupported);
    }

    /// <inheritdoc/>
    public nint GetBoundingRectangles()
    {
        if (Current(_element.Text.Value).IsEmpty)
        {
            return SafeArrays.FromDouble([]);
        }

        var bounds = _element.Node.Bounds;
        return SafeArrays.FromDouble([bounds.X, bounds.Y, bounds.Width, bounds.Height]);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IRawElementProviderSimple? GetEnclosingElement() => _element;

    /// <inheritdoc/>
    public string GetText(int maxLength)
    {
        var text = _element.Text.Value;
        var range = Current(text);
        var length = maxLength < 0 ? range.Length : Math.Min(range.Length, maxLength);
        return text.Substring(range.Start, length);
    }

    /// <inheritdoc/>
    public void Move(TextRangeUnit unit, int count, out int moved)
    {
        var move = TextRanges.Move(_element.Text.Value, Current(_element.Text.Value), unit, count);
        Set(move.Range);
        moved = move.Moved;
    }

    /// <inheritdoc/>
    public void MoveEndpointByUnit(TextPatternRangeEndpoint endpoint, TextRangeUnit unit, int count, out int moved)
    {
        var text = _element.Text.Value;
        var move = TextRanges.MoveEndpoint(text, Current(text), endpoint == TextPatternRangeEndpoint.End, unit, count);
        Set(move.Range);
        moved = move.Moved;
    }

    /// <inheritdoc/>
    public void MoveEndpointByRange(TextPatternRangeEndpoint endpoint, ITextRangeProvider targetRange, TextPatternRangeEndpoint targetEndpoint)
    {
        var text = _element.Text.Value;
        var range = Current(text);
        var offset = Endpoint(Other(targetRange).Current(text), targetEndpoint);
        Set(endpoint == TextPatternRangeEndpoint.End ? new(Math.Min(range.Start, offset), offset) : new(offset, Math.Max(range.End, offset)));
    }

    /// <inheritdoc/>
    public void Select() => _ = _element.Tree.SetCaret(_element.Id, Current(_element.Text.Value).Start);

    /// <inheritdoc/>
    public void AddToSelection() => throw UiaErrors.Unsupported();

    /// <inheritdoc/>
    public void RemoveFromSelection() => throw UiaErrors.Unsupported();

    /// <inheritdoc/>
    public void ScrollIntoView(bool alignToTop)
    {
        // The page scrolls to the caret as it moves, and the text has no scroll position of its own to set.
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public nint GetChildren() => SafeArrays.FromUnknowns([]);

    /// <summary>Gets the offset of one end of a range.</summary>
    /// <param name="range">The range.</param>
    /// <param name="endpoint">The end.</param>
    /// <returns>The offset.</returns>
    private static int Endpoint(TextSpan range, TextPatternRangeEndpoint endpoint) => endpoint == TextPatternRangeEndpoint.Start ? range.Start : range.End;

    /// <summary>Gets the range as one of this app's, which is the only kind a client can pass back.</summary>
    /// <param name="range">The range a client passed.</param>
    /// <returns>The range.</returns>
    /// <exception cref="ArgumentException">The range is not one this app made.</exception>
    private static UiaTextRange Other(ITextRangeProvider range) => range as UiaTextRange ?? throw new ArgumentException("The range is not one of this application's.", nameof(range));

    /// <summary>Gets the range clamped to the text.</summary>
    /// <param name="text">The element's text.</param>
    /// <returns>The range.</returns>
    private TextSpan Current(string text)
    {
        var start = Math.Clamp(_start, 0, text.Length);
        return new(start, Math.Clamp(_end, start, text.Length));
    }

    /// <summary>Stores a new range.</summary>
    /// <param name="range">The range.</param>
    private void Set(TextSpan range)
    {
        _start = range.Start;
        _end = range.End;
    }
}
