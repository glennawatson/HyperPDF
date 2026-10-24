// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.H264;

/// <summary>The reference frames kept for inter prediction: their marking (clause 8.2.5) and the reference lists built from them (clause 8.2.4). Frames only.</summary>
[DebuggerDisplay("H264Dpb: {_frames.Count} reference frames")]
internal sealed class H264Dpb
{
    /// <summary>The modification command that moves a picture number down.</summary>
    private const int SubtractShortTerm = 0;

    /// <summary>The modification command that moves a picture number up.</summary>
    private const int AddShortTerm = 1;

    /// <summary>The modification command that picks a long-term picture.</summary>
    private const int PickLongTerm = 2;

    /// <summary>The memory management operation that marks a short-term frame unused.</summary>
    private const int MmcoShortToUnused = 1;

    /// <summary>The memory management operation that marks a long-term frame unused.</summary>
    private const int MmcoLongToUnused = 2;

    /// <summary>The memory management operation that turns a short-term frame into a long-term one.</summary>
    private const int MmcoShortToLong = 3;

    /// <summary>The memory management operation that sets the largest long-term index.</summary>
    private const int MmcoMaxLongTerm = 4;

    /// <summary>The memory management operation that marks every frame unused.</summary>
    private const int MmcoClear = 5;

    /// <summary>The memory management operation that makes the current frame a long-term one.</summary>
    private const int MmcoCurrentToLong = 6;

    /// <summary>The frames marked as used for reference, short-term and long-term.</summary>
    private readonly List<H264RefPicture> _frames = [];

    /// <summary>The largest long-term frame index, or -1 when there are no long-term indices.</summary>
    private int _maxLongTermFrameIdx = -1;

    /// <summary>Gets the number of reference frames.</summary>
    internal int Count => _frames.Count;

    /// <summary>Gets the frames marked as used for reference, short-term and long-term.</summary>
    internal IReadOnlyList<H264RefPicture> Frames => _frames;

