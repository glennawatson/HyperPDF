// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.XObjects;

/// <summary>Where an image is going, which decides whether an alternate version replaces the main image.</summary>
public enum PdfImageUse
{
    /// <summary>Shown on screen: the main image is always used.</summary>
    Screen = 0,

    /// <summary>Sent to a printer: the alternate marked as the printing default is used when there is one.</summary>
    Print = 1,
}
