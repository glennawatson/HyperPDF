// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// Turns the codestream components into image channels (T.800 annex I.5.3): the component mapping box lists one channel
/// each and sends it through a palette column or straight to a component, and the channel definition box says which
/// channels are colour, in which colour order, and which are opacity. The result puts the colour channels first, in
/// colour order, then the opacity channels, then any others.
/// </summary>
internal static class JpxChannels
{
    /// <summary>The channel definition type of a colour channel.</summary>
    private const int ColourType = 0;

    /// <summary>The first channel definition type that is opacity (1 is opacity, 2 is premultiplied opacity).</summary>
    private const int FirstOpacityType = 1;

    /// <summary>The last channel definition type that is opacity.</summary>
    private const int LastOpacityType = 2;

    /// <summary>Builds the channels.</summary>
    /// <param name="file">The JP2 boxes.</param>
    /// <param name="geometry">The codestream geometry.</param>
    /// <param name="raw">Whether to ignore the palette, mapping and definitions, which an Indexed image needs to read its raw indices.</param>
    /// <returns>The channels, or <see langword="null"/> when the mapping names missing components or palette columns.</returns>
    internal static JpxChannelSet? Build(JpxFileInfo file, JpxGeometry geometry, bool raw)
    {
        var components = geometry.Components;
        if (raw)
        {
            return new(Direct(components), 0);
        }

        // A palette box needs a mapping box to say which components use it (I.5.3.5); without one the palette is unused.
        var channels = file is { Palette: { } palette, Mappings: { Length: > 0 } mappings } ? Mapped(components, palette, mappings) : Direct(components);
        if (channels is null)
        {
            return null;
        }

        var declared = file.OpacityKind >= 0 && channels.Length > 1 ? OpacityBoxDefinitions(channels.Length, file.OpacityKind) : file.Definitions;
        return declared is { Length: > 0 } definitions ? Arrange(channels, definitions) : new JpxChannelSet(channels, 0);
    }

    /// <summary>
    /// Gives the definitions an Opacity box stands for (ISO 15444-2 M.11.7.6): colour channel k has association k + 1 and
    /// the last channel is opacity (type 1) or premultiplied opacity (type 2) for the whole image.
    /// </summary>
    /// <param name="count">The channels.</param>
    /// <param name="kind">The box's type: 0 for opacity, 1 for premultiplied opacity.</param>
    /// <returns>One definition per channel.</returns>
    private static JpxChannelDefinition[] OpacityBoxDefinitions(int count, int kind)
    {
        var definitions = new JpxChannelDefinition[count];
        for (var c = 0; c < count - 1; c++)
        {
            definitions[c] = new(c, ColourType, c + 1);
        }

        definitions[count - 1] = new(count - 1, FirstOpacityType + kind, 0);
        return definitions;
    }

    /// <summary>Makes one channel per component.</summary>
    /// <param name="components">The components.</param>
    /// <returns>The channels.</returns>
    private static JpxChannel[] Direct(JpxComponentInfo[] components)
    {
        var channels = new JpxChannel[components.Length];
        for (var c = 0; c < channels.Length; c++)
        {
            channels[c] = new(c, -1, components[c].Precision, components[c].Signed);
        }

        return channels;
    }

    /// <summary>Makes one channel per component mapping entry (T.800 I.5.3.5).</summary>
    /// <param name="components">The components.</param>
    /// <param name="palette">The palette.</param>
    /// <param name="mappings">The component mapping, one entry per channel.</param>
    /// <returns>The channels, or <see langword="null"/> when an entry names a missing component or palette column.</returns>
    private static JpxChannel[]? Mapped(JpxComponentInfo[] components, JpxPalette palette, JpxChannelMapping[] mappings)
    {
        var channels = new JpxChannel[mappings.Length];
        for (var i = 0; i < channels.Length; i++)
        {
            var (component, throughPalette, column) = mappings[i];
            if (component >= components.Length || (throughPalette && column >= palette.Columns))
            {
                return null;
            }

            channels[i] = throughPalette
                ? new(component, column, palette.Precisions[column], palette.Signed[column])
                : new(component, -1, components[component].Precision, components[component].Signed);
        }

        return channels;
    }

