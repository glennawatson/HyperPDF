// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Reads dictionary entries by spelling. The structure readers use it for keys that are not <see cref="KnownName"/>
/// values; each lookup interns the spelling, so it is meant for one-off reads and not for the render path.
/// </summary>
internal static class PdfDictionaryExtensions
{
    /// <summary>Elements read from an array by position.</summary>
    /// <param name="array">The array, or null.</param>
    extension(PdfArray? array)
    {
        /// <summary>Gets the name spelling at a position.</summary>
        /// <param name="index">The position.</param>
        /// <returns>The name text, or null when the element is not a name.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal string? NameText(int index) => array?.Get(index).SpellName(array.Owner);

        /// <summary>Gets the text strings of an array, skipping other elements.</summary>
        /// <returns>The strings.</returns>
        internal string[] Texts()
        {
            if (array is null)
            {
                return [];
            }

            var texts = new List<string>(array.Count);
            for (var i = 0; i < array.Count; i++)
            {
                var value = array.Get(i);
                if (value.Kind == PdfKind.String)
                {
                    texts.Add(PdfText.Decode(value.AsStringBytes()));
                }
            }

            return [.. texts];
        }

        /// <summary>Gets the name spellings of an array, skipping other elements.</summary>
        /// <returns>The names.</returns>
        internal string[] NameTexts()
        {
            if (array is null)
            {
                return [];
            }

            var names = new List<string>(array.Count);
            for (var i = 0; i < array.Count; i++)
            {
                if (array.NameText(i) is { } name)
                {
                    names.Add(name);
                }
            }

            return [.. names];
        }

        /// <summary>Gets the numbers of an array, using zero for other elements.</summary>
        /// <returns>The numbers.</returns>
        internal double[] Numbers()
        {
            if (array is null)
            {
                return [];
            }

            var numbers = new double[array.Count];
            for (var i = 0; i < numbers.Length; i++)
            {
                numbers[i] = array.GetNumber(i);
            }

            return numbers;
        }
    }

    /// <summary>Entries read from a dictionary by key spelling.</summary>
    /// <param name="dictionary">The dictionary.</param>
    extension(PdfDictionary dictionary)
    {
        /// <summary>Gets the interned name for a key spelling.</summary>
        /// <param name="key">The spelling.</param>
        /// <returns>The name, or the none name when the dictionary has no owner.</returns>
        internal PdfName Key(string key) => dictionary.Owner is { } owner ? owner.Names.Intern(key) : default;

        /// <summary>Gets a value, following references.</summary>
        /// <param name="key">The key spelling.</param>
        /// <returns>The value, or null.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal PdfValue Value(string key) => dictionary.Get(dictionary.Key(key));

        /// <summary>Gets a value without following a reference.</summary>
        /// <param name="key">The key spelling.</param>
        /// <returns>The raw value, or null.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal PdfValue RawValue(string key) => dictionary.GetRaw(dictionary.Key(key));

        /// <summary>Determines whether a key is present.</summary>
        /// <param name="key">The key spelling.</param>
        /// <returns><see langword="true"/> when present.</returns>
        internal bool Has(string key) => dictionary.Key(key) is { IsNone: false } name && dictionary.ContainsKey(name);

        /// <summary>Gets a dictionary entry.</summary>
        /// <param name="key">The key spelling.</param>
        /// <returns>The dictionary (a stream's dictionary counts), or null.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal PdfDictionary? Dict(string key) => dictionary.Value(key).AsDictionary();

        /// <summary>Gets an array entry.</summary>
        /// <param name="key">The key spelling.</param>
        /// <returns>The array, or null.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal PdfArray? Array(string key) => dictionary.Value(key).AsArray();

        /// <summary>Gets a stream entry.</summary>
        /// <param name="key">The key spelling.</param>
        /// <returns>The stream, or null.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal PdfStream? Stream(string key) => dictionary.Value(key).AsStream();

        /// <summary>Gets a text string entry.</summary>
        /// <param name="key">The key spelling.</param>
        /// <returns>The text, or null.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal string? Text(string key) => dictionary.GetText(dictionary.Key(key));

        /// <summary>Gets an integer entry.</summary>
        /// <param name="key">The key spelling.</param>
        /// <param name="fallback">The value when missing.</param>
        /// <returns>The integer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int Int(string key, int fallback) => dictionary.Value(key).AsInt32(fallback);

        /// <summary>Gets a number entry.</summary>
        /// <param name="key">The key spelling.</param>
        /// <param name="fallback">The value when missing.</param>
        /// <returns>The number.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal double Num(string key, double fallback) => dictionary.Value(key).AsNumber(fallback);

        /// <summary>Gets a boolean entry.</summary>
        /// <param name="key">The key spelling.</param>
        /// <param name="fallback">The value when missing.</param>
        /// <returns>The boolean.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool Flag(string key, bool fallback) => dictionary.Value(key).AsBoolean(fallback);

        /// <summary>Gets the spelling of a name entry.</summary>
        /// <param name="key">The key spelling.</param>
        /// <returns>The name text, or null when the entry is not a name.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal string? NameText(string key) => dictionary.Value(key).SpellName(dictionary.Owner);

        /// <summary>Gets a rectangle entry.</summary>
        /// <param name="key">The key spelling.</param>
        /// <returns>The rectangle, or null when missing or malformed.</returns>
        internal PdfRectangle? Rect(string key) => PdfRectangle.TryFromArray(dictionary.Array(key), out var rectangle) ? rectangle : null;
    }

    /// <summary>Reads from a stream.</summary>
    /// <param name="stream">The stream, or null.</param>
    extension(PdfStream? stream)
    {
        /// <summary>Decodes the stream's data, or gives an empty array when it cannot be decoded.</summary>
        /// <returns>The decoded bytes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal byte[] DecodeOrEmpty() => stream.DecodeOrEmpty(CancellationToken.None);

        /// <summary>Decodes readable bytes without swallowing cancellation.</summary>
        /// <param name="cancellationToken">Cancels decoding.</param>
        /// <returns>The decoded bytes, or an empty array when the stream is absent or damaged.</returns>
        internal byte[] DecodeOrEmpty(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (stream is null)
            {
                return [];
            }

            try
            {
                return stream.DecodeToArray(cancellationToken);
            }
            catch (PdfException)
            {
                return [];
            }
        }
    }

    /// <summary>Reads from a value.</summary>
    /// <param name="value">The value.</param>
    extension(PdfValue value)
    {
        /// <summary>Formats the value as text when it is a string, name or number.</summary>
        /// <param name="owner">The object store that spells names.</param>
        /// <returns>The text, or null for other kinds.</returns>
        internal string? ScalarText(PdfObjectStore? owner) => value.Kind switch
        {
            PdfKind.String => PdfText.Decode(value.AsStringBytes()),
            PdfKind.Name => value.SpellName(owner),
            PdfKind.Integer => value.AsInteger().ToString(CultureInfo.InvariantCulture),
            PdfKind.Real => value.AsNumber().ToString("R", CultureInfo.InvariantCulture),
            PdfKind.Boolean => value.AsBoolean() ? "true" : "false",
            _ => null,
        };

        /// <summary>Gets the spelling of a name value.</summary>
        /// <param name="owner">The object store whose name table spells the name.</param>
        /// <returns>The name text, or null when the value is not a name.</returns>
        internal string? SpellName(PdfObjectStore? owner) =>
            owner is not null && value.TryGetName(out var name) ? owner.Names.GetString(name) : null;
    }
}
