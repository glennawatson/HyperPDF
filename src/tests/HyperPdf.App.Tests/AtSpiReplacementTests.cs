// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace HyperPdf.App.Tests;

/// <summary>
/// Checks that the stand-in for Avalonia's AT-SPI assembly has every member the Avalonia backends call. A backend that calls a member the
/// stand-in lacks would fail when the app starts, so an Avalonia upgrade that adds a call fails here first.
/// </summary>
public sealed class AtSpiReplacementTests
{
    /// <summary>The name of the assembly the stand-in replaces.</summary>
    private const string ReplacedAssembly = "Avalonia.FreeDesktop.AtSpi";

    /// <summary>Verifies that every member an Avalonia assembly refers to exists in the stand-in, and that its version is the one Avalonia asks for.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EveryMemberAvaloniaCallsExists()
    {
        var stand = typeof(Avalonia.FreeDesktop.AtSpi.AtSpiServer).Assembly;
        var missing = new List<string>();
        var asked = new List<Version>();
        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "Avalonia.*.dll"))
        {
            if (!string.Equals(Path.GetFileNameWithoutExtension(path), ReplacedAssembly, StringComparison.Ordinal))
            {
                Check(path, stand, missing, asked);
            }
        }

        await Assert.That(missing).IsEmpty();
        foreach (var version in asked)
        {
            await Assert.That(stand.GetName().Version!).IsGreaterThanOrEqualTo(version);
        }
    }

    /// <summary>Reads one assembly's references to the replaced assembly and records what the stand-in lacks.</summary>
    /// <param name="path">The assembly to read.</param>
    /// <param name="stand">The stand-in assembly.</param>
    /// <param name="missing">The members the stand-in lacks, which this adds to.</param>
    /// <param name="asked">The versions of the replaced assembly the reader asks for, which this adds to.</param>
    private static void Check(string path, Assembly stand, List<string> missing, List<Version> asked)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        if (!pe.HasMetadata)
        {
            return;
        }

        var metadata = pe.GetMetadataReader();
        foreach (var handle in metadata.AssemblyReferences)
        {
            var reference = metadata.GetAssemblyReference(handle);
            if (string.Equals(metadata.GetString(reference.Name), ReplacedAssembly, StringComparison.Ordinal))
            {
                asked.Add(reference.Version);
            }
        }

        foreach (var handle in metadata.MemberReferences)
        {
            var member = metadata.GetMemberReference(handle);
            if (IsFromReplaced(metadata, member, out var typeName) && !HasMember(stand, typeName, metadata.GetString(member.Name)))
            {
                missing.Add($"{Path.GetFileName(path)}: {typeName}.{metadata.GetString(member.Name)}");
            }
        }
    }

    /// <summary>Checks whether a member reference points into the replaced assembly.</summary>
    /// <param name="metadata">The referring assembly's metadata.</param>
    /// <param name="member">The member reference.</param>
    /// <param name="typeName">The full name of the type the member belongs to.</param>
    /// <returns>Whether the member belongs to the replaced assembly.</returns>
    private static bool IsFromReplaced(MetadataReader metadata, MemberReference member, out string typeName)
    {
        typeName = string.Empty;
        if (member.Parent.Kind != HandleKind.TypeReference)
        {
            return false;
        }

        var type = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
        if (type.ResolutionScope.Kind != HandleKind.AssemblyReference
            || !string.Equals(metadata.GetString(metadata.GetAssemblyReference((AssemblyReferenceHandle)type.ResolutionScope).Name), ReplacedAssembly, StringComparison.Ordinal))
        {
            return false;
        }

        typeName = $"{metadata.GetString(type.Namespace)}.{metadata.GetString(type.Name)}";
        return true;
    }

    /// <summary>Checks that the stand-in has a type with a member of a name.</summary>
    /// <param name="stand">The stand-in assembly.</param>
    /// <param name="typeName">The full name of the type.</param>
    /// <param name="name">The member's name.</param>
    /// <returns>Whether the stand-in has it.</returns>
    private static bool HasMember(Assembly stand, string typeName, string name)
    {
        var type = stand.GetType(typeName);
        if (type is null)
        {
            return false;
        }

        // The stand-in's types and members are public, because Avalonia's backend calls them from another assembly.
        const BindingFlags all = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
        return type.GetMember(name, all).Length > 0;
    }
}
