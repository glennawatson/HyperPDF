// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.Windowing.Windows;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
using Tmds.DBus.Protocol;

namespace HyperPdf.Head.Linux;

/// <summary>Opens the desktop file chooser through the session bus.</summary>
internal static class PortalFileChooser
{
    /// <summary>The service that selects the file chooser for the current desktop.</summary>
    private const string Desktop = "org.freedesktop.portal.Desktop";

    /// <summary>Requests a selection on a dedicated connection, which owns the portal request.</summary>
    /// <param name="request">The selection requested.</param>
    /// <param name="parent">The exported window identifier.</param>
    /// <returns>The paths selected, or null when there is no session bus.</returns>
    internal static IObservable<string[]>? Pick(FilePickerRequest request, string parent) =>
        DBusAddress.Session is { Length: > 0 } address
            ? Signal.FromAsync(async token => await ChooseAsync(address, parent, request, token).ConfigureAwait(false))
                .Catch<string[], DBusExceptionBase>(static error => Signal.Throw<string[]>(new IOException("The desktop file chooser is unavailable.", error)))
            : null;

    /// <summary>Subscribes before opening the chooser so a quick reply reaches the caller.</summary>
    /// <param name="address">The session bus.</param>
    /// <param name="parent">The parent window identifier.</param>
    /// <param name="request">The selection requested.</param>
    /// <param name="token">Cancels the request.</param>
    /// <returns>The selected paths.</returns>
    private static async Task<string[]> ChooseAsync(string address, string parent, FilePickerRequest request, CancellationToken token)
    {
        using var bus = new DBusConnection(address);
        await bus.ConnectAsync().ConfigureAwait(false);
        var id = $"hyperpdf_{Guid.NewGuid():N}";
        var sender = bus.UniqueName!.TrimStart(':').Replace('.', '_');
        var path = $"/org/freedesktop/portal/desktop/request/{sender}/{id}";
        var rule = new MatchRule { Type = MessageType.Signal, Sender = Desktop, Interface = "org.freedesktop.portal.Request", Member = "Response", Path = path };
        var answer = new TaskCompletionSource<string[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var observer = await bus.AddMatchAsync(
            rule,
            static (message, _) => ReadPaths(message),
            notice =>
            {
                if (notice.HasValue)
                {
                    _ = answer.TrySetResult(notice.Value);
                }
                else
                {
                    _ = answer.TrySetException(new IOException("The desktop file chooser disconnected.", notice.CompletionReason));
                }
            },
            emitOnCapturedContext: false,
            flags: ObserverFlags.EmitOnConnectionFailed | ObserverFlags.EmitOnReaderFailed).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        await bus.CallMethodAsync(Open(bus, parent, request, id)).WaitAsync(token).ConfigureAwait(false);
        return await answer.Task.WaitAsync(token).ConfigureAwait(false);
    }

    /// <summary>Builds the open or save call with the reader's starting directory.</summary>
    /// <param name="bus">The session connection.</param>
    /// <param name="parent">The parent window identifier.</param>
    /// <param name="request">The selection requested.</param>
    /// <param name="id">The token matching this request's response.</param>
    /// <returns>The method call.</returns>
    private static MessageBuffer Open(DBusConnection bus, string parent, FilePickerRequest request, string id)
    {
        var options = new Dictionary<string, VariantValue>(StringComparer.Ordinal) { ["handle_token"] = id, ["current_folder"] = VariantValue.Array(Encoding.UTF8.GetBytes(request.Folder + '\0')) };
        if (request.SaveName is { } name)
        {
            options["current_name"] = name;
        }
        else
        {
            options["directory"] = request.Directories;
            options["multiple"] = request.Multiple;
        }

        using var writer = bus.GetMessageWriter();
        writer.WriteMethodCallHeader(Desktop, "/org/freedesktop/portal/desktop", "org.freedesktop.portal.FileChooser", request.SaveName is null ? "OpenFile" : "SaveFile", "ssa{sv}");
        writer.WriteString(parent);
        writer.WriteString(request.Title);
        writer.WriteDictionary(options);
        return writer.CreateMessage();
    }

    /// <summary>Reads local paths from a chooser response.</summary>
    /// <param name="message">The response signal.</param>
    /// <returns>The selected paths, empty on cancellation.</returns>
    private static string[] ReadPaths(Message message)
    {
        var reader = message.GetBodyReader();
        if (reader.ReadUInt32() != 0 || !reader.ReadDictionaryOfStringToVariantValue().TryGetValue("uris", out var uris))
        {
            return [];
        }

        var paths = new List<string>();
        foreach (var value in uris.GetArray<string>())
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.IsFile)
            {
                paths.Add(uri.LocalPath);
            }
        }

        return [..paths];
    }
}
