// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Redaction;

/// <summary>What redaction does to drawn lines and shapes that a mark touches.</summary>
public enum RedactionLineArtChoice
{
    /// <summary>Leave drawings alone.</summary>
    Keep = 0,

    /// <summary>Remove drawings that lie wholly under a mark.</summary>
    RemoveCovered = 1,

    /// <summary>Remove every drawing a mark touches.</summary>
    RemoveTouched = 2,
}
