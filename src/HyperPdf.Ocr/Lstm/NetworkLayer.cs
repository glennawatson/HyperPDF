// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Ocr.Lstm;

/// <summary>One layer of a recognition model.</summary>
/// <param name="name">The layer's name in the model.</param>
/// <param name="inputs">The number of features it takes in each step.</param>
/// <param name="outputs">The number of features it gives in each step.</param>
[DebuggerDisplay("NetworkLayer: {Name}, {Inputs} in, {Outputs} out")]
public abstract class NetworkLayer(string name, int inputs, int outputs)
{
    /// <summary>Gets the layer's name in the model.</summary>
    public string Name { get; } = name;

    /// <summary>Gets the number of features the layer takes in each step.</summary>
    public int Inputs { get; } = inputs;

    /// <summary>Gets the number of features the layer gives in each step.</summary>
    public int Outputs { get; } = outputs;

    /// <summary>Gets how many input steps the layer turns into one step of a row.</summary>
    public virtual int XScale => 1;

    /// <summary>Runs the layer. The input stays the caller's; the result is a grid from the scratch memory, or the input itself for a layer that changes nothing.</summary>
    /// <param name="input">The values going in.</param>
    /// <param name="scratch">The working memory, which also supplies the values beyond the picture's edge.</param>
    /// <returns>The values coming out.</returns>
    public abstract FeatureMap Forward(FeatureMap input, RecognitionScratch scratch);
}
