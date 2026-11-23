// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Rendering;
using HyperPdf.ViewModels.Markup;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>Owns the picture the signature window shows of a mark that came from a picture file.</summary>
/// <param name="surfaces">Makes the picture's surface.</param>
[DebuggerDisplay("MarkPreview: {_frame}")]
internal sealed class MarkPreview(IRenderSurfaceFactory surfaces) : IDisposable
{
    /// <summary>The picture shown now.</summary>
    private MarkFrame _frame = MarkFrame.Empty;

    /// <inheritdoc/>
    public void Dispose()
    {
        MarkFrames.Release(_frame);
        _frame = MarkFrame.Empty;
    }

    /// <summary>Shows the window that makes a hand signature or initials, and gives the picture back when the window closes.</summary>
    /// <param name="showForm">Shows a window of rows and answers when it closes.</param>
    /// <param name="surfaces">Makes the picture's surface.</param>
    /// <param name="mark">The view model of the mark being made.</param>
    /// <returns>One value when the window closes: <see langword="true"/> to use the mark.</returns>
    internal static IObservable<bool> Show(Func<FormRequest, IObservable<bool>> showForm, IRenderSurfaceFactory surfaces, SignatureMarkViewModel mark)
    {
        ArgumentNullException.ThrowIfNull(mark);
        var preview = new MarkPreview(surfaces);
        return showForm(ToolForms.SignatureMark(mark, room => preview.Get(mark, room))).Finally(preview.Dispose);
    }

    /// <summary>Gets the picture of the mark being made.</summary>
    /// <param name="mark">The view model of the mark.</param>
    /// <param name="room">The room beside the window's rows.</param>
    /// <returns>The picture, or <see langword="null"/> for a typed or drawn mark or before a picture is chosen.</returns>
    internal IRenderSurface? Get(SignatureMarkViewModel mark, SurfaceSize room)
    {
        _frame = MarkFrames.Get(surfaces, _frame, mark.IsImage ? mark.Preview : null, room);
        return _frame.Surface;
    }
}
