// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Rendering;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Rendering.Cpu;

/// <summary>Draws one frame into the CPU backend's buffer and presents it on dispose. The backend keeps one pass and hands it out for every frame, so a frame allocates nothing.</summary>
/// <param name="owner">The backend that owns the frame buffer.</param>
internal sealed class CpuFramePass(CpuRenderBackend owner) : IFramePass
{
    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The number of steps a layer's weight is out of.</summary>
    private const int WeightSteps = 256;

    /// <summary>The largest weight a layer can have when it is mixed in.</summary>
    private const int MaxWeight = 255;

    /// <summary>Names a fill among the draws of a kept group.</summary>
    private const int FillDraw = 1;

    /// <summary>Names a picture among the draws of a kept group.</summary>
    private const int PictureDraw = 2;

    /// <summary>Names a smoothly scaled picture among the draws of a kept group.</summary>
    private const int SmoothPictureDraw = 3;

    /// <summary>Names a change of the clip among the draws of a kept group.</summary>
    private const int ClipDraw = 4;

    /// <summary>The shift of the alpha byte of a colour.</summary>
    private const int AlphaShift = 24;

    /// <summary>The fewest pixels an opaque fill has to hold it back for the pictures that may cover it. A smaller fill costs less to paint than to hold.</summary>
    private const long MinHeldPixels = 128 * 128;

    /// <summary>The draws the lists of a frame have room for at the start, enough for a window of controls and a page, so they do not grow while frames are drawn.</summary>
    private const int CommandCapacity = 256;

    /// <summary>The large opaque fill held back, with the pictures drawn over it, until it is known how much of it they cover.</summary>
    private readonly CoveredFill _covered = new();

    /// <summary>The pixels of a group of draws that is the same in most frames, kept so the group is not drawn again.</summary>
    private readonly KeptDraws _kept = new();

    /// <summary>The part of the frame drawing may reach, or <see langword="null"/> for the whole frame.</summary>
    private PixelClip? _clip;

    /// <summary>The clip in force when the trial of a kept group started.</summary>
    private PixelClip? _clipBeforeTrial;

    /// <summary>Whether a kept group is on trial, so draws are followed and not made.</summary>
    private bool _onTrial;

    /// <summary>Whether a kept group is being drawn, so draws are followed as they are made.</summary>
    private bool _keeping;

    /// <summary>The draws asked for in the frame being recorded.</summary>
    private List<PassCommand> _asked = [with(CommandCapacity)];

    /// <summary>The draws of the last frame that was drawn, which the frame buffer still shows.</summary>
    private List<PassCommand> _drawn = [with(CommandCapacity)];

    /// <summary>Which frame buffer the last frame was drawn into, so a new buffer is never taken for a drawn one.</summary>
    private int _drawnBuffer = -1;

    /// <summary>Whether the recorded draws are being made, so each call draws and is not recorded again.</summary>
    private bool _replaying;

    /// <summary>The frame as it was before the layer being drawn, or <see langword="null"/> when no layer is being drawn.</summary>
    private byte[]? _behind;

    /// <summary>How much of the layer shows, out of 256.</summary>
    private uint _layerWeight;

    /// <inheritdoc/>
    public SurfaceSize Size => owner.Size;

    /// <inheritdoc/>
    public void Clear(uint argb)
    {
        if (Ask(new(PassCommandKind.Clear, default, argb, null, 0, false, 0)))
        {
            return;
        }

        // A group that clears the frame is not one whose pixels can be kept.
        _kept.Spoil();
        if (_onTrial)
        {
            return;
        }

        _covered.Flush(owner);
        if (argb >> AlphaShift == MaxWeight)
        {
            // An opaque clear waits: the page paper that follows covers most of it, and only the margin needs painting then.
            var size = owner.Size;
            _covered.BeginClear(new(0, 0, size.Width, size.Height), argb);
            return;
        }

        CpuFill.Fill(owner.WritableFrame(), argb);
    }

