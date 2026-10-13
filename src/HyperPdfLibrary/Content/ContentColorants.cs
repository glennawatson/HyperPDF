// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Numerics;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Content;

/// <summary>
/// Names the colorants a colour paints and leaves out the hidden ones, for the separation view. A Separation or DeviceN
/// space paints the colorants it names. DeviceGray and one-component ICC spaces paint Black, and DeviceCMYK and four-component
/// ICC spaces paint Cyan, Magenta, Yellow and Black. RGB, Lab, CalRGB, CalGray and Indexed spaces paint no colorant.
/// </summary>
internal static class ContentColorants
{
    /// <summary>The most components a colour keeps, which limits how far a hidden colorant can be removed from it.</summary>
    internal const int KeptComponents = ColorState.SavedComponents;

    /// <summary>The name of the Black process colorant.</summary>
    internal const string Black = "Black";

    /// <summary>The name of the Cyan process colorant.</summary>
    internal const string Cyan = "Cyan";

    /// <summary>The name of the Magenta process colorant.</summary>
    internal const string Magenta = "Magenta";

    /// <summary>The name of the Yellow process colorant.</summary>
    internal const string Yellow = "Yellow";

    /// <summary>The colorant that stands for every colorant of the device.</summary>
    internal const string All = "All";

    /// <summary>The colorant that marks nothing.</summary>
    internal const string None = "None";

    /// <summary>The components of a CMYK colour.</summary>
    private const int CmykComponents = 4;

    /// <summary>The process colorants of one-component spaces.</summary>
    private static readonly string[] GrayNames = [ContentColorants.Black];

    /// <summary>The process colorants of four-component spaces.</summary>
    private static readonly string[] CmykNames = [ContentColorants.Cyan, ContentColorants.Magenta, ContentColorants.Yellow, ContentColorants.Black];

    /// <summary>Gets the names of the colorants a space paints, one per component.</summary>
    /// <param name="space">The colour space.</param>
    /// <returns>The names, or <see langword="null"/> when the space paints no colorant.</returns>
    internal static IReadOnlyList<string>? Names(PdfColorSpace space) => space.ColorantNames ?? space.Kind switch
    {
        PdfColorSpaceKind.DeviceGray => ContentColorants.GrayNames,
        PdfColorSpaceKind.DeviceCmyk => ContentColorants.CmykNames,
        PdfColorSpaceKind.IccBased when space.Components == 1 => ContentColorants.GrayNames,
        PdfColorSpaceKind.IccBased when space.Components == ContentColorants.CmykComponents => ContentColorants.CmykNames,
        _ => null,
    };

    /// <summary>Determines whether a space measures its component as light rather than ink, so that 1 means no colorant.</summary>
    /// <param name="space">The colour space.</param>
    /// <returns><see langword="true"/> for DeviceGray and one-component ICC spaces.</returns>
    internal static bool IsLight(PdfColorSpace space) =>
        space.Kind == PdfColorSpaceKind.DeviceGray || (space.Kind == PdfColorSpaceKind.IccBased && space.Components == 1);

