// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Speech.Melo;

/// <summary>
/// The spelling-to-sound network of g2p_en network (Apache-2.0), which MeloTTS uses for words its dictionary lacks: a GRU encoder
/// reads the letters and a GRU decoder writes ARPAbet phones, one at a time, until it writes the end mark. The weights
/// come from <c>g2p-en.bin</c>; the arithmetic follows g2p_en's own NumPy code.
/// </summary>
[DebuggerDisplay("SpellingToSound: {Hidden} hidden units")]
internal sealed class SpellingToSound
{
    /// <summary>The most phones written for one word.</summary>
    private const int MaxPhones = 20;

    /// <summary>The grapheme id of a letter outside a to z.</summary>
    private const int UnknownGrapheme = 1;

    /// <summary>The grapheme id of the end of the word.</summary>
    private const int EndGrapheme = 2;

    /// <summary>The grapheme id of the letter a.</summary>
    private const int FirstLetter = 3;

    /// <summary>The phoneme id that starts decoding.</summary>
    private const int StartPhoneme = 2;

    /// <summary>The phoneme id that ends decoding.</summary>
    private const int EndPhoneme = 3;

    /// <summary>The gates of a GRU: reset, update and new.</summary>
    private const int Gates = 3;

    /// <summary>The position of the new-state gate among the gates.</summary>
    private const int NewGate = 2;

    /// <summary>The phonemes by id.</summary>
    private static readonly string[] Phonemes =
    [
        "<pad>", "<unk>", "<s>", "</s>", "AA0", "AA1", "AA2", "AE0", "AE1", "AE2", "AH0", "AH1", "AH2", "AO0", "AO1", "AO2", "AW0", "AW1",
        "AW2", "AY0", "AY1", "AY2", "B", "CH", "D", "DH", "EH0", "EH1", "EH2", "ER0", "ER1", "ER2", "EY0", "EY1", "EY2", "F", "G", "HH",
        "IH0", "IH1", "IH2", "IY0", "IY1", "IY2", "JH", "K", "L", "M", "N", "NG", "OW0", "OW1", "OW2", "OY0", "OY1", "OY2", "P", "R", "S",
        "SH", "T", "TH", "UH0", "UH1", "UH2", "UW", "UW0", "UW1", "UW2", "V", "W", "Y", "Z", "ZH",
    ];

    /// <summary>The encoder's letter embeddings.</summary>
    private readonly Layer _encoderEmbedding;

    /// <summary>The encoder's input weights.</summary>
    private readonly Layer _encoderInput;

    /// <summary>The encoder's recurrent weights.</summary>
    private readonly Layer _encoderHidden;

    /// <summary>The encoder's input bias.</summary>
    private readonly float[] _encoderInputBias;

    /// <summary>The encoder's recurrent bias.</summary>
    private readonly float[] _encoderHiddenBias;

    /// <summary>The decoder's phone embeddings.</summary>
    private readonly Layer _decoderEmbedding;

    /// <summary>The decoder's input weights.</summary>
    private readonly Layer _decoderInput;

    /// <summary>The decoder's recurrent weights.</summary>
    private readonly Layer _decoderHidden;

    /// <summary>The decoder's input bias.</summary>
    private readonly float[] _decoderInputBias;

    /// <summary>The decoder's recurrent bias.</summary>
    private readonly float[] _decoderHiddenBias;

    /// <summary>The output layer's weights.</summary>
    private readonly Layer _output;

    /// <summary>The output layer's bias.</summary>
    private readonly float[] _outputBias;

    /// <summary>The hidden state, reused.</summary>
    private readonly float[] _state;

    /// <summary>The input gates, reused.</summary>
    private readonly float[] _inputGates;

    /// <summary>The recurrent gates, reused.</summary>
    private readonly float[] _hiddenGates;

    /// <summary>The output scores, reused.</summary>
    private readonly float[] _scores;

    /// <summary>Initializes a new instance of the <see cref="SpellingToSound"/> class.</summary>
    /// <param name="weights">The contents of <c>g2p-en.bin</c>: each array's rank, dimensions and little-endian floats.</param>
    /// <exception cref="InvalidDataException">Thrown when the weights are not g2p_en's.</exception>
    internal SpellingToSound(ReadOnlySpan<byte> weights)
    {
        var offset = 0;
        _encoderEmbedding = Read(weights, ref offset);
        _encoderInput = Read(weights, ref offset);
        _encoderHidden = Read(weights, ref offset);
        _encoderInputBias = Read(weights, ref offset).Values;
        _encoderHiddenBias = Read(weights, ref offset).Values;
        _decoderEmbedding = Read(weights, ref offset);
        _decoderInput = Read(weights, ref offset);
        _decoderHidden = Read(weights, ref offset);
        _decoderInputBias = Read(weights, ref offset).Values;
        _decoderHiddenBias = Read(weights, ref offset).Values;
        _output = Read(weights, ref offset);
        _outputBias = Read(weights, ref offset).Values;
        Hidden = _encoderHidden.Columns;
        if (_encoderHidden.Rows != Gates * Hidden || _output.Rows != Phonemes.Length || _decoderEmbedding.Rows != Phonemes.Length)
        {
            throw new InvalidDataException("The spelling-to-sound weights do not have g2p_en's shape.");
        }

        _state = new float[Hidden];
        _inputGates = new float[Gates * Hidden];
        _hiddenGates = new float[Gates * Hidden];
        _scores = new float[Phonemes.Length];
    }

    /// <summary>Gets the number of hidden units.</summary>
    internal int Hidden { get; }

