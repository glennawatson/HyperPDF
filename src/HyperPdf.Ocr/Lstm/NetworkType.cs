// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Ocr.Lstm;

/// <summary>The kinds of layer a model is built from, numbered as the model file numbers them.</summary>
public enum NetworkType
{
    /// <summary>No layer.</summary>
    None = 0,

    /// <summary>The picture going in.</summary>
    Input = 1,

    /// <summary>Joins the neighbours of each step into one step.</summary>
    Convolve = 2,

    /// <summary>Keeps the largest value of each block of steps.</summary>
    MaxPool = 3,

    /// <summary>Runs layers side by side on the same input.</summary>
    Parallel = 4,

    /// <summary>Runs the same layer side by side.</summary>
    Replicated = 5,

    /// <summary>A pair of recurrent layers, one running right to left.</summary>
    ParallelRightLeftLstm = 6,

    /// <summary>A pair of recurrent layers, one running bottom to top.</summary>
    ParallelUpDownLstm = 7,

    /// <summary>Four recurrent layers covering both directions of both axes.</summary>
    Parallel2DLstm = 8,

    /// <summary>Runs layers one after another.</summary>
    Series = 9,

    /// <summary>Stacks blocks of steps into one step.</summary>
    Reconfig = 10,

    /// <summary>Runs a layer on the picture mirrored left to right.</summary>
    XReversed = 11,

    /// <summary>Runs a layer on the picture mirrored top to bottom.</summary>
    YReversed = 12,

    /// <summary>Runs a layer on the picture with its two axes swapped.</summary>
    XYTranspose = 13,

    /// <summary>A recurrent layer along each row.</summary>
    Lstm = 14,

    /// <summary>A recurrent layer that gives only the last step of each row.</summary>
    LstmSummary = 15,

    /// <summary>A logistic layer.</summary>
    Logistic = 16,

    /// <summary>A layer clipped to 0 to 1.</summary>
    PositiveClip = 17,

    /// <summary>A layer clipped to -1 to 1.</summary>
    SymmetricClip = 18,

    /// <summary>A hyperbolic tangent layer.</summary>
    Tanh = 19,

    /// <summary>A rectifier layer.</summary>
    Relu = 20,

    /// <summary>A layer with no squashing.</summary>
    Linear = 21,

    /// <summary>A softmax layer trained for connectionist temporal classification.</summary>
    Softmax = 22,

    /// <summary>A softmax layer trained without it.</summary>
    SoftmaxNoCtc = 23,

    /// <summary>A recurrent layer with a softmax on its output.</summary>
    LstmSoftmax = 24,

    /// <summary>A recurrent layer with a softmax on its output, coded in binary.</summary>
    LstmSoftmaxEncoded = 25,

    /// <summary>A TensorFlow graph, which is not supported.</summary>
    TensorFlow = 26,
}
