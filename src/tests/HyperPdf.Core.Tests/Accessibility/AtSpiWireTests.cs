// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Platform.Linux.Accessibility.AtSpi;
using Tmds.DBus.Protocol;

namespace HyperPdf.Core.Tests.Accessibility;

/// <summary>Talks to the AT-SPI server over a real private D-Bus, the way a screen reader and D-Bus tools do.</summary>
public sealed class AtSpiWireTests
{
    /// <summary>The path of the window in the fake tree.</summary>
    private const string WindowPath = "/org/a11y/atspi/accessible/1";

    /// <summary>The path of the heading in the fake tree.</summary>
    private const string HeadingPath = "/org/a11y/atspi/accessible/2";

    /// <summary>The path of the page text in the fake tree.</summary>
    private const string PagePath = "/org/a11y/atspi/accessible/3";

    /// <summary>The path of the button in the fake tree.</summary>
    private const string ButtonPath = "/org/a11y/atspi/accessible/4";

    /// <summary>The path of an element the fake tree does not have.</summary>
    private const string MissingPath = "/org/a11y/atspi/accessible/99";

    /// <summary>The granularity number for a sentence.</summary>
    private const uint SentenceGranularity = 2;

    /// <summary>The offset inside the first sentence of the page.</summary>
    private const int InsideFirstSentence = 1;

    /// <summary>The reason a test is skipped when there is no D-Bus daemon.</summary>
    private const string NoDaemon = "dbus-daemon is not installed.";

    /// <summary>The method that names an element's role.</summary>
    private const string RoleNameMethod = "GetRoleName";

    /// <summary>The method that lists an element's attributes.</summary>
    private const string AttributesMethod = "GetAttributes";

    /// <summary>How long a tool may take to answer.</summary>
    private static readonly TimeSpan ToolTimeout = TimeSpan.FromSeconds(20);

    /// <summary>Writes the arguments of a call.</summary>
    /// <param name="writer">The writer, which the call's header has already been written to.</param>
    private delegate void WriteArguments(ref MessageWriter writer);

    /// <summary>Verifies the role name and the attributes of a heading, which is where a screen reader finds its level.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Heading_HasItsRoleNameAndLevel()
    {
        await using var fixture = await RequireFixtureAsync();

        var role = await fixture.CallAsync(HeadingPath, AtSpiNames.AccessibleInterface, RoleNameMethod, null, null, static (m, _) => m.GetBodyReader().ReadString());
        var level = await fixture.CallAsync(HeadingPath, AtSpiNames.AccessibleInterface, AttributesMethod, null, null, static (m, _) => ReadAttribute(m, "level"));

        await Assert.That(role).IsEqualTo("heading");
        await Assert.That(level).IsEqualTo("2");
    }

    /// <summary>Verifies the place in a set, which is where a screen reader finds "1 of 3".</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Button_HasItsPlaceInASet()
    {
        await using var fixture = await RequireFixtureAsync();

        var position = await fixture.CallAsync(ButtonPath, AtSpiNames.AccessibleInterface, AttributesMethod, null, null, static (m, _) => ReadAttribute(m, "posinset"));
        var size = await fixture.CallAsync(ButtonPath, AtSpiNames.AccessibleInterface, AttributesMethod, null, null, static (m, _) => ReadAttribute(m, "setsize"));

        await Assert.That(position).IsEqualTo("1");
        await Assert.That(size).IsEqualTo("3");
    }

    /// <summary>Verifies that the root lists the window as its child.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Root_ListsItsWindow()
    {
        await using var fixture = await RequireFixtureAsync();

        var child = await fixture.CallAsync(AtSpiNames.RootPath, AtSpiNames.AccessibleInterface, "GetChildren", null, null, static (m, _) => ReadFirstChildPath(m));

        await Assert.That(child).IsEqualTo(WindowPath);
    }

