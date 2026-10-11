// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdfLibrary.Writing;

/// <summary>Writes page-offset and shared-object hint tables from a measured file layout.</summary>
internal static class PdfLinearizationHintWriter
{
    /// <summary>The bit width of a 16-bit hint field.</summary>
    private const int ShortBits = 16;

    /// <summary>The bit width of a 32-bit hint field.</summary>
    private const int IntBits = 32;

    /// <summary>The shared-object denominator used for zero numerators.</summary>
    private const uint SharedDenominator = 4;

    /// <summary>Writes the hint stream and returns the shared-table byte offset.</summary>
    /// <param name="plan">The page and shared-object groups.</param>
    /// <param name="layout">The measured offsets of a preceding pass.</param>
    /// <param name="sharedOffset">The byte offset of the shared-object table.</param>
    /// <param name="outlineOffset">The byte offset of the outlines table, or zero when absent.</param>
    /// <returns>The uncompressed hint stream.</returns>
    internal static byte[] Build(PdfLinearizationPlan plan, PdfLinearizationLayout layout, out int sharedOffset, out int outlineOffset)
    {
        var lengths = PageLengths(layout);
        var shared = SharedObjectNumbers(plan);
        var sharedLengths = SharedLengths(plan, layout, shared);
        var bits = new PdfHintBitWriter();
        WritePageTable(bits, plan, layout, lengths, shared.Length);
        sharedOffset = bits.ByteCount;
        WriteSharedTable(bits, plan, layout, sharedLengths);
        outlineOffset = WriteOutlineTable(bits, plan, layout);
        return bits.ToArray();
    }

    /// <summary>Writes the optional outline group location after the shared-object table.</summary>
    /// <param name="writer">The bit writer.</param>
    /// <param name="plan">The outline object group.</param>
    /// <param name="layout">The preceding pass.</param>
    /// <returns>The table offset, or zero when the document has no indirect outlines.</returns>
    private static int WriteOutlineTable(PdfHintBitWriter writer, PdfLinearizationPlan plan, PdfLinearizationLayout layout)
    {
        if (plan.Outline.Length == 0)
        {
            return 0;
        }

        var offset = writer.ByteCount;
        var first = plan[plan.Outline[0]];
        var length = 0L;
        foreach (var graphNumber in plan.Outline)
        {
            length += layout.ObjectLengths[plan[graphNumber]];
        }

        writer.Write((uint)first, IntBits);
        writer.Write(PositiveField(layout.ObjectOffsets[first] - layout.HintLength), IntBits);
        writer.Write((uint)plan.Outline.Length, IntBits);
        writer.Write(PositiveField(length), IntBits);
        return offset;
    }

    /// <summary>Gets each page group's measured byte length, using placeholders before the first pass.</summary>
    /// <param name="layout">The preceding pass.</param>
    /// <returns>Page group lengths.</returns>
    private static uint[] PageLengths(PdfLinearizationLayout layout)
    {
        var lengths = new uint[layout.PageStarts.Length];
        for (var index = 0; index < lengths.Length; index++)
        {
            lengths[index] = PositiveField(layout.PageEnds[index] - layout.PageStarts[index]);
        }

        return lengths;
    }

    /// <summary>Gets all shared-table objects: the first-page group, then shared objects of later pages.</summary>
    /// <param name="plan">The planned groups.</param>
    /// <returns>Graph object numbers in shared-table order.</returns>
    private static int[] SharedObjectNumbers(PdfLinearizationPlan plan)
    {
        var first = plan.PageGroups[0];
        var numbers = new int[first.Length + plan.SharedOther.Length];
        first.CopyTo(numbers, 0);
        plan.SharedOther.CopyTo(numbers, first.Length);
        return numbers;
    }

