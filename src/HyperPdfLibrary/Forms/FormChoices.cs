// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>Reads the options and selection of a combo box or list box.</summary>
internal static class FormChoices
{
    /// <summary>The position of an option's export value in an option pair.</summary>
    private const int ValueSlot = 0;

    /// <summary>The position of an option's label in an option pair.</summary>
    private const int LabelSlot = 1;

    /// <summary>The selection size where bounded linear matching avoids a temporary table.</summary>
    private const int SmallSelection = 64;

    /// <summary>The decoded text length held on the stack during a comparison.</summary>
    private const int StackCharacters = 256;

    /// <summary>The number of decoded strings compared together.</summary>
    private const int ComparedStrings = 2;

    /// <summary>Counts a field's options.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <returns>The number of <c>/Opt</c> entries.</returns>
    internal static int Count(PdfDictionary field) => FieldAttributes.Find(field, KnownName.Opt).AsArray()?.Count ?? 0;

    /// <summary>Gets an option's export value.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <param name="index">The option index.</param>
    /// <returns>The value; empty when the option has none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string GetValue(PdfDictionary field, int index) => GetText(field, index, ValueSlot);

    /// <summary>Gets an option's label.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <param name="index">The option index.</param>
    /// <returns>The label; for an option that is a plain string, the string; empty when missing.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string GetLabel(PdfDictionary field, int index) => GetText(field, index, LabelSlot);

    /// <summary>Gets an option label's original PDF text bytes.</summary>
    /// <param name="field">The field.</param>
    /// <param name="index">The option index.</param>
    /// <returns>The label bytes, empty when absent.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<byte> GetLabelBytes(PdfDictionary field, int index) => GetTextBytes(field, index, LabelSlot);

    /// <summary>Gets every option's label.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <returns>The labels.</returns>
    internal static string[] GetLabels(PdfDictionary field)
    {
        var count = Count(field);
        if (count == 0)
        {
            return [];
        }

        var labels = new string[count];
        for (var i = 0; i < labels.Length; i++)
        {
            labels[i] = GetLabel(field, i);
        }

        return labels;
    }

