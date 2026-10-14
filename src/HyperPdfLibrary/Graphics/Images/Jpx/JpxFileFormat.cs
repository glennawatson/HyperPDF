// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// Reads the JP2 file format (ISO 15444-1 annex I): the boxes before the codestream, of which the header's colour,
/// palette, component mapping and channel definition boxes matter. Data that starts with a codestream marker is a raw
/// codestream.
/// </summary>
internal static class JpxFileFormat
{
    /// <summary>The box type of the JP2 header superbox, 'jp2h'.</summary>
    private const uint HeaderBox = 0x6A703268;

    /// <summary>The box type of the codestream box, 'jp2c'.</summary>
    private const uint CodestreamBox = 0x6A703263;

    /// <summary>The box type of the colour specification box, 'colr'.</summary>
    private const uint ColourBox = 0x636F6C72;

    /// <summary>The box type of the palette box, 'pclr'.</summary>
    private const uint PaletteBox = 0x70636C72;

    /// <summary>The box type of the component mapping box, 'cmap'.</summary>
    private const uint MappingBox = 0x636D6170;

    /// <summary>The box type of the channel definition box, 'cdef'.</summary>
    private const uint DefinitionBox = 0x63646566;

    /// <summary>The box type of a compositing layer header superbox, 'jplh' (ISO 15444-2 M.11.7).</summary>
    private const uint LayerHeaderBox = 0x6A706C68;

    /// <summary>The box type of a colour group superbox, 'cgrp' (ISO 15444-2 M.11.7.1).</summary>
    private const uint ColourGroupBox = 0x63677270;

    /// <summary>The box type of the opacity box, 'opct' (ISO 15444-2 M.11.7.6).</summary>
    private const uint OpacityBox = 0x6F706374;

    /// <summary>The box type of a fragment table superbox, 'ftbl' (ISO 15444-2 M.11.3).</summary>
    private const uint FragmentTableBox = 0x6674626C;

    /// <summary>The box type of a fragment list box, 'flst' (ISO 15444-2 M.11.3.1).</summary>
    private const uint FragmentListBox = 0x666C7374;

    /// <summary>The bytes of a fragment entry: a 64-bit offset, a 32-bit length and a 16-bit data reference.</summary>
    private const int FragmentBytes = 14;

    /// <summary>The offset of the length in a fragment entry.</summary>
    private const int FragmentLengthOffset = 8;

    /// <summary>The offset of the data reference in a fragment entry.</summary>
    private const int FragmentReferenceOffset = 12;

    /// <summary>The opacity type of an Opacity box whose last channel is opacity.</summary>
    private const int OpacityLast = 0;

    /// <summary>The opacity type of an Opacity box whose last channel is premultiplied opacity.</summary>
    private const int OpacityLastPremultiplied = 1;

    /// <summary>The bytes of a box header.</summary>
    private const int BoxHeaderBytes = 8;

    /// <summary>The bytes of a box header with an extended length.</summary>
    private const int ExtendedHeaderBytes = 16;

    /// <summary>The box length value that means an extended length follows.</summary>
    private const uint ExtendedLength = 1;

    /// <summary>The bytes of a box type field.</summary>
    private const int TypeBytes = 4;

    /// <summary>The colour method of an enumerated colour space.</summary>
    private const int EnumeratedMethod = 1;

    /// <summary>The colour method of a restricted ICC profile.</summary>
    private const int RestrictedIccMethod = 2;

    /// <summary>The colour method of any ICC profile (ISO 15444-2).</summary>
    private const int AnyIccMethod = 3;

    /// <summary>The bytes before the enumerated colour space in a colour box.</summary>
    private const int ColourHeaderBytes = 3;

    /// <summary>The enumerated colour space of sRGB.</summary>
    private const uint Srgb = 16;

    /// <summary>The enumerated colour space of greyscale.</summary>
    private const uint Gray = 17;

    /// <summary>The enumerated colour space of sYCC.</summary>
    private const uint Sycc = 18;