    /// <summary>Gets each shared object's measured byte length.</summary>
    /// <param name="plan">The planned numbering.</param>
    /// <param name="layout">The preceding pass.</param>
    /// <param name="shared">The shared-table graph numbers.</param>
    /// <returns>Object lengths in table order.</returns>
    private static uint[] SharedLengths(PdfLinearizationPlan plan, PdfLinearizationLayout layout, int[] shared)
    {
        var lengths = new uint[shared.Length];
        for (var index = 0; index < shared.Length; index++)
        {
            lengths[index] = PositiveField(layout.ObjectLengths[plan[shared[index]]]);
        }

        return lengths;
    }

    /// <summary>Writes the page header and its six packed rows.</summary>
    /// <param name="writer">The bit writer.</param>
    /// <param name="plan">The page groups.</param>
    /// <param name="layout">The preceding pass.</param>
    /// <param name="lengths">The page byte lengths.</param>
    /// <param name="sharedCount">The number of shared-table entries.</param>
    private static void WritePageTable(PdfHintBitWriter writer, PdfLinearizationPlan plan, PdfLinearizationLayout layout, uint[] lengths, int sharedCount)
    {
        var leastObjects = int.MaxValue;
        var mostObjects = 0;
        var mostShared = 0;
        foreach (var group in plan.PageGroups)
        {
            leastObjects = Math.Min(leastObjects, group.Length);
            mostObjects = Math.Max(mostObjects, group.Length);
        }

        foreach (var identifiers in plan.SharedIdentifiers)
        {
            mostShared = Math.Max(mostShared, identifiers.Length);
        }

        var leastLength = lengths[0];
        var mostLength = lengths[0];
        foreach (var length in lengths)
        {
            leastLength = Math.Min(leastLength, length);
            mostLength = Math.Max(mostLength, length);
        }

        var objectBits = Width((uint)(mostObjects - leastObjects));
        var lengthBits = Width(mostLength - leastLength);
        var sharedBits = Width((uint)mostShared);
        var identifierBits = Width((uint)sharedCount);
        var firstPosition = PositiveField(layout.PageStarts[0] - layout.HintLength);
        var stats = new PageHintStats((uint)leastObjects, firstPosition, objectBits, leastLength, lengthBits, sharedBits, identifierBits);
        WritePageHeader(writer, stats);
        WritePageRows(writer, plan, lengths, stats);
    }

    /// <summary>Writes the fixed page-offset table header.</summary>
    /// <param name="writer">The bit writer.</param>
    /// <param name="stats">The measured bounds and field widths.</param>
    private static void WritePageHeader(PdfHintBitWriter writer, PageHintStats stats)
    {
        writer.Write(stats.LeastObjects, IntBits);
        writer.Write(stats.FirstPosition, IntBits);
        writer.Write((uint)stats.ObjectBits, ShortBits);
        writer.Write(stats.LeastLength, IntBits);
        writer.Write((uint)stats.LengthBits, ShortBits);
        writer.Write(0, IntBits);
        writer.Write(0, ShortBits);
        writer.Write(stats.LeastLength, IntBits);
        writer.Write((uint)stats.LengthBits, ShortBits);
        writer.Write((uint)stats.SharedBits, ShortBits);
        writer.Write((uint)stats.IdentifierBits, ShortBits);
        writer.Write(0, ShortBits);
        writer.Write(SharedDenominator, ShortBits);
    }

    /// <summary>Writes byte-aligned object, length, shared and content rows.</summary>
    /// <param name="writer">The bit writer.</param>
    /// <param name="plan">The page groups and identifiers.</param>
    /// <param name="lengths">The page lengths.</param>
    /// <param name="stats">The measured bounds and field widths.</param>
    private static void WritePageRows(PdfHintBitWriter writer, PdfLinearizationPlan plan, uint[] lengths, PageHintStats stats)
    {
        foreach (var group in plan.PageGroups)
        {
            writer.Write((uint)(group.Length - stats.LeastObjects), stats.ObjectBits);
        }

        writer.Align();
        WriteLengthDeltas(writer, lengths, stats.LeastLength, stats.LengthBits);
        foreach (var identifiers in plan.SharedIdentifiers)
        {
            writer.Write((uint)identifiers.Length, stats.SharedBits);
        }

        writer.Align();
        foreach (var identifiers in plan.SharedIdentifiers)
        {
            foreach (var identifier in identifiers)
            {
                writer.Write((uint)identifier, stats.IdentifierBits);
            }
        }

        writer.Align();
        WriteLengthDeltas(writer, lengths, stats.LeastLength, stats.LengthBits);
    }

