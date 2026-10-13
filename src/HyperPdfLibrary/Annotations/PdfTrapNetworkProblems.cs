// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Annotations;

/// <summary>
/// What is wrong with a page's trap network annotation (ISO 32000-2 §12.5.6.21 and §14.11.6.2, deprecated in PDF 2.0).
/// The flags that say the traps no longer match the page, or cannot be checked, mean the network must be removed;
/// <see cref="NotLast"/>, <see cref="WrongFlags"/> and <see cref="MissingAppearanceState"/> can be fixed in place.
/// </summary>
[Flags]
public enum PdfTrapNetworkProblems
{
    /// <summary>The network conforms and matches its page.</summary>
    None = 0,

    /// <summary>The page has more than one trap network annotation; a page may have at most one.</summary>
    MultipleNetworks = 1 << 0,

    /// <summary>The network is not the last entry of the page's <c>/Annots</c> array, so it would not print last.</summary>
    NotLast = 1 << 1,

    /// <summary>The <c>/F</c> entry is missing or does not hold exactly the Print and ReadOnly flags.</summary>
    WrongFlags = 1 << 2,

    /// <summary>The network has no normal appearance, or its <c>/AS</c> does not name one of its appearances.</summary>
    MissingAppearance = 1 << 3,

    /// <summary>The network has a single normal appearance but no <c>/AS</c> entry, which it must have.</summary>
    MissingAppearanceState = 1 << 4,

    /// <summary>
    /// The network has neither <c>/LastModified</c> nor both <c>/Version</c> and <c>/AnnotStates</c>, or has all three,
    /// or one of them cannot be read, so whether it matches the page cannot be checked.
    /// </summary>
    MissingChangeTracking = 1 << 5,

    /// <summary>The page's content streams or resources changed after the traps were made.</summary>
    ContentChanged = 1 << 6,

    /// <summary>The appearance states of the page's other annotations changed after the traps were made.</summary>
    AnnotationsChanged = 1 << 7,

    /// <summary>The page's <c>/LastModified</c> date is later than the network's.</summary>
    PageNewer = 1 << 8,

    /// <summary>The network's <c>/Version</c> names an object a page copy cannot keep, such as a page.</summary>
    ReferencesLost = 1 << 9,
}
