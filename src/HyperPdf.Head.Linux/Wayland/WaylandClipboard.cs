// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;

namespace HyperPdf.Head.Linux.Wayland;

/// <summary>
/// The window's side of the Wayland clipboard. The window offers copied text as a data source, and writes it into a pipe each
/// time the compositor says an application wants it.
/// </summary>
/// <param name="connection">The compositor connection.</param>
[DebuggerDisplay("WaylandClipboard: {_text.Length} bytes")]
internal sealed unsafe class WaylandClipboard(WaylandConnection connection)
{
    /// <summary>The ends of a pipe.</summary>
    private const int PipeEnds = 2;

    /// <summary>The O_CLOEXEC flag, so a program the viewer starts does not inherit the pipe.</summary>
    private const int CloseOnExec = 0x80000;

    /// <summary>The size of a pollfd in bytes.</summary>
    private const int PollSize = 8;

    /// <summary>The poll event for data to read.</summary>
    private const short PollIn = 1;

    /// <summary>How long the owner of the clipboard has to write each piece of text, in milliseconds.</summary>
    private const int PasteWaitMilliseconds = 500;

    /// <summary>The bytes read from the pipe at a time.</summary>
    private const int ChunkSize = 4096;

    /// <summary>The most pasted text read: four megabytes.</summary>
    private const int MaxPasteBytes = 4 << 20;

    /// <summary>The kinds of text offered, most exact first.</summary>
    private static readonly string[] TextTypes = ["text/plain;charset=utf-8", "UTF8_STRING", "text/plain"];

    /// <summary>The data sources that are still alive. A source stays until the compositor cancels it, so every event for it is read in step.</summary>
    private readonly List<uint> _sources = [];

    /// <summary>The copied text as UTF-8.</summary>
    private byte[] _text = [];

    /// <summary>The wl_data_device_manager, or 0 when the compositor has none.</summary>
    private uint _manager;

    /// <summary>The wl_data_device of the seat.</summary>
    private uint _device;

    /// <summary>The wl_data_offer the compositor is describing, before it says what the offer is for.</summary>
    private uint _described;

    /// <summary>The wl_data_offer that is the clipboard now, or 0 when the clipboard is empty.</summary>
    private uint _selection;

    /// <summary>Whether the offer being described has text.</summary>
    private bool _describedHasText;

    /// <summary>Whether the clipboard has text.</summary>
    private bool _selectionHasText;

    /// <summary>Starts the clipboard for a seat.</summary>
    /// <param name="manager">The bound wl_data_device_manager.</param>
    /// <param name="seat">The bound wl_seat.</param>
    internal void Attach(uint manager, uint seat)
    {
        _manager = manager;
        _device = connection.NewId();
        var writer = connection.Writer;
        writer.Begin(manager, WaylandProtocol.ManagerGetDataDevice);
        writer.WriteUInt(_device);
        writer.WriteUInt(seat);
        writer.End();
        connection.Flush();
    }

    /// <summary>Offers text as the clipboard.</summary>
    /// <param name="text">The text.</param>
    /// <param name="serial">The serial of the input event that caused the copy, which the compositor checks.</param>
    internal void SetText(string text, uint serial)
    {
        if (_device == 0)
        {
            return;
        }

        _text = Encoding.UTF8.GetBytes(text);
        var source = connection.NewId();
        _sources.Add(source);
        var writer = connection.Writer;
        writer.Begin(_manager, WaylandProtocol.ManagerCreateDataSource);
        writer.WriteUInt(source);
        writer.End();
        foreach (var type in TextTypes)
        {
            writer.Begin(source, WaylandProtocol.DataSourceOffer);
            writer.WriteString(type);
            writer.End();
        }

        writer.Begin(_device, WaylandProtocol.DataDeviceSetSelection);
        writer.WriteUInt(source);
        writer.WriteUInt(serial);
        writer.End();
        connection.Flush();
    }

    /// <summary>Handles an event when it is for one of the clipboard's objects.</summary>
    /// <param name="objectId">The object the event is for.</param>
    /// <param name="opcode">The event.</param>
    /// <param name="reader">The event arguments.</param>
    /// <returns><see langword="true"/> when the event was the clipboard's.</returns>
    internal bool TryHandle(uint objectId, int opcode, ref WaylandMessageReader reader)
    {
        if (objectId == _device && _device != 0)
        {
            HandleDevice(opcode, ref reader);
            return true;
        }

        if (_sources.Contains(objectId))
        {
            HandleSource(objectId, opcode);
            return true;
        }

        if (objectId == _described && _described != 0)
        {
            // Each offer event names one kind of data. Only text can be pasted.
            _describedHasText |= opcode == WaylandProtocol.DataOfferOffer && string.Equals(reader.ReadString(), TextTypes[0], StringComparison.Ordinal);
            return true;
        }

        return objectId == _selection && _selection != 0;
    }

