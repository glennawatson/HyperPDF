// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Archiving;
using HyperPdf.Core.Documents;

namespace HyperPdf.ViewModels.Output;

/// <summary>Rewrites a file just saved as a PDF 2.0 file, so editing leaves current-standard files behind by default.</summary>
internal static class Pdf20Resave
{
    /// <summary>Replaces a saved file with the open document's PDF 2.0 form, when the engine can make one that keeps everything the file has.</summary>
    /// <param name="upgrade">The open document's writer and what the person agreed may be dropped.</param>
    /// <param name="path">The file just saved, which the open document's edits are all in.</param>
    /// <param name="cancellationToken">Cancels converting and writing.</param>
    /// <returns>Whether the file now is a PDF 2.0 file, and when it is not, the plain-words reason; a refusal leaves the saved file exactly as it was.</returns>
    internal static async Task<Pdf20ResaveResult> TryAsync(Pdf20Upgrade upgrade, string path, CancellationToken cancellationToken)
    {
        var converted = $"{path}.pdf20";
        try
        {
            Pdf20CopyResult result;
            var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write, Share = FileShare.None, Options = FileOptions.Asynchronous };
            await using (var stream = new FileStream(converted, options))
            {
                result = await upgrade.Writer.WritePdf20CopyAsync(stream, upgrade.Options, cancellationToken).ConfigureAwait(false);
            }

            if (!result.Converted)
            {
                return new(false, result.Problems.Count > 0 ? result.Problems[0].Description : null);
            }

            File.Move(converted, path, true);
            return new(true, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DocumentOpenException or InvalidOperationException or InvalidDataException)
        {
            return new(false, null);
        }
        finally
        {
            File.Delete(converted);
        }
    }
}