    /// <summary>Finds whether a picture is still kept for reference.</summary>
    /// <param name="picture">The picture.</param>
    /// <returns><see langword="true"/> when a later picture may refer to it.</returns>
    internal bool Holds(H264Picture picture)
    {
        foreach (var frame in _frames)
        {
            if (ReferenceEquals(frame.Picture, picture))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Forgets every frame.</summary>
    internal void Clear()
    {
        _frames.Clear();
        _maxLongTermFrameIdx = -1;
    }

    /// <summary>Builds reference list 0 of a P slice.</summary>
    /// <param name="header">The slice header.</param>
    /// <param name="sps">The sequence parameter set.</param>
    /// <returns>The list, as long as the slice says; empty places repeat the first frame.</returns>
    /// <exception cref="InvalidDataException">The slice refers to frames that are not there.</exception>
    internal H264RefPicture[] BuildList0(H264SliceHeader header, H264Sps sps)
    {
        var maxFrameNum = 1 << sps.Log2MaxFrameNum;
        var currentFrameNum = header.Picture.FrameNum;
        UpdateWraps(currentFrameNum, maxFrameNum);
        var count = header.Refs.NumRefIdxL0;
        var initial = new List<H264RefPicture>(_frames.Count);
        foreach (var frame in _frames)
        {
            if (!frame.IsLongTerm)
            {
                initial.Add(frame);
            }
        }

        initial.Sort(static (a, b) => b.FrameNumWrap.CompareTo(a.FrameNumWrap));
        var longTerm = new List<H264RefPicture>();
        foreach (var frame in _frames)
        {
            if (frame.IsLongTerm)
            {
                longTerm.Add(frame);
            }
        }

        longTerm.Sort(static (a, b) => a.LongTermFrameIdx.CompareTo(b.LongTermFrameIdx));
        initial.AddRange(longTerm);
        var list = new H264RefPicture?[count + 1];
        for (var index = 0; index < Math.Min(count, initial.Count); index++)
        {
            list[index] = initial[index];
        }

        Modify(list, header.Refs.ModificationsL0, count, currentFrameNum, maxFrameNum);
        return Complete(list, count);
    }

    /// <summary>Builds both reference lists of a B slice from picture order counts (clause 8.2.4.2.3).</summary>
    /// <param name="header">The slice header.</param>
    /// <param name="sps">The sequence parameter set.</param>
    /// <param name="currentPoc">The picture order count of the picture being decoded.</param>
    /// <returns>The two lists, as long as the slice says.</returns>
    /// <exception cref="InvalidDataException">The slice refers to frames that are not there.</exception>
    internal H264RefLists BuildListsB(H264SliceHeader header, H264Sps sps, int currentPoc)
    {
        var maxFrameNum = 1 << sps.Log2MaxFrameNum;
        var currentFrameNum = header.Picture.FrameNum;
        UpdateWraps(currentFrameNum, maxFrameNum);
        var before = new List<H264RefPicture>();
        var after = new List<H264RefPicture>();
        var longTerm = new List<H264RefPicture>();
        foreach (var frame in _frames)
        {
            if (frame.IsLongTerm)
            {
                longTerm.Add(frame);
            }
            else if (frame.Picture.Poc < currentPoc)
            {
                before.Add(frame);
            }
            else
            {
                after.Add(frame);
            }
        }

        before.Sort(static (a, b) => b.Picture.Poc.CompareTo(a.Picture.Poc));
        after.Sort(static (a, b) => a.Picture.Poc.CompareTo(b.Picture.Poc));
        longTerm.Sort(static (a, b) => a.LongTermFrameIdx.CompareTo(b.LongTermFrameIdx));
        var initial0 = new List<H264RefPicture>([.. before, .. after, .. longTerm]);
        var initial1 = new List<H264RefPicture>([.. after, .. before, .. longTerm]);
        if (initial1.Count > 1 && SameOrder(initial0, initial1))
        {
            var first = initial1[0];
            initial1[0] = initial1[1];
            initial1[1] = first;
        }

        var count0 = header.Refs.NumRefIdxL0;
        var count1 = header.Refs.NumRefIdxL1;
        var list0 = FillList(initial0, count0);
        var list1 = FillList(initial1, count1);
        Modify(list0, header.Refs.ModificationsL0, count0, currentFrameNum, maxFrameNum);
        Modify(list1, header.Refs.ModificationsL1, count1, currentFrameNum, maxFrameNum);
        return new(Complete(list0, count0), Complete(list1, count1));
    }

    /// <summary>Marks the frame just decoded and adds it to the reference frames when it is a reference (clause 8.2.5).</summary>
    /// <param name="current">The frame just decoded.</param>
    /// <param name="header">The header of its slices.</param>
    /// <param name="sps">The sequence parameter set.</param>
    internal void Mark(H264RefPicture current, H264SliceHeader header, H264Sps sps)
    {
        if (header.NalRefIdc == 0)
        {
            return;
        }

        if (header.IsIdr)
        {
            Clear();
            if (header.Marking.LongTermReference)
            {
                current.IsLongTerm = true;
                current.LongTermFrameIdx = 0;
                _maxLongTermFrameIdx = 0;
            }

            _frames.Add(current);
            return;
        }

        UpdateWraps(header.Picture.FrameNum, 1 << sps.Log2MaxFrameNum);
        if (header.Marking.Adaptive)
        {
            ApplyOperations(current, header.Marking.Operations, header.Picture.FrameNum);
        }
        else
        {
            SlideWindow(sps);
        }

        _frames.Add(current);
    }

    /// <summary>Copies the start of an initial list into a list one place longer than the slice says.</summary>
    /// <param name="initial">The initial list.</param>
    /// <param name="count">The length the slice asks for.</param>
    /// <returns>The list, with a spare place at the end for the modification commands.</returns>
    private static H264RefPicture?[] FillList(List<H264RefPicture> initial, int count)
    {
        var list = new H264RefPicture?[count + 1];
        for (var index = 0; index < Math.Min(count, initial.Count); index++)
        {
            list[index] = initial[index];
        }

        return list;
    }

    /// <summary>Tells whether two lists hold the same frames in the same order.</summary>
    /// <param name="first">One list.</param>
    /// <param name="second">The other list.</param>
    /// <returns><see langword="true"/> when they do.</returns>
    private static bool SameOrder(List<H264RefPicture> first, List<H264RefPicture> second)
    {
        if (first.Count != second.Count)
        {
            return false;
        }

        for (var index = 0; index < first.Count; index++)
        {
            if (!ReferenceEquals(first[index], second[index]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Moves the predicted picture number by a modification command.</summary>
    /// <param name="predicted">The predicted picture number.</param>
    /// <param name="command">The command: subtract or add.</param>
    /// <param name="maxFrameNum">The range of picture numbers.</param>
    /// <returns>The new prediction, wrapped into range.</returns>
    /// <exception cref="InvalidDataException">The command is not valid.</exception>
    private static int StepPicNum(int predicted, H264RefModification command, int maxFrameNum)
    {
        var difference = command.Value + 1;
        return command.Idc switch
        {
            SubtractShortTerm => predicted < difference ? predicted - difference + maxFrameNum : predicted - difference,
            AddShortTerm => predicted + difference >= maxFrameNum ? predicted + difference - maxFrameNum : predicted + difference,
            _ => throw new InvalidDataException("An H.264 reference list command is not valid."),
        };
    }

    /// <summary>Puts a frame at a place in the list, moving the rest down and dropping the frame's earlier copy.</summary>
    /// <param name="list">The list, one place longer than <paramref name="count"/>.</param>
    /// <param name="count">The length of the list.</param>
    /// <param name="index">The place.</param>
    /// <param name="target">The frame.</param>
    private static void Insert(H264RefPicture?[] list, int count, int index, H264RefPicture target)
    {
        for (var place = count; place > index; place--)
        {
            list[place] = list[place - 1];
        }

        list[index] = target;
        var next = index + 1;
        for (var place = index + 1; place <= count; place++)
        {
            if (ReferenceEquals(list[place], target))
            {
                continue;
            }

            list[next] = list[place];
            next++;
        }
    }

    /// <summary>Takes the finished list, filling places that no frame reaches.</summary>
    /// <param name="list">The list, one place longer than <paramref name="count"/>.</param>
    /// <param name="count">The length of the list.</param>
    /// <returns>The list.</returns>
    /// <exception cref="InvalidDataException">There is no frame at all to refer to.</exception>
    private static H264RefPicture[] Complete(H264RefPicture?[] list, int count)
    {
        var first = list[0] ?? throw new InvalidDataException("An H.264 inter slice has no reference frame.");
        var result = new H264RefPicture[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = list[index] ?? first;
        }

        return result;
    }

    /// <summary>Works out the frame number of every short-term frame relative to the frame being decoded.</summary>
    /// <param name="currentFrameNum">The frame_num of the frame being decoded.</param>
    /// <param name="maxFrameNum">The range of frame numbers.</param>
    private void UpdateWraps(int currentFrameNum, int maxFrameNum)
    {
        foreach (var frame in _frames)
        {
            frame.FrameNumWrap = frame.FrameNum > currentFrameNum ? frame.FrameNum - maxFrameNum : frame.FrameNum;
        }
    }

    /// <summary>Removes the oldest short-term frame when the reference frames are full (clause 8.2.5.3).</summary>
    /// <param name="sps">The sequence parameter set.</param>
    private void SlideWindow(H264Sps sps)
    {
        if (_frames.Count < Math.Max(sps.MaxRefFrames, 1))
        {
            return;
        }

        H264RefPicture? oldest = null;
        foreach (var frame in _frames)
        {
            if (!frame.IsLongTerm && (oldest is null || frame.FrameNumWrap < oldest.FrameNumWrap))
            {
                oldest = frame;
            }
        }

        if (oldest is not null)
        {
            _ = _frames.Remove(oldest);
        }
    }

    /// <summary>Applies the memory management control operations of a slice header (clause 8.2.5.4).</summary>
    /// <param name="current">The frame just decoded.</param>
    /// <param name="operations">The operations.</param>
    /// <param name="currentFrameNum">The frame_num of the frame just decoded.</param>
    private void ApplyOperations(H264RefPicture current, H264Mmco[] operations, int currentFrameNum)
    {
        foreach (var operation in operations)
        {
            switch (operation.Operation)
            {
                case MmcoShortToUnused:
                {
                    _ = _frames.Remove(FindShortTerm(currentFrameNum - (operation.Difference + 1)));
                    break;
                }

                case MmcoLongToUnused:
                {
                    _ = _frames.RemoveAll(frame => frame.IsLongTerm && frame.LongTermFrameIdx == operation.LongTermPicNum);
                    break;
                }

                case MmcoShortToLong:
                {
                    var target = FindShortTerm(currentFrameNum - (operation.Difference + 1));
                    AssignLongTerm(target, operation.LongTermFrameIdx);
                    break;
                }

                case MmcoMaxLongTerm:
                {
                    _maxLongTermFrameIdx = operation.LongTermFrameIdx - 1;
                    _ = _frames.RemoveAll(frame => frame.IsLongTerm && frame.LongTermFrameIdx > _maxLongTermFrameIdx);
                    break;
                }

                case MmcoClear:
                {
                    Clear();
                    current.FrameNum = 0;
                    current.FrameNumWrap = 0;
                    break;
                }

                case MmcoCurrentToLong:
                {
                    AssignLongTerm(current, operation.LongTermFrameIdx);
                    break;
                }

                default:
                {
                    break;
                }
            }
        }
    }

    /// <summary>Finds the short-term frame that has a picture number.</summary>
    /// <param name="picNum">The picture number, which counts up through frame number wraps.</param>
    /// <returns>The frame.</returns>
    /// <exception cref="InvalidDataException">No such frame is marked.</exception>
    private H264RefPicture FindShortTerm(int picNum)
    {
        foreach (var frame in _frames)
        {
            if (!frame.IsLongTerm && frame.FrameNumWrap == picNum)
            {
                return frame;
            }
        }

        throw new InvalidDataException("An H.264 slice refers to a reference frame that is not there.");
    }

    /// <summary>Makes a frame a long-term reference with an index, freeing any other frame that has the index.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="index">The long-term frame index.</param>
    private void AssignLongTerm(H264RefPicture frame, int index)
    {
        _ = _frames.RemoveAll(other => other.IsLongTerm && other.LongTermFrameIdx == index && !ReferenceEquals(other, frame));
        frame.IsLongTerm = true;
        frame.LongTermFrameIdx = index;
    }

    /// <summary>Applies the reference list modification commands of a slice (clause 8.2.4.3).</summary>
    /// <param name="list">The list, one place longer than the slice says.</param>
    /// <param name="commands">The commands.</param>
    /// <param name="count">The length of the list.</param>
    /// <param name="currentFrameNum">The frame_num of the frame being decoded.</param>
    /// <param name="maxFrameNum">The range of frame numbers.</param>
    /// <exception cref="InvalidDataException">A command names a frame that is not there.</exception>
    private void Modify(H264RefPicture?[] list, H264RefModification[] commands, int count, int currentFrameNum, int maxFrameNum)
    {
        var predicted = currentFrameNum;
        var index = 0;
        foreach (var command in commands)
        {
            if (index >= count)
            {
                break;
            }

            H264RefPicture target;
            if (command.Idc == PickLongTerm)
            {
                target = FindLongTerm(command.Value);
            }
            else
            {
                predicted = StepPicNum(predicted, command, maxFrameNum);
                var picNum = predicted > currentFrameNum ? predicted - maxFrameNum : predicted;
                target = FindShortTerm(picNum);
            }

            Insert(list, count, index, target);
            index++;
        }
    }

    /// <summary>Finds a long-term frame by its long-term picture number.</summary>
    /// <param name="longTermPicNum">The number.</param>
    /// <returns>The frame.</returns>
    /// <exception cref="InvalidDataException">No such frame is marked.</exception>
    private H264RefPicture FindLongTerm(int longTermPicNum)
    {
        foreach (var frame in _frames)
        {
            if (frame.IsLongTerm && frame.LongTermFrameIdx == longTermPicNum)
            {
                return frame;
            }
        }

        throw new InvalidDataException("An H.264 slice refers to a long-term frame that is not there.");
    }
}
