// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.H264;

/// <summary>Reads the coded block pattern, quantiser change and residual of an inter macroblock coded with CAVLC, and adds the residual to its prediction.</summary>
[DebuggerDisplay("H264CavlcInterResidual: qp {Qp}")]
internal sealed class H264CavlcInterResidual
{
    /// <summary>The picture being decoded.</summary>
    private readonly H264Picture _picture;

    /// <summary>The macroblock being decoded.</summary>
    private readonly H264CurrentMb _mb;

    /// <summary>The number of the slice.</summary>
    private readonly int _slice;

    /// <summary>Whether the pictures may use the 8x8 transform.</summary>
    private readonly bool _transform8x8Allowed;

    /// <summary>Makes the samples of the macroblocks.</summary>
    private readonly H264IntraReconstructor _reconstructor;

    /// <summary>Initializes a new instance of the <see cref="H264CavlcInterResidual"/> class.</summary>
    /// <param name="picture">The picture to decode into.</param>
    /// <param name="mb">The macroblock being decoded.</param>
    /// <param name="slice">The number of the slice.</param>
    /// <param name="reconstructor">Makes the samples of the macroblocks.</param>
    /// <param name="transform8x8Allowed">Whether the pictures may use the 8x8 transform.</param>
    internal H264CavlcInterResidual(H264Picture picture, H264CurrentMb mb, int slice, H264IntraReconstructor reconstructor, bool transform8x8Allowed)
    {
        _picture = picture;
        _mb = mb;
        _slice = slice;
        _reconstructor = reconstructor;
        _transform8x8Allowed = transform8x8Allowed;
    }

    /// <summary>Gets or sets the luma quantiser of the macroblock before.</summary>
    internal int Qp { get; set; }

    /// <summary>Reads the residual of the macroblock whose motion has been read, and adds it to the prediction.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="allow8x8">Whether every partition of the macroblock is at least 8x8, so it may use the 8x8 transform.</param>
    /// <exception cref="InvalidDataException">The coded block pattern is not valid.</exception>
    internal void Read(ref MsbBitReader reader, bool allow8x8)
    {
        var code = (int)ExpGolomb.ReadUe(ref reader);
        if (code > H264CodedBlockPattern.MaxCodeNumber)
        {
            throw new InvalidDataException("An H.264 coded block pattern is not valid.");
        }

        var pattern = H264CodedBlockPattern.Lookup(code, false);
        _mb.CbpLuma = pattern & H264CodedBlockPattern.LumaMask;
        _mb.CbpChroma = pattern >> H264CodedBlockPattern.ChromaShift;
        if (pattern != 0)
        {
            if (_transform8x8Allowed && allow8x8 && _mb.CbpLuma != 0)
            {
                _mb.Transform8x8 = reader.ReadBit();
                _picture.Transform8x8[_mb.Address] = _mb.Transform8x8;
            }

            Qp = (Qp + ExpGolomb.ReadSe(ref reader) + H264Layout.QpRange) % H264Layout.QpRange;
            H264CavlcResidual.Read(ref reader, _picture, _mb, _slice);
            _reconstructor.AddInterResidual(_mb, Qp);
        }

        _picture.Qp[_mb.Address] = (sbyte)Qp;
    }
}
