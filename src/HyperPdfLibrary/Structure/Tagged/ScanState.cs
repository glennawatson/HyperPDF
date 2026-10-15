// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// What <see cref="MarkedContentScanner"/> keeps while it walks a page: the operators found, which open marked content levels
/// carry a marked content id or are artifacts, and the images painted outside all of those (ISO 32000-2 14.8.2.2).
/// </summary>
[DebuggerDisplay("ScanState: {UntaggedImages} untagged images")]
internal sealed class ScanState
{
    /// <summary>The operators found, in running order.</summary>
    private readonly List<ScannedMark> _output;

    /// <summary>Whether each open level has an id or is an artifact, innermost last.</summary>
    private readonly List<bool> _levels = [];

    /// <summary>The number of open levels that have an id or are artifacts.</summary>
    private int _real;

    /// <summary>Initializes a new instance of the <see cref="ScanState"/> class.</summary>
    /// <param name="output">Receives the operators.</param>
    internal ScanState(List<ScannedMark> output) => _output = output;

    /// <summary>Gets or sets a value indicating whether the page paints an image.</summary>
    internal bool Images { get; set; }

    /// <summary>Gets the number of images painted outside marked content with an id and outside artifacts.</summary>
    internal int UntaggedImages { get; private set; }

    /// <summary>Records a marked content operator and opens its level.</summary>
    /// <param name="mark">The operator.</param>
    /// <param name="isArtifact">Whether its tag is <c>/Artifact</c>.</param>
    internal void Open(ScannedMark mark, bool isArtifact)
    {
        _output.Add(mark);
        var real = isArtifact || mark.Mcid >= 0;
        _levels.Add(real);
        _real += real ? 1 : 0;
    }

    /// <summary>Closes the innermost level.</summary>
    internal void Close()
    {
        if (_levels.Count == 0)
        {
            return;
        }

        _real -= _levels[^1] ? 1 : 0;
        _levels.RemoveAt(_levels.Count - 1);
    }

    /// <summary>Counts an image painted at the current level.</summary>
    internal void CountImage() => UntaggedImages += _real == 0 ? 1 : 0;
}
