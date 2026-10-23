// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace HyperPdf.Vision;

/// <summary>
/// The GPT-2 vocabulary: the text of each token id. Tokens are written with one printable character for each byte
/// (byte-level BPE), so turning ids back into text means mapping those characters to bytes and reading the bytes as UTF-8.
/// </summary>
[DebuggerDisplay("CaptionVocabulary: {Count} tokens")]
internal sealed class CaptionVocabulary
{
    /// <summary>The count of byte values.</summary>
    private const int ByteValues = 256;

    /// <summary>About how many bytes a token takes, which sizes the buffer.</summary>
    private const int BytesPerToken = 2;

    /// <summary>The first printable character the bytes map to themselves.</summary>
    private const int FirstPrintable = '!';

    /// <summary>The last printable ASCII character.</summary>
    private const int LastPrintable = '~';

    /// <summary>The first printable character of the Latin-1 range that maps to itself.</summary>
    private const int FirstLatin = 0xA1;

    /// <summary>The last of the first Latin-1 run that maps to itself.</summary>
    private const int LastLatinRunOne = 0xAC;

    /// <summary>The first of the second Latin-1 run that maps to itself.</summary>
    private const int FirstLatinRunTwo = 0xAE;

    /// <summary>The token strings, indexed by id; an id with no token is <see langword="null"/>.</summary>
    private readonly string?[] _tokens;

    /// <summary>The byte each token character stands for.</summary>
    private readonly Dictionary<char, byte> _bytes;

    /// <summary>Initializes a new instance of the <see cref="CaptionVocabulary"/> class.</summary>
    /// <param name="tokens">The token strings, indexed by id.</param>
    private CaptionVocabulary(string?[] tokens)
    {
        _tokens = tokens;
        _bytes = BuildByteMap();
    }

    /// <summary>Gets the number of token ids, which is one past the highest id.</summary>
    internal int Count => _tokens.Length;

    /// <summary>Reads a vocabulary from the <c>vocab.json</c> file: an object that maps each token string to its id.</summary>
    /// <param name="json">The file's bytes.</param>
    /// <returns>The vocabulary.</returns>
    /// <exception cref="InvalidDataException">The file is not an object of token ids.</exception>
    internal static CaptionVocabulary Parse(ReadOnlySpan<byte> json)
    {
        var reader = new Utf8JsonReader(json);
        var pairs = new List<KeyValuePair<string, int>>();
        try
        {
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                throw new InvalidDataException("The vocabulary is not a JSON object.");
            }

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                var token = reader.GetString()!;
                _ = reader.Read();
                pairs.Add(new(token, reader.GetInt32()));
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The vocabulary is not valid JSON.", ex);
        }

        var highest = -1;
        foreach (var (_, id) in pairs)
        {
            highest = Math.Max(highest, id);
        }

        var tokens = new string?[highest + 1];
        foreach (var (token, id) in pairs)
        {
            tokens[id] = token;
        }

        return new(tokens);
    }

    /// <summary>Turns token ids into text.</summary>
    /// <param name="ids">The ids.</param>
    /// <returns>The text, which may start with a space.</returns>
    internal string Decode(ReadOnlySpan<long> ids)
    {
        var bytes = new ArrayBufferWriter<byte>(ids.Length * BytesPerToken);
        foreach (var id in ids)
        {
            if ((ulong)id >= (ulong)_tokens.Length || _tokens[id] is not { } token)
            {
                continue;
            }

            foreach (var character in token)
            {
                if (_bytes.TryGetValue(character, out var value))
                {
                    bytes.Write([value]);
                }
            }
        }

        return Encoding.UTF8.GetString(bytes.WrittenSpan);
    }

    /// <summary>Builds the map from a token character to the byte it stands for.</summary>
    /// <returns>The map.</returns>
    private static Dictionary<char, byte> BuildByteMap()
    {
        var map = new Dictionary<char, byte>(ByteValues);
        var extra = 0;
        for (var value = 0; value < ByteValues; value++)
        {
            var identity = value is >= FirstPrintable and <= LastPrintable or >= FirstLatin and <= LastLatinRunOne or >= FirstLatinRunTwo and <= byte.MaxValue;
            map[(char)(identity ? value : ByteValues + extra)] = (byte)value;
            extra += identity ? 0 : 1;
        }

        return map;
    }
}
