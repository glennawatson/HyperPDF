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
    /// <returns>A cold signal giving the response once, which the subscriber disposes; unsubscribing cancels the request.</returns>
    [Post("/cognitiveservices/v1")]
    IObservable<HttpResponseMessage> Synthesize([Header("Ocp-Apim-Subscription-Key")] string key, [Body] HttpContent ssml);
}
