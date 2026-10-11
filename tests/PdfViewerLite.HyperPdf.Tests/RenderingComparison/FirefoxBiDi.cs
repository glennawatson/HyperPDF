// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;

namespace PdfViewerLite.HyperPdf.Tests.RenderingComparison;

/// <summary>Owns a headless Firefox process and its direct WebDriver BiDi connection.</summary>
internal sealed class FirefoxBiDi : IAsyncDisposable
{
    /// <summary>The maximum browser startup wait.</summary>
    private const int StartupSeconds = 60;

    /// <summary>The maximum asynchronous cleanup wait.</summary>
    private const int CleanupSeconds = 30;

    /// <summary>The protocol receive buffer length.</summary>
    private const int ReceiveBufferLength = 65_536;

    /// <summary>The maximum retained browser diagnostic lines.</summary>
    private const int DiagnosticLines = 32;

    /// <summary>The protocol property that names a browsing context.</summary>
    private const string ContextProperty = "context";

    /// <summary>Protects the bounded browser diagnostics.</summary>
    private readonly Lock _diagnosticGate = new();

    /// <summary>The last browser diagnostic lines.</summary>
    private readonly Queue<string> _diagnostics = new();

    /// <summary>The owned headless browser process.</summary>
    private readonly Process _process;

    /// <summary>The direct browser protocol connection.</summary>
    private readonly ClientWebSocket _socket = new();

    /// <summary>Cancels owned background operations.</summary>
    private readonly CancellationTokenSource _lifetime = new();

    /// <summary>The isolated temporary browser profile.</summary>
    private readonly string _profile;

    /// <summary>Drains the browser output stream.</summary>
    private Task _stdout = Task.CompletedTask;

    /// <summary>Drains the browser error stream.</summary>
    private Task _stderr = Task.CompletedTask;

    /// <summary>The next protocol command identifier.</summary>
    private int _commandId;

    /// <summary>Tracks completed lifetime teardown.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="FirefoxBiDi"/> class.</summary>
    /// <param name="process">The process.</param>
    /// <param name="profile">The profile.</param>
    private FirefoxBiDi(Process process, string profile)
    {
        _process = process;
        _profile = profile;
    }

    /// <summary>Gets the version negotiated with the actual running browser.</summary>
    internal string Version { get; private set; } = string.Empty;

    /// <summary>Gets the build identifier negotiated with the actual running browser.</summary>
    internal string Build { get; private set; } = string.Empty;

    /// <summary>Gets the isolated page's browsing context.</summary>
    internal string Context { get; private set; } = string.Empty;

