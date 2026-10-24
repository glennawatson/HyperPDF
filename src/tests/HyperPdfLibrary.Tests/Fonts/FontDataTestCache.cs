// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Owns an isolated cache folder that is created only by the code under test.</summary>
internal sealed class FontDataTestCache : IDisposable
{
    /// <summary>Gets the isolated cache root.</summary>
    internal string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), $"hyperpdf-font-cache-{Guid.NewGuid():N}");

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(DirectoryPath))
        {
            Directory.Delete(DirectoryPath, true);
        }
    }
}
