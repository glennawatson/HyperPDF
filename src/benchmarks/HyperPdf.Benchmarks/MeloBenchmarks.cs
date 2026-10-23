// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.Speech.Melo;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures MeloTTS's C# front end: cutting a sentence into BERT word pieces, guessing an unknown word with the
/// spelling-to-sound network, and preparing a whole sentence for the model. Small stand-in data files are built in
/// memory; the neural models need their downloaded files, so they are not run here. Allocations come from the
/// EventPipe trace.
/// </summary>
public class MeloBenchmarks
{
    /// <summary>The sentence read: dictionary words, punctuation and one unknown word.</summary>
    private const string Sentence = "the garden behind the library, and the zyxglorp.";

    /// <summary>The width of the network's letter and phone embeddings.</summary>
    private const int Embedding = 64;

    /// <summary>The network's hidden units.</summary>
    private const int Hidden = 128;

    /// <summary>The network's graphemes: padding, unknown, end and a to z.</summary>
    private const int Graphemes = 29;

    /// <summary>The network's phonemes.</summary>
    private const int Phonemes = 74;

    /// <summary>The gates of a GRU.</summary>
    private const int Gates = 3;

    /// <summary>The step between stand-in weights along a sine wave.</summary>
    private const float Step = 0.37F;

    /// <summary>The word piece ids, reused.</summary>
    private readonly List<int> _pieces = [];

    /// <summary>The guessed phones, reused.</summary>
    private readonly List<string> _phones = [];

    /// <summary>The model input, reused.</summary>
    private readonly MeloInput _input = new();

    /// <summary>The tokenizer.</summary>
    private WordPieceTokenizer _tokenizer = null!;

    /// <summary>The spelling-to-sound network.</summary>
    private SpellingToSound _spelling = null!;

    /// <summary>The front end.</summary>
    private MeloFrontEnd _frontEnd = null!;

    /// <summary>Builds the stand-in data files and the front end.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var vocabulary = new StringBuilder();
        for (var i = 0; i < WordPieceTokenizer.Start; i++)
        {
            _ = vocabulary.Append("[unused").Append(i).Append("]\n");
        }

        _ = vocabulary.Append("[CLS]\n[SEP]\nthe\ngarden\nbehind\nlibrary\nand\n,\n.\nz\n##y\n##x\n##g\n##lo\n##rp\n");
        _tokenizer = new(vocabulary.ToString());
        var symbols = MeloSymbols.Load(
            """{"symbols": ["_", "dh", "ah", "g", "aa", "r", "d", "n", "b", "ih", "hh", "ay", "l", "eh", "iy", "ae", ",", ".", "UNK"], "speakers": {"EN-AU": 3}, "sampleRate": 44100}"""u8);
        var lexicon = MeloLexicon.Load(
            "THE\tDH AH0\nGARDEN\tG AA1 R D AH0 N\nBEHIND\tB IH0 HH AY1 N D\nLIBRARY\tL AY1 B R EH2 R IY0\nAND\tAE1 N D\n",
            symbols);
        _spelling = new(StandInWeights());
        _frontEnd = new(symbols, lexicon, _spelling, _tokenizer);
    }

    /// <summary>Cuts a sentence into BERT word pieces.</summary>
    /// <returns>The piece count.</returns>
    [Benchmark]
    public int Tokenize()
    {
        _tokenizer.Tokenize(Sentence, _pieces);
        return _pieces.Count;
    }

    /// <summary>Guesses an unknown word's phones.</summary>
    /// <returns>The phone count.</returns>
    [Benchmark]
    public int GuessWord()
    {
        _phones.Clear();
        _spelling.Predict("zyxglorp", _phones);
        return _phones.Count;
    }

    /// <summary>Prepares a sentence for the model: pieces, phones, tones, blanks and alignment.</summary>
    /// <returns>The phone count.</returns>
    [Benchmark]
    public int Prepare()
    {
        _frontEnd.Prepare(Sentence, _input);
        return _input.Phones.Count;
    }

    /// <summary>Writes stand-in weights with g2p_en's shapes.</summary>
    /// <returns>The weights file.</returns>
    private static byte[] StandInWeights()
    {
        int[][] shapes =
        [
            [Graphemes, Embedding], [Gates * Hidden, Embedding], [Gates * Hidden, Hidden], [Gates * Hidden], [Gates * Hidden],
            [Phonemes, Embedding], [Gates * Hidden, Embedding], [Gates * Hidden, Hidden], [Gates * Hidden], [Gates * Hidden],
            [Phonemes, Hidden], [Phonemes],
        ];
        var position = 0;
        using var stream = new MemoryStream();
        Span<byte> number = stackalloc byte[sizeof(float)];
        foreach (var shape in shapes)
        {
            BinaryPrimitives.WriteInt32LittleEndian(number, shape.Length);
            stream.Write(number);
            var count = 1;
            foreach (var size in shape)
            {
                BinaryPrimitives.WriteInt32LittleEndian(number, size);
                stream.Write(number);
                count *= size;
            }

            for (var i = 0; i < count; i++)
            {
                // Small, varied, repeatable values; the benchmark measures the work, not what is predicted.
                position++;
                BinaryPrimitives.WriteSingleLittleEndian(number, MathF.Sin(position * Step) / Embedding);
                stream.Write(number);
            }
        }

        return stream.ToArray();
    }
}