    /// <summary>Finds the option with an export value.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <param name="value">The export value.</param>
    /// <returns>The option index, or -1.</returns>
    internal static int FindValue(PdfDictionary field, string value)
    {
        var count = Count(field);
        for (var i = 0; i < count; i++)
        {
            if (string.Equals(GetValue(field, i), value, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Finds the option with a label.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <param name="label">The label.</param>
    /// <returns>The option index, or -1.</returns>
    internal static int FindLabel(PdfDictionary field, string label)
    {
        var count = Count(field);
        for (var i = 0; i < count; i++)
        {
            if (string.Equals(GetLabel(field, i), label, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Finds the first selected option.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <returns>The option index, or -1.</returns>
    internal static int FirstSelected(PdfDictionary field)
    {
        var count = Count(field);
        if (UsesSelectedIndices(field))
        {
            for (var i = 0; i < count; i++)
            {
                if (IsSelectedIndex(field, i))
                {
                    return i;
                }
            }

            return -1;
        }

        // The selected text is read once, and each option is compared without making a string for it.
        var value = FieldAttributes.Find(field, KnownName.V);
        if (value.Kind == PdfKind.String)
        {
            return FirstEqualTo(field, count, value.AsStringBytes());
        }

        var selected = SelectedTexts(value);
        for (var i = 0; selected.Length > 0 && i < count; i++)
        {
            if (EqualsAny(GetTextBytes(field, i, ValueSlot), selected))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Determines whether an option is selected.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <param name="index">The option index.</param>
    /// <param name="useIndices">Whether <c>/I</c> decides, rather than <c>/V</c>.</param>
    /// <returns><see langword="true"/> when selected.</returns>
    internal static bool IsSelected(PdfDictionary field, int index, bool useIndices) =>
        useIndices ? IsSelectedIndex(field, index) : IsSelectedValue(field, GetValue(field, index));

    /// <summary>
    /// Determines whether the field's <c>/I</c> entry describes the selection: it must exist and, when there is a
    /// <c>/V</c> entry, name exactly the options that <c>/V</c> names.
    /// </summary>
    /// <param name="field">The field dictionary.</param>
    /// <returns><see langword="true"/> when <c>/I</c> is trusted.</returns>
    internal static bool UsesSelectedIndices(PdfDictionary field)
    {
        var indices = FieldAttributes.Find(field, KnownName.I);
        if (indices.IsNull)
        {
            return false;
        }

        var value = FieldAttributes.Find(field, KnownName.V);
        if (value.IsNull)
        {
            return true;
        }

        var count = CountIndices(indices);
        if (count is >= 0 and <= SmallSelection)
        {
            Span<bool> consumed = stackalloc bool[count];
            consumed.Clear();
            return MatchSelection(field, value, indices, consumed);
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        return TryCountValues(value, indices, counts) && IndicesMatch(field, indices, counts);
    }

    /// <summary>Matches a small selection while retaining duplicate value counts.</summary>
    /// <param name="field">The field.</param>
    /// <param name="value">The values.</param>
    /// <param name="indices">The indices.</param>
    /// <param name="consumed">Tracks the matched values.</param>
    /// <returns>Whether every selected index and value matches.</returns>
    private static bool MatchSelection(PdfDictionary field, PdfValue value, PdfValue indices, Span<bool> consumed)
    {
        var values = value.AsArray();
        var count = values?.Count ?? (value.Kind == PdfKind.String ? 1 : 0);
        if (count != consumed.Length)
        {
            return false;
        }

        var indexArray = indices.AsArray();
        var options = FieldAttributes.Find(field, KnownName.Opt).AsArray();
        for (var i = 0; i < consumed.Length; i++)
        {
            var index = indexArray is null ? indices : indexArray.Get(i);
            if (!TryGetOption(options, index, out var option))
            {
                return false;
            }

            if (!ConsumeValue(values, value, option, consumed))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Gets the export value at a valid option index.</summary>
    /// <param name="options">The options.</param>
    /// <param name="index">The option index value.</param>
    /// <param name="option">Receives the export value.</param>
    /// <returns>Whether the index names an option.</returns>
    private static bool TryGetOption(PdfArray? options, PdfValue index, out PdfValue option)
    {
        option = default;
        if (options is null || !index.IsNumber || (uint)index.AsInt32() >= (uint)options.Count)
        {
            return false;
        }

        option = options.Get(index.AsInt32());
        if (option.AsArray() is { } pair)
        {
            option = pair.Get(ValueSlot);
        }

        return true;
    }

    /// <summary>Consumes one equal export value.</summary>
    /// <param name="values">The value array, when present.</param>
    /// <param name="value">The scalar value otherwise.</param>
    /// <param name="option">The option's export value.</param>
    /// <param name="consumed">Tracks matched entries.</param>
    /// <returns>Whether an unmatched value was found.</returns>
    private static bool ConsumeValue(PdfArray? values, PdfValue value, PdfValue option, Span<bool> consumed)
    {
        var optionBytes = option.Kind == PdfKind.String ? option.AsStringBytes() : default;
        for (var i = 0; i < consumed.Length; i++)
        {
            var candidate = values is null ? value : values.Get(i);
            if (consumed[i] || candidate.Kind != PdfKind.String || !SameText(candidate.AsStringBytes(), optionBytes))
            {
                continue;
            }

            consumed[i] = true;
            return true;
        }

        return false;
    }

    /// <summary>Compares PDF strings across their supported text encodings.</summary>
    /// <param name="left">The first string.</param>
    /// <param name="right">The second string.</param>
    /// <returns>Whether their decoded text matches.</returns>
    private static bool SameText(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.SequenceEqual(right))
        {
            return true;
        }

        var length = PdfText.GetDecodedLength(left);
        if (length != PdfText.GetDecodedLength(right))
        {
            return false;
        }

        char[]? rented = null;
        Span<char> characters = length <= StackCharacters / ComparedStrings
            ? stackalloc char[length * ComparedStrings]
            : (rented = ArrayPool<char>.Shared.Rent(length * ComparedStrings));
        try
        {
            var first = characters[..length];
            var second = characters.Slice(length, length);
            _ = PdfText.Decode(left, first);
            _ = PdfText.Decode(right, second);
            return first.SequenceEqual(second);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Reads an option's text at a position.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <param name="index">The option index.</param>
    /// <param name="slot">The position within an option pair.</param>
    /// <returns>The text; empty when missing.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetText(PdfDictionary field, int index, int slot) =>
        PdfText.Decode(GetTextBytes(field, index, slot));

    /// <summary>Reads an option's text bytes.</summary>
    /// <param name="field">The field.</param>
    /// <param name="index">The option index.</param>
    /// <param name="slot">The position within an option pair.</param>
    /// <returns>The text bytes, empty when absent.</returns>
    private static ReadOnlySpan<byte> GetTextBytes(PdfDictionary field, int index, int slot)
    {
        if (FieldAttributes.Find(field, KnownName.Opt).AsArray() is not { } options)
        {
            return default;
        }

        var option = options.Get(index);
        if (option.AsArray() is { } pair)
        {
            option = pair.Get(slot);
        }

        return option.Kind == PdfKind.String ? option.AsStringBytes() : default;
    }

    /// <summary>Finds the first option whose text equals a selected string.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <param name="count">The options.</param>
    /// <param name="selectedBytes">The bytes of the string <c>/V</c> holds.</param>
    /// <returns>The option index, or -1.</returns>
    private static int FirstEqualTo(PdfDictionary field, int count, ReadOnlySpan<byte> selectedBytes)
    {
        var length = PdfText.GetDecodedLength(selectedBytes);
        char[]? rented = null;
        var buffer = length <= StackCharacters ? stackalloc char[StackCharacters] : (rented = ArrayPool<char>.Shared.Rent(length));
        try
        {
            var selected = buffer[..PdfText.Decode(selectedBytes, buffer)];
            for (var i = 0; i < count; i++)
            {
                if (EqualsText(GetTextBytes(field, i, ValueSlot), selected))
                {
                    return i;
                }
            }

            return -1;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Reads the texts <c>/V</c> selects when it is an array: the array's strings.</summary>
    /// <param name="value">The value of <c>/V</c>.</param>
    /// <returns>The texts; empty when <c>/V</c> selects nothing.</returns>
    private static string[] SelectedTexts(PdfValue value)
    {
        if (value.AsArray() is not { } array)
        {
            return [];
        }

        List<string> texts = [];
        for (var i = 0; i < array.Count; i++)
        {
            if (array.Get(i).Kind == PdfKind.String)
            {
                texts.Add(FieldAttributes.ReadText(array.Get(i)));
            }
        }

        return [.. texts];
    }

    /// <summary>Determines whether an option's text equals one of the selected texts.</summary>
    /// <param name="option">The option's text bytes.</param>
    /// <param name="selected">The selected texts.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    private static bool EqualsAny(ReadOnlySpan<byte> option, string[] selected)
    {
        foreach (var candidate in selected)
        {
            if (EqualsText(option, candidate))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether an option's text equals a selected text, without making a string for the option.</summary>
    /// <param name="option">The option's text bytes.</param>
    /// <param name="selected">The selected text.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    private static bool EqualsText(ReadOnlySpan<byte> option, ReadOnlySpan<char> selected)
    {
        var length = PdfText.GetDecodedLength(option);
        char[]? rented = null;
        var buffer = length <= StackCharacters ? stackalloc char[StackCharacters] : (rented = ArrayPool<char>.Shared.Rent(length));
        try
        {
            return buffer[..PdfText.Decode(option, buffer)].SequenceEqual(selected);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Determines whether the value names an option.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <param name="optionValue">The option's export value.</param>
    /// <returns><see langword="true"/> when <c>/V</c> is that string, or an array holding it.</returns>
    private static bool IsSelectedValue(PdfDictionary field, string optionValue)
    {
        var value = FieldAttributes.Find(field, KnownName.V);
        if (value.AsArray() is { } array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                if (array.Get(i).Kind == PdfKind.String && string.Equals(FieldAttributes.ReadText(array.Get(i)), optionValue, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return value.Kind == PdfKind.String && string.Equals(FieldAttributes.ReadText(value), optionValue, StringComparison.Ordinal);
    }

    /// <summary>Determines whether <c>/I</c> lists an option.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <param name="index">The option index.</param>
    /// <returns><see langword="true"/> when listed.</returns>
    private static bool IsSelectedIndex(PdfDictionary field, int index)
    {
        var indices = FieldAttributes.Find(field, KnownName.I);
        if (indices.AsArray() is { } array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                if (array.Get(i).IsNumber && array.GetInt32(i) == index)
                {
                    return true;
                }
            }
        }

        return indices.IsNumber && indices.AsInt32() == index;
    }

    /// <summary>Counts the values <c>/V</c> holds, checking that there are as many as indices.</summary>
    /// <param name="value">The <c>/V</c> value.</param>
    /// <param name="indices">The <c>/I</c> value.</param>
    /// <param name="counts">Receives how many times each value occurs.</param>
    /// <returns><see langword="false"/> when the counts cannot match.</returns>
    private static bool TryCountValues(PdfValue value, PdfValue indices, Dictionary<string, int> counts)
    {
        var indexCount = CountIndices(indices);
        if (indexCount < 0)
        {
            return false;
        }

        if (value.AsArray() is { } array)
        {
            if (array.Count != indexCount)
            {
                return false;
            }

            for (var i = 0; i < array.Count; i++)
            {
                Increment(counts, array.Get(i));
            }

            return true;
        }

        if (value.Kind == PdfKind.String)
        {
            Increment(counts, value);
            return indexCount == 1;
        }

        return true;
    }

    /// <summary>Counts the indices <c>/I</c> holds.</summary>
    /// <param name="indices">The <c>/I</c> value.</param>
    /// <returns>The count; -1 when it is neither an array nor a number.</returns>
    private static int CountIndices(PdfValue indices)
    {
        if (indices.AsArray() is { } array)
        {
            return array.Count;
        }

        return indices.IsNumber ? 1 : -1;
    }

    /// <summary>Counts one string value.</summary>
    /// <param name="counts">The counts.</param>
    /// <param name="value">The value; ignored unless it is a string.</param>
    private static void Increment(Dictionary<string, int> counts, PdfValue value)
    {
        if (value.Kind != PdfKind.String)
        {
            return;
        }

        var text = FieldAttributes.ReadText(value);
        counts[text] = counts.GetValueOrDefault(text) + 1;
    }

    /// <summary>Checks that each listed index names an option whose value <c>/V</c> also names.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <param name="indices">The <c>/I</c> value.</param>
    /// <param name="counts">The value counts, which are used up.</param>
    /// <returns><see langword="true"/> when both agree exactly.</returns>
    private static bool IndicesMatch(PdfDictionary field, PdfValue indices, Dictionary<string, int> counts)
    {
        var optionCount = Count(field);
        var array = indices.AsArray();
        var total = array?.Count ?? 1;
        for (var i = 0; i < total; i++)
        {
            var entry = array is null ? indices : array.Get(i);
            if (!entry.IsNumber || (uint)entry.AsInt32() >= (uint)optionCount || !Consume(counts, GetValue(field, entry.AsInt32())))
            {
                return false;
            }
        }

        return array is null ? counts.ContainsKey(GetValue(field, indices.AsInt32())) || counts.Count == 0 : counts.Count == 0;
    }

    /// <summary>Uses up one occurrence of a value.</summary>
    /// <param name="counts">The counts.</param>
    /// <param name="text">The value.</param>
    /// <returns><see langword="false"/> when the value was not counted.</returns>
    private static bool Consume(Dictionary<string, int> counts, string text)
    {
        if (!counts.TryGetValue(text, out var remaining))
        {
            return false;
        }

        if (remaining <= 1)
        {
            _ = counts.Remove(text);
        }
        else
        {
            counts[text] = remaining - 1;
        }

        return true;
    }
}
