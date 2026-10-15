// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Http;

/// <summary>Finds the <see cref="DownloadFile"/> of each file kept in a folder, building its paths the first time it is asked for.</summary>
internal sealed class DownloadFiles
{
    /// <summary>The most files kept; past this the list starts again, as only a few folders and files are ever used.</summary>
    private const int MaxFiles = 512;

    /// <summary>Guards <see cref="_files"/>.</summary>
    private readonly Lock _gate = new();

    /// <summary>The files found so far.</summary>
    private readonly Dictionary<DownloadKey, DownloadFile> _files = [];

    /// <summary>Finds a file in a folder.</summary>
    /// <param name="directory">The folder.</param>
    /// <param name="localName">The file's path inside the folder.</param>
    /// <returns>The file.</returns>
    /// <exception cref="InvalidDataException">Thrown when the name leaves the folder.</exception>
    internal DownloadFile For(string directory, string localName)
    {
        var key = new DownloadKey(directory, localName);
        lock (_gate)
        {
            if (_files.TryGetValue(key, out var found))
            {
                return found;
            }

            var file = new DownloadFile(TargetIn(directory, localName));
            if (_files.Count >= MaxFiles)
            {
                _files.Clear();
            }

            _files[key] = file;
            return file;
        }
    }

    /// <summary>Gets a file's path inside a folder, refusing names that would place it outside.</summary>
    /// <param name="directory">The folder.</param>
    /// <param name="localName">The file's path inside the folder.</param>
    /// <returns>The full path.</returns>
    /// <exception cref="InvalidDataException">Thrown when the name leaves the folder.</exception>
    private static string TargetIn(string directory, string localName)
    {
        var root = Path.GetFullPath(directory);
        var target = Path.GetFullPath(Path.Combine(root, localName));
        if (!target.StartsWith(Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{localName} is outside the download folder.");
        }

        return target;
    }

    /// <summary>A folder and the name of a file inside it.</summary>
    /// <param name="Directory">The folder.</param>
    /// <param name="LocalName">The file's path inside the folder.</param>
    private readonly record struct DownloadKey(string Directory, string LocalName);
}