    /// <summary>Determines whether a colorant is hidden.</summary>
    /// <param name="hidden">The hidden names.</param>
    /// <param name="name">The colorant name.</param>
    /// <returns><see langword="true"/> when hidden.</returns>
    internal static bool IsHidden(ImmutableArray<string> hidden, string name)
    {
        foreach (var candidate in hidden)
        {
            if (string.Equals(candidate, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reports a colour to the collector, if there is one.</summary>
    /// <param name="self">The owned interpreter state.</param>
    /// <param name="colour">The colour painted.</param>
    internal static void Report(ContentInterpreter self, in ColorState colour)
    {
        if (self.Colorants is { } sink && colour.Pattern is null && !colour.PaintsNothing)
        {
            sink.Painted(colour.Space, colour.Components, true);
        }
    }

    /// <summary>
    /// Leaves the hidden colorants out of a colour. A Separation of a hidden colorant, and a DeviceN space of only hidden
    /// colorants, paint nothing. A hidden process colorant or DeviceN colorant is removed from the colour, which then paints
    /// the other colorants and clears the hidden one beneath it, as that plate would show. A DeviceN space of more than four
    /// tints cannot be changed that way and paints whole.
    /// </summary>
    /// <param name="self">The owned interpreter state.</param>
    /// <param name="colour">The colour, which changes when a colorant is removed.</param>
    /// <param name="rendering">The rendering ICC conversions use.</param>
    /// <returns><see langword="false"/> when nothing is left to paint.</returns>
    internal static bool Hide(ContentInterpreter self, ref ColorState colour, IccRendering rendering)
    {
        var hidden = self.Cache.HiddenColorants;
        if (hidden.IsEmpty || colour.Pattern is not null || ContentColorants.Names(colour.Space) is not { } names)
        {
            return true;
        }

        Span<float> components = stackalloc float[PdfColorSpace.MaxComponents];
        components.Clear();
        colour.Components.CopyTo(components);
        var remaining = ContentColorants.Remove(hidden, names, ContentColorants.IsLight(colour.Space), components, out var removed);
        if (!removed)
        {
            return true;
        }

        if (!remaining || colour.Space.Kind == PdfColorSpaceKind.Separation)
        {
            return false;
        }

        colour = ColorState.Resolve(colour.Space, components, rendering) with { PaintsNothing = colour.PaintsNothing };
        return true;
    }

    /// <summary>Reports an image's colour space to the collector and tells whether the image is left out because its colorants are hidden.</summary>
    /// <param name="self">The owned interpreter state.</param>
    /// <param name="image">The image dictionary.</param>
    /// <returns><see langword="true"/> when the image is drawn.</returns>
    internal static bool ImageVisible(ContentInterpreter self, PdfDictionary image)
    {
        if (self.Colorants is null && self.Cache.HiddenColorants.IsEmpty)
        {
            return true;
        }

        var value = image.Get(KnownName.ColorSpace);
        if (value.IsNull)
        {
            value = image.Get(KnownName.CS);
        }

        if (value.IsNull)
        {
            return true;
        }

        var space = self.Cache.GetColorSpace(value, ContentExecution.CurrentResources(self)?.GetDictionary(KnownName.ColorSpace));
        self.Colorants?.Painted(space, Vector4.One, false);
        return ContentColorants.AnyVisible(self.Cache.HiddenColorants, space);
    }

    /// <summary>Determines whether a space paints a colorant that is not hidden, or paints none at all.</summary>
    /// <param name="hidden">The hidden names.</param>
    /// <param name="space">The colour space.</param>
    /// <returns><see langword="false"/> when the space paints colorants and all of them are hidden.</returns>
    internal static bool AnyVisible(ImmutableArray<string> hidden, PdfColorSpace space)
    {
        if (hidden.IsEmpty || ContentColorants.Names(space) is not { } names)
        {
            return true;
        }

        for (var i = 0; i < names.Count; i++)
        {
            if (!ContentColorants.IsHidden(hidden, names[i]) && names[i] != ContentColorants.None)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Clears the components of the hidden colorants.</summary>
    /// <param name="hidden">The hidden names.</param>
    /// <param name="names">The names of the colour's colorants.</param>
    /// <param name="light">Whether the space measures light, so that 1 is no colorant.</param>
    /// <param name="components">The colour's components, which change.</param>
    /// <param name="removed">Receives whether a component was cleared.</param>
    /// <returns>Whether a colorant that is not hidden, and not /None, is left.</returns>
    private static bool Remove(ImmutableArray<string> hidden, IReadOnlyList<string> names, bool light, Span<float> components, out bool removed)
    {
        removed = false;
        var remaining = false;
        for (var i = 0; i < names.Count; i++)
        {
            if (!ContentColorants.IsHidden(hidden, names[i]))
            {
                remaining |= names[i] != ContentColorants.None;
            }
            else if (i < ContentColorants.KeptComponents)
            {
                components[i] = light ? 1 : 0;
                removed = true;
            }
            else
            {
                // A tint past the kept components cannot be cleared, so the colour still paints it.
                remaining = true;
            }
        }

        return remaining;
    }
}
