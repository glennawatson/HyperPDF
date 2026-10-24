// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Ocr.Lstm;

/// <summary>The start of a model. It says what picture the model takes and passes it on unchanged.</summary>
[DebuggerDisplay("InputLayer: {Name}, {Shape}")]
public sealed class InputLayer : NetworkLayer
{
    /// <summary>Initializes a new instance of the <see cref="InputLayer"/> class.</summary>
    /// <param name="name">The layer's name.</param>
    /// <param name="inputs">The input count the model file stores.</param>
    /// <param name="outputs">The output count the model file stores.</param>
    /// <param name="shape">The shape of picture the model takes.</param>
    public InputLayer(string name, int inputs, int outputs, InputShape shape)
        : base(name, inputs, outputs) => Shape = shape;

    /// <summary>Gets the shape of picture the model takes.</summary>
    public InputShape Shape { get; }

    /// <inheritdoc/>
    public override FeatureMap Forward(FeatureMap input, RecognitionScratch scratch) => input;
}
