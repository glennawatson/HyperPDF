// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;
using Microsoft.Win32.SafeHandles;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// Generates requested external assets (predefined CMaps and CID to Unicode tables) once and persists only those assets.
/// The substitute faces are embedded in the assembly and never pass through here.
/// </summary>
internal static class FontDataResources
{
    /// <summary>Gets the cache folder, created only on the first requested asset.</summary>
    internal static string DirectoryPath { get; } = Path.Combine(
        Environment.GetEnvironmentVariable("HYPERPDF_FONT_DATA")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HyperPdfLibrary", "FontData"), "v3");

    /// <summary>Finds the path of a cached asset in the default cache, without contacting its upstream source.</summary>
    /// <param name="folder">The resource group.</param>
    /// <param name="name">The filename.</param>
    /// <returns>The path, or null when the asset is not cached.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string? Locate(string folder, string name) => Locate(DirectoryPath, folder, name);

    /// <summary>Finds the path of an asset in a supplied cache.</summary>
    /// <param name="directory">The cache root.</param>
    /// <param name="folder">The resource group.</param>
    /// <param name="name">The filename.</param>
    /// <returns>The path, or null when the asset is not cached.</returns>
    internal static string? Locate(string directory, string folder, string name)
    {
        if (name.Length == 0 || name.Contains('/') || name.Contains('\\') || name.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        var path = Path.Combine(directory, folder, name);
        return File.Exists(path) ? path : null;
    }

    /// <summary>Opens an asset from a supplied cache.</summary>
    /// <param name="directory">The cache root.</param>
    /// <param name="folder">The resource group.</param>
    /// <param name="name">The filename.</param>
    /// <returns>The owned handle, or null.</returns>
    internal static SafeFileHandle? Open(string directory, string folder, string name) =>
        Locate(directory, folder, name) is { } path ? File.OpenHandle(path) : null;

    /// <summary>Generates a missing asset; failed and cancelled attempts leave no cache entry.</summary>
    /// <param name="state">The selected resource cache.</param>
    /// <param name="folder">The resource group.</param>
    /// <param name="name">The filename.</param>
    /// <param name="generate">Builds the asset bytes.</param>
    /// <param name="cancellationToken">Cancels generation and I/O.</param>
    /// <returns>A task completing when the asset is cached.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ValueTask EnsureAsync(FontDataResourceState state, string folder, string name, Func<CancellationToken, ValueTask<byte[]>> generate, CancellationToken cancellationToken) =>
        EnsureAsync(state, DirectoryPath, folder, name, generate, cancellationToken);

    /// <summary>Generates one resource in a supplied cache.</summary>
    /// <param name="state">The selected resource cache.</param>
    /// <param name="directory">The cache root.</param>
    /// <param name="folder">The resource group.</param>
    /// <param name="name">The filename.</param>
    /// <param name="generate">Builds the resource bytes.</param>
    /// <param name="cancellationToken">Cancels generation and I/O.</param>
    /// <returns>A task completing when the resource is cached.</returns>
    internal static async ValueTask EnsureAsync(
        FontDataResourceState state,
        string directory,
        string folder,
        string name,
        Func<CancellationToken, ValueTask<byte[]>> generate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();
        var paths = state.Paths.GetOrAdd(new(directory, folder, name), FontAssetPaths.Create);
        var path = paths.Path;
        if (state.Availability.TryGetValue(path, out var known) && known.IsAvailable && File.Exists(path))
        {
            return;
        }

        var entry = known ?? state.Availability.GetOrAdd(path, static _ => new FontDataAvailability());
        if (File.Exists(path))
        {
            entry.ObserveAvailability(state);
            return;
        }

        // An offline service never generates, so it never reaches the network: the asset stays missing and is listed for the caller.
        if (state.Mode == PdfFontDataMode.Offline)
        {
            _ = state.Missing.TryAdd(paths.MissingKey, true);
            return;
        }

        var gate = state.Gates.GetOrAdd(path, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(path))
            {
                entry.ObserveAvailability(state);
                return;
            }

            var bytes = await generate(cancellationToken).ConfigureAwait(false);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
            try
            {
                await File.WriteAllBytesAsync(temporary, bytes, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                await WriteNoticeAsync(directory, folder, cancellationToken).ConfigureAwait(false);
                Publish(temporary, path);
                entry.ObserveAvailability(state);
            }
            finally
            {
                File.Delete(temporary);
            }
        }
        finally
        {
            _ = gate.Release();
        }
    }

    /// <summary>Publishes a complete file without replacing one another process already published.</summary>
    /// <param name="temporary">The complete temporary file.</param>
    /// <param name="path">The final cache entry.</param>
    private static void Publish(string temporary, string path)
    {
        try
        {
            File.Move(temporary, path);
        }
        catch (IOException) when (File.Exists(path))
        {
            // Another process finished the same resource while this process was generating it.
        }
    }

    /// <summary>Retains the upstream notice alongside generated assets.</summary>
    /// <param name="directory">The cache root.</param>
    /// <param name="folder">The resource group.</param>
    /// <param name="cancellationToken">Cancels I/O.</param>
    /// <returns>A task completing when the notice is persisted.</returns>
    /// <exception cref="InvalidOperationException">The upstream notice is missing from the assembly.</exception>
    private static async ValueTask WriteNoticeAsync(string directory, string folder, CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, folder, "LICENSE.txt");
        if (File.Exists(path))
        {
            return;
        }

        await using var source = typeof(FontDataResources).Assembly.GetManifestResourceStream($"HyperPdfLibrary.{folder}.LICENSE.txt")
            ?? throw new InvalidOperationException("The font data notice is missing.");
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var output = File.Create(temporary))
            {
                await source.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }

            Publish(temporary, path);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
