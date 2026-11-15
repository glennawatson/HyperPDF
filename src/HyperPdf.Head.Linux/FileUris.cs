// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Head.Linux;

/// <summary>Reads the list of file addresses a file manager hands over when files are dropped, whichever window system it drops them through.</summary>
internal static class FileUris
{
    /// <summary>The type that lists files as addresses.</summary>
    internal const string ListType = "text/uri-list";

    /// <summary>The scheme of a dropped file's address.</summary>
    private const string FileScheme = "file://";

    /// <summary>Turns a list of addresses, one on each line, into the paths of the files it names. Anything that is not a file is left out.</summary>
    /// <param name="text">The list.</param>
    /// <returns>The paths.</returns>
    internal static string[] PathsOf(string text)
    {
        var paths = new List<string>();
        foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith(FileScheme, StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(line.Trim(), UriKind.Absolute, out var address) && address.IsFile)
            {
                paths.Add(address.LocalPath);
            }
        }

        return [.. paths];
    }
}
