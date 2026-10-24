// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Builds the reference picture lists of a slice from the reference picture set (8.3.4).</summary>
internal static class H265RefListBuilder
{
    /// <summary>Builds both lists and records them in the slice context and in the picture.</summary>
    /// <param name="context">The slice context, whose header is set.</param>
    /// <param name="set">The reference picture set of the picture.</param>
    /// <exception cref="InvalidDataException">A P or B slice has no reference picture to refer to.</exception>
    internal static void Build(H265SliceContext context, H265RefPicSet set)
    {
        var header = context.Header;
        context.RefList0.Clear();
        context.RefList1.Clear();
        if (header.SliceType != H265SliceType.I)
        {
            var total = set.StCurrBefore.Length + set.StCurrAfter.Length + set.LtCurr.Length;
            if (total == 0)
            {
                throw new InvalidDataException("An H.265 inter slice has no reference pictures.");
            }

            BuildList(context, context.RefList0, new(header.Refs.NumRefIdxL0, header.Refs.ModificationL0), set.StCurrBefore, set.StCurrAfter, set.LtCurr);
            if (header.SliceType == H265SliceType.B)
            {
                BuildList(context, context.RefList1, new(header.Refs.NumRefIdxL1, header.Refs.ModificationL1), set.StCurrAfter, set.StCurrBefore, set.LtCurr);
            }
        }

        context.NoBackwardPred = IsNoBackward(context);
        context.Picture.SliceRefs.Add(Describe(context));
    }

    /// <summary>Tells whether every reference picture of the slice comes before the current picture or at the same time in output order.</summary>
    /// <param name="context">The slice context.</param>
    /// <returns>The NoBackwardPredFlag.</returns>
    private static bool IsNoBackward(H265SliceContext context)
    {
        var poc = context.Picture.Poc;
        foreach (var picture in context.RefList0)
        {
            if (picture.Poc > poc)
            {
                return false;
            }
        }

        foreach (var picture in context.RefList1)
        {
            if (picture.Poc > poc)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Builds one list (8-8 and 8-10): the three groups in turn, repeated to fill the list, then optionally reordered.</summary>
    /// <param name="context">The slice context, whose work area holds the initial list.</param>
    /// <param name="list">Receives the list, which must be empty.</param>
    /// <param name="shape">The length of the list and its reordering.</param>
    /// <param name="first">The group that comes first.</param>
    /// <param name="second">The group that comes second.</param>
    /// <param name="longTerm">The long-term group.</param>
    private static void BuildList(H265SliceContext context, List<H265Picture> list, H265ListShape shape, H265Picture[] first, H265Picture[] second, H265Picture[] longTerm)
    {
        var length = Math.Max(shape.Count, first.Length + second.Length + longTerm.Length);
        var initial = context.RefInitial;
        initial.Clear();
        while (initial.Count < length)
        {
            Append(initial, length, first);
            Append(initial, length, second);
            Append(initial, length, longTerm);
        }

        for (var i = 0; i < shape.Count; i++)
        {
            list.Add(shape.Modification is null ? initial[i] : initial[shape.Modification[i]]);
        }
    }

    /// <summary>Appends a group to the initial list, stopping when the list is full.</summary>
    /// <param name="target">The initial list.</param>
    /// <param name="length">The length the initial list grows to.</param>
    /// <param name="group">The group.</param>
    private static void Append(List<H265Picture> target, int length, H265Picture[] group)
    {
        for (var i = 0; i < group.Length && target.Count < length; i++)
        {
            target.Add(group[i]);
        }
    }

    /// <summary>Describes the lists as order counts and long-term flags, which later pictures need to follow the motion.</summary>
    /// <param name="context">The slice context.</param>
    /// <returns>The description.</returns>
    private static H265SliceRefs Describe(H265SliceContext context) =>
        new(PocsOf(context.RefList0), LongTermFlagsOf(context.RefList0), PocsOf(context.RefList1), LongTermFlagsOf(context.RefList1));

    /// <summary>Lists the picture order counts of a reference list.</summary>
    /// <param name="list">The list.</param>
    /// <returns>The counts.</returns>
    private static int[] PocsOf(List<H265Picture> list)
    {
        var pocs = new int[list.Count];
        for (var i = 0; i < pocs.Length; i++)
        {
            pocs[i] = list[i].Poc;
        }

        return pocs;
    }

    /// <summary>Flags which pictures of a reference list are long-term references.</summary>
    /// <param name="list">The list.</param>
    /// <returns>The flags.</returns>
    private static bool[] LongTermFlagsOf(List<H265Picture> list)
    {
        var flags = new bool[list.Count];
        for (var i = 0; i < flags.Length; i++)
        {
            flags[i] = list[i].IsLongTerm;
        }

        return flags;
    }
}
