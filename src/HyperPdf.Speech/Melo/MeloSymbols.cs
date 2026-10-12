// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace HyperPdf.Speech.Melo;

/// <summary>
/// MeloTTS-English's symbol table, speakers and sample rate, read from <c>melo-en.json</c>, and the rules that turn an
/// ARPAbet phone such as <c>AH0</c> into the symbol id and tone the model was trained on.
/// </summary>
[DebuggerDisplay("MeloSymbols: {Count} symbols")]
internal sealed class MeloSymbols
{
    /// <summary>The id of the blank placed between phones, and of the padding phone.</summary>
    internal const long Blank = 0;

    /// <summary>The first tone id of English; tones 0 to 3 follow it.</summary>
    internal const long EnglishToneStart = 7;

    /// <summary>The language id of English.</summary>
    internal const long English = 2;

    /// <summary>The longest phone read.</summary>
    private const int MaxPhoneLength = 8;

    /// <summary>The symbol of a phone the model does not know.</summary>
    private const string UnknownSymbol = "UNK";

    /// <summary>The ARPAbet phones MeloTTS reads stress from, as its English front end lists them.</summary>
    private static readonly FrozenSet<string> Arpabet =
        ("AA0 AA1 AA2 AE0 AE1 AE2 AH0 AH1 AH2 AO0 AO1 AO2 AW0 AW1 AW2 AY0 AY1 AY2 B CH D DH EH0 EH1 EH2 ER ER0 ER1 ER2 EY0 EY1 EY2 F G HH "
        + "IH IH0 IH1 IH2 IY0 IY1 IY2 JH K L M N NG OW0 OW1 OW2 OY0 OY1 OY2 P R S SH T TH UH0 UH1 UH2 UW0 UW1 UW2 V W Y Z ZH")
        .Split(' ')
        .ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The ids by symbol.</summary>
    private readonly FrozenDictionary<string, int> _ids;

    /// <summary>The symbols by id.</summary>
    private readonly string[] _names;

    /// <summary>Initializes a new instance of the <see cref="MeloSymbols"/> class.</summary>
    /// <param name="symbols">The symbols, in id order.</param>
    /// <param name="speakers">The speaker ids by name.</param>
    /// <param name="sampleRate">The sample rate in hertz.</param>
    private MeloSymbols(List<string> symbols, Dictionary<string, int> speakers, int sampleRate)
    {
        var ids = new Dictionary<string, int>(symbols.Count, StringComparer.Ordinal);
        for (var i = 0; i < symbols.Count; i++)
        {
            _ = ids.TryAdd(symbols[i], i);
        }

        _ids = ids.ToFrozenDictionary(StringComparer.Ordinal);
        _names = [.. symbols];
        Speakers = speakers.ToFrozenDictionary(StringComparer.Ordinal);
        SampleRate = sampleRate;
        Unknown = _ids[UnknownSymbol];
    }

    /// <summary>Gets the number of symbols.</summary>
    internal int Count => _ids.Count;

    /// <summary>Gets the speaker ids by name, such as <c>EN-AU</c>.</summary>
    internal FrozenDictionary<string, int> Speakers { get; }

    /// <summary>Gets the sample rate in hertz.</summary>
    internal int SampleRate { get; }

    /// <summary>Gets the id of the unknown symbol.</summary>
    internal int Unknown { get; }

    /// <summary>Reads the table.</summary>
    /// <param name="json">The UTF-8 JSON of <c>melo-en.json</c>.</param>
    /// <returns>The table.</returns>
    /// <exception cref="InvalidDataException">Thrown when the file is not a MeloTTS table.</exception>
    internal static MeloSymbols Load(ReadOnlySpan<byte> json)
    {
        var reader = new Utf8JsonReader(json);
        var symbols = new List<string>();
        var speakers = new Dictionary<string, int>(StringComparer.Ordinal);
        var sampleRate = 0;
        _ = reader.Read();
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var name = reader.GetString();
            _ = reader.Read();
            switch (name)
            {
                case "symbols":
                {
                    ReadSymbols(ref reader, symbols);
                    break;
                }

                case "speakers":
                {
                    ReadSpeakers(ref reader, speakers);
                    break;
                }

                case "sampleRate":
                {
                    sampleRate = reader.GetInt32();
                    break;
                }

                default:
                {
                    reader.Skip();
                    break;
                }
            }
        }