    /// <summary>Verifies that a property is read through the standard properties interface.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Name_IsReadThroughProperties()
    {
        await using var fixture = await RequireFixtureAsync();

        var name = await fixture.CallAsync(
            WindowPath,
            AtSpiNames.PropertiesInterface,
            "Get",
            "ss",
            static (ref MessageWriter writer) =>
            {
                writer.WriteString(AtSpiNames.AccessibleInterface);
                writer.WriteString("Name");
            },
            static (m, _) => m.GetBodyReader().ReadVariantValue().GetString());

        await Assert.That(name).IsEqualTo("Test window");
    }

    /// <summary>Verifies that a screen reader can ask for the sentence at an offset in the page text.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PageText_GivesTheSentenceAtAnOffset()
    {
        await using var fixture = await RequireFixtureAsync();

        var sentence = await fixture.CallAsync(
            PagePath,
            AtSpiNames.TextInterface,
            "GetStringAtOffset",
            "iu",
            static (ref MessageWriter writer) =>
            {
                writer.WriteInt32(InsideFirstSentence);
                writer.WriteUInt32(SentenceGranularity);
            },
            static (m, _) => m.GetBodyReader().ReadString());

        await Assert.That(sentence).IsEqualTo("First sentence here. ");
    }

    /// <summary>Verifies that a request for an element that does not exist is answered with an error.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnknownElement_IsAnError()
    {
        await using var fixture = await RequireFixtureAsync();

        await Assert.That(async () => await fixture.CallAsync(MissingPath, AtSpiNames.AccessibleInterface, RoleNameMethod, null, null, static (m, _) => m.GetBodyReader().ReadString()))
            .Throws<DBusErrorReplyException>();
    }

    /// <summary>Verifies that <c>busctl</c> can find and call a method by name, which needs the introspection data to be complete.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Busctl_CanCallAMethodByName()
    {
        await using var fixture = await RequireFixtureAsync();

        var output = await RunToolAsync("busctl", [$"--address={fixture.Address}", "--json=short", "call", fixture.ServerName, HeadingPath, AtSpiNames.AccessibleInterface, RoleNameMethod]);

        await Assert.That(output).Contains("heading");
    }

    /// <summary>Verifies that <c>busctl</c> can call a method on the application root, which the screen reader check starts from.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Busctl_CanCallAMethodOnTheRoot()
    {
        await using var fixture = await RequireFixtureAsync();

        var output = await RunToolAsync("busctl", [$"--address={fixture.Address}", "--json=short", "call", fixture.ServerName, AtSpiNames.RootPath, AtSpiNames.AccessibleInterface, RoleNameMethod]);

        await Assert.That(output).Contains("application");
    }

    /// <summary>Starts the fixture, or skips the test when there is no D-Bus daemon.</summary>
    /// <returns>The fixture.</returns>
    private static async Task<Fixture> RequireFixtureAsync()
    {
        var fixture = await Fixture.TryStartAsync().ConfigureAwait(false);
        if (fixture is null)
        {
            Skip.Test(NoDaemon);
        }

        return fixture!;
    }

    /// <summary>Reads one value out of an attribute map reply.</summary>
    /// <param name="message">The reply.</param>
    /// <param name="key">The attribute's name.</param>
    /// <returns>The value, or an empty string when the map has no such attribute.</returns>
    private static string ReadAttribute(Message message, string key)
    {
        var reader = message.GetBodyReader();
        var end = reader.ReadDictionaryStart();
        while (reader.HasNext(end))
        {
            reader.AlignStruct();
            var name = reader.ReadString();
            var value = reader.ReadString();
            if (name == key)
            {
                return value;
            }
        }

        return string.Empty;
    }

    /// <summary>Reads the object path of the first element in a list of references.</summary>
    /// <param name="message">The reply.</param>
    /// <returns>The path.</returns>
    private static string ReadFirstChildPath(Message message)
    {
        var reader = message.GetBodyReader();
        var end = reader.ReadArrayStart(DBusType.Struct);
        reader.AlignStruct();
        _ = reader.ReadString();
        var path = reader.ReadObjectPathAsString();
        reader.SkipTo(end);
        return path;
    }

