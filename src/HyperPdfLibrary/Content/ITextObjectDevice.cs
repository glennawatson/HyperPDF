// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Content;

/// <summary>
/// A device that only collects text. The interpreter tells it where each text-showing operator starts, so glyphs
/// group into one run per operator; it has no use for images.
/// </summary>
internal interface ITextObjectDevice : IContentDevice
{
    /// <summary>Gets a value indicating whether the device keeps paths; text collectors never look at them.</summary>
    bool IContentDevice.RetainsPaths => false;

    /// <summary>Marks the start of a text-showing operator (<c>Tj</c>, <c>TJ</c>, <c>'</c> or <c>"</c>).</summary>
    void BeginTextObject();
}
