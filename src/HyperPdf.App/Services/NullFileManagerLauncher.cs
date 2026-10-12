// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Platform;

namespace HyperPdf.App.Services;

/// <summary>A file manager launcher for platforms without one.</summary>
internal sealed class NullFileManagerLauncher : IFileManagerLauncher
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<bool> ShowItemAsync(string filePath, CancellationToken cancellationToken) => Task.FromResult(false);
}