    /// <inheritdoc/>
    public void FillRect(in LayoutRect area, uint argb)
    {
        if (Ask(new(PassCommandKind.Fill, area, argb, null, 0, false, 0)))
        {
            return;
        }

        var size = owner.Size;
        var reach = _clip ?? new PixelClip(0, 0, size.Width, size.Height);
        var rows = new BlitRows(
            Math.Max((int)Math.Floor(area.X), reach.Left),
            Math.Max((int)Math.Floor(area.Y), reach.Top),
            Math.Min((int)Math.Ceiling(area.X + area.Width), reach.Right),
            Math.Min((int)Math.Ceiling(area.Y + area.Height), reach.Bottom));
        if (FollowFill(rows, argb) || rows.IsEmpty)
        {
            return;
        }

        var holdable = argb >> AlphaShift == MaxWeight && (long)(rows.Right - rows.Left) * (rows.Bottom - rows.Top) >= MinHeldPixels;
        if (!holdable || !_covered.TryNarrowClear(owner, rows))
        {
            _covered.Flush(owner);
        }

        if (holdable)
        {
            // A large opaque fill is usually the paper of a page, which its tiles are about to cover, so it waits for them.
            _covered.Begin(rows, argb);
            return;
        }

        owner.Fill(rows, argb);
    }

    /// <inheritdoc/>
    public void DrawSurface(IRenderSurface surface, in LayoutRect destination, bool smooth)
    {
        if (surface is not CpuPixelSurface cpu)
        {
            throw new ArgumentException("The surface was not made by the CPU backend.", nameof(surface));
        }

        if (Ask(new(PassCommandKind.Picture, destination, 0, cpu, cpu.Version, smooth, 0)))
        {
            return;
        }

        var request = new BlitRequest(
            (int)Math.Round(destination.X),
            (int)Math.Round(destination.Y),
            (int)Math.Round(destination.Width),
            (int)Math.Round(destination.Height),
            smooth,
            cpu.IsOpaque) { Clip = _clip };
        if (FollowSurface(cpu, request))
        {
            return;
        }

        if (_covered.TryDefer(cpu, request))
        {
            return;
        }

        _covered.Flush(owner);
        owner.Blit(cpu, request);
    }

    /// <inheritdoc/>
    public void SetClip(in LayoutRect area)
    {
        if (Ask(new(PassCommandKind.SetClip, area, 0, null, 0, false, 0)))
        {
            return;
        }

        // What is waiting was drawn under the clip in force until now.
        FlushUnlessOnTrial();
        var size = owner.Size;
        _clip = PixelClip.Of(area, size.Width, size.Height);
        FollowClip();
    }

    /// <inheritdoc/>
    public void ClearClip()
    {
        if (Ask(new(PassCommandKind.ClearClip, default, 0, null, 0, false, 0)))
        {
            return;
        }

        FlushUnlessOnTrial();
        _clip = null;
        FollowClip();
    }

    /// <inheritdoc/>
    public bool BeginKeptTrial()
    {
        // The draws are held until the frame ends, and the trial of a kept group is made then, over the draws held. With
        // nothing kept there is nothing a trial could find, so the group is drawn straight away.
        if (!_replaying || _onTrial || _keeping || !_kept.HasKept)
        {
            return false;
        }

        _onTrial = true;
        _clipBeforeTrial = _clip;
        _kept.Begin();
        return true;
    }

    /// <inheritdoc/>
    public bool EndKeptTrial()
    {
        if (!_onTrial)
        {
            return false;
        }

        _onTrial = false;
        var size = owner.Size;
        if (!_kept.Matches(size.Width, size.Height))
        {
            // The group is drawn for real next, from the clip it started under.
            _kept.Forget();
            _clip = _clipBeforeTrial;
            return false;
        }

        // What waited before the group goes into the frame first, as it would have when the group drew over it.
        _covered.Flush(owner);
        _kept.Restore(owner.WritableFrame(), owner.Stride);
        owner.KeptRestores++;
        return true;
    }

    /// <inheritdoc/>
    public void BeginKeptGroup()
    {
        if (Ask(new(PassCommandKind.BeginKept, default, 0, null, 0, false, 0)))
        {
            return;
        }

        _keeping = !_onTrial;
        _kept.Begin();
    }

    /// <inheritdoc/>
    public void EndKeptGroup()
    {
        if (Ask(new(PassCommandKind.EndKept, default, 0, null, 0, false, 0)) || !_keeping)
        {
            return;
        }

        _keeping = false;
        _covered.Flush(owner);
        _kept.Capture(owner.FramePixels, owner.Stride, owner.Size.Height);
    }

