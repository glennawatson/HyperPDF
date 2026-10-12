// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Redaction;

/// <summary>What redaction does to a picture that a mark touches.</summary>
public enum RedactionImageChoice
{
    /// <summary>Leave the picture alone.</summary>
    Keep = 0,

    /// <summary>Remove the whole picture.</summary>
    Remove = 1,

    /// <summary>Blank only the part of the picture under the mark.</summary>
    BlankPart = 2,
}
