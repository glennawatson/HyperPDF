// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>The quantization of a QCD or QCC marker: the guard bits and each sub-band's exponent and mantissa.</summary>
/// <param name="Style">The quantization style: none, scalar derived or scalar expounded.</param>
/// <param name="GuardBits">The number of guard bits.</param>
/// <param name="Steps">The step sizes as <c>exponent &lt;&lt; 11 | mantissa</c>, one per sub-band given.</param>
[DebuggerDisplay("JpxQuantization: style {Style}, {GuardBits} guard bits")]
internal sealed record JpxQuantization(int Style, int GuardBits, ushort[] Steps)
{
    /// <summary>The style with no quantization: one exponent byte per sub-band.</summary>
    internal const int NoQuantization = 0;

    /// <summary>The style with one step size that the other sub-bands derive from.</summary>
    internal const int ScalarDerived = 1;

    /// <summary>The style with a step size for every sub-band.</summary>
    internal const int ScalarExpounded = 2;

    /// <summary>The bits of the mantissa.</summary>
    internal const int MantissaBits = 11;

    /// <summary>The mask of the mantissa.</summary>
    internal const int MantissaMask = (1 << MantissaBits) - 1;

    /// <summary>The bits of the quantization byte below the guard bits.</summary>
    private const int GuardShift = 5;

    /// <summary>The mask of the style bits.</summary>
    private const int StyleMask = 0x1F;

    /// <summary>The shift of the exponent in a no-quantization byte.</summary>
    private const int ExponentShift = 3;

    /// <summary>The bytes of a 16-bit step.</summary>
    private const int StepBytes = 2;

    /// <summary>The sub-bands of each resolution above the lowest.</summary>
    private const int BandsPerLevel = 3;

    /// <summary>Reads the Sqcd and SPqcd fields (or Sqcc and SPqcc).</summary>
    /// <param name="fields">The fields.</param>
    /// <returns>The quantization, or <see langword="null"/> when invalid.</returns>
    internal static JpxQuantization? Read(ReadOnlySpan<byte> fields)
    {
        if (fields.IsEmpty)
        {
            return null;
        }

        var style = fields[0] & StyleMask;
        var guard = fields[0] >> GuardShift;
        var body = fields[1..];
        return style switch
        {
            NoQuantization => new(style, guard, ReadExponents(body)),
            ScalarDerived when body.Length >= StepBytes => new(style, guard, [BinaryPrimitives.ReadUInt16BigEndian(body)]),
            ScalarExpounded when body.Length >= StepBytes => new(style, guard, ReadSteps(body)),
            _ => null,
        };
    }

    /// <summary>Gets the step of a sub-band.</summary>
    /// <param name="band">The sub-band index: 0 for LL, then three per resolution.</param>
    /// <returns>The step as <c>exponent &lt;&lt; 11 | mantissa</c>.</returns>
    internal int GetStep(int band)
    {
        if (Style != ScalarDerived)
        {
            // Our rule: a sub-band the marker leaves out (damaged data; T.800 A.6.4 requires one entry per sub-band) has step zero, so it decodes as silence.
            return band < Steps.Length ? Steps[band] : 0;
        }

        var first = Steps[0];
        var level = band == 0 ? 0 : (band - 1) / BandsPerLevel;
        var exponent = Math.Max((first >> MantissaBits) - level, 0);
        return (exponent << MantissaBits) | (first & MantissaMask);
    }

    /// <summary>Reads one exponent byte per sub-band.</summary>
    /// <param name="body">The SPqcd bytes.</param>
    /// <returns>The steps with zero mantissas.</returns>
    private static ushort[] ReadExponents(ReadOnlySpan<byte> body)
    {
        var steps = new ushort[body.Length];
        for (var i = 0; i < steps.Length; i++)
        {
            steps[i] = (ushort)((body[i] >> ExponentShift) << MantissaBits);
        }

        return steps;
    }

    /// <summary>Reads one 16-bit step per sub-band.</summary>
    /// <param name="body">The SPqcd bytes.</param>
    /// <returns>The steps.</returns>
    private static ushort[] ReadSteps(ReadOnlySpan<byte> body)
    {
        var steps = new ushort[body.Length / StepBytes];
        for (var i = 0; i < steps.Length; i++)
        {
            steps[i] = BinaryPrimitives.ReadUInt16BigEndian(body[(i * StepBytes)..]);
        }

        return steps;
    }
}