    /// <inheritdoc/>
    public void SetOpacity(double opacity)
    {
        if (Ask(new(PassCommandKind.SetOpacity, default, 0, null, 0, false, opacity)))
        {
            return;
        }

        // A group that fades is mixed with the frame behind it, which is not the group's to keep.
        _kept.Spoil();
        if (_onTrial)
        {
            return;
        }

        // What was drawn at the opacity before is mixed in first, then the frame is kept so what comes next can be mixed with it.
        _covered.Flush(owner);
        EndLayer();
        if (opacity >= 1)
        {
            return;
        }

        var frame = owner.WritableFrame();
        _behind = ArrayPool<byte>.Shared.Rent(frame.Length);
        frame.CopyTo(_behind);
        _layerWeight = (uint)Math.Clamp(Math.Round(Math.Max(opacity, 0) * WeightSteps), 0, MaxWeight);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // A frame whose draws are one for one the draws of the frame before would leave the pixels the frame buffer already
        // holds, so nothing is drawn. The first draw must clear the frame, or the pixels would also depend on what was there.
        if (_asked.Count > 0
            && _asked[0].Kind == PassCommandKind.Clear
            && owner.FrameBuffer == _drawnBuffer
            && CollectionsMarshal.AsSpan(_asked).SequenceEqual(CollectionsMarshal.AsSpan(_drawn)))
        {
            _asked.Clear();
            owner.PresentUnchanged();
            return;
        }

        Replay();
        _drawnBuffer = owner.FrameBuffer;

        // The pass is handed out again for the next frame, which starts without a clip or a layer.
        _covered.Flush(owner);
        EndLayer();
        _clip = null;
        _onTrial = false;
        _keeping = false;
        owner.Present();
    }

    /// <summary>Holds a draw until the frame ends, unless the held draws are being made now.</summary>
    /// <param name="command">The draw.</param>
    /// <returns><see langword="true"/> when the draw was held, so the caller makes nothing yet.</returns>
    private bool Ask(in PassCommand command)
    {
        if (_replaying)
        {
            return false;
        }

        _asked.Add(command);
        return true;
    }

    /// <summary>Makes the draws held for the frame, and keeps them as the draws the frame buffer shows.</summary>
    private void Replay()
    {
        _replaying = true;
        try
        {
            var asked = CollectionsMarshal.AsSpan(_asked);
            for (var i = 0; i < asked.Length; i++)
            {
                i = asked[i].Kind == PassCommandKind.BeginKept ? ReplayKept(asked, i) : Make(asked[i], i);
            }
        }
        finally
        {
            _replaying = false;
        }

        // The lists change places, so the next frame records into the room of the frame before the last.
        var drawn = _drawn;
        _drawn = _asked;
        _asked = drawn;
        _asked.Clear();
    }

    /// <summary>
    /// Makes the draws of a group whose pixels are kept between frames. When pixels are kept the draws are first followed
    /// without drawing, and when they are the draws the pixels were kept from, the pixels are put back in their place.
    /// </summary>
    /// <param name="asked">The draws held for the frame.</param>
    /// <param name="start">The place of the draw that starts the group.</param>
    /// <returns>The place of the draw that ends the group.</returns>
    private int ReplayKept(ReadOnlySpan<PassCommand> asked, int start)
    {
        var end = start + 1;
        while (end < asked.Length && asked[end].Kind != PassCommandKind.EndKept)
        {
            end++;
        }

        if (BeginKeptTrial())
        {
            MakeRange(asked, start + 1, end);
            if (EndKeptTrial())
            {
                return end;
            }
        }

        BeginKeptGroup();
        MakeRange(asked, start + 1, end);
        EndKeptGroup();
        return end;
    }

    /// <summary>Makes a run of the held draws.</summary>
    /// <param name="asked">The draws held for the frame.</param>
    /// <param name="from">The first draw to make.</param>
    /// <param name="to">The draw after the last to make.</param>
    private void MakeRange(ReadOnlySpan<PassCommand> asked, int from, int to)
    {
        for (var i = from; i < to; i++)
        {
            _ = Make(asked[i], i);
        }
    }

