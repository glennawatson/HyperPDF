// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Accessibility;
using Tmds.DBus.Protocol;

namespace HyperPdf.Platform.Linux.Accessibility.AtSpi;

/// <summary>
/// Answers the methods and properties of <c>org.a11y.atspi.Text</c>, which is how a screen reader moves through the page
/// by character, word, sentence and line.
/// </summary>
internal static class AtSpiTextMethods
{
    /// <summary>The boundary type for a character.</summary>
    private const uint BoundaryCharacter = 0;

    /// <summary>The boundary type for the start of a word.</summary>
    private const uint BoundaryWordStart = 1;

    /// <summary>The boundary type for the end of a word.</summary>
    private const uint BoundaryWordEnd = 2;

    /// <summary>The boundary type for the start of a sentence.</summary>
    private const uint BoundarySentenceStart = 3;

    /// <summary>The boundary type for the end of a sentence.</summary>
    private const uint BoundarySentenceEnd = 4;

    /// <summary>The granularity for a word, in the numbers <c>GetStringAtOffset</c> takes.</summary>
    private const uint GranularityWord = 1;

    /// <summary>The granularity for a sentence.</summary>
    private const uint GranularitySentence = 2;

    /// <summary>The granularity for a line.</summary>
    private const uint GranularityLine = 3;

    /// <summary>The granularity for a paragraph.</summary>
    private const uint GranularityParagraph = 4;

    /// <summary>The signature of a text stretch: the text and where it starts and ends.</summary>
    private const string StretchSignature = "sii";

    /// <summary>The signature of a set of attributes and the stretch they cover.</summary>
    private const string AttributeRunSignature = "a{ss}ii";

    /// <summary>How many offsets a request for the box around one character carries.</summary>
    private const int CharacterOffsets = 1;

    /// <summary>How many offsets a request for the box around a range carries.</summary>
    private const int RangeOffsets = 2;

    /// <summary>Which stretch around an offset is asked for.</summary>
    private enum StretchKind
    {
        /// <summary>The stretch that holds the offset.</summary>
        At = 0,

        /// <summary>The stretch before the one that holds the offset.</summary>
        Before = 1,

        /// <summary>The stretch after the one that holds the offset.</summary>
        After = 2,
    }

