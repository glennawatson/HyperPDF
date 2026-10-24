// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Derives the motion vector predictor of a prediction block that is not coded in merge mode (8.5.3.2.6).</summary>
internal static class H265MvPredictor
{
    /// <summary>Derives the predictor.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="block">The prediction block.</param>
    /// <param name="request">The reference list and index the vector is for, and which list entry to pick.</param>
    /// <returns>The predictor.</returns>
    internal static MotionVector Derive(H265SliceContext context, in H265PuBlock block, H265MvpRequest request)
    {
        var spatial = FindSpatial(context, block, request);

        // Two different spatial candidates fill the list, so the temporal candidate is not used.
        if (spatial.Distinct)
        {
            return request.MvpFlag == 0 ? spatial.A : spatial.B;
        }

        if (spatial.Any && request.MvpFlag == 0)
        {
            return spatial.First;
        }

        // The list is the spatial candidate if any, then the temporal candidate, then zero vectors.
        return request.MvpFlag <= (spatial.Any ? 1 : 0) && H265TemporalMotion.TryGet(context, block.Area, request.List, request.RefIdx, out var colVector)
            ? colVector
            : default;
    }

    /// <summary>Finds the candidates on the left and above (8.5.3.2.7).</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="block">The prediction block.</param>
    /// <param name="request">The reference list and index the vector is for.</param>
    /// <returns>The candidates.</returns>
    private static H265SpatialMvp FindSpatial(H265SliceContext context, in H265PuBlock block, H265MvpRequest request)
    {
        var search = new H265MvpSearch(context, block, request.List, request.RefIdx);
        var foundA = search.FindLeft(out var mvA);
        var foundB = search.FindAbove(out var mvB);
        if (search.IsScaled)
        {
            return new(foundA, mvA, foundB, mvB);
        }

        var promoted = foundB || foundA;
        var left = foundB ? mvB : mvA;
        foundB = search.FindAboveScaled(out mvB);
        return new(promoted, left, foundB, mvB);
    }
}