    /// <summary>Writes the shared-object table and one length entry for each object.</summary>
    /// <param name="writer">The bit writer.</param>
    /// <param name="plan">The shared-object groups.</param>
    /// <param name="layout">The preceding pass.</param>
    /// <param name="lengths">The shared-object byte lengths.</param>
    private static void WriteSharedTable(PdfHintBitWriter writer, PdfLinearizationPlan plan, PdfLinearizationLayout layout, uint[] lengths)
    {
        var leastLength = lengths[0];
        var mostLength = lengths[0];
        foreach (var length in lengths)
        {
            leastLength = Math.Min(leastLength, length);
            mostLength = Math.Max(mostLength, length);
        }

        var lengthBits = Width(mostLength - leastLength);
        var hasOther = plan.SharedOther.Length > 0;
        writer.Write(hasOther ? (uint)plan[plan.SharedOther[0]] : 0, IntBits);
        writer.Write(hasOther ? PositiveField(layout.ObjectOffsets[plan[plan.SharedOther[0]]] - layout.HintLength) : 0, IntBits);
        writer.Write((uint)plan.PageGroups[0].Length, IntBits);
        writer.Write((uint)lengths.Length, IntBits);
        writer.Write(0, ShortBits);
        writer.Write(leastLength, IntBits);
        writer.Write((uint)lengthBits, ShortBits);
        WriteLengthDeltas(writer, lengths, leastLength, lengthBits);
        for (var index = 0; index < lengths.Length; index++)
        {
            writer.Write(0, 1);
        }

        writer.Align();
    }

    /// <summary>Writes a byte-aligned row of lengths less the row minimum.</summary>
    /// <param name="writer">The bit writer.</param>
    /// <param name="lengths">The lengths.</param>
    /// <param name="least">The row minimum.</param>
    /// <param name="width">The encoded delta width.</param>
    private static void WriteLengthDeltas(PdfHintBitWriter writer, uint[] lengths, uint least, int width)
    {
        foreach (var length in lengths)
        {
            writer.Write(length - least, width);
        }

        writer.Align();
    }

    /// <summary>Gets a positive 32-bit hint field, using one until offsets are measured.</summary>
    /// <param name="value">The measured value.</param>
    /// <returns>The bounded field.</returns>
    /// <exception cref="PdfException">The field needs more than 32 bits.</exception>
    private static uint PositiveField(long value)
    {
        if (value > uint.MaxValue)
        {
            throw new PdfException(PdfError.Format, "A linearization hint exceeds its 32-bit field.");
        }

        return value > 0 ? (uint)value : 1;
    }

    /// <summary>Gets the bits needed for the largest unsigned delta.</summary>
    /// <param name="maximum">The largest delta.</param>
    /// <returns>The bit width.</returns>
    private static int Width(uint maximum) => maximum == 0 ? 0 : BitOperations.Log2(maximum) + 1;

    /// <summary>The bounds and widths of a page-offset hint table.</summary>
    /// <param name="LeastObjects">The fewest objects on a page.</param>
    /// <param name="FirstPosition">The first-page object offset without the hint object.</param>
    /// <param name="ObjectBits">The object-count delta width.</param>
    /// <param name="LeastLength">The shortest page group.</param>
    /// <param name="LengthBits">The page-length delta width.</param>
    /// <param name="SharedBits">The shared-count width.</param>
    /// <param name="IdentifierBits">The shared-identifier width.</param>
    private readonly record struct PageHintStats(
        uint LeastObjects,
        uint FirstPosition,
        int ObjectBits,
        uint LeastLength,
        int LengthBits,
        int SharedBits,
        int IdentifierBits);
}
