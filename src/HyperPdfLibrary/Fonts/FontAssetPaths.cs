// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts;

/// <summary>The strings derived from a <see cref="FontAssetKey"/>, kept so repeated requests for an asset build nothing.</summary>
/// <param name="Path">The full path of the cached file.</param>
/// <param name="MissingKey">The group and file name an offline service lists when the file is absent.</param>
internal sealed record FontAssetPaths(string Path, string MissingKey)
{
    /// <summary>Builds the strings for an asset.</summary>
    /// <param name="key">The asset.</param>
    /// <returns>The derived strings.</returns>
    internal static FontAssetPaths Create(FontAssetKey key) =>
        new(System.IO.Path.Combine(key.Directory, key.Folder, key.Name), $"{key.Folder}/{key.Name}");
}
