// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>How much of a text a user can select.</summary>
internal enum SupportedTextSelection
{
    /// <summary>Nothing can be selected.</summary>
    None = 0,

    /// <summary>One stretch can be selected.</summary>
    Single = 1,

    /// <summary>Several stretches can be selected.</summary>
    Multiple = 2,
}
