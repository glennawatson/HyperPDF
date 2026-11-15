// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;
using HyperPdf.Head.Linux.X11;
using LinuxNative = HyperPdf.Head.Linux.X11.NativeMethods;

namespace HyperPdf.Windowing.Tests;

/// <summary>Plays the file manager of a drag: offers files to a window over the drag and drop protocol, drops them, and hands them over when the window asks.</summary>
internal static unsafe class X11DragSource
{
    /// <summary>The size of an X event in bytes.</summary>
    private const int XEventSize = 192;

    /// <summary>The event type of a selection request.</summary>
    private const int SelectionRequestType = 30;

    /// <summary>The event type that says a selection was converted.</summary>
    private const int SelectionNotifyType = 31;

    /// <summary>The event type of a client message.</summary>
    private const int ClientMessageType = 33;

    /// <summary>The offset of the window in a client message.</summary>
    private const int WindowOffset = 32;

    /// <summary>The offset of the message type in a client message.</summary>
    private const int MessageTypeOffset = 40;

    /// <summary>The offset of the format in a client message.</summary>
    private const int FormatOffset = 48;

    /// <summary>The offset of the first value in a client message.</summary>
    private const int DataOffset = 56;

    /// <summary>The offset of the requestor in a selection request.</summary>
    private const int RequestorOffset = 40;

    /// <summary>The offset of the selection in a selection request.</summary>
    private const int RequestSelectionOffset = 48;

    /// <summary>The offset of the target in a selection request.</summary>
    private const int RequestTargetOffset = 56;

    /// <summary>The offset of the property in a selection request.</summary>
    private const int RequestPropertyOffset = 64;

    /// <summary>The offset of the requestor in a selection notify event.</summary>
    private const int NotifyRequestorOffset = 32;

    /// <summary>The offset of the selection in a selection notify event.</summary>
    private const int NotifySelectionOffset = 40;

    /// <summary>The offset of the target in a selection notify event.</summary>
    private const int NotifyTargetOffset = 48;

    /// <summary>The offset of the property in a selection notify event.</summary>
    private const int NotifyPropertyOffset = 56;

    /// <summary>The number of bits in each value of a client message.</summary>
    private const int ThirtyTwoBits = 32;

    /// <summary>The number of bits in each byte of text.</summary>
    private const int EightBits = 8;

    /// <summary>The version of the drag and drop protocol, shifted to where an enter message keeps it.</summary>
    private const long VersionFlags = 5L << 24;

    /// <summary>The wait for one turn of the window, in milliseconds.</summary>
    private const int TurnMilliseconds = 20;

    /// <summary>The turns the source stays after the window has the files.</summary>
    private const int SettleTurns = 5;

    /// <summary>The longest the drag is played for, in seconds.</summary>
    private const double TimeoutSeconds = 20;

    /// <summary>Gets the addresses of the dropped files, as a file manager lists them: percent encoded, one on each line.</summary>
    private static ReadOnlySpan<byte> UriList => "file:///tmp/hyperpdf%20drop/first.pdf\r\nfile:///tmp/hyperpdf%20drop/second.pdf\r\n"u8;

    /// <summary>Offers files to a window, drops them, and plays the source until the window has them.</summary>
    /// <param name="name">The display name of the X server.</param>
    /// <param name="window">The window the files are dropped on.</param>
    /// <param name="done">Tells whether the window has the files.</param>
    internal static void Drag(string name, X11Window window, Func<bool> done)
    {
        var bytes = Encoding.ASCII.GetBytes($"{name}\0");
        nint display;
        fixed (byte* text = bytes)
        {
            display = LinuxNative.XOpenDisplay(text);
        }

        var source = LinuxNative.XCreateSimpleWindow(display, LinuxNative.XRootWindow(display, 0), 0, 0, 1, 1, 0, 0, 0);
        var selection = Atom(display, "XdndSelection"u8);
        var uriList = Atom(display, "text/uri-list"u8);
        _ = LinuxNative.XSetSelectionOwner(display, selection, source, 0);
        var target = (nint)window.NativeHandle;
        Send(display, target, new(Atom(display, "XdndEnter"u8), source, VersionFlags, uriList, 0, 0));
        Send(display, target, new(Atom(display, "XdndPosition"u8), source, 0, 1, 0, Atom(display, "XdndActionCopy"u8)));
        Send(display, target, new(Atom(display, "XdndDrop"u8), source, 0, 0, 0, 0));
        _ = LinuxNative.XFlush(display);
        var started = Stopwatch.GetTimestamp();
        var buffer = new byte[XEventSize];
        while (!done() && Stopwatch.GetElapsedTime(started).TotalSeconds < TimeoutSeconds)
        {
            window.PumpEvents(TurnMilliseconds);
            Serve(display, buffer, uriList);
        }

        // The window tells the source it is done with the drop, so the source stays until that is sent.
        for (var turn = 0; turn < SettleTurns; turn++)
        {
            window.PumpEvents(TurnMilliseconds);
            Serve(display, buffer, uriList);
        }

        _ = LinuxNative.XCloseDisplay(display);
    }

