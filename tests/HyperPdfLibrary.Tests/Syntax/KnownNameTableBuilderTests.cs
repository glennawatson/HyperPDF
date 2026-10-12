// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Scripts;

namespace HyperPdfLibrary.Tests.Syntax;

/// <summary>Checks that known-name tables reproduce their documented fixed spellings and ids.</summary>
public sealed class KnownNameTableBuilderTests
{
    /// <summary>The checked-in blob and perfect hash reproduce the enum's documented names.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CheckedInTablesMatchEnumSpellings()
    {
        var source = await ReadResourceAsync("KnownName.cs");
        var expected = await ReadResourceAsync("KnownNameSpellings.cs");
        await Assert.That(KnownNameTableBuilder.Emit(source).ReplaceLineEndings("\n")).IsEqualTo(expected.ReplaceLineEndings("\n"));
    }

    /// <summary>Unknown source ids cannot silently change the fixed name-id mapping.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NonContiguousIdsAreRejected()
    {
        const string Source = "public enum KnownName\n{\n    /// <summary>The <c>/Type</c> name.</summary>\n    Type = 2,\n}\n";
        await Assert.That(static () => KnownNameTableBuilder.Emit(Source)).Throws<InvalidDataException>();
    }

    /// <summary>A new name colliding with the sampled hash requires a stronger hash before tables can be regenerated.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CollidingEdgeHashesAreRejected()
    {
        const string Source = "public enum KnownName\n{\n    /// <summary>The <c>/ABCDAWXYZ</c> name.</summary>\n    First = 1,\n"
            + "    /// <summary>The <c>/ABCDBWXYZ</c> name.</summary>\n    Second = 2,\n}\n";
        await Assert.That(static () => KnownNameTableBuilder.Emit(Source)).Throws<InvalidDataException>();
    }

    /// <summary>Reads the source snapshots embedded for deterministic regeneration checks.</summary>
    /// <param name="name">The resource name.</param>
    /// <returns>The source text.</returns>
    private static async Task<string> ReadResourceAsync(string name)
    {
        await using var stream = typeof(KnownNameTableBuilderTests).Assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
