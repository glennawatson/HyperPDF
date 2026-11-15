// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace HyperPdf.Head.Linux.X11;

/// <summary>Takes files dragged from a file manager and dropped on the window, with the drag and drop protocol (XDND) version 5.</summary>
internal static unsafe class X11Drops
{
    /// <summary>The event type of a client message.</summary>
    internal const int ClientMessageType = 33;

    /// <summary>The event type that says a selection was converted.</summary>
    internal const int SelectionNotifyType = 31;

    /// <summary>The size of an X event in bytes.</summary>
    private const int XEventSize = 192;

    /// <summary>The offset of the window in a client message.</summary>
    private const int WindowOffset = 32;

    /// <summary>The offset of the message type in a client message.</summary>
    private const int MessageTypeOffset = 40;

    /// <summary>The offset of the format in a client message.</summary>
    private const int FormatOffset = 48;

    /// <summary>The offset of the first value in a client message.</summary>
    private const int DataOffset = 56;

    /// <summary>The offset of the selection in a selection notify event.</summary>
    private const int SelectionOffset = 40;

    /// <summary>The offset of the property in a selection notify event.</summary>
    private const int PropertyOffset = 56;

    /// <summary>The number of bits in each value of a client message.</summary>
    private const int ThirtyTwoBits = 32;

    /// <summary>The version of the protocol the window speaks.</summary>
    private const int ProtocolVersion = 5;

    /// <summary>The atom type that holds atoms, for the property that says the window takes drops.</summary>
    private const nint AtomType = 4;

    /// <summary>The mode that replaces a property.</summary>
    private const int Replace = 0;

    /// <summary>How many units of a dropped list are read at most.</summary>
    private const nint MaxListUnits = 1 << 16;

    /// <summary>The shift that finds the version in the flags of an enter message.</summary>
    private const int VersionShift = 24;

    /// <summary>The flag of an enter message that says the drag offers more than three types.</summary>
    private const long MoreTypes = 1;

    /// <summary>The flag of a status or finished message that says the window takes the drop.</summary>
    private const long WillAccept = 1;

    /// <summary>The scheme of a dropped file's address.</summary>
    private const string FileScheme = "file://";

    /// <summary>The place of the source window in the values of a message.</summary>
    private const int SourceValue = 0;

    /// <summary>The place of the flags in the values of an enter message.</summary>
    private const int FlagsValue = 1;

    /// <summary>The place of the first offered type in the values of an enter message.</summary>
    private const int FirstTypeValue = 2;

    /// <summary>The place of the time in the values of a drop message.</summary>
    private const int TimeValue = 2;

    /// <summary>The place of the third and last offered type in the values of an enter message.</summary>
    private const int LastTypeValue = 4;

    /// <summary>Tells the programs that the window takes dropped files.</summary>
    /// <param name="display">The display.</param>
    /// <param name="window">The window.</param>
    /// <param name="atoms">The atoms of the protocol.</param>
    internal static void Declare(nint display, nint window, in X11DropAtoms atoms)
    {
        int* version = stackalloc int[] { ProtocolVersion };
        _ = NativeMethods.XChangeProperty(display, window, (nint)atoms.Aware, AtomType, ThirtyTwoBits, Replace, (byte*)version, 1);
    }

    /// <summary>Takes a message or a conversion that belongs to a drop, and ignores any other.</summary>
    /// <param name="display">The display.</param>
    /// <param name="window">The window.</param>
    /// <param name="atoms">The atoms of the protocol.</param>
    /// <param name="offer">The drag that is over the window, which is brought up to date.</param>
    /// <param name="raw">The X event bytes.</param>
    /// <param name="files">Receives the files when a drop is complete.</param>
    /// <returns><see langword="true"/> when the event belonged to a drop.</returns>
    internal static bool Handle(nint display, nint window, in X11DropAtoms atoms, ref X11DropOffer offer, byte* raw, out string[] files)
    {
        files = [];
        var type = *(int*)raw;
        if (type == SelectionNotifyType)
        {
            if (*(long*)(raw + SelectionOffset) != atoms.Selection)
            {
                return false;
            }

            files = Receive(display, window, atoms, offer, *(long*)(raw + PropertyOffset));
            offer = X11DropOffer.None;
            return true;
        }

        if (type != ClientMessageType)
        {
            return false;
        }

        var message = *(long*)(raw + MessageTypeOffset);
        var data = (long*)(raw + DataOffset);
        if (message == atoms.Enter)
        {
            offer = new(data[SourceValue], OffersFiles(display, atoms, data));
        }
        else if (message == atoms.Position)
        {
            Reply(display, data[SourceValue], new(atoms.Status, window, offer.Accepts ? WillAccept : 0, offer.Accepts ? atoms.CopyAction : 0));
        }
        else if (message == atoms.Leave)
        {
            offer = X11DropOffer.None;
        }
        else if (message == atoms.Drop)
        {
            offer = Drop(display, window, atoms, offer, data[TimeValue]);
        }
        else
        {
            return false;
        }

        return true;
    }

