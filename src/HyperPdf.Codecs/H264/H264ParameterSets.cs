// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.H264;

/// <summary>The parameter sets a stream has sent so far. A picture parameter set is read when a slice first uses it, with the sequence parameter set it names.</summary>
[DebuggerDisplay("H264ParameterSets")]
internal sealed class H264ParameterSets
{
    /// <summary>The number of sequence parameter sets a stream can have.</summary>
    private const int SpsCount = 32;

    /// <summary>The number of picture parameter sets a stream can have.</summary>
    private const int PpsCount = 256;

    /// <summary>The sequence parameter sets, by number.</summary>
    private readonly H264Sps?[] _sps = new H264Sps?[SpsCount];

    /// <summary>The payloads of the picture parameter sets, by number, as they were sent.</summary>
    private readonly byte[]?[] _ppsRaw = new byte[]?[PpsCount];

    /// <summary>The picture parameter sets that have been read, by number.</summary>
    private readonly H264Pps?[] _pps = new H264Pps?[PpsCount];

    /// <summary>Takes in a sequence parameter set.</summary>
    /// <param name="rbsp">The payload of its NAL unit.</param>
    /// <exception cref="InvalidDataException">The set is damaged.</exception>
    /// <exception cref="NotSupportedException">The set uses a feature this decoder does not read.</exception>
    internal void AddSps(ReadOnlySpan<byte> rbsp)
    {
        var sps = H264SpsReader.Read(rbsp);
        if (!sps.Equals(_sps[sps.Id]))
        {
            // A picture parameter set was read with the old sequence set, so it is read again when next used.
            Array.Clear(_pps);
        }

        _sps[sps.Id] = sps;
    }

    /// <summary>Takes in a picture parameter set.</summary>
    /// <param name="rbsp">The payload of its NAL unit.</param>
    /// <exception cref="InvalidDataException">The payload is damaged.</exception>
    internal void AddPps(ReadOnlySpan<byte> rbsp)
    {
        var reader = new MsbBitReader(rbsp);
        var id = (int)ExpGolomb.ReadUe(ref reader);
        if (id >= PpsCount || reader.IsOverrun)
        {
            throw new InvalidDataException("An H.264 picture parameter set is damaged.");
        }

        _ppsRaw[id] = rbsp.ToArray();
        _pps[id] = null;
    }

    /// <summary>Gets the first sequence parameter set that was sent.</summary>
    /// <returns>The set, or <see langword="null"/> when none was sent.</returns>
    internal H264Sps? FirstSps()
    {
        foreach (var sps in _sps)
        {
            if (sps is not null)
            {
                return sps;
            }
        }

        return null;
    }

    /// <summary>Gets a sequence parameter set.</summary>
    /// <param name="id">The set number.</param>
    /// <returns>The set, or <see langword="null"/> when none was sent.</returns>
    internal H264Sps? GetSps(int id) => (uint)id < SpsCount ? _sps[id] : null;

    /// <summary>Gets a picture parameter set, reading it if this is the first use.</summary>
    /// <param name="id">The set number.</param>
    /// <returns>The set, or <see langword="null"/> when none was sent or its sequence set is missing.</returns>
    /// <exception cref="InvalidDataException">The set is damaged.</exception>
    /// <exception cref="NotSupportedException">The set uses a feature this decoder does not read.</exception>
    internal H264Pps? GetPps(int id)
    {
        if ((uint)id >= PpsCount || _ppsRaw[id] is not { } raw)
        {
            return null;
        }

        if (_pps[id] is { } cached)
        {
            return cached;
        }

        var sps = GetSps(H264PpsReader.ReadSequenceId(raw));
        if (sps is null)
        {
            return null;
        }

        var pps = H264PpsReader.Read(raw, sps);
        _pps[id] = pps;
        return pps;
    }
}
