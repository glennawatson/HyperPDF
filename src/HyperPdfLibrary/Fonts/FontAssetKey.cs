// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts;

/// <summary>Names one external font asset, so its path and missing-list key are built once per service.</summary>
/// <param name="Directory">The cache root.</param>
/// <param name="Folder">The resource group.</param>
/// <param name="Name">The file name.</param>
internal readonly record struct FontAssetKey(string Directory, string Folder, string Name);