    /// <summary>Tells whether a drag offers files, from the types the enter message lists and, for a long list, from the source window.</summary>
    /// <param name="display">The display.</param>
    /// <param name="atoms">The atoms of the protocol.</param>
    /// <param name="data">The values of the enter message.</param>
    /// <returns><see langword="true"/> when the drag offers a list of addresses.</returns>
    private static bool OffersFiles(nint display, in X11DropAtoms atoms, long* data)
    {
        for (var i = FirstTypeValue; i <= LastTypeValue; i++)
        {
            if (data[i] == atoms.UriList)
            {
                return true;
            }
        }

        if ((data[FlagsValue] & MoreTypes) == 0 || (data[FlagsValue] >> VersionShift) < ProtocolVersion - 1)
        {
            return false;
        }

        nint type;
        int format;
        nuint count;
        nuint remaining;
        byte* list;
        if (NativeMethods.XGetWindowProperty(display, (nint)data[SourceValue], (nint)atoms.TypeList, 0, MaxListUnits, 0, AtomType, &type, &format, &count, &remaining, &list) != 0 || list is null)
        {
            return false;
        }

        var found = false;
        for (nuint i = 0; i < count && !found; i++)
        {
            found = ((long*)list)[i] == atoms.UriList;
        }

        _ = NativeMethods.XFree(list);
        return found;
    }

    /// <summary>Handles the drop: asks for the files when the drag offers them, and finishes the drag when it does not.</summary>
    /// <param name="display">The display.</param>
    /// <param name="window">The window.</param>
    /// <param name="atoms">The atoms of the protocol.</param>
    /// <param name="offer">The drag over the window.</param>
    /// <param name="time">The time of the drop.</param>
    /// <returns>The drag after the drop.</returns>
    private static X11DropOffer Drop(nint display, nint window, in X11DropAtoms atoms, X11DropOffer offer, long time)
    {
        if (!offer.Accepts)
        {
            Reply(display, offer.Source, new(atoms.Finished, window, 0, 0));
            return X11DropOffer.None;
        }

        _ = NativeMethods.XConvertSelection(display, (nint)atoms.Selection, (nint)atoms.UriList, (nint)atoms.Data, window, (nint)time);
        return offer;
    }

    /// <summary>Reads the files that arrived, and tells the source the drop is done.</summary>
    /// <param name="display">The display.</param>
    /// <param name="window">The window.</param>
    /// <param name="atoms">The atoms of the protocol.</param>
    /// <param name="offer">The drag that was asked for its files.</param>
    /// <param name="property">The property the files arrived in, or 0 when the source refused.</param>
    /// <returns>The paths of the files.</returns>
    private static string[] Receive(nint display, nint window, in X11DropAtoms atoms, X11DropOffer offer, long property)
    {
        var paths = property == 0 ? [] : ReadPaths(display, window, (nint)property);
        var took = paths.Length > 0;
        Reply(display, offer.Source, new(atoms.Finished, window, took ? WillAccept : 0, took ? atoms.CopyAction : 0));
        return paths;
    }

    /// <summary>Reads the list of addresses in a property, and turns each into a path.</summary>
    /// <param name="display">The display.</param>
    /// <param name="window">The window.</param>
    /// <param name="property">The property.</param>
    /// <returns>The paths of the files in the list.</returns>
    private static string[] ReadPaths(nint display, nint window, nint property)
    {
        nint type;
        int format;
        nuint count;
        nuint remaining;
        byte* data;
        if (NativeMethods.XGetWindowProperty(display, window, property, 0, MaxListUnits, 1, 0, &type, &format, &count, &remaining, &data) != 0 || data is null)
        {
            return [];
        }

        var text = Encoding.UTF8.GetString(data, (int)count);
        _ = NativeMethods.XFree(data);
        return FileUris.PathsOf(text);
    }

    /// <summary>Sends a reply to the source of a drag.</summary>
    /// <param name="display">The display.</param>
    /// <param name="target">The window the reply is for, which is the source of the drag.</param>
    /// <param name="reply">What the reply says.</param>
    private static void Reply(nint display, long target, in X11DropReply reply)
    {
        byte* message = stackalloc byte[XEventSize];
        new Span<byte>(message, XEventSize).Clear();
        *(int*)message = ClientMessageType;
        *(long*)(message + WindowOffset) = target;
        *(long*)(message + MessageTypeOffset) = reply.Type;
        *(int*)(message + FormatOffset) = ThirtyTwoBits;
        var values = (long*)(message + DataOffset);
        values[0] = reply.Sender;
        values[1] = reply.Flags;

        // The place of the action in a status message is the last of its five values.
        values[LastTypeValue] = reply.Action;
        _ = NativeMethods.XSendEvent(display, (nint)target, 0, 0, message);
        _ = NativeMethods.XFlush(display);
    }
}