    /// <summary>Gets bounded recent output for diagnosing browser failures.</summary>
    internal string Diagnostics
    {
        get
        {
            lock (_diagnosticGate)
            {
                return string.Join(Environment.NewLine, _diagnostics);
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(CleanupSeconds));
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(true);
            }

            _socket.Dispose();
            await _process.WaitForExitAsync(cleanup.Token);
            await Task.WhenAll(_stdout, _stderr).WaitAsync(cleanup.Token);
        }
        finally
        {
            _socket.Dispose();
            await _lifetime.CancelAsync();
            _lifetime.Dispose();
            _process.Dispose();

            // Let profile deletion use its own deadline after the process wait has ended.
            await FirefoxProfileCleanup.DeleteAsync(_profile, CancellationToken.None);
        }
    }

    /// <summary>Starts an isolated browser and awaits its actual remote endpoint.</summary>
    /// <param name="cancellationToken">Cancels startup.</param>
    /// <returns>The owned browser connection.</returns>
    /// <exception cref="IOException">Firefox cannot start or initialize the protocol connection.</exception>
    /// <exception cref="TimeoutException">Firefox did not finish its protocol startup within the bound.</exception>
    internal static async Task<FirefoxBiDi> CreateAsync(CancellationToken cancellationToken)
    {
        var executable = FirefoxBrowserDiscovery.FindExecutable();
        var profile = Path.Combine(Path.GetTempPath(), $"pvl-firefox-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(profile);
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var argument in new[] { "--headless", "--new-instance", "--profile", profile, "--remote-debugging-port", "0" })
        {
            start.ArgumentList.Add(argument);
        }

        Process process;
        try
        {
            process = Process.Start(start) ?? throw new IOException("Firefox could not start.");
        }
        catch
        {
            Directory.Delete(profile, true);
            throw;
        }

        var browser = new FirefoxBiDi(process, profile);
        try
        {
            await browser.InitializeAsync(cancellationToken);
            return browser;
        }
        catch (Exception exception)
        {
            var diagnostics = browser.Diagnostics;
            await browser.DisposeAsync();
            if (exception is OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                throw new TimeoutException($"Firefox did not initialize within {StartupSeconds} seconds: {diagnostics}", exception);
            }

            throw new IOException($"Firefox initialization failed: {diagnostics}", exception);
        }
    }

    /// <summary>Navigates the isolated page and waits for document loading.</summary>
    /// <param name="url">The loopback page URL.</param>
    /// <param name="cancellationToken">Cancels navigation.</param>
    /// <returns>A task.</returns>
    internal async Task NavigateAsync(string url, CancellationToken cancellationToken) => await CommandAsync(
            "browsingContext.navigate",
            writer =>
        {
            writer.WriteString(ContextProperty, Context);
            writer.WriteString(nameof(url), url);
            writer.WriteString("wait", "complete");
        },
            cancellationToken);

    /// <summary>Drains browser output and identifies the actual protocol endpoint.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="endpoint">The endpoint.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task.</returns>
    private async Task CaptureAsync(StreamReader reader, TaskCompletionSource<string> endpoint, CancellationToken cancellationToken)
    {
        const string marker = "WebDriver BiDi listening on ";
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            lock (_diagnosticGate)
            {
                _diagnostics.Enqueue(line);
                if (_diagnostics.Count > DiagnosticLines)
                {
                    _ = _diagnostics.Dequeue();
                }
            }

            var index = line.IndexOf(marker, StringComparison.Ordinal);
            if (index >= 0)
            {
                _ = endpoint.TrySetResult(line[(index + marker.Length)..].Trim());
            }
        }
    }

    /// <summary>Awaits browser startup and creates an isolated page context.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="IOException">Firefox exits before publishing its protocol endpoint.</exception>
    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        using var startup = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        startup.CancelAfter(TimeSpan.FromSeconds(StartupSeconds));
        var endpoint = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _stdout = CaptureAsync(_process.StandardOutput, endpoint, _lifetime.Token);
        _stderr = CaptureAsync(_process.StandardError, endpoint, _lifetime.Token);
        var exited = _process.WaitForExitAsync(startup.Token);
        var first = await Task.WhenAny(endpoint.Task, exited).WaitAsync(startup.Token);
        if (first == exited)
        {
            throw new IOException("Firefox exited before publishing a WebDriver BiDi endpoint.");
        }

        var address = await endpoint.Task.WaitAsync(startup.Token);
        await _socket.ConnectAsync(new($"{address.TrimEnd('/')}/session"), startup.Token);
        var session = await CommandAsync(
            "session.new",
            static writer =>
        {
            writer.WriteStartObject("capabilities");
            writer.WriteEndObject();
        },
            startup.Token);
        var capabilities = session.GetProperty("result").GetProperty("capabilities");
        Version = capabilities.GetProperty("browserVersion").GetString()!;
        Build = capabilities.GetProperty("moz:buildID").GetString()!;

        // pdf.js schedules display renders on requestAnimationFrame, which Firefox throttles or pauses in background
        // tabs, so the render page must be the visible, selected tab.
        var created = await CommandAsync(
            "browsingContext.create",
            static writer =>
        {
            writer.WriteString("type", "tab");
            writer.WriteBoolean("background", false);
        },
            startup.Token);
        Context = created.GetProperty("result").GetProperty(ContextProperty).GetString()!;
        _ = await CommandAsync("browsingContext.activate", writer => writer.WriteString(ContextProperty, Context), startup.Token);
    }

    /// <summary>Sends one protocol command and awaits its matching response.</summary>
    /// <param name="method">The method.</param>
    /// <param name="parameters">The parameters.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The operation result.</returns>
    private async Task<JsonElement> CommandAsync(string method, Action<Utf8JsonWriter> parameters, CancellationToken cancellationToken)
    {
        await using var command = new MemoryStream();
        var id = Interlocked.Increment(ref _commandId);
        await using (var writer = new Utf8JsonWriter(command))
        {
            writer.WriteStartObject();
            writer.WriteNumber(nameof(id), id);
            writer.WriteString(nameof(method), method);
            writer.WriteStartObject("params");
            parameters(writer);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        await _socket.SendAsync(command.ToArray().AsMemory(), WebSocketMessageType.Text, true, cancellationToken);
        return await ReceiveAsync(id, cancellationToken);
    }

    /// <summary>Awaits a matching protocol response and rejects browser errors.</summary>
    /// <param name="id">The id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The operation result.</returns>
    /// <exception cref="IOException">Firefox closes the connection or reports a protocol error.</exception>
    private async Task<JsonElement> ReceiveAsync(int id, CancellationToken cancellationToken)
    {
        var buffer = new byte[ReceiveBufferLength];
        while (true)
        {
            await using var message = new MemoryStream();
            ValueWebSocketReceiveResult received;
            do
            {
                received = await _socket.ReceiveAsync(buffer.AsMemory(), cancellationToken);
                if (received.MessageType == WebSocketMessageType.Close)
                {
                    throw new IOException("Firefox closed the WebDriver BiDi connection.");
                }

                message.Write(buffer, 0, received.Count);
            }
            while (!received.EndOfMessage);

            using var document = JsonDocument.Parse(message.ToArray());
            var root = document.RootElement;
            if (!root.TryGetProperty(nameof(id), out var actual) || actual.GetInt32() != id)
            {
                continue;
            }

            if (root.GetProperty("type").GetString() == "error")
            {
                throw new IOException(root.GetRawText());
            }

            return root.Clone();
        }
    }
}