    /// <summary>Runs a tool and returns what it prints.</summary>
    /// <param name="tool">The tool's name.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The standard output.</returns>
    /// <exception cref="InvalidOperationException">The tool printed an error.</exception>
    private static async Task<string> RunToolAsync(string tool, string[] arguments)
    {
        var info = new ProcessStartInfo(tool, arguments) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        using var process = Process.Start(info)!;
        using var timeout = new CancellationTokenSource(ToolTimeout);
        var output = await process.StandardOutput.ReadToEndAsync(timeout.Token).ConfigureAwait(false);
        var error = await process.StandardError.ReadToEndAsync(timeout.Token).ConfigureAwait(false);
        await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(error) ? output : throw new InvalidOperationException($"{tool} failed: {error}");
    }

    /// <summary>A private bus with the AT-SPI handler on one connection and a client on another.</summary>
    [DebuggerDisplay("Fixture: {ServerName}")]
    private sealed class Fixture : IAsyncDisposable
    {
        /// <summary>The private bus.</summary>
        private readonly PrivateBus _bus;

        /// <summary>The connection the handler is on.</summary>
        private readonly DBusConnection _server;

        /// <summary>The connection that makes the calls.</summary>
        private readonly DBusConnection _client;

        /// <summary>Initializes a new instance of the <see cref="Fixture"/> class.</summary>
        /// <param name="bus">The private bus.</param>
        /// <param name="server">The connection the handler is on.</param>
        /// <param name="client">The connection that makes the calls.</param>
        private Fixture(PrivateBus bus, DBusConnection server, DBusConnection client)
        {
            _bus = bus;
            _server = server;
            _client = client;
        }

        /// <summary>Gets the bus address.</summary>
        internal string Address => _bus.Address;

        /// <summary>Gets the unique name of the connection the handler is on.</summary>
        internal string ServerName => _server.UniqueName ?? string.Empty;

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            _client.Dispose();
            _server.Dispose();
            await _bus.DisposeAsync().ConfigureAwait(false);
        }

        /// <summary>Starts a bus with the handler on it.</summary>
        /// <returns>The fixture, or <see langword="null"/> when <c>dbus-daemon</c> is not installed.</returns>
        internal static async Task<Fixture?> TryStartAsync()
        {
            var bus = await PrivateBus.TryStartAsync().ConfigureAwait(false);
            if (bus is null)
            {
                return null;
            }

            var server = await bus.ConnectAsync().ConfigureAwait(false);
            var client = await bus.ConnectAsync().ConfigureAwait(false);
            server.AddMethodHandler(new AtSpiObjectHandler(new(new FakeAccessibleTree(), server.UniqueName ?? string.Empty)));
            return new(bus, server, client);
        }

        /// <summary>Calls a method on the handler.</summary>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <param name="path">The object path.</param>
        /// <param name="interfaceName">The interface.</param>
        /// <param name="member">The method.</param>
        /// <param name="signature">The signature of the arguments, or <see langword="null"/> for none.</param>
        /// <param name="arguments">Writes the arguments, or <see langword="null"/> for none.</param>
        /// <param name="reader">Reads the result.</param>
        /// <returns>The result.</returns>
        internal async Task<T> CallAsync<T>(string path, string interfaceName, string member, string? signature, WriteArguments? arguments, MessageValueReader<T> reader)
        {
            var writer = _client.GetMessageWriter();
            MessageBuffer message;
            try
            {
                writer.WriteMethodCallHeader(ServerName, path, interfaceName, member, signature);
                arguments?.Invoke(ref writer);
                message = writer.CreateMessage();
            }
            finally
            {
                writer.Dispose();
            }

            return await _client.CallMethodAsync(message, reader).ConfigureAwait(false);
        }
    }
}