    /// <summary>Reads the text on the clipboard. The owning application is given a short time to write it.</summary>
    /// <returns>The text, or <see langword="null"/> when the clipboard holds no text or its owner does not answer in time.</returns>
    internal string? GetText()
    {
        if (_sources.Count > 0)
        {
            // The window itself owns the clipboard, and it cannot answer its own request while it waits for the answer.
            return Encoding.UTF8.GetString(_text);
        }

        int* ends = stackalloc int[PipeEnds];
        if (_selection == 0 || !_selectionHasText || NativeMethods.Pipe(ends, CloseOnExec) != 0)
        {
            return null;
        }

        var writer = connection.Writer;
        writer.Begin(_selection, WaylandProtocol.DataOfferReceive);
        writer.WriteString(TextTypes[0]);
        writer.End();
        connection.FlushWithDescriptor(ends[1]);

        // The compositor hands the write end to the owner. This copy is closed so the read ends when the owner has written.
        _ = NativeMethods.Close(ends[1]);
        var text = ReadPipe(ends[0]);
        _ = NativeMethods.Close(ends[0]);
        return text;
    }

    /// <summary>Reads a pipe to its end, giving up when nothing arrives for a while.</summary>
    /// <param name="descriptor">The read end.</param>
    /// <returns>The text read, or <see langword="null"/> when none arrived.</returns>
    private static string? ReadPipe(int descriptor)
    {
        byte* wait = stackalloc byte[PollSize];
        *(int*)wait = descriptor;
        *(short*)(wait + sizeof(int)) = PollIn;
        byte* chunk = stackalloc byte[ChunkSize];
        using var all = new MemoryStream();
        while (all.Length < MaxPasteBytes && NativeMethods.Poll(wait, 1, PasteWaitMilliseconds) > 0)
        {
            var count = NativeMethods.Read(descriptor, chunk, ChunkSize);
            if (count <= 0)
            {
                break;
            }

            all.Write(new(chunk, (int)count));
        }

        return all.Length == 0 ? null : Encoding.UTF8.GetString(all.GetBuffer(), 0, (int)all.Length);
    }

    /// <summary>Handles an event of the data device: a new offer being described, or the offer that is now the clipboard.</summary>
    /// <param name="opcode">The event.</param>
    /// <param name="reader">The event arguments.</param>
    private void HandleDevice(int opcode, ref WaylandMessageReader reader)
    {
        if (opcode == WaylandProtocol.DataDeviceDataOffer)
        {
            // An offer that was described and never became the clipboard, as a drag is, is let go.
            Destroy(_described == _selection ? 0 : _described);
            _described = reader.ReadUInt();
            _describedHasText = false;
            return;
        }

        if (opcode != WaylandProtocol.DataDeviceSelection)
        {
            return;
        }

        var selection = reader.ReadUInt();
        Destroy(_selection == selection ? 0 : _selection);
        _selection = selection;
        _selectionHasText = selection != 0 && selection == _described && _describedHasText;
        _described = 0;
    }

    /// <summary>Lets go of an offer, so offers do not pile up.</summary>
    /// <param name="offer">The wl_data_offer, or 0 for none.</param>
    private void Destroy(uint offer)
    {
        if (offer == 0)
        {
            return;
        }

        connection.Writer.Begin(offer, WaylandProtocol.DataOfferDestroy);
        connection.Writer.End();
        connection.Flush();
    }

    /// <summary>Handles an event of a data source: a request for the text, or the end of the source.</summary>
    /// <param name="source">The wl_data_source.</param>
    /// <param name="opcode">The event.</param>
    private void HandleSource(uint source, int opcode)
    {
        if (opcode == WaylandProtocol.DataSourceSend && connection.TryTakeDescriptor(out var descriptor))
        {
            WriteText(descriptor);
            return;
        }

        if (opcode != WaylandProtocol.DataSourceCancelled)
        {
            return;
        }

        _ = _sources.Remove(source);
        connection.Writer.Begin(source, WaylandProtocol.DataSourceDestroy);
        connection.Writer.End();
        connection.Flush();
    }

    /// <summary>Writes the text into the pipe an application reads it from, then closes the pipe.</summary>
    /// <param name="descriptor">The write end of the pipe.</param>
    private void WriteText(int descriptor)
    {
        fixed (byte* text = _text)
        {
            nint count = 0;
            for (var written = 0; written < _text.Length; written += (int)count)
            {
                count = NativeMethods.Write(descriptor, text + written, (nuint)(_text.Length - written));
                if (count <= 0)
                {
                    // The application stopped reading, so the rest has nowhere to go.
                    break;
                }
            }
        }

        _ = NativeMethods.Close(descriptor);
    }
}