    /// <summary>The enumerated colour space of e-sYCC.</summary>
    private const uint Eycc = 24;

    /// <summary>The enumerated colour space of CMYK.</summary>
    private const uint Cmyk = 12;

    /// <summary>The enumerated colour space of CIELab.</summary>
    private const uint CieLab = 14;

    /// <summary>The bytes of a palette box before the column sizes.</summary>
    private const int PaletteHeaderBytes = 3;

    /// <summary>The most palette entries.</summary>
    private const int MaxPaletteEntries = 1024;

    /// <summary>The bit of a palette size byte that marks a signed column.</summary>
    private const int SignedBit = 0x80;

    /// <summary>The bits of a palette size byte that hold the precision less one.</summary>
    private const int PrecisionMask = 0x7F;

    /// <summary>The widest palette column.</summary>
    private const int MaxPaletteBits = 31;

    /// <summary>The bits of a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bytes of a component mapping entry.</summary>
    private const int MappingBytes = 4;

    /// <summary>The offset of the mapping type in a component mapping entry.</summary>
    private const int MappingTypeOffset = 2;

    /// <summary>The offset of the palette column in a component mapping entry.</summary>
    private const int MappingColumnOffset = 3;

    /// <summary>The bytes of a channel definition entry.</summary>
    private const int DefinitionBytes = 6;

    /// <summary>The offset of the type in a channel definition entry.</summary>
    private const int DefinitionTypeOffset = 2;

    /// <summary>The offset of the association in a channel definition entry.</summary>
    private const int DefinitionAssociationOffset = 4;

    /// <summary>The bytes of a 16-bit field.</summary>
    private const int ShortBytes = 2;

    /// <summary>Reads the boxes, or recognises a raw codestream.</summary>
    /// <param name="data">The JPXDecode data.</param>
    /// <returns>The file information, or <see langword="null"/> when no codestream is found.</returns>
    internal static JpxFileInfo? Read(ReadOnlySpan<byte> data)
    {
        if (data.Length >= ShortBytes && BinaryPrimitives.ReadUInt16BigEndian(data) == JpxMarkers.StartOfCodestream)
        {
            return new(new(0, data.Length), JpxColorSpace.Unspecified, null, null, null);
        }

        var builder = new Builder();
        var position = 0;
        while (NextBox(data, ref position, out var type, out var body))
        {
            if (type == CodestreamBox)
            {
                return builder.Finish(new(body.Offset, body.Length));
            }

            if (type == FragmentTableBox)
            {
                // The first codestream decides; one that is fragmented is read only when it is a single fragment in this file.
                return ReadFragment(data, body) is { } fragment ? builder.Finish(fragment) : null;
            }

            if (type == HeaderBox)
            {
                ReadHeader(data.Slice(body.Offset, body.Length), body.Offset, builder);
            }
            else if (type == LayerHeaderBox && builder.Layer is null)
            {
                // The first compositing layer is the one shown; its boxes override the defaults of the JP2 header box (M.11.7).
                builder.Layer = new();
                ReadHeader(data.Slice(body.Offset, body.Length), body.Offset, builder.Layer);
            }
        }

        return null;
    }

    /// <summary>Finds the codestream a fragment table box describes.</summary>
    /// <param name="data">The file.</param>
    /// <param name="table">The contents of the fragment table box.</param>
    /// <returns>Where the codestream is, or <see langword="null"/> when it has several fragments, is outside this file or is damaged.</returns>
    private static JpxDataRange? ReadFragment(ReadOnlySpan<byte> data, JpxDataRange table)
    {
        var contents = data.Slice(table.Offset, table.Length);
        var position = 0;
        while (NextBox(contents, ref position, out var type, out var body))
        {
            if (type != FragmentListBox || body.Length < ShortBytes + FragmentBytes || BinaryPrimitives.ReadUInt16BigEndian(contents[body.Offset..]) != 1)
            {
                continue;
            }

            var entry = contents.Slice(body.Offset + ShortBytes, FragmentBytes);
            var offset = BinaryPrimitives.ReadUInt64BigEndian(entry);
            var length = BinaryPrimitives.ReadUInt32BigEndian(entry[FragmentLengthOffset..]);
            var reference = BinaryPrimitives.ReadUInt16BigEndian(entry[FragmentReferenceOffset..]);
            return reference == 0 && offset <= (ulong)data.Length && length <= (ulong)data.Length - offset ? new((int)offset, (int)length) : null;
        }

        return null;
    }

