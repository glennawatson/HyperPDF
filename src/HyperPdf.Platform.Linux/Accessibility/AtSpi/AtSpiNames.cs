// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Platform.Linux.Accessibility.AtSpi;

/// <summary>The bus names, object paths, interface names and member names of the AT-SPI2 protocol that the server speaks.</summary>
internal static class AtSpiNames
{
    /// <summary>The service on the session bus that gives out the address of the accessibility bus.</summary>
    internal const string BusService = "org.a11y.Bus";

    /// <summary>The object on the session bus that gives out the address of the accessibility bus.</summary>
    internal const string BusPath = "/org/a11y/bus";

    /// <summary>The interface that gives out the address.</summary>
    internal const string BusInterface = "org.a11y.Bus";

    /// <summary>The method that returns the accessibility bus address.</summary>
    internal const string GetAddressMethod = "GetAddress";

    /// <summary>The interface with the switches that say a screen reader is in use.</summary>
    internal const string StatusInterface = "org.a11y.Status";

    /// <summary>The property that is true when assistive technologies are enabled.</summary>
    internal const string IsEnabledProperty = "IsEnabled";

    /// <summary>The property that is true when a screen reader is running.</summary>
    internal const string ScreenReaderEnabledProperty = "ScreenReaderEnabled";

    /// <summary>The well known name of the registry daemon on the accessibility bus.</summary>
    internal const string RegistryService = "org.a11y.atspi.Registry";

    /// <summary>The prefix of every accessible object's path.</summary>
    internal const string ObjectPrefix = "/org/a11y/atspi/accessible";

    /// <summary>The path of the application object, the root of the tree.</summary>
    internal const string RootPath = "/org/a11y/atspi/accessible/root";

    /// <summary>The path that stands for no object.</summary>
    internal const string NullPath = "/org/a11y/atspi/null";

    /// <summary>The segment of the root's path.</summary>
    internal const string RootSegment = "root";

    /// <summary>The interface that lets an application attach itself to the registry's tree.</summary>
    internal const string SocketInterface = "org.a11y.atspi.Socket";

    /// <summary>The method that attaches an application.</summary>
    internal const string EmbedMethod = "Embed";

    /// <summary>The interface every accessible object has.</summary>
    internal const string AccessibleInterface = "org.a11y.atspi.Accessible";

    /// <summary>The interface of the application object.</summary>
    internal const string ApplicationInterface = "org.a11y.atspi.Application";

    /// <summary>The interface of objects that have a place and size.</summary>
    internal const string ComponentInterface = "org.a11y.atspi.Component";

    /// <summary>The interface of objects that can do something when activated.</summary>
    internal const string ActionInterface = "org.a11y.atspi.Action";

    /// <summary>The interface of objects with text.</summary>
    internal const string TextInterface = "org.a11y.atspi.Text";

    /// <summary>The interface of documents.</summary>
    internal const string DocumentInterface = "org.a11y.atspi.Document";

    /// <summary>The standard properties interface.</summary>
    internal const string PropertiesInterface = "org.freedesktop.DBus.Properties";

    /// <summary>The signal interface for object events.</summary>
    internal const string ObjectEventInterface = "org.a11y.atspi.Event.Object";

    /// <summary>The signal interface for window events.</summary>
    internal const string WindowEventInterface = "org.a11y.atspi.Event.Window";

    /// <summary>The signal interface for focus events.</summary>
    internal const string FocusEventInterface = "org.a11y.atspi.Event.Focus";

    /// <summary>The signal member for a change of an object's property.</summary>
    internal const string PropertyChangeSignal = "PropertyChange";

    /// <summary>The signal member for a change of an object's state.</summary>
    internal const string StateChangedSignal = "StateChanged";

    /// <summary>The signal member for children being added or removed.</summary>
    internal const string ChildrenChangedSignal = "ChildrenChanged";

    /// <summary>The signal member for the caret moving.</summary>
    internal const string TextCaretMovedSignal = "TextCaretMoved";

    /// <summary>The signal member for text being inserted or deleted.</summary>
    internal const string TextChangedSignal = "TextChanged";

    /// <summary>The signal member for a window becoming active.</summary>
    internal const string ActivateSignal = "Activate";

    /// <summary>The signal member for a window no longer being active.</summary>
    internal const string DeactivateSignal = "Deactivate";

    /// <summary>The signal member for the keyboard focus arriving.</summary>
    internal const string FocusSignal = "Focus";

    /// <summary>The signature of an event signal: detail, two numbers, a value and a dictionary of properties.</summary>
    internal const string EventSignature = "siiva{sv}";

    /// <summary>The name of the application object's version property.</summary>
    internal const string VersionProperty = "Version";

    /// <summary>The application name the toolkit reports.</summary>
    internal const string ToolkitName = "HyperPDF";

    /// <summary>The version of the AT-SPI protocol the server follows.</summary>
    internal const string ProtocolVersion = "2.1";
}
