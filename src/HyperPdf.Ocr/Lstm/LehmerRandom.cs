// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Ocr.Lstm;

/// <summary>
/// The Lehmer generator the model was trained with (multiplier 48271, modulus 2<sup>31</sup> - 1). A convolution fills
/// the area beyond the picture's edge with its values, so a model sees the same border every time.
/// </summary>
[DebuggerDisplay("LehmerRandom: state {_state}")]
public sealed class LehmerRandom
{
    /// <summary>The multiplier.</summary>
    private const ulong Multiplier = 48_271;

    /// <summary>The modulus.</summary>
    private const ulong Modulus = int.MaxValue;

    /// <summary>The number the model's sample count is multiplied by to make a seed.</summary>
    private const long SeedFactor = 0x10000001;

    /// <summary>The span of values <see cref="NextSigned"/> gives.</summary>
    private const double SignedSpan = 2.0;

    /// <summary>The generator's state.</summary>
    private ulong _state;

    /// <summary>Initializes a new instance of the <see cref="LehmerRandom"/> class, seeded as a model seeds it before reading a line.</summary>
    /// <param name="sampleIteration">The number of samples the model was trained on.</param>
    public LehmerRandom(int sampleIteration) => Reset(sampleIteration);

    /// <summary>Starts the generator again, seeded as a model seeds it before reading a line.</summary>
    /// <param name="sampleIteration">The number of samples the model was trained on.</param>
    public void Reset(int sampleIteration)
    {
        _state = (ulong)(sampleIteration * SeedFactor % (long)Modulus);
        if (_state == 0)
        {
            _state = 1;
        }

        _ = Next();
    }

    /// <summary>Gets the next number from 1 to 2<sup>31</sup> - 2.</summary>
    /// <returns>The number.</returns>
    public int Next()
    {
        _state = _state * Multiplier % Modulus;
        return (int)_state;
    }

    /// <summary>Gets the next number as a float from -1 to 1.</summary>
    /// <returns>The number.</returns>
    public float NextSigned() => (float)((SignedSpan * Next() / int.MaxValue) - 1.0);
}
