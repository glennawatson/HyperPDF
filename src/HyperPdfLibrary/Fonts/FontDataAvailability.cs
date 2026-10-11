// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts;

/// <summary>Records whether one cache entry has been observed available.</summary>
[DebuggerDisplay("Available {IsAvailable}")]
internal sealed class FontDataAvailability
{
    /// <summary>Guards first availability.</summary>
    private readonly Lock _gate = new();

    /// <summary>Whether the asset has been observed.</summary>
    private int _available;

    /// <summary>Gets whether the asset has been observed available.</summary>
    internal bool IsAvailable => Volatile.Read(ref _available) != 0;

    /// <summary>Advances generation once when this asset becomes available.</summary>
    /// <param name="state">The resource state.</param>
    internal void ObserveAvailability(FontDataResourceState state)
    {
        if (IsAvailable)
        {
            return;
        }

        lock (_gate)
        {
            if (_available != 0)
            {
                return;
            }

            state.Advance();
            Volatile.Write(ref _available, 1);
        }
    }
}
