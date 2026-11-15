// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Rendering;
using HyperPdf.Windowing.Rendering;
using ReactiveUI.Primitives;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>What a form dialog shows.</summary>
/// <param name="Title">The heading.</param>
/// <param name="Describe">Adds the rows, reading the view model as it is now.</param>
/// <param name="AcceptText">The text of the button that agrees, or <see langword="null"/> for a dialog that only closes.</param>
/// <param name="CanAccept">Tells whether the user can agree now, or <see langword="null"/> when they always can.</param>
/// <param name="Changes">Signals each change the view model makes by itself, so the dialog is laid out again.</param>
/// <param name="Picture">Makes the picture shown beside the rows, no larger than the room it is given, or <see langword="null"/> for a dialog without one.</param>
/// <param name="Bind">Follows the changes the user makes in the dialog and writes them to the view model. It gives back what ends the following.</param>
[DebuggerDisplay("FormRequest: {Title}")]
internal sealed record FormRequest(
    string Title,
    Action<FormBuilder> Describe,
    string? AcceptText,
    Func<bool>? CanAccept,
    IObservable<RxVoid> Changes,
    Func<SurfaceSize, IRenderSurface?>? Picture,
    Func<IObservable<FormEdit>, IDisposable> Bind)
{
    /// <summary>Gets the answers the view model gives by itself, such as when its own command finishes the dialog. The first one closes the dialog with that answer.</summary>
    internal IObservable<bool>? Answered { get; init; }

    /// <summary>
    /// Gets what takes the strokes drawn on the dialog's picture, or <see langword="null"/> for a dialog that cannot be drawn on. It is
    /// given the stream of strokes and returns what ends it.
    /// </summary>
    internal Func<IObservable<PadStroke>, IDisposable>? Pad { get; init; }

    /// <summary>Gets what the drawing pad starts with, given where it lies, or <see langword="null"/> when it starts blank.</summary>
    internal Func<LayoutRect, PadInk?>? PadSeed { get; init; }

    /// <summary>Gets whether the drawing pad is shown now, or <see langword="null"/> when it always is.</summary>
    internal Func<bool>? PadShown { get; init; }

    /// <summary>Gets a stream that wipes the pad each time it signals, or <see langword="null"/> when the pad is never wiped.</summary>
    internal IObservable<RxVoid>? PadCleared { get; init; }
}
