// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Fonts;

/// <summary>Generates requested assets once and persists only those assets.</summary>
internal static class FontDataResources
{
    /// <summary>Gets the cache folder, created only on the first requested asset.</summary>
    internal static string DirectoryPath { get; } = Path.Combine(
        Environment.GetEnvironmentVariable("HYPERPDF_FONT_DATA")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HyperPdfLibrary", "FontData"), "v3");

    /// <summary>Opens a cached asset without contacting its upstream source.</summary>
    /// <param name="folder">The resource group.</param>
    /// <param name="name">The filename.</param>
    /// <returns>The owned stream, or null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Stream? Open(string folder, string name) => Open(DirectoryPath, folder, name);

    /// <summary>Opens an asset from a supplied cache.</summary>
    /// <param name="directory">The cache root.</param>
    /// <param name="folder">The resource group.</param>
    /// <param name="name">The filename.</param>
    /// <returns>The owned stream, or null.</returns>
    internal static Stream? Open(string directory, string folder, string name)
    {
        if (name.Length == 0 || name.Contains('/') || name.Contains('\\') || name.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        var path = Path.Combine(directory, folder, name);
        return File.Exists(path) ? File.OpenRead(path) : null;
    }

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
        var path = Path.Combine(directory, folder, name);
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
