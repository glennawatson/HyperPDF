// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Finding;

/// <summary>One search in a folder: the token that stops it and its number, so a search that ends late can tell a newer one replaced it.</summary>
/// <param name="Version">The number of the search among those started.</param>
/// <param name="Token">Stops the search.</param>
internal readonly record struct FolderSearchRun(int Version, CancellationToken Token);
