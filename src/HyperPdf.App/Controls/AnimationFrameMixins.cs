// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Controls;

/// <summary>The display's animation frames as a stream, so an ease or a glide is one chain that ends when its subscription does.</summary>
internal static class AnimationFrameMixins
{
    /// <summary>Adds the frame stream of a window.</summary>
    /// <param name="top">The window.</param>
    extension(TopLevel top)
    {
        /// <summary>
        /// Gets the frames the window draws, from the first frame after subscribing until the subscription ends. Each value
        /// is the frame time. Avalonia offers frames only as a one-off callback, so each frame asks for the next.
        /// </summary>
        internal IObservable<TimeSpan> AnimationFrames => Signal.Create<TimeSpan>(observer => new FrameRequests(top, observer).Start());
    }

    /// <summary>Asks a window for frames one at a time and passes them on until the subscription is disposed.</summary>
    [DebuggerDisplay("FrameRequests: Stopped = {IsStopped}")]
    private sealed class FrameRequests : IDisposable
    {
        /// <summary>The window.</summary>
        private readonly TopLevel _top;

        /// <summary>Where the frames go.</summary>
        private readonly IObserver<TimeSpan> _observer;

        /// <summary>The frame callback, made once so frames do not allocate.</summary>
        private readonly Action<TimeSpan> _frame;

        /// <summary>1 once the subscription is disposed, after which no more frames are asked for.</summary>
        private int _stopped;

        /// <summary>Initializes a new instance of the <see cref="FrameRequests"/> class.</summary>
        /// <param name="top">The window.</param>
        /// <param name="observer">Where the frames go.</param>
        internal FrameRequests(TopLevel top, IObserver<TimeSpan> observer)
        {
            _top = top;
            _observer = observer;
            _frame = PassFrame;
        }

        /// <summary>Gets a value indicating whether the subscription was disposed.</summary>
        private bool IsStopped => Volatile.Read(ref _stopped) != 0;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => Volatile.Write(ref _stopped, 1);

        /// <summary>Asks for the first frame.</summary>
        /// <returns>The subscription, which stops the frames when disposed.</returns>
        internal FrameRequests Start()
        {
            _top.RequestAnimationFrame(_frame);
            return this;
        }

        /// <summary>Passes a frame on and asks for the next one while the subscription lasts.</summary>
        /// <param name="time">The frame time.</param>
        private void PassFrame(TimeSpan time)
        {
            if (IsStopped)
            {
                return;
            }

            _observer.OnNext(time);
            if (!IsStopped)
            {
                _top.RequestAnimationFrame(_frame);
            }
        }
    }
}
