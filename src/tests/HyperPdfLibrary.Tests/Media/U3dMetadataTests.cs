// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Media.U3d;

namespace HyperPdfLibrary.Tests.Media;

/// <summary>Checks that meta data is recorded as text and that an action or file it names is never run or opened.</summary>
public sealed class U3dMetadataTests
{
    /// <summary>The block type of a group node.</summary>
    private const uint GroupType = 0xFFFFFF21;

    /// <summary>The attribute flag for a binary value.</summary>
    private const uint Binary = 1;

    /// <summary>The attribute flag for a hidden value.</summary>
    private const uint Hidden = 2;

    /// <summary>The attribute flag for an action.</summary>
    private const uint Action = 0x100;

    /// <summary>The attribute flag for a file.</summary>
    private const uint FileFlag = 0x200;

    /// <summary>The most entries kept.</summary>
    private const int MostEntries = 1_000;

    /// <summary>A count no section can hold.</summary>
    private const uint HugeCount = 4_000_000_000;

    /// <summary>The entries written in the first test.</summary>
    private const int EntryCount = 4;

    /// <summary>The index of the action entry.</summary>
    private const int ActionIndex = 2;

    /// <summary>A binary value's size.</summary>
    private const uint BinarySize = 3;

    /// <summary>Text, hidden, action and file entries are all recorded, and the ones that ask to be run or opened are marked so and left alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecordsEntriesWithoutRunningThem()
    {
        var writer = new U3dFileWriter(false);
        var meta = U3dFileWriter.Meta(new(0, "Author", "Ada"), new(Hidden, "Secret", "x"), new(Action, "OnOpen", "javascript:run()"), new(FileFlag, "Open", "payload.exe"));
        var file = writer.Add(U3dFileWriter.Block(GroupType, new U3dValueWriter(true).String("Root").U32(0).ToArray(), meta)).ToArray();

        var model = U3dModelReader.Read(file);

        await Assert.That(model.Metadata.Length).IsEqualTo(EntryCount);
        await Assert.That(model.Metadata[0].Value).IsEqualTo("Ada");
        await Assert.That(model.Metadata[0].AsksToRun).IsFalse();
        await Assert.That(model.Metadata[ActionIndex].AsksToRun).IsTrue();
        await Assert.That(model.Metadata[ActionIndex].Value).IsEqualTo("javascript:run()");
        await Assert.That(model.Metadata[ActionIndex + 1].AsksToRun).IsTrue();
        await Assert.That(model.Nodes.Single().Name).IsEqualTo("Root");
    }

    /// <summary>A binary value is skipped and kept as an empty text value.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SkipsBinaryValues()
    {
        var meta = new U3dValueWriter(true).U32(1).U32(Binary).String("Blob").U32(BinarySize).U8(1).U8(1).U8(1).ToArray();
        var writer = new U3dFileWriter(false);

        var model = U3dModelReader.Read(writer.Add(U3dFileWriter.Block(GroupType, new U3dValueWriter(true).String("Root").U32(0).ToArray(), meta)).ToArray());

        await Assert.That(model.Metadata.Single().Key).IsEqualTo("Blob");
        await Assert.That(model.Metadata.Single().Value).IsEmpty();
        await Assert.That(model.Metadata.Single().Attributes).IsEqualTo(Binary);
    }

    /// <summary>A section with a count it cannot hold, a binary size that runs past it, or that is cut short is refused with a reason.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesDamagedSections()
    {
        var node = new U3dValueWriter(true).String("Root").U32(0).ToArray();
        var huge = new U3dValueWriter(true).U32(HugeCount).ToArray();
        var overrun = new U3dValueWriter(true).U32(1).U32(Binary).String("Blob").U32(HugeCount).ToArray();
        var cut = new U3dValueWriter(true).U32(1).U32(0).String("Key").ToArray();

        foreach (var meta in new[] { huge, overrun, cut })
        {
            var model = U3dModelReader.Read(new U3dFileWriter(false).Add(U3dFileWriter.Block(GroupType, node, meta)).ToArray());

            await Assert.That(model.Metadata).IsEmpty();
            await Assert.That(model.Problems.Length).IsGreaterThan(0);
            await Assert.That(model.Nodes.Single().Name).IsEqualTo("Root");
        }
    }

    /// <summary>Only the first thousand entries of a file are kept.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsOnlyTheFirstEntries()
    {
        var entries = new U3dMetaEntry[MostEntries + 1];
        Array.Fill(entries, default(U3dMetaEntry) with { Key = "k", Value = "v" });
        var meta = U3dFileWriter.Meta(entries);
        var node = new U3dValueWriter(true).String("Root").U32(0).ToArray();

        var model = U3dModelReader.Read(new U3dFileWriter(false).Add(U3dFileWriter.Block(GroupType, node, meta)).ToArray());

        await Assert.That(model.Metadata.Length).IsEqualTo(MostEntries);
    }
}
