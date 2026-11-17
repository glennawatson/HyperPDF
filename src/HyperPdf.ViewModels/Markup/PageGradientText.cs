// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Core.Documents;

namespace HyperPdf.ViewModels.Markup;

/// <summary>Reads the text of a gradient menu entry: <c>linear:ANGLE:RRGGBB:RRGGBB</c> or <c>radial:RRGGBB:RRGGBB</c>.</summary>
internal static class PageGradientText
{
    /// <summary>The number of digits of a colour.</summary>
    private const int ColourDigits = 6;

    /// <summary>Reads a gradient.</summary>
    /// <param name="text">The text.</param>
    /// <param name="gradient">Receives the gradient.</param>
    /// <returns><see langword="true"/> when the text is a gradient.</returns>
    internal static bool TryParse(string? text, out PageGradient gradient)
    {
        gradient = default;
        return text?.Split(':') switch
        {
            ["linear", var angle, var from, var to] => TryLinear(angle, from, to, out gradient),
            ["radial", var centre, var edge] => TryRadial(centre, edge, out gradient),
            _ => false,
        };
    }

    /// <summary>Reads a gradient along a line.</summary>
    /// <param name="angle">The direction in degrees.</param>
    /// <param name="from">The first colour.</param>
    /// <param name="to">The second colour.</param>
    /// <param name="gradient">Receives the gradient.</param>
    /// <returns><see langword="true"/> when the parts are a gradient.</returns>
    private static bool TryLinear(string angle, string from, string to, out PageGradient gradient)
    {
        gradient = default;
        if (!float.TryParse(angle, NumberStyles.Float, CultureInfo.InvariantCulture, out var degrees) || !float.IsFinite(degrees))
        {
            return false;
        }

        if (!TryColour(from, out var start) || !TryColour(to, out var end))
        {
            return false;
        }

        gradient = new(start, end, false, degrees);
        return true;
    }

    /// <summary>Reads a gradient out from the middle.</summary>
    /// <param name="centre">The colour in the middle.</param>
    /// <param name="edge">The colour at the edge.</param>
    /// <param name="gradient">Receives the gradient.</param>
    /// <returns><see langword="true"/> when the parts are a gradient.</returns>
    private static bool TryRadial(string centre, string edge, out PageGradient gradient)
    {
        gradient = default;
        if (!TryColour(centre, out var inner) || !TryColour(edge, out var outer))
        {
            return false;
        }

        gradient = new(inner, outer, true, 0F);
        return true;
    }

    /// <summary>Reads six hexadecimal digits.</summary>
    /// <param name="digits">The digits.</param>
    /// <param name="colour">Receives the colour.</param>
    /// <returns><see langword="true"/> when they are six digits.</returns>
    private static bool TryColour(string digits, out uint colour)
    {
        colour = 0;
        return digits.Length == ColourDigits && uint.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out colour);
    }
}