    /// <summary>
    /// Orders the channels with the channel definition box (T.800 I.5.3.6). Colour channel k, from 1, goes in slot k - 1;
    /// opacity channels follow; the rest keep their order. Definitions that name a colour twice, leave a gap in the
    /// colours or name a missing channel are ignored as a whole, so the channels stay in file order and no colour count is claimed.
    /// </summary>
    /// <param name="channels">The channels in file order.</param>
    /// <param name="definitions">The channel definitions.</param>
    /// <returns>The channels in colour order and the number of colour channels.</returns>
    private static JpxChannelSet Arrange(JpxChannel[] channels, JpxChannelDefinition[] definitions)
    {
        var slots = new int[channels.Length];
        Array.Fill(slots, -1);
        var colours = 0;
        foreach (var (channel, type, association) in definitions)
        {
            if (channel >= channels.Length)
            {
                return new(channels, 0);
            }

            if (type != ColourType)
            {
                continue;
            }

            if (association < 1 || association > channels.Length || slots[association - 1] >= 0)
            {
                return new(channels, 0);
            }

            slots[association - 1] = channel;
            colours++;
        }

        if (colours == 0 || !AreContiguous(slots, colours))
        {
            return new(channels, 0);
        }

        var opacityType = FindOpacityType(definitions, channels.Length);
        return new(Order(channels, slots, colours, definitions), colours) { Opacity = opacityType < 0 ? JpxChannelSet.NoOpacity : colours, Premultiplied = opacityType == LastOpacityType };
    }

    /// <summary>Finds the first opacity channel the definitions declare.</summary>
    /// <param name="definitions">The channel definitions.</param>
    /// <param name="channels">The number of channels.</param>
    /// <returns>The channel definition type of the first opacity channel, or -1 when there is none.</returns>
    private static int FindOpacityType(JpxChannelDefinition[] definitions, int channels)
    {
        foreach (var (channel, type, _) in definitions)
        {
            if (channel < channels && type is >= FirstOpacityType and <= LastOpacityType)
            {
                return type;
            }
        }

        return -1;
    }

    /// <summary>Determines whether the first slots, and only those, name a channel.</summary>
    /// <param name="slots">The channel in each colour slot, or -1.</param>
    /// <param name="colours">The number of named slots.</param>
    /// <returns><see langword="true"/> when slots 0 to colours - 1 are all named.</returns>
    private static bool AreContiguous(ReadOnlySpan<int> slots, int colours)
    {
        for (var i = 0; i < colours; i++)
        {
            if (slots[i] < 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Lays out colour channels, then opacity channels, then the remaining ones.</summary>
    /// <param name="channels">The channels in file order.</param>
    /// <param name="slots">The channel in each colour slot.</param>
    /// <param name="colours">The number of colour channels.</param>
    /// <param name="definitions">The channel definitions.</param>
    /// <returns>The channels in output order.</returns>
    private static JpxChannel[] Order(JpxChannel[] channels, int[] slots, int colours, JpxChannelDefinition[] definitions)
    {
        var order = new List<int>(channels.Length);
        var placed = new bool[channels.Length];
        for (var i = 0; i < colours; i++)
        {
            order.Add(slots[i]);
            placed[slots[i]] = true;
        }

        foreach (var (channel, type, _) in definitions)
        {
            if (type is < FirstOpacityType or > LastOpacityType || placed[channel])
            {
                continue;
            }

            order.Add(channel);
            placed[channel] = true;
        }

        for (var c = 0; c < channels.Length; c++)
        {
            if (placed[c])
            {
                continue;
            }

            order.Add(c);
        }

        var ordered = new JpxChannel[channels.Length];
        for (var i = 0; i < ordered.Length; i++)
        {
            ordered[i] = channels[order[i]];
        }

        return ordered;
    }
}
