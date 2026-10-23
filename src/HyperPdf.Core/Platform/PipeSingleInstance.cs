// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using HyperPdf.Core.Diagnostics;
using ReactiveUI.Primitives.Advanced;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Core.Platform;

/// <summary>
/// Keeps one running window per user on Windows and macOS. The first launch owns a named mutex and listens on a named
/// pipe (a Unix domain socket on macOS); later launches send their documents down the pipe and exit. The pipe accepts
/// only the same user. Each message is the activation token on the first line, then one path or URI per line.
/// </summary>
[DebuggerDisplay("PipeSingleInstance: {_name}")]
public sealed class PipeSingleInstance : ISingleInstance
{
    /// <summary>The longest path a Unix domain socket takes on macOS, the shortest limit of the Unix platforms.</summary>
    private const int MaxSocketPath = 104;

    /// <summary>The prefix .NET puts before a pipe's name to make its socket file in the temporary folder.</summary>
    private const string SocketPrefix = "CoreFxPipe_";

    /// <summary>The prefix of a shortened pipe name.</summary>
    private const string ShortPrefix = "pvl-";

    /// <summary>How many bytes of the name's hash a shortened pipe name keeps.</summary>
    private const int ShortHashBytes = 8;

    /// <summary>How long a forwarding launch waits for the running instance.</summary>
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);

    /// <summary>The pipe and mutex name.</summary>
    private readonly string _name;

    /// <summary>The ownership claim.</summary>
    private readonly Mutex _claim;

    /// <summary>Stops listening.</summary>
    private readonly CancellationTokenSource _stop = new();

    /// <summary>Requests received from other launches.</summary>
    private readonly Signal<OpenRequest> _openRequests = new();

    /// <summary>1 once disposed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="PipeSingleInstance"/> class.</summary>
    /// <param name="name">The pipe and mutex name.</param>
    /// <param name="claim">The ownership claim, already held.</param>
    private PipeSingleInstance(string name, Mutex claim)
    {
        _name = name;
        _claim = claim;

        // The first pipe exists before the claim is returned, so a launch straight after finds it. The token is taken
        // here because a quick Dispose would otherwise dispose its source before the listener reads it.
        var first = CreateServer(name);
        var token = _stop.Token;
        _ = Task.Run(() => ListenAsync(first, token));
        OpenRequests = new(_openRequests);
    }

    /// <inheritdoc/>
    public AsObservableSignal<OpenRequest> OpenRequests { get; }

    /// <summary>Gets the name used for the current user.</summary>
    /// <param name="application">The application name.</param>
    /// <returns>A name safe for pipes and mutexes, for example <c>HyperPDF-glenn</c>.</returns>
    public static string NameFor(string application)
    {
        ArgumentException.ThrowIfNullOrEmpty(application);
        var user = Environment.UserName;
        var builder = new StringBuilder(application.Length + 1 + user.Length).Append(application).Append('-');
        foreach (var c in user)
        {
            _ = builder.Append(char.IsAsciiLetterOrDigit(c) ? c : '_');
        }

        return builder.ToString();
    }

    /// <summary>Encodes a request as the message sent down the pipe.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The message.</returns>
    public static string Encode(OpenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var builder = new StringBuilder().Append(request.ActivationToken).Append('\n');
        foreach (var uri in request.Uris)
        {
            if (uri.AsSpan().IndexOfAny('\r', '\n') < 0)
            {
                _ = builder.Append(uri).Append('\n');
            }
        }

        return builder.ToString();
    }

    /// <summary>Decodes a message received from the pipe.</summary>
    /// <param name="message">The message.</param>
    /// <returns>The request.</returns>
    public static OpenRequest Decode(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var lines = message.Split('\n');
        var uris = new List<string>(Math.Max(0, lines.Length - 1));
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Length > 0)
            {
                uris.Add(lines[i]);
            }
        }

        return new(uris, lines[0].Length > 0 ? lines[0] : null);
    }

    /// <summary>Sends a request to the running instance.</summary>
    /// <param name="name">The pipe name, from <see cref="NameFor"/>.</param>
    /// <param name="request">The documents and activation token.</param>
    /// <returns><see langword="true"/> when a running instance took the request.</returns>
    public static async Task<bool> TryForwardAsync(string name, OpenRequest request)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            await using var client = new NamedPipeClientStream(".", PipeNameFor(name), PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            using var timeout = new CancellationTokenSource(ConnectTimeout);
            await client.ConnectAsync(timeout.Token).ConfigureAwait(false);
            var bytes = Encoding.UTF8.GetBytes(Encode(request));
            await client.WriteAsync(bytes, timeout.Token).ConfigureAwait(false);
            await client.FlushAsync(timeout.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Claims the single running window for this user.</summary>
    /// <param name="name">The pipe and mutex name, from <see cref="NameFor"/>.</param>
    /// <returns>The claim, or <see langword="null"/> when another process holds it.</returns>
    public static PipeSingleInstance? TryStart(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Mutex claim;
        try
        {
            claim = new(true, name, out var createdNew);
            if (!createdNew)
            {
                claim.Dispose();
                return null;
            }
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        return new(name, claim);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _stop.Cancel();
        _stop.Dispose();
        _claim.Dispose();
        _openRequests.Dispose();
    }

    /// <summary>
    /// Gets the pipe name for a claim name. On Unix the pipe is a socket file in the temporary folder, whose path
    /// macOS limits to 104 characters and whose folder there is already long, so a name that would not fit is
    /// replaced by a short one made from its hash; both launches compute the same name.
    /// </summary>
    /// <param name="name">The claim name.</param>
    /// <returns>The pipe name.</returns>
    internal static string PipeNameFor(string name)
    {
        if (OperatingSystem.IsWindows() || Path.GetTempPath().Length + SocketPrefix.Length + name.Length <= MaxSocketPath)
        {
            return name;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(name));
        return ShortPrefix + Convert.ToHexStringLower(hash.AsSpan(0, ShortHashBytes));
    }

    /// <summary>Creates a pipe ready for the next launch.</summary>
    /// <param name="name">The claim name.</param>
    /// <returns>The pipe.</returns>
    private static NamedPipeServerStream CreateServer(string name) =>
        new(PipeNameFor(name), PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

    /// <summary>Creates the next pipe after a failed connection.</summary>
    /// <param name="name">The claim name.</param>
    /// <returns>The pipe, or <see langword="null"/> when the pipe cannot be created again.</returns>
    private static NamedPipeServerStream? TryCreateServer(string name)
    {
        try
        {
            return CreateServer(name);
        }
        catch (IOException ex)
        {
            AppLog.Warning(ex, "Single instance pipe could not be recreated.");
            return null;
        }
    }

    /// <summary>Accepts one connection after another until disposed.</summary>
    /// <param name="first">The first pipe, already created.</param>
    /// <param name="token">Stops listening.</param>
    /// <returns>A task.</returns>
    private async Task ListenAsync(NamedPipeServerStream first, CancellationToken token)
    {
        var server = first;
        while (!token.IsCancellationRequested)
        {
            try
            {
                await using (server.ConfigureAwait(false))
                {
                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                    using var reader = new StreamReader(server, Encoding.UTF8);
                    var message = await reader.ReadToEndAsync(token).ConfigureAwait(false);
                    _openRequests.OnNext(Decode(message));
                }

                server = CreateServer(_name);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (IOException ex)
            {
                AppLog.Warning(ex, "Single instance pipe failed.");
                if (TryCreateServer(_name) is not { } replacement)
                {
                    // Later launches open their own window rather than reaching this one.
                    return;
                }

                server = replacement;
            }
        }

        await server.DisposeAsync().ConfigureAwait(false);
    }
}
