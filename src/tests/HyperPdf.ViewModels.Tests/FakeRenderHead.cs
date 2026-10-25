// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Rendering;
using HyperPdf.Core.Tests.Fakes;

namespace HyperPdf.ViewModels.Tests;

/// <summary>A render head with no toolkit: tiles are plain memory, and it records what the view models ask of it.</summary>
[DebuggerDisplay("FakeRenderHead: {InvalidateCount} frames")]
public sealed class FakeRenderHead : IRenderHead
{
    /// <summary>The number of frames requested.</summary>
    private int _invalidateCount;

    /// <summary>Gets the number of frames requested so far.</summary>
    public int InvalidateCount => Volatile.Read(ref _invalidateCount);

    /// <summary>Gets the recovery request the hub handed over, or <see langword="null"/> before a factory was made.</summary>
    public Action? RecoverSoftware { get; private set; }

    /// <summary>Gets the number of surface factories made.</summary>
    public int FactoriesCreated { get; private set; }

    /// <summary>Gets the number of surface factories disposed.</summary>
    public int FactoriesDisposed { get; private set; }

    /// <summary>Gets or sets the picture the head returns for any path.</summary>
    public DecodedImage? Image { get; set; }

    /// <summary>Gets the paths the head was asked to read.</summary>
    public List<string> ReadPaths { get; } = [];

    /// <inheritdoc/>
    public string NoticesFolder { get; set; } = Path.Combine(Path.GetTempPath(), "hyperpdf-no-notices");

    /// <inheritdoc/>
    public IOwnedSurfaceFactory CreateSurfaceFactory(Action recoverSoftware)
    {
        RecoverSoftware = recoverSoftware;
        FactoriesCreated++;
        return new Factory(this);
    }

    /// <inheritdoc/>
    public void InvalidateDisplay() => Interlocked.Increment(ref _invalidateCount);

    /// <inheritdoc/>
    public DecodedImage? LoadImage(string path)
    {
        ReadPaths.Add(path);
        return Image;
    }

    /// <summary>A factory whose tiles are plain memory.</summary>
    /// <param name="owner">The head that made it.</param>
    [DebuggerDisplay("Factory")]
    private sealed class Factory(FakeRenderHead owner) : IOwnedSurfaceFactory
    {
        /// <inheritdoc/>
        public IRenderSurface Create(int width, int height) => new FakeSurface(width, height);

        /// <inheritdoc/>
        public void Dispose() => owner.FactoriesDisposed++;
    }
}
