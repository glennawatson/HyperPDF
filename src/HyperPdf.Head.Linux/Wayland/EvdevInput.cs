// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Presentation.Input;

namespace HyperPdf.Head.Linux.Wayland;

/// <summary>
/// Turns the Linux input codes a Wayland compositor reports into the viewer's own input types. The codes name key positions, so the
/// letter and sign keys are found where a US layout has them; the navigation keys are the same on every layout.
/// </summary>
internal static class EvdevInput
{
    /// <summary>The code of Escape.</summary>
    private const uint EscapeCode = 1;

    /// <summary>The code of the minus key.</summary>
    private const uint MinusCode = 12;

    /// <summary>The code of the equals key, which is plus with Shift.</summary>
    private const uint EqualsCode = 13;

    /// <summary>The code of Backspace.</summary>
    private const uint BackspaceCode = 14;

    /// <summary>The code of Tab.</summary>
    private const uint TabCode = 15;

    /// <summary>The code of Enter.</summary>
    private const uint EnterCode = 28;

    /// <summary>The code of the A key.</summary>
    private const uint LetterACode = 30;

    /// <summary>The code of the C key.</summary>
    private const uint LetterCCode = 46;

    /// <summary>The code of the space bar.</summary>
    private const uint SpaceCode = 57;

    /// <summary>The code of F2.</summary>
    private const uint F2Code = 60;

    /// <summary>The code of minus on the number pad.</summary>
    private const uint PadMinusCode = 74;

    /// <summary>The code of plus on the number pad.</summary>
    private const uint PadPlusCode = 78;

    /// <summary>The code of Enter on the number pad.</summary>
    private const uint PadEnterCode = 96;

    /// <summary>The code of Home.</summary>
    private const uint HomeCode = 102;

    /// <summary>The code of the up arrow.</summary>
    private const uint UpCode = 103;

    /// <summary>The code of Page Up.</summary>
    private const uint PageUpCode = 104;

    /// <summary>The code of the left arrow.</summary>
    private const uint LeftCode = 105;

    /// <summary>The code of the right arrow.</summary>
    private const uint RightCode = 106;

    /// <summary>The code of End.</summary>
    private const uint EndCode = 107;

    /// <summary>The code of the down arrow.</summary>
    private const uint DownCode = 108;

    /// <summary>The code of Page Down.</summary>
    private const uint PageDownCode = 109;

    /// <summary>The code of Delete.</summary>
    private const uint DeleteCode = 111;

    /// <summary>The code of the left pointer button.</summary>
    private const uint LeftButtonCode = 0x110;

    /// <summary>The code of the right pointer button.</summary>
    private const uint RightButtonCode = 0x111;

    /// <summary>The code of the middle pointer button.</summary>
    private const uint MiddleButtonCode = 0x112;

    /// <summary>The axis amount a compositor reports for one wheel notch.</summary>
    private const double AxisPerNotch = 15;

    /// <summary>Finds the viewer key of a key code.</summary>
    /// <param name="code">The key code.</param>
    /// <returns>The key, or <see cref="ViewerKey.Other"/> for a key the viewer has no use for.</returns>
    internal static ViewerKey ToKey(uint code) => code switch
    {
        LeftCode => ViewerKey.Left,
        RightCode => ViewerKey.Right,
        UpCode => ViewerKey.Up,
        DownCode => ViewerKey.Down,
        PageUpCode => ViewerKey.PageUp,
        PageDownCode => ViewerKey.PageDown,
        HomeCode => ViewerKey.Home,
        EndCode => ViewerKey.End,
        _ => ToEditKey(code),
    };

    /// <summary>Turns a button code into a pointer button.</summary>
    /// <param name="code">The button code.</param>
    /// <returns>The button, or <see cref="ViewerButtons.None"/> for an extra button.</returns>
    internal static ViewerButtons ToButton(uint code) => code switch
    {
        LeftButtonCode => ViewerButtons.Left,
        MiddleButtonCode => ViewerButtons.Middle,
        RightButtonCode => ViewerButtons.Right,
        _ => ViewerButtons.None,
    };

    /// <summary>Turns a wl_pointer axis amount into scroll units. Wayland counts down and right as positive, and the viewer counts up and left as positive.</summary>
    /// <param name="amount">The axis amount.</param>
    /// <returns>The scroll in notch units.</returns>
    internal static int ToScroll(double amount) => (int)Math.Round(-amount * Windowing.Windows.WindowEvent.ScrollNotch / AxisPerNotch);

    /// <summary>Finds the viewer key of a code that is not a navigation key.</summary>
    /// <param name="code">The key code.</param>
    /// <returns>The key.</returns>
    private static ViewerKey ToEditKey(uint code) => code switch
    {
        EnterCode or PadEnterCode => ViewerKey.Enter,
        EscapeCode => ViewerKey.Escape,
        DeleteCode => ViewerKey.Delete,
        BackspaceCode => ViewerKey.Back,
        TabCode => ViewerKey.Tab,
        SpaceCode => ViewerKey.Space,
        F2Code => ViewerKey.F2,
        EqualsCode or PadPlusCode => ViewerKey.Plus,
        MinusCode or PadMinusCode => ViewerKey.Minus,
        LetterACode => ViewerKey.A,
        LetterCCode => ViewerKey.C,
        _ => ViewerKey.Other,
    };
}
