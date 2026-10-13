// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>Prepares the next block of rows of several component streams at once, one stream for each item of the loop.</summary>
internal sealed class JpxComponentPrepareJob : JpxParallelJob
{
    /// <summary>The component streams of the tile.</summary>
    private readonly JpxComponentStream[] _components;

    /// <summary>Initializes a new instance of the <see cref="JpxComponentPrepareJob"/> class.</summary>
    /// <param name="components">The component streams; the first <c>count</c> of them are prepared.</param>
    internal JpxComponentPrepareJob(JpxComponentStream[] components) => _components = components;

    /// <summary>Prepares the first components of the tile on the calling thread and on pool threads.</summary>
    /// <param name="count">The components to prepare.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Run(int count) => Dispatch(count, true);

    /// <inheritdoc/>
    protected override void RunWorker()
    {
        while (TryClaim(out var index))
        {
            _components[index].Prepare();
        }
    }
}
