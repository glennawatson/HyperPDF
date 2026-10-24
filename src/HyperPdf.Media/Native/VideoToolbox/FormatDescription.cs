// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdf.Codecs;

namespace HyperPdf.Media.Native.VideoToolbox;

/// <summary>Makes the CoreMedia format description that tells VideoToolbox how a track's pictures are coded, from the parameter sets in the track's configuration record.</summary>
internal static unsafe class FormatDescription
{
    /// <summary>Makes the description of a track.</summary>
    /// <param name="track">The H.264 or H.265 track.</param>
    /// <param name="failure">Receives why no description was made, when none was.</param>
    /// <returns>The description, or <see langword="null"/> when the configuration holds no usable parameter sets or CoreMedia refused them.</returns>
    internal static CoreFoundationHandle? Create(MediaTrack track, out DecoderFailure failure)
    {
        ArgumentNullException.ThrowIfNull(track);
        var lengthSize = AnnexB.LengthSize(track.Codec, track.Configuration.Span);
        var stream = new ArrayBufferWriter<byte>();
        if (lengthSize == 0 || !AnnexB.WriteParameterSets(track.Codec, track.Configuration.Span, stream))
        {
            failure = new("The track has no parameter sets", 0);
            return null;
        }

        var units = new List<Range>();
        AnnexB.ListUnits(stream.WrittenSpan, units);
        var sets = new byte*[units.Count];
        var sizes = new nint[units.Count];
        fixed (byte* bytes = stream.WrittenSpan)
        {
            for (var i = 0; i < units.Count; i++)
            {
                var start = units[i].Start.Value;
                sets[i] = bytes + start;
                sizes[i] = units[i].End.Value - start;
            }

            return CreateFrom(track.Codec, sets, sizes, lengthSize, out failure);
        }
    }

    /// <summary>Asks CoreMedia for the description.</summary>
    /// <param name="codec">The coding.</param>
    /// <param name="sets">The start of each parameter set.</param>
    /// <param name="sizes">The length of each parameter set.</param>
    /// <param name="lengthSize">The bytes of each NAL unit's length in the pictures.</param>
    /// <param name="failure">Receives why no description was made, when none was.</param>
    /// <returns>The description, or <see langword="null"/>.</returns>
    private static CoreFoundationHandle? CreateFrom(MediaCodec codec, byte*[] sets, nint[] sizes, int lengthSize, out DecoderFailure failure)
    {
        void* description;
        int status;
        fixed (byte** setPointers = sets)
        {
            fixed (nint* sizePointers = sizes)
            {
                status = codec == MediaCodec.H265
                    ? NativeMethods.CMVideoFormatDescriptionCreateFromHEVCParameterSets(null, sets.Length, setPointers, sizePointers, lengthSize, null, &description)
                    : NativeMethods.CMVideoFormatDescriptionCreateFromH264ParameterSets(null, sets.Length, setPointers, sizePointers, lengthSize, &description);
            }
        }

        if (status != 0)
        {
            failure = new("CoreMedia did not accept the parameter sets", status);
            return null;
        }

        failure = default;
        return new(description);
    }
}
