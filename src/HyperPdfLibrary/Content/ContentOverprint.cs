// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Colors;

namespace HyperPdfLibrary.Content;

/// <summary>
/// Previews overprint (ISO 32000-2 11.7.4.3) on an RGB device, which holds no separate colorants to leave alone. The special
/// overprint blend keeps a backdrop colorant the paint does not specify. The preview paints such objects with the Multiply
/// blend instead, in which an unspecified colorant is white (1.0 in the group's subtractive view, as 11.7.4.2 assumes
/// for it) and so leaves the backdrop's share of that colorant in the result. It matches the specified result where the
/// backdrop is blank paper or holds only the colorants the paint replaces; where the backdrop holds a colorant the paint
/// does specify, the preview darkens it instead of replacing it.
/// </summary>
internal static class ContentOverprint
{
    /// <summary>The overprint mode that treats zero DeviceCMYK components as unspecified.</summary>
    internal const int NonzeroMode = 1;

    /// <summary>Chooses the blend a paint uses.</summary>
    /// <param name="self">The owned interpreter state.</param>
    /// <param name="state">The graphics state.</param>
    /// <param name="stroke">Whether the paint is a stroke; otherwise a fill.</param>
    /// <returns>Multiply when the paint overprints and keeps backdrop colorants, otherwise the state's own blend.</returns>
    internal static PdfBlendMode BlendFor(ContentInterpreter self, ref GraphicsState state, bool stroke)
    {
        var overprint = stroke ? state.StrokeOverprint : state.FillOverprint;
        if (!overprint || state.BlendMode != PdfBlendMode.Normal || !self.Cache.SimulateOverprint)
        {
            return state.BlendMode;
        }

        return ContentOverprint.KeepsBackdrop(stroke ? state.Stroke : state.Fill, state.OverprintMode) ? PdfBlendMode.Multiply : state.BlendMode;
    }

    /// <summary>
    /// Determines whether painting a colour with overprint leaves some backdrop colorants alone. DeviceCMYK does so in
    /// overprint mode 1 for the components that are zero. A Separation does so for every colorant but its own, unless it
    /// is /All. A DeviceN space does so unless it names all four process colorants. Other spaces specify every colorant of
    /// a process device, so they replace the backdrop.
    /// </summary>
    /// <param name="colour">The colour.</param>
    /// <param name="mode">The overprint mode.</param>
    /// <returns><see langword="true"/> when the paint does not replace the whole backdrop.</returns>
    internal static bool KeepsBackdrop(in ColorState colour, int mode)
    {
        if (colour.Pattern is not null || colour.PaintsNothing)
        {
            return false;
        }

        return colour.Space.NamedKind switch
        {
            PdfColorSpaceKind.DeviceCmyk => mode == ContentOverprint.NonzeroMode && ContentOverprint.HasZeroComponent(colour),
            PdfColorSpaceKind.Separation => !ContentOverprint.IsAll(colour.Space),
            PdfColorSpaceKind.DeviceN => !ContentOverprint.NamesAllProcess(colour.Space),
            _ => false,
        };
    }

    /// <summary>Determines whether a DeviceCMYK colour has a zero component.</summary>
    /// <param name="colour">The colour.</param>
    /// <returns><see langword="true"/> when one of the four components is zero.</returns>
    private static bool HasZeroComponent(in ColorState colour)
    {
        var components = colour.Components;
        return components.X == 0 || components.Y == 0 || components.Z == 0 || components.W == 0;
    }

    /// <summary>Determines whether a Separation space is /All.</summary>
    /// <param name="space">The space.</param>
    /// <returns><see langword="true"/> for /All.</returns>
    private static bool IsAll(PdfColorSpace space) =>
        space.ColorantNames is { Count: > 0 } names && names[0] == ContentColorants.All;

    /// <summary>Determines whether a DeviceN space names Cyan, Magenta, Yellow and Black.</summary>
    /// <param name="space">The space.</param>
    /// <returns><see langword="true"/> when all four are named.</returns>
    private static bool NamesAllProcess(PdfColorSpace space)
    {
        if (space.ColorantNames is not { } names)
        {
            return false;
        }

        var found = 0;
        foreach (var name in names)
        {
            if (name is ContentColorants.Cyan or ContentColorants.Magenta or ContentColorants.Yellow or ContentColorants.Black)
            {
                found++;
            }
        }

        return found == ContentColorants.KeptComponents;
    }
}
