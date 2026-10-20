// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Media.U3d;

/// <summary>Collects what a U3D file declares while it is read, within the limits of the reader.</summary>
internal sealed class U3dSceneBuilder
{
    /// <summary>The most problems kept; later ones are left out.</summary>
    private const int MaxProblems = 16;

    /// <summary>The names of the extensions the file declares.</summary>
    private readonly List<string> _extensions = [];

    /// <summary>What stopped the file being read in full.</summary>
    private readonly List<string> _problems = [];

    /// <summary>Gets the scene nodes.</summary>
    internal List<U3dNode> Nodes { get; } = [];

    /// <summary>Gets the shapes.</summary>
    internal List<U3dGeometry> Geometry { get; } = [];

    /// <summary>Gets the resources.</summary>
    internal List<U3dResource> Resources { get; } = [];

    /// <summary>Gets or sets the major version.</summary>
    internal short MajorVersion { get; set; }

    /// <summary>Gets or sets the minor version.</summary>
    internal short MinorVersion { get; set; }

    /// <summary>Gets or sets the profile flags.</summary>
    internal uint Profile { get; set; }

    /// <summary>Gets or sets the units scaling factor.</summary>
    internal double? Units { get; set; }

    /// <summary>Gets or sets the number of blocks read.</summary>
    internal int BlockCount { get; set; }

    /// <summary>Gets or sets the number of blocks that hold data that is kept but not decoded.</summary>
    internal int ContinuationBlocks { get; set; }

    /// <summary>Gets a value indicating whether the reader must stop, because a limit was reached or a block could not be framed.</summary>
    internal bool Stopped { get; private set; }

    /// <summary>Notes a problem, once.</summary>
    /// <param name="text">What went wrong, in plain words.</param>
    internal void Problem(string text)
    {
        if (_problems.Count < MaxProblems && !_problems.Contains(text))
        {
            _problems.Add(text);
        }
    }

    /// <summary>Notes a problem and stops reading.</summary>
    /// <param name="text">What went wrong, in plain words.</param>
    internal void Stop(string text)
    {
        Problem(text);
        Stopped = true;
    }

    /// <summary>Notes an extension the file declares.</summary>
    /// <param name="name">The extension's name.</param>
    internal void Extension(string name)
    {
        if (!_extensions.Contains(name))
        {
            _extensions.Add(name);
        }
    }

    /// <summary>Makes the scene.</summary>
    /// <returns>The scene.</returns>
    internal U3dScene Build() =>
        new(MajorVersion, MinorVersion, Profile, Units, [.. Nodes], [.. Geometry], [.. Resources], BlockCount, ContinuationBlocks, NamesOf(_extensions), NamesOf(_problems));

    /// <summary>Copies a list of names, sharing the empty array when there are none.</summary>
    /// <param name="names">The names to copy.</param>
    /// <returns>An array with the same names.</returns>
    private static string[] NamesOf(List<string> names) => names.Count == 0 ? [] : [.. names];
}
