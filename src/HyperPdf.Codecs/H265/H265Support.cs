// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Checks that a stream uses only the coding tools this decoder implements.</summary>
internal static class H265Support
{
    /// <summary>Checks a sequence parameter set.</summary>
    /// <param name="sps">The set.</param>
    /// <exception cref="NotSupportedException">The set uses a tool that is not implemented.</exception>
    internal static void Check(H265Sps sps)
    {
        if (sps.Range.ExtendedPrecision || sps.Range.CabacBypassAlignment)
        {
            throw new NotSupportedException("H.265 extended precision processing and bypass alignment are not supported.");
        }

        if (sps.SeparatePlanes)
        {
            throw new NotSupportedException("H.265 with separate colour planes is not supported.");
        }
    }

    /// <summary>Checks a picture parameter set.</summary>
    /// <param name="pps">The set.</param>
    /// <exception cref="NotSupportedException">The set uses a tool that is not implemented.</exception>
    internal static void Check(H265Pps pps)
    {
        if (pps.Range.CrossComponentPrediction)
        {
            throw new NotSupportedException("H.265 cross-component prediction is not supported.");
        }
    }
}