    /// <summary>Reads the next box header and moves past the box.</summary>
    /// <param name="data">The data.</param>
    /// <param name="position">The box start; moved to the next box.</param>
    /// <param name="type">Receives the box type.</param>
    /// <param name="body">Receives where the box contents are.</param>
    /// <returns><see langword="false"/> when no complete box header remains.</returns>
    private static bool NextBox(ReadOnlySpan<byte> data, ref int position, out uint type, out JpxDataRange body)
    {
        type = 0;
        body = default;
        if (position + BoxHeaderBytes > data.Length)
        {
            return false;
        }

        var length = (long)BinaryPrimitives.ReadUInt32BigEndian(data[position..]);
        type = BinaryPrimitives.ReadUInt32BigEndian(data[(position + TypeBytes)..]);
        var header = BoxHeaderBytes;
        if (length == ExtendedLength)
        {
            if (position + ExtendedHeaderBytes > data.Length)
            {
                return false;
            }

            length = (long)Math.Min(BinaryPrimitives.ReadUInt64BigEndian(data[(position + BoxHeaderBytes)..]), long.MaxValue);
            header = ExtendedHeaderBytes;
        }

        // A length of zero, or one past the end, runs to the end of the data.
        var end = length == 0 || position + length > data.Length ? data.Length : position + (int)length;
        if (end < position + header)
        {
            return false;
        }

        body = new(position + header, end - position - header);
        position = end;
        return true;
    }

    /// <summary>Reads the boxes inside the JP2 header box.</summary>
    /// <param name="header">The header box contents.</param>
    /// <param name="offset">Where the header box contents start in the file.</param>
    /// <param name="builder">Collects what the boxes say.</param>
    private static void ReadHeader(ReadOnlySpan<byte> header, int offset, Builder builder)
    {
        var position = 0;
        while (NextBox(header, ref position, out var type, out var range))
        {
            var body = header.Slice(range.Offset, range.Length);
            switch (type)
            {
                case ColourBox:
                {
                    builder.OfferColour(body.Length >= ColourHeaderBytes ? ReadColour(body, offset + range.Offset) : new(JpxColorSpace.Unknown, 0, 0));
                    break;
                }

                case PaletteBox:
                {
                    builder.Palette ??= ReadPalette(body);
                    break;
                }

                case MappingBox:
                {
                    builder.Mappings ??= ReadMappings(body);
                    break;
                }

                case DefinitionBox:
                {
                    builder.Definitions ??= ReadDefinitions(body);
                    break;
                }

                case ColourGroupBox:
                {
                    ReadHeader(body, offset + range.Offset, builder);
                    break;
                }

                case OpacityBox:
                {
                    builder.OpacityKind = ReadOpacityKind(body);
                    break;
                }
            }
        }
    }

    /// <summary>Reads the type of an Opacity box (ISO 15444-2 M.11.7.6). A chroma-key, and any reserved type, is not used: PDF ignores chroma-key transparency.</summary>
    /// <param name="body">The box contents.</param>
    /// <returns>0 or 1 for a last opacity channel, or -1.</returns>
    private static int ReadOpacityKind(ReadOnlySpan<byte> body) =>
        !body.IsEmpty && body[0] is OpacityLast or OpacityLastPremultiplied ? body[0] : -1;

