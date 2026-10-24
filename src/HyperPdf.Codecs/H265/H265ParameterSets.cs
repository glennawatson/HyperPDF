// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The sequence and picture parameter sets a stream has sent so far, by number.</summary>
internal sealed class H265ParameterSets
{
    /// <summary>The sequence parameter sets.</summary>
    private readonly Dictionary<int, H265Sps> _sequence = [];

    /// <summary>The picture parameter sets.</summary>
    private readonly Dictionary<int, H265Pps> _picture = [];

    /// <summary>Stores a sequence parameter set, replacing any with the same number.</summary>
    /// <param name="sps">The set.</param>
    internal void Add(H265Sps sps) => _sequence[sps.Id] = sps;

    /// <summary>Stores a picture parameter set, replacing any with the same number.</summary>
    /// <param name="pps">The set.</param>
    internal void Add(H265Pps pps) => _picture[pps.Id] = pps;

    /// <summary>Gets the first sequence parameter set that was added.</summary>
    /// <returns>The set, or <see langword="null"/> when none was added.</returns>
    internal H265Sps? FirstSps()
    {
        using var enumerator = _sequence.Values.GetEnumerator();
        return enumerator.MoveNext() ? enumerator.Current : null;
    }

    /// <summary>Finds the sets a slice uses.</summary>
    /// <param name="ppsId">The picture parameter set number from the slice header.</param>
    /// <param name="pps">Receives the picture parameter set.</param>
    /// <param name="sps">Receives the sequence parameter set it names.</param>
    /// <returns><see langword="true"/> when both sets have been sent.</returns>
    internal bool TryFind(int ppsId, out H265Pps pps, out H265Sps sps)
    {
        sps = null!;
        return _picture.TryGetValue(ppsId, out pps!) && _sequence.TryGetValue(pps.SpsId, out sps!);
    }
}
