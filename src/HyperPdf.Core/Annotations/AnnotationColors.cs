// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Annotations;

/// <summary>
/// The annotation colours offered: soft tones that keep text readable when multiplied over it (docs/COMFORT.md).
/// Each has a name, so colour is never the only way to tell them apart.
/// </summary>
public static class AnnotationColors
{
    /// <summary>Gets soft yellow, the default highlight.</summary>
    public static uint Sand => 0xF0DC96U;

    /// <summary>Gets soft green.</summary>
    public static uint Sage => 0xBCDCB0U;

    /// <summary>Gets soft blue.</summary>
    public static uint Slate => 0xB4CCDCU;

    /// <summary>Gets soft red.</summary>
    public static uint Clay => 0xE8BCB4U;

    /// <summary>Gets soft purple.</summary>
    public static uint Heather => 0xD4C8E4U;

    /// <summary>Gets soft orange.</summary>
    public static uint Peach => 0xF0CCA8U;

    /// <summary>Gets soft grey.</summary>
    public static uint Stone => 0xD0D0CCU;

    /// <summary>Gets the dark ink used for drawings, text boxes and signatures.</summary>
    public static uint Ink => 0x23324AU;

    /// <summary>Gets the named colours in menu order.</summary>
    public static IReadOnlyList<(string Name, uint Color)> All { get; } =
        [("Yellow", Sand), ("Green", Sage), ("Blue", Slate), ("Red", Clay), ("Purple", Heather), ("Orange", Peach), ("Grey", Stone), ("Dark blue", Ink)];

    /// <summary>Gets the name of a colour, matching a soft colour or its deeper tone, or <see langword="null"/> for any other colour.</summary>
    /// <param name="color">The colour.</param>
    /// <returns>The name.</returns>
    public static string? GetName(uint color)
    {
        foreach (var (name, value) in All)
        {
            if (value == color || Deep(value) == color)
            {
                return name;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the deeper tone of a soft colour, for lines, shapes and stamps, which need more weight than a highlight to
    /// be seen; still muted rather than saturated. Colours without a deeper tone are returned as they are.
    /// </summary>
    /// <param name="color">The soft colour.</param>
    /// <returns>The deeper tone.</returns>
    public static uint Deep(uint color) => color switch
    {
        0xF0DC96U => 0x9A7420U,
        0xBCDCB0U => 0x4F7A44U,
        0xB4CCDCU => 0x3D6A8CU,
        0xE8BCB4U => 0xA8473AU,
        0xD4C8E4U => 0x6A5A8CU,
        0xF0CCA8U => 0xA0602AU,
        0xD0D0CCU => 0x5A5E66U,
        _ => color,
    };
}