    /// <summary>
    /// Reads a colour specification box (T.800 I.5.3.3): the method, the precedence, the approximation and, for the
    /// enumerated method, the colour space, and for an ICC method (restricted ICC, or any ICC of the Part 2 method 3) the
    /// profile. A vendor method gives an unknown space, so the channel count decides (ISO 32000-2 section 7.4.9).
    /// </summary>
    /// <param name="body">The box contents, at least <see cref="ColourHeaderBytes"/> long.</param>
    /// <param name="offset">Where the box contents start in the file.</param>
    /// <returns>The candidate colour space.</returns>
    private static ColourCandidate ReadColour(ReadOnlySpan<byte> body, int offset)
    {
        var space = body.Length >= ColourHeaderBytes + TypeBytes && body[0] == EnumeratedMethod
            ? FromEnumerated(BinaryPrimitives.ReadUInt32BigEndian(body[ColourHeaderBytes..]))
            : JpxColorSpace.Unknown;
        var isProfile = body[0] is RestrictedIccMethod or AnyIccMethod && body.Length > ColourHeaderBytes;
        var candidate = new ColourCandidate(space, (sbyte)body[1], body[2]) { Profile = isProfile ? new(offset + ColourHeaderBytes, body.Length - ColourHeaderBytes) : default };
        return space == JpxColorSpace.Lab ? WithLab(candidate, body[(ColourHeaderBytes + TypeBytes)..]) : candidate;
    }

    /// <summary>
    /// Adds the enumerated parameters of a CIELab specification (ISO 15444-2 M.11.7.4.1). Without them the defaults apply; a
    /// field of another length breaks the rule that all parameters are given, so the specification is not used.
    /// </summary>
    /// <param name="candidate">The candidate, with the CIELab space.</param>
    /// <param name="field">The EP field, the rest of the box.</param>
    /// <returns>The candidate with its parameters, or an unknown space.</returns>
    private static ColourCandidate WithLab(in ColourCandidate candidate, ReadOnlySpan<byte> field) => field.Length switch
    {
        0 => candidate,
        JpxLabParameters.Bytes => candidate with { Lab = JpxLabParameters.Read(field) },
        _ => candidate with { Space = JpxColorSpace.Unknown },
    };

    /// <summary>Maps an enumerated colour space (T.800 table I.10, and I.5.3.3 for e-sYCC and CMYK).</summary>
    /// <param name="enumerated">The enumerated colour space.</param>
    /// <returns>The colour space.</returns>
    private static JpxColorSpace FromEnumerated(uint enumerated) => enumerated switch
    {
        Srgb => JpxColorSpace.Srgb,
        Gray => JpxColorSpace.Gray,
        Sycc => JpxColorSpace.Sycc,
        Eycc => JpxColorSpace.Eycc,
        Cmyk => JpxColorSpace.Cmyk,
        CieLab => JpxColorSpace.Lab,
        _ => JpxColorSpace.Unknown,
    };

    /// <summary>Reads a palette box.</summary>
    /// <param name="body">The box contents.</param>
    /// <returns>The palette, or <see langword="null"/> when invalid.</returns>
    private static JpxPalette? ReadPalette(ReadOnlySpan<byte> body)
    {
        if (body.Length < PaletteHeaderBytes)
        {
            return null;
        }

        var count = BinaryPrimitives.ReadUInt16BigEndian(body);
        var columns = body[ShortBytes];
        if (count is 0 or > MaxPaletteEntries || columns == 0 || body.Length < PaletteHeaderBytes + columns)
        {
            return null;
        }

        var precisions = new int[columns];
        var signed = new bool[columns];
        var entryBytes = 0;
        for (var i = 0; i < columns; i++)
        {
            var size = body[PaletteHeaderBytes + i];
            precisions[i] = Math.Min((size & PrecisionMask) + 1, MaxPaletteBits);
            signed[i] = (size & SignedBit) != 0;
            entryBytes += (precisions[i] + ByteBits - 1) / ByteBits;
        }

        var values = body[(PaletteHeaderBytes + columns)..];
        return values.Length < count * entryBytes ? null : new(ReadEntries(values, count, precisions), count, columns, precisions, signed);
    }

