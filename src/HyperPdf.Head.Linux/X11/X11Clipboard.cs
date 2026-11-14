// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;

namespace HyperPdf.Head.Linux.X11;

/// <summary>
/// The window's side of the X11 clipboard. X11 keeps no copy of copied text: the window that copied owns the selection and hands
/// the text to each application that asks, for as long as it stays open.
/// </summary>
/// <param name="display">The display.</param>
/// <param name="window">The window that owns the selection.</param>
/// <param name="atoms">The atoms the clipboard uses.</param>
[DebuggerDisplay("X11Clipboard: {_text.Length} bytes")]
internal sealed unsafe class X11Clipboard(X11DisplayHandle display, nint window, X11ClipboardAtoms atoms)
{
    /// <summary>The XEvent type of a lost selection.</summary>
    internal const int SelectionClearType = 29;

    /// <summary>The XEvent type of a request for the selection.</summary>
    internal const int SelectionRequestType = 30;

    /// <summary>The XEvent type of the answer to a request.</summary>
    private const int SelectionNotifyType = 31;

    /// <summary>The size of an XEvent union in bytes.</summary>
    private const int XEventSize = 192;

    /// <summary>The offset of the requesting window in XSelectionRequestEvent.</summary>
    private const int RequestorOffset = 40;

    /// <summary>The offset of the selection in XSelectionRequestEvent.</summary>
    private const int RequestSelectionOffset = 48;

    /// <summary>The offset of the wanted type in XSelectionRequestEvent.</summary>
    private const int RequestTargetOffset = 56;

    /// <summary>The offset of the property to fill in XSelectionRequestEvent.</summary>
    private const int RequestPropertyOffset = 64;

    /// <summary>The offset of the time in XSelectionRequestEvent.</summary>
    private const int RequestTimeOffset = 72;

    /// <summary>The offset of the requesting window in XSelectionEvent.</summary>
    private const int NotifyRequestorOffset = 32;

    /// <summary>The offset of the selection in XSelectionEvent.</summary>
    private const int NotifySelectionOffset = 40;

    /// <summary>The offset of the type in XSelectionEvent.</summary>
    private const int NotifyTargetOffset = 48;

    /// <summary>The offset of the property in XSelectionEvent.</summary>
    private const int NotifyPropertyOffset = 56;

    /// <summary>The offset of the time in XSelectionEvent.</summary>
    private const int NotifyTimeOffset = 64;

    /// <summary>The predefined atom of the type that lists atoms.</summary>
    private const nint AtomType = 4;

    /// <summary>The predefined atom of Latin-1 text.</summary>
    private const nint StringType = 31;

    /// <summary>The property format of bytes.</summary>
    private const int ByteFormat = 8;

    /// <summary>The property format of atoms, which Xlib passes as C longs.</summary>
    private const int AtomFormat = 32;

    /// <summary>The types offered to an application that asks what it can have.</summary>
    private const int OfferedTypes = 2;

    /// <summary>The size of a pollfd in bytes.</summary>
    private const int PollSize = 8;

    /// <summary>The poll event for data to read.</summary>
    private const short PollIn = 1;

    /// <summary>How many times the answer to a paste is looked for.</summary>
    private const int PasteTries = 25;

    /// <summary>How long each look waits for the server, in milliseconds. Together the tries give the owner half a second.</summary>
    private const int PasteWaitMilliseconds = 20;

    /// <summary>The most pasted text read, in 32-bit units: four megabytes.</summary>
    private const nint MaxPasteUnits = 1 << 20;

    /// <summary>The copied text as UTF-8.</summary>
    private byte[] _text = [];

    /// <summary>Whether this window owns the clipboard.</summary>
    private bool _owned;

    /// <summary>Takes the clipboard with new text.</summary>
    /// <param name="text">The text.</param>
    internal void SetText(string text)
    {
        _text = Encoding.UTF8.GetBytes(text);
        _owned = true;
        _ = NativeMethods.XSetSelectionOwner(display, (nint)atoms.Clipboard, window, 0);
        _ = NativeMethods.XFlush(display);
    }

    /// <summary>Forgets the text after another application took the clipboard.</summary>
    internal void Clear()
    {
        _text = [];
        _owned = false;
    }

