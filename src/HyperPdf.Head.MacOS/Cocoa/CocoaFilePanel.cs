// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Head.MacOS.Cocoa;

/// <summary>Runs the AppKit open or save panel on the window thread.</summary>
internal static class CocoaFilePanel
{
    /// <summary>Shows the panel and returns the selected filesystem paths.</summary>
    /// <param name="request">The selection requested.</param>
    /// <returns>The chosen paths, empty on cancellation.</returns>
    internal static string[] Choose(FilePickerRequest request)
    {
        var saving = request.SaveName is not null;
        var panel = NativeMethods.Send(NativeMethods.GetClass(saving ? "NSSavePanel" : "NSOpenPanel"), NativeMethods.Selector(saving ? "savePanel" : "openPanel"));
        var directory = NativeMethods.Send(NativeMethods.GetClass("NSURL"), NativeMethods.Selector("fileURLWithPath:"), Cocoa.Text(request.Folder));
        NativeMethods.SendVoid(panel, NativeMethods.Selector("setDirectoryURL:"), directory);
        NativeMethods.SendVoid(panel, NativeMethods.Selector("setTitle:"), Cocoa.Text(request.Title));
        if (request.SaveName is { } name)
        {
            NativeMethods.SendVoid(panel, NativeMethods.Selector("setNameFieldStringValue:"), Cocoa.Text(name));
        }
        else
        {
            NativeMethods.SendVoidFlag(panel, NativeMethods.Selector("setCanChooseDirectories:"), (byte)(request.Directories ? 1 : 0));
            NativeMethods.SendVoidFlag(panel, NativeMethods.Selector("setCanChooseFiles:"), (byte)(request.Directories ? 0 : 1));
            NativeMethods.SendVoidFlag(panel, NativeMethods.Selector("setAllowsMultipleSelection:"), (byte)(request.Multiple ? 1 : 0));
        }

        if (NativeMethods.Send(panel, NativeMethods.Selector("runModal")) != 1)
        {
            return [];
        }

        if (saving)
        {
            return [ReadPath(NativeMethods.Send(panel, NativeMethods.Selector("URL")))];
        }

        var urls = NativeMethods.Send(panel, NativeMethods.Selector("URLs"));
        var paths = new string[checked((int)NativeMethods.SendCount(urls, NativeMethods.Selector("count")))];
        for (var i = 0; i < paths.Length; i++)
        {
            paths[i] = ReadPath(NativeMethods.Send(urls, NativeMethods.Selector("objectAtIndex:"), i));
        }

        return paths;
    }

    /// <summary>Reads the path of a selected file URL.</summary>
    /// <param name="url">The URL.</param>
    /// <returns>The local path.</returns>
    /// <exception cref="IOException">The selected URL has no path.</exception>
    private static string ReadPath(nint url) =>
        Marshal.PtrToStringUTF8(NativeMethods.Send(NativeMethods.Send(url, NativeMethods.Selector("path")), Selectors.Utf8String)) ?? throw new IOException("The file panel returned no path.");
}
