// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Ocr.Lstm;

/// <summary>Reads the layers of a model from its stored form.</summary>
public static class NetworkLoader
{
    /// <summary>The flag saying a group of layers stores a learning rate for each layer.</summary>
    private const int LayerLearningRateFlag = 64;

    /// <summary>The value of the training byte of a model still being trained.</summary>
    private const sbyte TrainingEnabled = 1;

    /// <summary>The number of gates in a recurrent layer that looks one way along a row.</summary>
    private const int GateCount = 4;

    /// <summary>The names the model file gives each layer type, in the order of <see cref="NetworkType"/>.</summary>
    private static readonly string[] TypeNames =
    [
        "Invalid", "Input", "Convolve", "Maxpool", "Parallel", "Replicated", "ParBidiLSTM", "DepParUDLSTM",
        "Par2dLSTM", "Series", "Reconfig", "RTLReversed", "TTBReversed", "XYTranspose", "LSTM", "SummLSTM",
        "Logistic", "LinLogistic", "LinTanh", "Tanh", "Relu", "Linear", "Softmax", "SoftmaxNoCTC",
        "LSTMSoftmax", "LSTMBinarySoftmax", "TensorFlow",
    ];

    /// <summary>Reads a layer and the layers inside it.</summary>
    /// <param name="reader">The reader, placed at the start of the layer.</param>
    /// <returns>The layer.</returns>
    /// <exception cref="InvalidDataException">The layer is damaged or of a kind that is not supported.</exception>
    public static NetworkLayer Read(ref ModelReader reader)
    {
        var type = ReadType(ref reader);
        var header = ReadHeader(ref reader);
        if (header.Training == TrainingEnabled)
        {
            throw new InvalidDataException("The recognition model holds training state, which it should not.");
        }

        return type switch
        {
            NetworkType.Input => new InputLayer(header.Name, header.Inputs, header.Outputs, ReadShape(ref reader)),
            NetworkType.Convolve => new ConvolveLayer(header.Name, header.Inputs, reader.ReadInt32(), reader.ReadInt32()),
            NetworkType.MaxPool => new MaxPoolLayer(header.Name, header.Inputs, reader.ReadInt32(), reader.ReadInt32()),
            NetworkType.Reconfig => new ReconfigLayer(header.Name, header.Inputs, reader.ReadInt32(), reader.ReadInt32()),
            NetworkType.Series => new SeriesLayer(header.Name, ReadLayers(ref reader, header.Flags)),
            NetworkType.Parallel or NetworkType.Replicated or NetworkType.ParallelRightLeftLstm or NetworkType.ParallelUpDownLstm =>
                new ParallelLayer(header.Name, ReadLayers(ref reader, header.Flags)),
            NetworkType.XReversed or NetworkType.YReversed or NetworkType.XYTranspose =>
                new ReversedLayer(header.Name, type, ReadOnlyLayer(ReadLayers(ref reader, header.Flags))),
            NetworkType.Lstm => ReadLstm(ref reader, header, false),
            NetworkType.LstmSummary => ReadLstm(ref reader, header, true),
            >= NetworkType.Logistic and <= NetworkType.SoftmaxNoCtc => new FullyConnectedLayer(header.Name, type, WeightMatrix.Read(ref reader)),
            _ => throw new InvalidDataException($"The recognition model uses a {type} layer, which is not supported."),
        };
    }

    /// <summary>Reads the type of the next layer, which is a code or, for the code zero, a name.</summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The type.</returns>
    /// <exception cref="InvalidDataException">The type is not known.</exception>
    private static NetworkType ReadType(ref ModelReader reader)
    {
        var code = reader.ReadSByte();
        if (code != (sbyte)NetworkType.None)
        {
            return Enum.IsDefined((NetworkType)code) ? (NetworkType)code : throw new InvalidDataException("The recognition model has an unknown layer type.");
        }

        var name = reader.ReadString();
        var index = Array.IndexOf(TypeNames, name);
        return index < 0 ? throw new InvalidDataException($"The recognition model has an unknown layer type '{name}'.") : (NetworkType)index;
    }

    /// <summary>Reads the values every layer starts with.</summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The header.</returns>
    private static LayerHeader ReadHeader(ref ModelReader reader)
    {
        var training = reader.ReadSByte();
        _ = reader.ReadSByte();
        var flags = reader.ReadInt32();
        var inputs = reader.ReadInt32();
        var outputs = reader.ReadInt32();
        _ = reader.ReadInt32();
        return new(training, flags, inputs, outputs, reader.ReadString());
    }

    /// <summary>Reads the shape of an input layer.</summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The shape.</returns>
    private static InputShape ReadShape(ref ModelReader reader) =>
        new(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());

    /// <summary>Reads the layers inside a group, and the learning rates some groups keep for them.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="flags">The group's flags.</param>
    /// <returns>The layers.</returns>
    private static NetworkLayer[] ReadLayers(ref ModelReader reader, int flags)
    {
        var count = reader.ReadUInt32();
        _ = reader.CheckedLength(count, sizeof(byte));
        var layers = new NetworkLayer[count];
        for (var i = 0; i < layers.Length; i++)
        {
            layers[i] = Read(ref reader);
        }

        if ((flags & LayerLearningRateFlag) != 0)
        {
            var rates = reader.ReadUInt32();
            _ = reader.ReadBytes(reader.CheckedLength(rates, sizeof(float)));
        }

        return layers;
    }

    /// <summary>Takes the one layer a mirroring group holds.</summary>
    /// <param name="layers">The layers read.</param>
    /// <returns>The layer.</returns>
    /// <exception cref="InvalidDataException">The group does not hold exactly one layer.</exception>
    private static NetworkLayer ReadOnlyLayer(NetworkLayer[] layers) =>
        layers.Length == 1 ? layers[0] : throw new InvalidDataException("A mirroring layer must hold exactly one layer.");

    /// <summary>Reads a recurrent layer.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="header">The layer's header.</param>
    /// <param name="summary">Whether the layer gives only the last step of each row.</param>
    /// <returns>The layer.</returns>
    /// <exception cref="InvalidDataException">The layer looks along both axes or is damaged.</exception>
    private static LstmLayer ReadLstm(ref ModelReader reader, LayerHeader header, bool summary)
    {
        var sources = reader.ReadInt32();
        var gates = new WeightMatrix[GateCount];
        gates[0] = WeightMatrix.Read(ref reader);
        var cells = gates[0].Outputs;
        if (sources != header.Inputs + cells)
        {
            throw new InvalidDataException("The recognition model has a recurrent layer that looks along both axes, which is not supported.");
        }

        for (var i = 1; i < gates.Length; i++)
        {
            gates[i] = WeightMatrix.Read(ref reader);
        }

        return new(header.Name, header.Inputs, cells, gates, summary);
    }
}
