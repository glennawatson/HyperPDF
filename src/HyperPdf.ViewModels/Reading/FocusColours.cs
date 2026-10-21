// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.ViewModels.Reading;

/// <summary>The page and text colours Focus Mode reads in, as 0xRRGGBB values.</summary>
/// <param name="Page">The page colour.</param>
/// <param name="Text">The text colour.</param>
[DebuggerDisplay("FocusColours: page {Page}, text {Text}")]
public readonly record struct FocusColours(uint Page, uint Text)
{
    /// <summary>The red share of perceived brightness.</summary>
    private const double RedWeight = 0.299;

    /// <summary>The green share of perceived brightness.</summary>
    private const double GreenWeight = 0.587;

    /// <summary>The blue share of perceived brightness.</summary>
    private const double BlueWeight = 0.114;

    /// <summary>The luminance above which the page is light, so its text is dark.</summary>
    private const double LightPage = 0.55;

    /// <summary>Where the green channel starts, in bits.</summary>
    private const int GreenShift = 8;

    /// <summary>Where the red channel starts, in bits.</summary>
    private const int RedShift = 16;

    /// <summary>The mask of one colour channel.</summary>
    private const uint ChannelMask = 0xFF;

    /// <summary>The dark text read on a light page.</summary>
    private const uint DarkText = 0x222222;

    /// <summary>The light text read on a dark page.</summary>
    private const uint LightText = 0xE4E0D8;

    /// <summary>The colour choices after the first, which follows the pages: warm paper, green, dark and white.</summary>
    private static readonly FocusColours[] Choices =
    [
        new(0xF4ECD8, 0x3B3A36),
        new(0xE3EBDD, 0x2F3A2F),
        new(0x2A2826, 0xD8D2C8),
        new(0xFFFFFF, 0x1A1A1A),
    ];

    /// <summary>Gets the page and text colours of a choice; the first follows the app's page colour.</summary>
    /// <param name="choice">The choice, 0 for the app's page colour.</param>
    /// <param name="paper">The app's page colour as 0xRRGGBB, used when the choice follows the pages.</param>
    /// <returns>The colours.</returns>
    public static FocusColours Resolve(int choice, uint paper) =>
        choice > 0 && choice <= Choices.Length
            ? Choices[choice - 1]
            : new(paper, Luminance(paper) > LightPage ? DarkText : LightText);

    /// <summary>Gets a colour's relative brightness.</summary>
    /// <param name="rgb">The colour as 0xRRGGBB.</param>
    /// <returns>From 0 (black) to 1 (white).</returns>
    public static double Luminance(uint rgb) =>
        ((RedWeight * Channel(rgb, RedShift)) + (GreenWeight * Channel(rgb, GreenShift)) + (BlueWeight * Channel(rgb, 0))) / byte.MaxValue;

    /// <summary>Reads one channel of a packed colour.</summary>
    /// <param name="rgb">The colour as 0xRRGGBB.</param>
    /// <param name="shift">Where the channel starts, in bits.</param>
    /// <returns>The channel, 0 to 255.</returns>
    private static uint Channel(uint rgb, int shift) => (rgb >> shift) & ChannelMask;
}