    /// <summary>Reads the text on the clipboard. The owning application is asked and given a short time to answer.</summary>
    /// <returns>The text, or <see langword="null"/> when the clipboard holds no text or its owner does not answer in time.</returns>
    internal string? GetText()
    {
        if (_owned)
        {
            // The window itself owns the clipboard, so asking the server would only bring the request back here.
            return Encoding.UTF8.GetString(_text);
        }

        _ = NativeMethods.XConvertSelection(display, (nint)atoms.Clipboard, (nint)atoms.Utf8String, (nint)atoms.Paste, window, 0);
        _ = NativeMethods.XFlush(display);
        byte* notify = stackalloc byte[XEventSize];
        byte* wait = stackalloc byte[PollSize];
        *(int*)wait = NativeMethods.XConnectionNumber(display);
        *(short*)(wait + sizeof(int)) = PollIn;
        for (var tries = 0; tries < PasteTries; tries++)
        {
            if (NativeMethods.XCheckTypedWindowEvent(display, window, SelectionNotifyType, notify) != 0)
            {
                return *(nint*)(notify + NotifyPropertyOffset) == 0 ? null : ReadPasted();
            }

            // Only the answer is taken from the queue, so key and pointer events that arrive meanwhile wait for the next pump.
            _ = Wayland.NativeMethods.Poll(wait, 1, PasteWaitMilliseconds);
        }

        return null;
    }

    /// <summary>Answers an application that asked for the clipboard.</summary>
    /// <param name="request">The XSelectionRequestEvent.</param>
    internal void Answer(byte* request)
    {
        var requestor = *(nint*)(request + RequestorOffset);
        var target = *(nint*)(request + RequestTargetOffset);
        var property = *(nint*)(request + RequestPropertyOffset);
        var filled = property != 0 && Fill(requestor, target, property);
        byte* notify = stackalloc byte[XEventSize];
        new Span<byte>(notify, XEventSize).Clear();
        *(int*)notify = SelectionNotifyType;
        *(nint*)(notify + NotifyRequestorOffset) = requestor;
        *(nint*)(notify + NotifySelectionOffset) = *(nint*)(request + RequestSelectionOffset);
        *(nint*)(notify + NotifyTargetOffset) = target;

        // A property of none tells the application its request was refused.
        *(nint*)(notify + NotifyPropertyOffset) = filled ? property : 0;
        *(nint*)(notify + NotifyTimeOffset) = *(nint*)(request + RequestTimeOffset);
        _ = NativeMethods.XSendEvent(display, requestor, 0, 0, notify);
        _ = NativeMethods.XFlush(display);
    }

    /// <summary>Writes what was asked for into the property the application named.</summary>
    /// <param name="requestor">The asking window.</param>
    /// <param name="target">The type asked for.</param>
    /// <param name="property">The property to fill.</param>
    /// <returns><see langword="true"/> when the type is one the clipboard offers.</returns>
    private bool Fill(nint requestor, nint target, nint property)
    {
        if (target == (nint)atoms.Targets)
        {
            nint* offered = stackalloc nint[OfferedTypes] { (nint)atoms.Targets, (nint)atoms.Utf8String };
            _ = NativeMethods.XChangeProperty(display, requestor, property, AtomType, AtomFormat, 0, (byte*)offered, OfferedTypes);
            return true;
        }

        if (target != (nint)atoms.Utf8String && target != StringType)
        {
            return false;
        }

        fixed (byte* text = _text)
        {
            _ = NativeMethods.XChangeProperty(display, requestor, property, target, ByteFormat, 0, text, _text.Length);
        }

        return true;
    }

    /// <summary>Reads and removes the property another application wrote the pasted text into.</summary>
    /// <returns>The text, or <see langword="null"/> when the property is not bytes of text.</returns>
    private string? ReadPasted()
    {
        nint type;
        int format;
        nuint count;
        nuint remaining;
        byte* data;
        if (NativeMethods.XGetWindowProperty(display, window, (nint)atoms.Paste, 0, MaxPasteUnits, 1, 0, &type, &format, &count, &remaining, &data) != 0 || data is null)
        {
            return null;
        }

        var text = format == ByteFormat ? Encoding.UTF8.GetString(data, (int)count) : null;
        _ = NativeMethods.XFree(data);
        return text;
    }
}
