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

    /// <summary>The newest wl_data_offer the compositor described, which the window never reads.</summary>
    private uint _offer;

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
            if (opcode == WaylandProtocol.DataDeviceDataOffer)
            {
                KeepOffer(reader.ReadUInt());
            }

            return true;
        }

        if (_sources.Contains(objectId))
        {
            HandleSource(objectId, opcode);
            return true;
        }

        return objectId == _offer && _offer != 0;
    }

    /// <summary>Remembers the newest offer and lets go of the one before it, so offers do not pile up.</summary>
    /// <param name="offer">The new wl_data_offer.</param>
    private void KeepOffer(uint offer)
    {
        if (_offer != 0)
        {
            connection.Writer.Begin(_offer, WaylandProtocol.DataOfferDestroy);
            connection.Writer.End();
            connection.Flush();
        }

        _offer = offer;
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
