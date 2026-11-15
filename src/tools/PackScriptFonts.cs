#!/usr/bin/env -S dotnet run --file
#:property TargetFramework=net11.0

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Compression;

namespace HyperPdf.Scripts;

/// <summary>
/// Packs the Noto fonts the viewer draws scripts with when no installed font has them. Each <c>Noto*-Regular.ttf</c>
/// in the input folder is written to the output folder as a zlib stream named <c>Noto*-Regular.ttf.z</c>, which the
/// engine project embeds. The fonts come from github.com/notofonts and are licensed under the SIL Open Font License 1.1.
/// </summary>
internal static class PackScriptFonts
{
    /// <summary>The longest the whole run may take.</summary>
    private const int TimeoutMinutes = 5;

    /// <summary>The arguments: the folder of fonts and the folder for the packed files.</summary>
    private const int ArgumentCount = 2;

    /// <summary>Packs the fonts.</summary>
    /// <param name="args">The folder of fonts and the folder the packed files are written to.</param>
    /// <returns>A task whose result is 0 when at least one font was packed, and 1 otherwise.</returns>
    /// <exception cref="ArgumentException">The arguments are wrong.</exception>
    internal static async Task<int> Main(string[] args)
    {
        if (args.Length != ArgumentCount)
        {
            throw new ArgumentException("Provide the folder of Noto*-Regular.ttf files and the folder to write the .ttf.z files to.");
        }

        var output = Path.GetFullPath(args[1]);
        _ = Directory.CreateDirectory(output);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(TimeoutMinutes));
        var packed = 0;
        foreach (var path in Directory.GetFiles(Path.GetFullPath(args[0]), "Noto*-Regular.ttf"))
        {
            var target = Path.Combine(output, $"{Path.GetFileName(path)}.z");
            var before = await PackAsync(path, target, timeout.Token).ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"{Path.GetFileName(target)}: {before} -> {new FileInfo(target).Length} bytes").ConfigureAwait(false);
            packed++;
        }

        return packed > 0 ? 0 : 1;
    }

    /// <summary>Writes one font as a zlib stream.</summary>
    /// <param name="path">The font file.</param>
    /// <param name="target">The packed file.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The size of the font file.</returns>
    private static async Task<long> PackAsync(string path, string target, CancellationToken cancellationToken)
    {
        await using var input = File.OpenRead(path);
        await using var file = File.Create(target);
        await using var zlib = new ZLibStream(file, CompressionLevel.SmallestSize);
        await input.CopyToAsync(zlib, cancellationToken).ConfigureAwait(false);
        return input.Length;
    }
}
