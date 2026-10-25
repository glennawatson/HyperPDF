// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Tmds.DBus.Protocol;

namespace HyperPdf.Platform.Linux.Accessibility.AtSpi;

/// <summary>Sends AT-SPI event signals on the accessibility bus, which is how a screen reader hears that something changed.</summary>
[DebuggerDisplay("AtSpiEmitter: {_session.BusName}")]
internal sealed class AtSpiEmitter
{
    /// <summary>The connection the signals are sent on.</summary>
    private readonly DBusConnection _connection;

    /// <summary>The server the signals come from.</summary>
    private readonly AtSpiSession _session;

    /// <summary>Initializes a new instance of the <see cref="AtSpiEmitter"/> class.</summary>
    /// <param name="connection">The connection the signals are sent on.</param>
    /// <param name="session">The server the signals come from.</param>
    internal AtSpiEmitter(DBusConnection connection, AtSpiSession session)
    {
        _connection = connection;
        _session = session;
    }

    /// <summary>The kind of value an event carries after its two numbers.</summary>
    private enum Payload
    {
        /// <summary>A 32 bit number.</summary>
        Number = 0,

        /// <summary>A string.</summary>
        Text = 1,

        /// <summary>A reference to an element.</summary>
        Reference = 2,
    }

    /// <summary>Tells screen readers a state of an element changed.</summary>
    /// <param name="id">The element.</param>
    /// <param name="state">The state's name, such as "checked".</param>
    /// <param name="on">Whether the state is now set.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void StateChanged(int id, string state, bool on) =>
        Emit(id, AtSpiNames.ObjectEventInterface, AtSpiNames.StateChangedSignal, new(state, on ? 1 : 0, 0, Payload.Number, 0, string.Empty));

    /// <summary>Tells screen readers a property of an element changed.</summary>
    /// <param name="id">The element.</param>
    /// <param name="property">The property, such as "accessible-name".</param>
    /// <param name="value">The new value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void PropertyChanged(int id, string property, string value) =>
        Emit(id, AtSpiNames.ObjectEventInterface, AtSpiNames.PropertyChangeSignal, new(property, 0, 0, Payload.Text, 0, value));

    /// <summary>Tells screen readers a child was added to or removed from an element.</summary>
    /// <param name="parentId">The parent.</param>
    /// <param name="added">Whether the child was added; otherwise it was removed.</param>
    /// <param name="index">The child's index among the parent's children.</param>
    /// <param name="childId">The child.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ChildrenChanged(int parentId, bool added, int index, int childId) =>
        Emit(parentId, AtSpiNames.ObjectEventInterface, AtSpiNames.ChildrenChangedSignal, new(added ? "add" : "remove", index, 0, Payload.Reference, childId, string.Empty));

    /// <summary>Tells screen readers the caret moved.</summary>
    /// <param name="id">The element.</param>
    /// <param name="offset">The new offset.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void CaretMoved(int id, int offset) =>
        Emit(id, AtSpiNames.ObjectEventInterface, AtSpiNames.TextCaretMovedSignal, new(string.Empty, offset, 0, Payload.Number, 0, string.Empty));

    /// <summary>Tells screen readers text was inserted or deleted.</summary>
    /// <param name="id">The element.</param>
    /// <param name="inserted">Whether text was inserted; otherwise it was deleted.</param>
    /// <param name="offset">Where the change starts.</param>
    /// <param name="text">The text that was inserted or deleted.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void TextChanged(int id, bool inserted, int offset, string text) =>
        Emit(id, AtSpiNames.ObjectEventInterface, AtSpiNames.TextChangedSignal, new(inserted ? "insert" : "delete", offset, text.Length, Payload.Text, 0, text));

    /// <summary>Tells screen readers a window became active or stopped being active.</summary>
    /// <param name="id">The window.</param>
    /// <param name="active">Whether it is now active.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WindowActivity(int id, bool active) =>
        Emit(id, AtSpiNames.WindowEventInterface, active ? AtSpiNames.ActivateSignal : AtSpiNames.DeactivateSignal, new(string.Empty, 0, 0, Payload.Number, 0, string.Empty));

    /// <summary>Tells screen readers the keyboard focus moved to an element, in the old and the new style of event.</summary>
    /// <param name="id">The element.</param>
    internal void FocusMoved(int id)
    {
        StateChanged(id, "focused", true);
        Emit(id, AtSpiNames.FocusEventInterface, AtSpiNames.FocusSignal, new(string.Empty, 0, 0, Payload.Number, 0, string.Empty));
    }

    /// <summary>Sends one event signal.</summary>
    /// <param name="id">The element the event is about.</param>
    /// <param name="interfaceName">The signal's interface.</param>
    /// <param name="member">The signal's name.</param>
    /// <param name="body">What the signal carries.</param>
    private void Emit(int id, string interfaceName, string member, in EventBody body)
    {
        var writer = _connection.GetMessageWriter();
        try
        {
            writer.WriteSignalHeader(null!, AtSpiPaths.For(id, _session.RootId), interfaceName, member, AtSpiNames.EventSignature);
            writer.WriteString(body.Detail);
            writer.WriteInt32(body.First);
            writer.WriteInt32(body.Second);
            WritePayload(ref writer, body);

            var properties = writer.WriteDictionaryStart();
            writer.WriteDictionaryEnd(properties);
            _ = _connection.TrySendMessage(writer.CreateMessage());
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Writes the variant that follows an event's two numbers.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="body">What the signal carries.</param>
    private void WritePayload(ref MessageWriter writer, in EventBody body)
    {
        switch (body.Kind)
        {
            case Payload.Text:
            {
                writer.WriteVariantString(body.Text);
                break;
            }

            case Payload.Reference:
            {
                AtSpiWire.WriteReferenceVariant(ref writer, _session, body.Reference);
                break;
            }

            default:
            {
                writer.WriteVariantInt32(0);
                break;
            }
        }
    }

    /// <summary>What an event signal carries after its object path.</summary>
    /// <param name="Detail">The detail string.</param>
    /// <param name="First">The first number.</param>
    /// <param name="Second">The second number.</param>
    /// <param name="Kind">The kind of the variant.</param>
    /// <param name="Reference">The element a reference points to, when the variant is a reference.</param>
    /// <param name="Text">The string, when the variant is a string.</param>
    [DebuggerDisplay("EventBody: {Detail}")]
    private readonly record struct EventBody(string Detail, int First, int Second, Payload Kind, int Reference, string Text);
}
