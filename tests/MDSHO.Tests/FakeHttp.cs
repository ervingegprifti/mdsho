using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MDSHO.Tests {

  /// <summary>A recorded request: method, URL and body text.</summary>
  public sealed record FakeRequest(string Method, string Url, string Body, string Authorization, string ContentType, byte[] BodyBytes = null);

  /// <summary>An HTTP handler for tests: answers each request with <see cref="Respond"/> and records it.</summary>
  public sealed class FakeHttp : HttpMessageHandler {
    private readonly object gate = new object();

    public FakeHttp(Func<FakeRequest, HttpResponseMessage> respond) { Respond = respond; }

    public Func<FakeRequest, HttpResponseMessage> Respond { get; set; }
    public List<FakeRequest> Requests { get; } = new List<FakeRequest>();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
      byte[] bytes = request.Content == null ? new byte[0] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
      string body = Encoding.UTF8.GetString(bytes);
      FakeRequest r = new FakeRequest(request.Method.Method, request.RequestUri.ToString(), body,
        request.Headers.Authorization?.ToString(), request.Content?.Headers.ContentType?.MediaType, bytes);
      lock (this.gate) {
        Requests.Add(r);
      }
      HttpResponseMessage response = Respond(r);
      response.RequestMessage = request;
      return response;
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode code = HttpStatusCode.OK) {
      return new HttpResponseMessage(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    public static HttpResponseMessage Bytes(byte[] bytes) {
      return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
    }

    public static HttpResponseMessage Status(HttpStatusCode code, string retryAfterSeconds = null) {
      HttpResponseMessage m = new HttpResponseMessage(code) { Content = new StringContent("") };
      if (retryAfterSeconds != null) {
        m.Headers.TryAddWithoutValidation("Retry-After", retryAfterSeconds);
      }
      return m;
    }
  }
}
