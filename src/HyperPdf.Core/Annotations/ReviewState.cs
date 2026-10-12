// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Annotations;

/// <summary>A comment's review status, as PDF's Review state model records it.</summary>
public enum ReviewState
{
    /// <summary>No status, or the status was cleared.</summary>
    None = 0,

    /// <summary>The reviewer accepts the comment.</summary>
    Accepted = 1,

    /// <summary>The reviewer rejects the comment.</summary>
    Rejected = 2,

    /// <summary>The comment no longer applies.</summary>
    Cancelled = 3,

    /// <summary>The comment has been dealt with.</summary>
    Completed = 4,
}