    /// <summary>Gets an atom by name.</summary>
    /// <param name="display">The display.</param>
    /// <param name="name">The name in ASCII, without a terminator.</param>
    /// <returns>The atom.</returns>
    private static nint Atom(nint display, ReadOnlySpan<byte> name)
    {
        var terminated = new byte[name.Length + 1];
        name.CopyTo(terminated);
        fixed (byte* pointer = terminated)
        {
            return LinuxNative.XInternAtom(display, pointer, 0);
        }
    }

    /// <summary>Answers every selection request that waits for the source.</summary>
    /// <param name="display">The source's connection.</param>
    /// <param name="buffer">Room for an event.</param>
    /// <param name="uriList">The atom of the list type.</param>
    private static void Serve(nint display, byte[] buffer, nint uriList)
    {
        fixed (byte* event_ = buffer)
        {
            while (LinuxNative.XPending(display) > 0)
            {
                _ = LinuxNative.XNextEvent(display, event_);
                if (*(int*)event_ == SelectionRequestType)
                {
                    Answer(display, event_, uriList);
                }
            }
        }
    }

    /// <summary>Sends a client message of five values.</summary>
    /// <param name="display">The display.</param>
    /// <param name="target">The window the message is for.</param>
    /// <param name="message">What the message says.</param>
    private static void Send(nint display, nint target, in DragMessage message)
    {
        var bytes = new byte[XEventSize];
        fixed (byte* raw = bytes)
        {
            *(int*)raw = ClientMessageType;
            *(nint*)(raw + WindowOffset) = target;
            *(nint*)(raw + MessageTypeOffset) = (nint)message.Type;
            *(int*)(raw + FormatOffset) = ThirtyTwoBits;
            var values = (long*)(raw + DataOffset);
            values[0] = message.First;
            values[1] = message.Second;
            values[2] = message.Third;
            values[3] = message.Fourth;
            values[4] = message.Fifth;
            _ = LinuxNative.XSendEvent(display, target, 0, 0, raw);
        }
    }

    /// <summary>Answers the window's request for the dropped files: puts the list on the window's property and says it is there.</summary>
    /// <param name="display">The display.</param>
    /// <param name="request">The selection request event.</param>
    /// <param name="uriList">The atom of the list type.</param>
    private static void Answer(nint display, byte* request, nint uriList)
    {
        var requestor = *(nint*)(request + RequestorOffset);
        var property = *(nint*)(request + RequestPropertyOffset);
        fixed (byte* data = UriList)
        {
            _ = LinuxNative.XChangeProperty(display, requestor, property, uriList, EightBits, 0, data, UriList.Length);
        }

        var bytes = new byte[XEventSize];
        fixed (byte* notify = bytes)
        {
            *(int*)notify = SelectionNotifyType;
            *(nint*)(notify + NotifyRequestorOffset) = requestor;
            *(nint*)(notify + NotifySelectionOffset) = *(nint*)(request + RequestSelectionOffset);
            *(nint*)(notify + NotifyTargetOffset) = *(nint*)(request + RequestTargetOffset);
            *(nint*)(notify + NotifyPropertyOffset) = property;
            _ = LinuxNative.XSendEvent(display, requestor, 0, 0, notify);
        }

        _ = LinuxNative.XFlush(display);
    }

    /// <summary>A client message of five values.</summary>
    /// <param name="Type">The atom of the message.</param>
    /// <param name="First">The first value.</param>
    /// <param name="Second">The second value.</param>
    /// <param name="Third">The third value.</param>
    /// <param name="Fourth">The fourth value.</param>
    /// <param name="Fifth">The fifth value.</param>
    private readonly record struct DragMessage(long Type, long First, long Second, long Third, long Fourth, long Fifth);
}