    /// <summary>Gets the methods by name.</summary>
    internal static Dictionary<string, AtSpiMethod> Methods { get; } = new(StringComparer.Ordinal)
    {
        ["GetText"] = static (in AtSpiCall call, ref Reader reader) => ReplyText(call, reader.ReadInt32(), reader.ReadInt32()),
        ["SetCaretOffset"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyBool(call.Session.Tree.SetCaret(call.Node.Id, reader.ReadInt32())),
        ["GetTextAtOffset"] = static (in AtSpiCall call, ref Reader reader) => ReplyStretch(call, reader.ReadInt32(), FromBoundary(reader.ReadUInt32()), StretchKind.At),
        ["GetTextBeforeOffset"] = static (in AtSpiCall call, ref Reader reader) => ReplyStretch(call, reader.ReadInt32(), FromBoundary(reader.ReadUInt32()), StretchKind.Before),
        ["GetTextAfterOffset"] = static (in AtSpiCall call, ref Reader reader) => ReplyStretch(call, reader.ReadInt32(), FromBoundary(reader.ReadUInt32()), StretchKind.After),
        ["GetStringAtOffset"] = static (in AtSpiCall call, ref Reader reader) => ReplyStretch(call, reader.ReadInt32(), FromGranularity(reader.ReadUInt32()), StretchKind.At),
        ["GetCharacterAtOffset"] = static (in AtSpiCall call, ref Reader reader) => ReplyCharacter(call, reader.ReadInt32()),
        ["GetCharacterExtents"] = static (in AtSpiCall call, ref Reader reader) => ReplyExtentsAfterOffsets(call, ref reader, CharacterOffsets),
        ["GetRangeExtents"] = static (in AtSpiCall call, ref Reader reader) => ReplyExtentsAfterOffsets(call, ref reader, RangeOffsets),
        ["GetOffsetAtPoint"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyInt32(0),
        ["GetNSelections"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyInt32(HasSelection(call) ? 1 : 0),
        ["GetSelection"] = static (in AtSpiCall call, ref Reader reader) => ReplySelection(call, reader.ReadInt32()),
        ["AddSelection"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyBool(false),
        ["RemoveSelection"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyBool(false),
        ["SetSelection"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyBool(false),
        ["GetAttributes"] = static (in AtSpiCall call, ref Reader reader) => ReplyAttributeRun(call, reader.ReadInt32()),
        ["GetAttributeRun"] = static (in AtSpiCall call, ref Reader reader) => ReplyAttributeRun(call, reader.ReadInt32()),
        ["GetAttributeValue"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyString(string.Empty),
        ["GetDefaultAttributes"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyStringMap(new Dictionary<string, string>(StringComparer.Ordinal)),
        ["GetDefaultAttributeSet"] = static (in AtSpiCall call, ref Reader reader) => call.ReplyStringMap(new Dictionary<string, string>(StringComparer.Ordinal)),
    };

    /// <summary>Gets the properties by name.</summary>
    internal static Dictionary<string, AtSpiProperty> Properties { get; } = new(StringComparer.Ordinal)
    {
        ["CharacterCount"] = static (in AtSpiCall call, ref MessageWriter writer) => writer.WriteVariantInt32(call.Node.Text?.Value.Length ?? 0),
        ["CaretOffset"] = static (in AtSpiCall call, ref MessageWriter writer) => writer.WriteVariantInt32(call.Node.Text?.CaretOffset ?? -1),
    };

    /// <summary>Gets the text of the element.</summary>
    /// <param name="call">The request.</param>
    /// <returns>The text, or an empty string.</returns>
    private static string ValueOf(in AtSpiCall call) => call.Node.Text?.Value ?? string.Empty;

    /// <summary>Maps an old style boundary type to a size of stretch.</summary>
    /// <param name="boundary">The boundary type.</param>
    /// <returns>The granularity.</returns>
    private static TextGranularity FromBoundary(uint boundary) => boundary switch
    {
        BoundaryCharacter => TextGranularity.Character,
        BoundaryWordStart or BoundaryWordEnd => TextGranularity.Word,
        BoundarySentenceStart or BoundarySentenceEnd => TextGranularity.Sentence,
        _ => TextGranularity.Line,
    };

    /// <summary>Maps a granularity number to a size of stretch.</summary>
    /// <param name="granularity">The granularity number.</param>
    /// <returns>The granularity.</returns>
    private static TextGranularity FromGranularity(uint granularity) => granularity switch
    {
        GranularityWord => TextGranularity.Word,
        GranularitySentence => TextGranularity.Sentence,
        GranularityLine => TextGranularity.Line,
        GranularityParagraph => TextGranularity.Paragraph,
        _ => TextGranularity.Character,
    };

    /// <summary>Checks whether any text is selected.</summary>
    /// <param name="call">The request.</param>
    /// <returns>Whether a selection exists.</returns>
    private static bool HasSelection(in AtSpiCall call) => call.Node.Text is { SelectionStart: >= 0 } text && text.SelectionEnd > text.SelectionStart;

    /// <summary>Replies with part of the text.</summary>
    /// <param name="call">The request.</param>
    /// <param name="start">The offset of the first character.</param>
    /// <param name="end">The offset after the last character; -1 or past the end means the end of the text.</param>
    private static void ReplyText(in AtSpiCall call, int start, int end)
    {
        var value = ValueOf(call);
        var from = Math.Clamp(start, 0, value.Length);
        var to = end < 0 || end > value.Length ? value.Length : Math.Max(from, end);
        call.ReplyString(value[from..to]);
    }

    /// <summary>Replies with a stretch of text and where it starts and ends.</summary>
    /// <param name="call">The request.</param>
    /// <param name="offset">The offset the stretch is found from.</param>
    /// <param name="granularity">The size of stretch.</param>
    /// <param name="kind">Which stretch around the offset is wanted.</param>
    private static void ReplyStretch(in AtSpiCall call, int offset, TextGranularity granularity, StretchKind kind)
    {
        var value = ValueOf(call);
        var span = kind switch
        {
            StretchKind.Before => TextBoundaries.Before(value, offset, granularity),
            StretchKind.After => TextBoundaries.After(value, offset, granularity),
            _ => TextBoundaries.At(value, offset, granularity),
        };

        using var writer = call.Context.CreateReplyWriter(StretchSignature);
        writer.WriteString(value[span.Start..span.End]);
        writer.WriteInt32(span.Start);
        writer.WriteInt32(span.End);
        call.Context.Reply(writer.CreateMessage());
    }

    /// <summary>Replies with the character at an offset.</summary>
    /// <param name="call">The request.</param>
    /// <param name="offset">The offset.</param>
    private static void ReplyCharacter(in AtSpiCall call, int offset)
    {
        var value = ValueOf(call);
        call.ReplyInt32(offset >= 0 && offset < value.Length ? value[offset] : 0);
    }

    /// <summary>
    /// Replies with the box around a character or a range of text, which is the whole element because the app keeps no glyph
    /// boxes for the page text. The offsets that come first in the request are read and ignored.
    /// </summary>
    /// <param name="call">The request.</param>
    /// <param name="reader">The arguments.</param>
    /// <param name="offsets">How many offsets come before the coordinate type.</param>
    private static void ReplyExtentsAfterOffsets(in AtSpiCall call, ref Reader reader, int offsets)
    {
        for (var index = 0; index < offsets; index++)
        {
            _ = reader.ReadInt32();
        }

        var bounds = AtSpiGeometry.Extents(call.Session.Tree, call.Node, reader.ReadUInt32());
        var writer = call.Context.CreateReplyWriter("(iiii)");
        try
        {
            AtSpiWire.WriteRectangle(ref writer, bounds.X, bounds.Y, bounds.Width, bounds.Height);
            call.Context.Reply(writer.CreateMessage());
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Replies with the selection.</summary>
    /// <param name="call">The request.</param>
    /// <param name="index">The selection's index; only the first exists.</param>
    private static void ReplySelection(in AtSpiCall call, int index)
    {
        if (index != 0 || !HasSelection(call))
        {
            call.Context.ReplyError("org.freedesktop.DBus.Error.InvalidArgs", "There is no such selection.");
            return;
        }

        using var writer = call.Context.CreateReplyWriter("ii");
        writer.WriteInt32(call.Node.Text!.SelectionStart);
        writer.WriteInt32(call.Node.Text.SelectionEnd);
        call.Context.Reply(writer.CreateMessage());
    }

    /// <summary>Replies that the whole text has no formatting.</summary>
    /// <param name="call">The request.</param>
    /// <param name="offset">The offset asked about.</param>
    private static void ReplyAttributeRun(in AtSpiCall call, int offset)
    {
        var writer = call.Context.CreateReplyWriter(AttributeRunSignature);
        try
        {
            AtSpiWire.WriteStringMap(ref writer, new Dictionary<string, string>(StringComparer.Ordinal));
            writer.WriteInt32(0);
            writer.WriteInt32(Math.Max(ValueOf(call).Length, Math.Max(offset, 0)));
            call.Context.Reply(writer.CreateMessage());
        }
        finally
        {
            writer.Dispose();
        }
    }
}
