// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Microsoft.Diagnostics.Tracing.Etlx;

namespace HyperPdf.Scripts;

/// <summary>
/// Turns a sampled frame of a trace into the source line it was running, from the portable PDB the build embeds in its assemblies.
/// The runtime reports an offset into the method's IL, and the PDB's sequence points say which line that offset belongs to.
/// </summary>
internal sealed class SourceLines
{
    /// <summary>The environment variable naming the folder to search for the built assemblies, which embed their PDB files.</summary>
    internal const string SymbolsVariable = "HYPERPDF_SYMBOLS";

    /// <summary>The mask that keeps the row of a metadata token.</summary>
    private const int RowMask = 0x00FFFFFF;

    /// <summary>The PDB readers found so far by assembly name; <see langword="null"/> when the assembly has no PDB.</summary>
    private readonly Dictionary<string, OpenPdb?> _pdbs = [with(StringComparer.OrdinalIgnoreCase)];

    /// <summary>The lines already resolved, by method and IL offset.</summary>
    private readonly Dictionary<LineKey, string?> _lines = [];

    /// <summary>The folder searched for the assemblies.</summary>
    private readonly string _folder;

    /// <summary>Initializes a new instance of the <see cref="SourceLines"/> class.</summary>
    /// <param name="folder">The folder holding the built assemblies, searched with its subfolders.</param>
    internal SourceLines(string folder) => _folder = folder;

    /// <summary>Opens the lookup from <see cref="SymbolsVariable"/>.</summary>
    /// <returns>The lookup, or <see langword="null"/> when the variable is not set.</returns>
    internal static SourceLines? FromEnvironment() =>
        Environment.GetEnvironmentVariable(SymbolsVariable) is { Length: > 0 } folder && Directory.Exists(folder) ? new(folder) : null;

    /// <summary>Finds the source line a frame was running.</summary>
    /// <param name="address">The frame's code address.</param>
    /// <returns>The file and line such as <c>Rc4.cs:41</c>, or <see langword="null"/> when the frame has no source.</returns>
    internal string? Find(TraceCodeAddress address)
    {
        if (address.Method is not { } method || address.ILOffset < 0 || string.IsNullOrEmpty(address.ModuleName))
        {
            return null;
        }

        var key = new LineKey(address.ModuleName, method.MethodToken, address.ILOffset);
        if (_lines.TryGetValue(key, out var known))
        {
            return known;
        }

        var line = Resolve(key);
        _lines[key] = line;
        return line;
    }

    /// <summary>Reads the name of a source file from the PDB.</summary>
    /// <param name="reader">The PDB.</param>
    /// <param name="document">The document.</param>
    /// <returns>The file name without its folders.</returns>
    private static string FileName(MetadataReader reader, DocumentHandle document)
    {
        var blob = reader.GetBlobReader(reader.GetDocument(document).Name);
        _ = blob.ReadByte();
        var last = string.Empty;
        while (blob.RemainingBytes > 0)
        {
            var part = reader.GetBlobReader(blob.ReadBlobHandle());
            last = part.Length == 0 ? last : part.ReadUTF8(part.Length);
        }

        return last;
    }

    /// <summary>Reads the portable PDB embedded in an assembly.</summary>
    /// <param name="path">The assembly.</param>
    /// <returns>The PDB, or <see langword="null"/> when the assembly embeds none.</returns>
    private static MetadataReaderProvider? ReadEmbedded(string path)
    {
        using var stream = File.OpenRead(path);
        using var image = new PEReader(stream);
        foreach (var entry in image.ReadDebugDirectory())
        {
            if (entry.Type == DebugDirectoryEntryType.EmbeddedPortablePdb)
            {
                return image.ReadEmbeddedPortablePdbDebugDirectoryData(entry);
            }
        }

        return null;
    }

    /// <summary>Looks a line up in the PDB of an assembly.</summary>
    /// <param name="key">The method and IL offset.</param>
    /// <returns>The file and line, or <see langword="null"/> when there is none.</returns>
    private string? Resolve(LineKey key)
    {
        if (Open(key.Module) is not { } pdb)
        {
            return null;
        }

        var handle = MetadataTokens.MethodDebugInformationHandle(key.Token & RowMask);
        var best = default(SequencePoint);
        var found = false;
        foreach (var point in pdb.Reader.GetMethodDebugInformation(handle).GetSequencePoints())
        {
            if (point.Offset > key.Offset)
            {
                break;
            }

            if (point.IsHidden)
            {
                continue;
            }

            best = point;
            found = true;
        }

        return found ? $"{FileName(pdb.Reader, best.Document)}:{best.StartLine}" : null;
    }

    /// <summary>Opens the PDB of an assembly once.</summary>
    /// <param name="module">The assembly's name.</param>
    /// <returns>The reader, or <see langword="null"/> when no PDB was found.</returns>
    private OpenPdb? Open(string module)
    {
        if (_pdbs.TryGetValue(module, out var open))
        {
            return open;
        }

        OpenPdb? opened = null;
        var file = module.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? module : $"{module}.dll";
        foreach (var path in Directory.EnumerateFiles(_folder, file, SearchOption.AllDirectories))
        {
            if (ReadEmbedded(path) is not { } provider)
            {
                continue;
            }

            opened = new(provider, provider.GetMetadataReader());
            break;
        }

        _pdbs[module] = opened;
        return opened;
    }

    /// <summary>One method at one IL offset of one assembly.</summary>
    /// <param name="Module">The assembly's name.</param>
    /// <param name="Token">The method's metadata token.</param>
    /// <param name="Offset">The IL offset.</param>
    private readonly record struct LineKey(string Module, int Token, int Offset);

    /// <summary>A PDB that has been opened.</summary>
    /// <param name="Provider">The owner of the PDB's memory.</param>
    /// <param name="Reader">The reader over it.</param>
    private sealed record OpenPdb(MetadataReaderProvider Provider, MetadataReader Reader);
}