    /// <summary>Reads the palette entries.</summary>
    /// <param name="values">The entry bytes.</param>
    /// <param name="count">The number of entries.</param>
    /// <param name="precisions">The bits of each column.</param>
    /// <returns>The values, entry by entry.</returns>
    private static int[] ReadEntries(ReadOnlySpan<byte> values, int count, int[] precisions)
    {
        var entries = new int[count * precisions.Length];
        var position = 0;
        for (var i = 0; i < entries.Length; i++)
        {
            var bytes = (precisions[i % precisions.Length] + ByteBits - 1) / ByteBits;
            var value = 0;
            for (var b = 0; b < bytes; b++)
            {
                value = (value << ByteBits) | values[position + b];
            }

            entries[i] = value;
            position += bytes;
        }

        return entries;
    }

    /// <summary>Reads a component mapping box.</summary>
    /// <param name="body">The box contents.</param>
    /// <returns>The mappings.</returns>
    private static JpxChannelMapping[] ReadMappings(ReadOnlySpan<byte> body)
    {
        var mappings = new JpxChannelMapping[body.Length / MappingBytes];
        for (var i = 0; i < mappings.Length; i++)
        {
            var entry = body.Slice(i * MappingBytes, MappingBytes);
            mappings[i] = new(BinaryPrimitives.ReadUInt16BigEndian(entry), entry[MappingTypeOffset] != 0, entry[MappingColumnOffset]);
        }

        return mappings;
    }

    /// <summary>Reads a channel definition box.</summary>
    /// <param name="body">The box contents.</param>
    /// <returns>The definitions.</returns>
    private static JpxChannelDefinition[] ReadDefinitions(ReadOnlySpan<byte> body)
    {
        if (body.Length < ShortBytes)
        {
            return [];
        }

        var count = Math.Min((int)BinaryPrimitives.ReadUInt16BigEndian(body), (body.Length - ShortBytes) / DefinitionBytes);
        var definitions = new JpxChannelDefinition[count];
        for (var i = 0; i < count; i++)
        {
            var entry = body.Slice(ShortBytes + (i * DefinitionBytes), DefinitionBytes);
            definitions[i] = new(
                BinaryPrimitives.ReadUInt16BigEndian(entry),
                BinaryPrimitives.ReadUInt16BigEndian(entry[DefinitionTypeOffset..]),
                BinaryPrimitives.ReadUInt16BigEndian(entry[DefinitionAssociationOffset..]));
        }

        return definitions;
    }

    /// <summary>One colour specification box and how it ranks against the others.</summary>
    /// <param name="Space">The colour space; unknown for an ICC profile or a vendor method.</param>
    /// <param name="Precedence">The precedence, a signed byte; higher wins.</param>
    /// <param name="Approximation">The approximation; 1 is the best, 0 means unknown.</param>
    private readonly record struct ColourCandidate(JpxColorSpace Space, int Precedence, int Approximation)
    {
        /// <summary>The rank of an unknown approximation, worse than any stated one.</summary>
        private const int UnknownApproximation = 256;

        /// <summary>The shift that puts "space is known" above the precedence in the ranking key.</summary>
        private const int KnownShift = 20;

        /// <summary>The shift that puts the precedence above the approximation in the ranking key.</summary>
        private const int PrecedenceShift = 10;

        /// <summary>The amount that makes a signed precedence positive.</summary>
        private const int PrecedenceBias = 128;

        /// <summary>The ranking key's ceiling for the approximation part, above any rank.</summary>
        private const int ApproximationCeiling = 1_023;

        /// <summary>Gets where the ICC profile is in the file, or an empty range for an enumerated or vendor specification.</summary>
        internal JpxDataRange Profile { get; init; }

        /// <summary>Gets the explicit CIELab parameters of an enumerated CIELab specification, or <see langword="null"/>.</summary>
        internal JpxLabParameters? Lab { get; init; }

        /// <summary>
        /// Gets the ranking key: a known space or profile first, then the higher precedence, then the better approximation
        /// (1 is best; 0, unknown, ranks last).
        /// </summary>
        private int Key =>
            ((Space == JpxColorSpace.Unknown && Profile.Length == 0 ? 0 : 1) << KnownShift)
            + ((Precedence + PrecedenceBias) << PrecedenceShift)
            + (ApproximationCeiling - (Approximation == 0 ? UnknownApproximation : Approximation));

        /// <summary>Determines whether this candidate ranks above another.</summary>
        /// <param name="other">The other candidate.</param>
        /// <returns><see langword="true"/> when this one is better.</returns>
        internal bool Beats(in ColourCandidate other) => Key > other.Key;
    }

