// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// A set of components as bits, so a decode can skip the components its output does not read. The first 32 components
/// have a bit each; any later component always counts as wanted.
/// </summary>
internal static class JpxComponentMask
{
    /// <summary>Every component.</summary>
    internal const uint All = uint.MaxValue;

    /// <summary>The components that have a bit.</summary>
    private const int Bits = 32;

    /// <summary>The first three components, which the multiple component transform reads together.</summary>
    private const uint TransformComponents = 0b111;

    /// <summary>Determines whether a component is in the set.</summary>
    /// <param name="mask">The set.</param>
    /// <param name="component">The component index.</param>
    /// <returns><see langword="true"/> when the component is wanted.</returns>
    internal static bool Contains(uint mask, int component) => component >= Bits || (mask & (1U << component)) != 0;

    /// <summary>Adds a component to a set.</summary>
    /// <param name="mask">The set.</param>
    /// <param name="component">The component index.</param>
    /// <returns>The set with the component.</returns>
    internal static uint With(uint mask, int component) => component >= Bits ? mask : mask | (1U << component);

    /// <summary>Adds the components the multiple component transform reads together to a set.</summary>
    /// <param name="mask">The set.</param>
    /// <returns>The set with the first three components.</returns>
    internal static uint WithTransformComponents(uint mask) => mask | TransformComponents;
}
