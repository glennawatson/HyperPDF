// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using HyperPdf.Core.Diagnostics;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Viewer.Services;

/// <summary>
/// Keeps one viewer running for each user. A second launch with files hands the files to the first and ends, so the files open as tabs
/// of the window that is already there. A pipe the operating system names for the user carries them, on every system.
/// </summary>
[DebuggerDisplay("SingleInstance: {_name}")]
public sealed class SingleInstance : IDisposable
{
    /// <summary>The start of the pipe's name; the user's name follows, so each user has a pipe of their own.</summary>
    private const string PipePrefix = "HyperPdfViewer-";

    /// <summary>The longest user name that goes into the pipe's name as it is.</summary>
    private const int LongestUserName = 20;

    /// <summary>The bytes of a hash that name the pipe of a user with a long name.</summary>
    private const int HashBytes = 8;

    /// <summary>The longest a second launch waits for the first to answer, in milliseconds.</summary>
    private const int ConnectMilliseconds = 250;

    /// <summary>The pipe's name.</summary>
    private readonly string _name;

    /// <summary>Ends the wait for a second launch.</summary>
    private readonly CancellationTokenSource _stop = new();

    /// <summary>Carries each file a second launch handed over.</summary>
    private readonly Signal<string> _requests = new();

    /// <summary>The wait for second launches, which runs until the viewer ends.</summary>
    private readonly Task _listening;

    /// <summary>Initializes a new instance of the <see cref="SingleInstance"/> class.</summary>
    /// <param name="name">The pipe's name.</param>
    private SingleInstance(string name)
    {
        _name = name;
        _listening = ListenAsync(_stop.Token);
    }

    /// <summary>Gets the files that second launches handed over, one value for each file.</summary>
    public IObservable<string> Requests => _requests;

    /// <summary>Gets the task that waits for second launches.</summary>
    internal Task Listening => _listening;

    /// <summary>Hands the files to the viewer that is already running, or becomes the viewer that runs.</summary>
    /// <param name="paths">The files or addresses named on the command line.</param>
    /// <returns>The running viewer's pipe when this launch is the first, or <see langword="null"/> when the files went to the first and this launch should end.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SingleInstance? Start(IReadOnlyList<string> paths) => Start(paths, NameFor(Environment.UserName));

    /// <inheritdoc/>
    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
        _requests.Dispose();
    }

    /// <summary>Gets the pipe name of a user. macOS limits a socket path to 104 characters and puts the pipe in a long temporary folder, so a long user name becomes a hash of itself.</summary>
    /// <param name="user">The user's name.</param>
    /// <returns>The pipe's name.</returns>
    internal static string NameFor(string user) => user.Length <= LongestUserName
        ? PipePrefix + user
        : PipePrefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user)), 0, HashBytes);

    /// <summary>Hands the files to the viewer listening on a pipe, or becomes the viewer that listens on it.</summary>
    /// <param name="paths">The files or addresses named on the command line.</param>
    /// <param name="name">The pipe's name.</param>
    /// <returns>The viewer's pipe when this launch is the first, or <see langword="null"/> when the files went to the first.</returns>
    internal static SingleInstance? Start(IReadOnlyList<string> paths, string name)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return paths.Count > 0 && Forward(name, paths) ? null : new(name);
    }

    /// <summary>Writes the files to the running viewer, one on each line.</summary>
    /// <param name="name">The pipe's name.</param>
    /// <param name="paths">The files or addresses.</param>
    /// <returns><see langword="true"/> when a viewer was running and took them.</returns>
    private static bool Forward(string name, IReadOnlyList<string> paths)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", name, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            client.Connect(ConnectMilliseconds);
            using var writer = new StreamWriter(client, Encoding.UTF8);
            for (var i = 0; i < paths.Count; i++)
            {
                // A relative name means a place in this launch's folder, which the running viewer does not share.
                writer.WriteLine(Uri.TryCreate(paths[i], UriKind.Absolute, out var address) && address.Scheme != Uri.UriSchemeFile ? paths[i] : Path.GetFullPath(paths[i]));
            }

            writer.Flush();
            return true;
        }
        catch (Exception error) when (error is TimeoutException or IOException or UnauthorizedAccessException)
        {
            // Nothing is running, or what runs cannot be reached: this launch becomes the viewer.
            return false;
        }
    }

    /// <summary>Takes the files of each second launch until the viewer ends.</summary>
    /// <param name="token">Ends the wait.</param>
    /// <returns>A task that ends with the viewer.</returns>
    private async Task ListenAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await using var server = new NamedPipeServerStream(_name, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                using var reader = new StreamReader(server, Encoding.UTF8);
                while (await reader.ReadLineAsync(token).ConfigureAwait(false) is { Length: > 0 } line)
                {
                    _requests.OnNext(line);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The viewer is ending.
        }
        catch (Exception error) when (error is IOException or ObjectDisposedException)
        {
            AppLog.Error(error, "The wait for a second launch ended.");
        }
    }
}
