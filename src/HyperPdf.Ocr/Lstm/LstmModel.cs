// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Ocr.Lstm;

/// <summary>A trained recurrent network that reads one line of text from a picture.</summary>
[DebuggerDisplay("LstmModel: {Characters.Count} characters, input {Shape}")]
public sealed class LstmModel
{
    /// <summary>The network.</summary>
    private readonly NetworkLayer _network;

    /// <summary>The number of samples the model was trained on, which seeds the noise beyond a picture's edge.</summary>
    private readonly int _sampleIteration;

    /// <summary>Initializes a new instance of the <see cref="LstmModel"/> class.</summary>
    /// <param name="network">The network.</param>
    /// <param name="shape">The shape of picture the network takes.</param>
    /// <param name="characters">The characters the network can recognise.</param>
    /// <param name="coder">The coder that turns the network's outputs into characters.</param>
    /// <param name="blank">The code that means no character.</param>
    /// <param name="sampleIteration">The number of samples the model was trained on.</param>
    private LstmModel(NetworkLayer network, InputShape shape, CharacterSet characters, CharacterCoder coder, int blank, int sampleIteration)
    {
        _network = network;
        Shape = shape;
        Characters = characters;
        Coder = coder;
        BlankCode = blank;
        _sampleIteration = sampleIteration;
    }

    /// <summary>Gets the network.</summary>
    public NetworkLayer Network => _network;

    /// <summary>Gets the shape of picture the network takes.</summary>
    public InputShape Shape { get; }

    /// <summary>Gets the characters the network can recognise.</summary>
    public CharacterSet Characters { get; }

    /// <summary>Gets the coder that turns the network's outputs into characters.</summary>
    public CharacterCoder Coder { get; }

    /// <summary>Gets the code that means no character.</summary>
    public int BlankCode { get; }

    /// <summary>Gets how many pixels of a picture each output step covers.</summary>
    public int StepWidth => _network.XScale;

    /// <summary>Loads a model from a language data file.</summary>
    /// <param name="data">The language data.</param>
    /// <returns>The model.</returns>
    /// <exception cref="InvalidDataException">The file does not hold a supported model.</exception>
    public static LstmModel Load(TrainedData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var reader = new ModelReader(data.Get(TrainedDataComponent.Lstm).Span);
        var network = NetworkLoader.Read(ref reader);
        _ = reader.ReadString();
        _ = reader.ReadInt32();
        _ = reader.ReadInt32();
        var sampleIteration = reader.ReadInt32();
        var blank = reader.ReadInt32();
        var characters = CharacterSet.Read(data.Get(TrainedDataComponent.LstmUnicharset).Span);
        var recoderReader = new ModelReader(data.Get(TrainedDataComponent.LstmRecoder).Span);
        var coder = CharacterCoder.Read(ref recoderReader);
        var shape = FindShape(network);
        if (network.Outputs != coder.CodeRange)
        {
            throw new InvalidDataException("The network's outputs do not match the character codes.");
        }

        return new(network, shape, characters, coder, blank, sampleIteration);
    }

    /// <summary>Reads a prepared line.</summary>
    /// <param name="line">The line, from <see cref="LinePreparer.Prepare"/>. It stays the caller's.</param>
    /// <param name="scratch">The working memory.</param>
    /// <returns>The network's output: one row, a step for every <see cref="StepWidth"/> pixels, and a probability for each code. Give it back to the scratch memory when done.</returns>
    public FeatureMap Run(FeatureMap line, RecognitionScratch scratch)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(scratch);
        scratch.Random.Reset(_sampleIteration);
        return _network.Forward(line, scratch);
    }

    /// <summary>Finds the shape of the input layer, which opens every model.</summary>
    /// <param name="network">The network.</param>
    /// <returns>The shape.</returns>
    /// <exception cref="InvalidDataException">The network does not start with an input layer.</exception>
    private static InputShape FindShape(NetworkLayer network)
    {
        var layer = network;
        while (layer is SeriesLayer series && series.Layers.Count > 0)
        {
            layer = series.Layers[0];
        }

        return layer is InputLayer input ? input.Shape : throw new InvalidDataException("The network does not start with an input layer.");
    }
}