        if (symbols.Count == 0 || sampleRate <= 0 || !symbols.Contains(UnknownSymbol))
        {
            throw new InvalidDataException("The MeloTTS symbol table is incomplete.");
        }

        return new(symbols, speakers, sampleRate);
    }

    /// <summary>Packs a symbol id and a tone into one value.</summary>
    /// <param name="id">The symbol id.</param>
    /// <param name="tone">The tone, 0 to 3.</param>
    /// <returns>The packed phone.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Pack(int id, int tone) => (tone << 16) | id;

    /// <summary>Gets a packed phone's symbol id.</summary>
    /// <param name="phone">The packed phone.</param>
    /// <returns>The symbol id.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int IdOf(int phone) => phone & 0xFFFF;

    /// <summary>Gets a packed phone's tone.</summary>
    /// <param name="phone">The packed phone.</param>
    /// <returns>The tone.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int ToneOf(int phone) => phone >> 16;

    /// <summary>Gets a symbol's name.</summary>
    /// <param name="id">The symbol id.</param>
    /// <returns>The name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal string Name(long id) => _names[id];

    /// <summary>Turns a dictionary phone into a packed phone: the stress digit becomes the tone, the rest the symbol.</summary>
    /// <param name="arpabet">The phone, such as <c>AH0</c> or <c>K</c>.</param>
    /// <returns>The packed phone.</returns>
    internal int FromDictionary(ReadOnlySpan<char> arpabet)
    {
        if (arpabet.Length > MaxPhoneLength)
        {
            return Pack(Unknown, 0);
        }

        var tone = 0;
        if (!arpabet.IsEmpty && char.IsAsciiDigit(arpabet[^1]))
        {
            tone = arpabet[^1] - '0' + 1;
            arpabet = arpabet[..^1];
        }

        Span<char> lower = stackalloc char[MaxPhoneLength];
        var length = arpabet.ToLowerInvariant(lower);
        return Pack(Symbol(lower[..length]), tone);
    }

    /// <summary>Turns a phone the spelling-to-sound network predicted into a packed phone, as MeloTTS does for unknown words.</summary>
    /// <param name="arpabet">The phone.</param>
    /// <returns>The packed phone; phones outside MeloTTS's ARPAbet list are unknown.</returns>
    internal int FromPrediction(string arpabet) => Arpabet.Contains(arpabet) ? FromDictionary(arpabet) : Pack(Symbol(arpabet), 0);

    /// <summary>Gets a symbol's id after MeloTTS's replacements, or the unknown symbol's.</summary>
    /// <param name="symbol">The symbol.</param>
    /// <returns>The id.</returns>
    internal int Symbol(ReadOnlySpan<char> symbol)
    {
        symbol = symbol switch
        {
            "v" => "V",
            "..." => "…",
            "：" or "；" or "，" or "·" or "、" => ",",
            "。" or "\n" => ".",
            "！" => "!",
            "？" => "?",
            _ => symbol,
        };
        return _ids.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(symbol, out var id) ? id : Unknown;
    }

    /// <summary>Reads the symbol list.</summary>
    /// <param name="reader">The reader, at the list's start.</param>
    /// <param name="symbols">Receives the symbols.</param>
    private static void ReadSymbols(ref Utf8JsonReader reader, List<string> symbols)
    {
        while (reader.Read() && reader.TokenType == JsonTokenType.String)
        {
            symbols.Add(reader.GetString()!);
        }
    }

    /// <summary>Reads the speakers.</summary>
    /// <param name="reader">The reader, at the object's start.</param>
    /// <param name="speakers">Receives the speaker ids by name.</param>
    private static void ReadSpeakers(ref Utf8JsonReader reader, Dictionary<string, int> speakers)
    {
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var speaker = reader.GetString()!;
            _ = reader.Read();
            speakers[speaker] = reader.GetInt32();
        }
    }
}
