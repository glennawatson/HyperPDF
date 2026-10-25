// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Tests.Media;

/// <summary>A folder of files that exists for one test and is deleted with it.</summary>
[DebuggerDisplay("TemporaryFolder: {_path}")]
internal sealed class TemporaryFolder : IDisposable
{
    /// <summary>The folder.</summary>
    private readonly string _path = Directory.CreateTempSubdirectory("hyperpdf-media-").FullName;

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(_path, true);

    /// <summary>Writes a file.</summary>
    /// <param name="name">The file name.</param>
    /// <param name="bytes">What the file holds.</param>
    /// <returns>The full path of the file.</returns>
    internal string WriteFile(string name, byte[] bytes)
    {
        var path = Path.Combine(_path, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