    /// <summary>Predicts a word's ARPAbet phones.</summary>
    /// <param name="word">The lower-case word.</param>
    /// <param name="phones">Receives the phones.</param>
    internal void Predict(ReadOnlySpan<char> word, List<string> phones)
    {
        ArgumentNullException.ThrowIfNull(phones);
        Array.Clear(_state);
        for (var i = 0; i <= word.Length; i++)
        {
            var grapheme = i == word.Length ? EndGrapheme : Grapheme(word[i]);
            Step(_encoderEmbedding.Row(grapheme), _encoderInput, _encoderHidden, _encoderInputBias, _encoderHiddenBias);
        }

        var previous = StartPhoneme;
        for (var i = 0; i < MaxPhones; i++)
        {
            Step(_decoderEmbedding.Row(previous), _decoderInput, _decoderHidden, _decoderInputBias, _decoderHiddenBias);
            var best = 0;
            for (var p = 0; p < _scores.Length; p++)
            {
                _scores[p] = Dot(_output.Row(p), _state) + _outputBias[p];
                if (_scores[p] > _scores[best])
                {
                    best = p;
                }
            }

            if (best == EndPhoneme)
            {
                break;
            }

            phones.Add(Phonemes[best]);
            previous = best;
        }
    }

    /// <summary>Reads one array.</summary>
    /// <param name="weights">The weights file.</param>
    /// <param name="offset">The read position, moved past the array.</param>
    /// <returns>The array as rows and columns.</returns>
    /// <exception cref="InvalidDataException">Thrown when the file ends early.</exception>
    private static Layer Read(ReadOnlySpan<byte> weights, ref int offset)
    {
        if (offset + sizeof(int) > weights.Length)
        {
            throw new InvalidDataException("The spelling-to-sound weights are truncated.");
        }

        var rank = BinaryPrimitives.ReadInt32LittleEndian(weights[offset..]);
        offset += sizeof(int);
        var rows = 1;
        var columns = 1;
        for (var i = 0; i < rank; i++)
        {
            var size = BinaryPrimitives.ReadInt32LittleEndian(weights[offset..]);
            offset += sizeof(int);
            if (i == rank - 1)
            {
                columns = size;
            }
            else
            {
                rows *= size;
            }
        }

        var values = new float[rows * columns];
        var bytes = values.Length * sizeof(float);
        if (offset + bytes > weights.Length)
        {
            throw new InvalidDataException("The spelling-to-sound weights are truncated.");
        }

        for (var i = 0; i < values.Length; i++)
        {
            values[i] = BinaryPrimitives.ReadSingleLittleEndian(weights[(offset + (i * sizeof(float)))..]);
        }

        offset += bytes;
        return new(values, rows, columns);
    }

    /// <summary>Gets a letter's grapheme id.</summary>
    /// <param name="letter">The letter.</param>
    /// <returns>The id; letters outside a to z are unknown.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Grapheme(char letter) => letter is >= 'a' and <= 'z' ? FirstLetter + (letter - 'a') : UnknownGrapheme;

    /// <summary>Multiplies two vectors.</summary>
    /// <param name="left">The first.</param>
    /// <param name="right">The second.</param>
    /// <returns>The dot product.</returns>
    private static float Dot(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        var lanes = Vector<float>.Count;
        var sums = Vector<float>.Zero;
        var i = 0;
        for (; i + lanes <= left.Length; i += lanes)
        {
            sums += new Vector<float>(left[i..]) * new Vector<float>(right[i..]);
        }

        var sum = Vector.Sum(sums);
        for (; i < left.Length; i++)
        {
            sum += left[i] * right[i];
        }

        return sum;
    }

    /// <summary>The logistic function.</summary>
    /// <param name="x">The input.</param>
    /// <returns>The output, 0 to 1.</returns>
    private static float Sigmoid(float x) => 1F / (1F + MathF.Exp(-x));

    /// <summary>Runs one GRU step on the hidden state.</summary>
    /// <param name="input">The step's input.</param>
    /// <param name="inputWeights">The input weights.</param>
    /// <param name="hiddenWeights">The recurrent weights.</param>
    /// <param name="inputBias">The input bias.</param>
    /// <param name="hiddenBias">The recurrent bias.</param>
    private void Step(ReadOnlySpan<float> input, Layer inputWeights, Layer hiddenWeights, float[] inputBias, float[] hiddenBias)
    {
        for (var g = 0; g < _inputGates.Length; g++)
        {
            _inputGates[g] = Dot(inputWeights.Row(g), input) + inputBias[g];
            _hiddenGates[g] = Dot(hiddenWeights.Row(g), _state) + hiddenBias[g];
        }

        for (var h = 0; h < Hidden; h++)
        {
            var reset = Sigmoid(_inputGates[h] + _hiddenGates[h]);
            var update = Sigmoid(_inputGates[Hidden + h] + _hiddenGates[Hidden + h]);
            var candidate = MathF.Tanh(_inputGates[(NewGate * Hidden) + h] + (reset * _hiddenGates[(NewGate * Hidden) + h]));
            _state[h] = ((1 - update) * candidate) + (update * _state[h]);
        }
    }

    /// <summary>A weight matrix stored by rows.</summary>
    /// <param name="Values">The values.</param>
    /// <param name="Rows">The rows.</param>
    /// <param name="Columns">The columns.</param>
    [DebuggerDisplay("Layer: {Rows}x{Columns}")]
    private sealed record Layer(float[] Values, int Rows, int Columns)
    {
        /// <summary>Gets a row.</summary>
        /// <param name="row">The row.</param>
        /// <returns>Its values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<float> Row(int row) => Values.AsSpan(row * Columns, Columns);
    }
}
