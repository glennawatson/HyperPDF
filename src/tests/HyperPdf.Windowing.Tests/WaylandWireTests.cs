// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Head.Linux.Wayland;

namespace HyperPdf.Windowing.Tests;

/// <summary>Checks the Wayland wire encoding against the layout the protocol defines.</summary>
public sealed class WaylandWireTests
{
    /// <summary>The wl_display object id.</summary>
    private const uint Display = 1;

    /// <summary>The wl_display.get_registry opcode.</summary>
    private const int GetRegistry = 1;

    /// <summary>The new registry object id.</summary>
    private const uint Registry = 2;

    /// <summary>The size of a message with one word argument.</summary>
    private const int OneWordMessage = 12;

    /// <summary>The Wayland word size in bytes.</summary>
    private const int Word = 4;

    /// <summary>A negative number used to check signed arguments.</summary>
    private const int Negative = -7;

    /// <summary>The bytes the protocol defines for get_registry with the new id 2: object 1, opcode 1, size 12, then the id.</summary>
    private static readonly byte[] GetRegistryBytes = [1, 0, 0, 0, 1, 0, OneWordMessage, 0, (byte)Registry, 0, 0, 0];

    /// <summary>A request with one new id is twelve bytes with the size in the head.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RequestWithOneArgumentHasTheProtocolLayout()
    {
        var writer = new WaylandMessageWriter();
        writer.Begin(Display, GetRegistry);
        writer.WriteUInt(Registry);
        writer.End();

        var bytes = writer.Written.ToArray();

        await Assert.That(bytes.Length).IsEqualTo(OneWordMessage);
        await Assert.That(bytes).IsEquivalentTo(GetRegistryBytes);
    }

    /// <summary>A string is padded to a whole word and reads back unchanged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StringRoundTripsWithPadding()
    {
        var writer = new WaylandMessageWriter();
        writer.Begin(Registry, 0);
        writer.WriteString("xdg_wm_base");
        writer.WriteInt(Negative);
        writer.End();

        var bytes = writer.Written.ToArray();
        var found = WaylandMessageReader.TryReadHeader(bytes, out var header);
        var reader = new WaylandMessageReader(header, bytes.AsSpan(WaylandHeader.Length));
        var text = reader.ReadString();
        var number = reader.ReadInt();

        await Assert.That(found).IsTrue();
        await Assert.That(header.Size).IsEqualTo(bytes.Length);
        await Assert.That(bytes.Length % Word).IsEqualTo(0);
        await Assert.That(text).IsEqualTo("xdg_wm_base");
        await Assert.That(number).IsEqualTo(Negative);
    }

    /// <summary>A half received message is not read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PartialMessageIsNotRead()
    {
        var writer = new WaylandMessageWriter();
        writer.Begin(Display, GetRegistry);
        writer.WriteUInt(Registry);
        writer.End();

        var found = WaylandMessageReader.TryReadHeader(writer.Written[..^1], out _);

        await Assert.That(found).IsFalse();
    }
}