    /// <summary>Makes one held draw.</summary>
    /// <param name="command">The draw.</param>
    /// <param name="place">The place of the draw among the held draws.</param>
    /// <returns>The same place.</returns>
    private int Make(in PassCommand command, int place)
    {
        switch (command.Kind)
        {
            case PassCommandKind.Clear:
            {
                Clear(command.Argb);
                break;
            }

            case PassCommandKind.Fill:
            {
                FillRect(command.Area, command.Argb);
                break;
            }

            case PassCommandKind.Picture when command.Surface is { IsGone: false } surface:
            {
                DrawSurface(surface, command.Area, command.Smooth);
                break;
            }

            case PassCommandKind.SetClip:
            {
                SetClip(command.Area);
                break;
            }

            case PassCommandKind.ClearClip:
            {
                ClearClip();
                break;
            }

            case PassCommandKind.SetOpacity:
            {
                SetOpacity(command.Opacity);
                break;
            }

            default:
            {
                // The end of a kept group, met outside one, and a picture given up since it was asked for draw nothing.
                break;
            }
        }

        return place;
    }

    /// <summary>Follows a fill for the kept group, when one is on trial or being drawn.</summary>
    /// <param name="rows">Where the fill lands.</param>
    /// <param name="argb">The colour.</param>
    /// <returns><see langword="true"/> when the group is on trial, so the fill is not made.</returns>
    private bool FollowFill(in BlitRows rows, uint argb)
    {
        if (!_onTrial && !_keeping)
        {
            return false;
        }

        _kept.Note(FillDraw, rows, argb, 0);
        if (_keeping)
        {
            _kept.Cover(rows, argb >> AlphaShift == MaxWeight);
        }

        return _onTrial;
    }

    /// <summary>Follows a picture for the kept group, when one is on trial or being drawn.</summary>
    /// <param name="surface">The picture.</param>
    /// <param name="request">Where it goes.</param>
    /// <returns><see langword="true"/> when the group is on trial, so the picture is not drawn.</returns>
    private bool FollowSurface(CpuPixelSurface surface, in BlitRequest request)
    {
        if (!_onTrial && !_keeping)
        {
            return false;
        }

        // The picture is known by which one it is and how often it has been written, so new pixels in it draw the group afresh.
        var identity = (uint)RuntimeHelpers.GetHashCode(surface);
        _kept.Note(request.Smooth ? SmoothPictureDraw : PictureDraw, new(request.X, request.Y, request.X + request.Width, request.Y + request.Height), identity, surface.Version);
        if (_keeping)
        {
            _kept.Cover(BlitRows.Clip(request, owner.Grid), request.Opaque);
        }

        return _onTrial;
    }

    /// <summary>Follows a change of the clip for the kept group, when one is on trial or being drawn.</summary>
    private void FollowClip()
    {
        if (!_onTrial && !_keeping)
        {
            return;
        }

        var clip = _clip ?? new PixelClip(-1, -1, -1, -1);
        _kept.Note(ClipDraw, new(clip.Left, clip.Top, clip.Right, clip.Bottom), 0, 0);
    }

    /// <summary>Puts what is waiting into the frame, unless a kept group is on trial and nothing is drawn.</summary>
    private void FlushUnlessOnTrial()
    {
        if (!_onTrial)
        {
            _covered.Flush(owner);
        }
    }

    /// <summary>
    /// Mixes the layer drawn since <see cref="SetOpacity"/> with the frame as it was before the layer: the layer was drawn over the frame, so
    /// the mix leaves the frame as it was where the layer is transparent and fades it elsewhere. Outside the clip nothing changed, so it mixes to the same.
    /// </summary>
    private void EndLayer()
    {
        if (_behind is not { } behind)
        {
            return;
        }

        _behind = null;
        var frame = owner.WritableFrame();
        var stride = owner.Stride;
        for (var offset = 0; offset < frame.Length; offset += stride)
        {
            // What the layer left as it was needs no mixing: a row of the bars above the pages, and the start of a row that
            // crosses the side panel. Only the part of a row from its first changed pixel on is mixed.
            var row = frame.Slice(offset, stride);
            var before = behind.AsSpan(offset, stride);
            var same = row.CommonPrefixLength(before) & -BytesPerPixel;
            if (same >= stride)
            {
                continue;
            }

            var changed = row[same..];
            CpuBlend.MixRows(changed, before[same..], changed, _layerWeight);
        }

        ArrayPool<byte>.Shared.Return(behind);
    }
}