    /// <summary>Collects the header boxes while they are read.</summary>
    private sealed class Builder
    {
        /// <summary>The colour specification chosen so far.</summary>
        private ColourCandidate? _colour;

        /// <summary>The best enumerated colour specification, which stands in when the best one is a profile that cannot be used.</summary>
        private ColourCandidate? _enumerated;

        /// <summary>Gets or sets the palette.</summary>
        internal JpxPalette? Palette { get; set; }

        /// <summary>Gets or sets the component mapping.</summary>
        internal JpxChannelMapping[]? Mappings { get; set; }

        /// <summary>Gets or sets the channel definitions.</summary>
        internal JpxChannelDefinition[]? Definitions { get; set; }

        /// <summary>Gets or sets the opacity type of an Opacity box, or -1.</summary>
        internal int OpacityKind { get; set; } = -1;

        /// <summary>Gets or sets what the first compositing layer header box says, or <see langword="null"/> when there is none.</summary>
        internal Builder? Layer { get; set; }

        /// <summary>
        /// Considers a colour specification box. A box naming a space we know beats one we do not; among known spaces the
        /// higher precedence wins, then the better approximation; the earlier box wins a tie.
        /// </summary>
        /// <param name="candidate">The candidate.</param>
        internal void OfferColour(in ColourCandidate candidate)
        {
            if (_colour is not { } best || candidate.Beats(best))
            {
                _colour = candidate;
            }

            if (candidate.Space != JpxColorSpace.Unknown && (_enumerated is not { } bestEnumerated || candidate.Beats(bestEnumerated)))
            {
                _enumerated = candidate;
            }
        }

        /// <summary>
        /// Builds the file information. The first compositing layer's boxes win over those of the JP2 header box: its colour
        /// specifications replace the header's, and its channel definition or Opacity box replaces the header's definitions.
        /// </summary>
        /// <param name="codestream">Where the codestream is.</param>
        /// <returns>The file information.</returns>
        internal JpxFileInfo Finish(in JpxDataRange codestream)
        {
            var merged = Merge();
            return new(codestream, merged._enumerated?.Space ?? JpxColorSpace.Unknown, merged.Palette, merged.Mappings, merged.Definitions)
            {
                Profile = merged._colour?.Profile ?? default,
                OpacityKind = merged.OpacityKind,
                Lab = merged._enumerated?.Lab,
            };
        }

        /// <summary>Lays the compositing layer's boxes over the JP2 header's.</summary>
        /// <returns>The builder that holds what applies.</returns>
        private Builder Merge()
        {
            if (Layer is not { } layer)
            {
                return this;
            }

            var merged = new Builder { Palette = layer.Palette ?? Palette, Mappings = layer.Mappings ?? Mappings, OpacityKind = layer.OpacityKind };
            var useLayerColours = layer._colour is not null;
            merged._colour = useLayerColours ? layer._colour : _colour;
            merged._enumerated = useLayerColours ? layer._enumerated : _enumerated;

            // An Opacity box of the layer replaces any channel definitions; a layer with definitions of its own replaces the header's Opacity box.
            if (layer.OpacityKind < 0)
            {
                merged.Definitions = layer.Definitions ?? Definitions;
                merged.OpacityKind = layer.Definitions is null ? OpacityKind : -1;
            }

            return merged;
        }
    }
}
