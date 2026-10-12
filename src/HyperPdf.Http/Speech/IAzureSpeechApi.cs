// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Refit;

namespace HyperPdf.Http.Speech;

/// <summary>Azure AI Speech's text to speech REST endpoint, used with the person's own key.</summary>
[Headers("User-Agent: HyperPDF", "X-Microsoft-OutputFormat: raw-24khz-16bit-mono-pcm")]
public interface IAzureSpeechApi
{
    /// <summary>Speaks SSML into 24 kHz 16 bit mono PCM.</summary>
    /// <param name="key">The subscription key.</param>
    /// <param name="ssml">The SSML document, sent as <c>application/ssml+xml</c>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The response; the caller disposes it.</returns>
    [Post("/cognitiveservices/v1")]
    Task<HttpResponseMessage> SynthesizeAsync([Header("Ocp-Apim-Subscription-Key")] string key, [Body] HttpContent ssml, CancellationToken cancellationToken);
}
