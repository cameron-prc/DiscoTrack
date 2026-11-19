using System.Net;
using System.Text;

namespace DiscoTrack.Infrastructure.Tests;

public sealed class StubHttpHandler : HttpMessageHandler
{
	private readonly List<(Func<HttpRequestMessage, bool> Match, string Body, HttpStatusCode Status)> _responses = [];

	public List<HttpRequestMessage> Requests { get; } = [];

	public StubHttpHandler When(Func<HttpRequestMessage, bool> match, string body, HttpStatusCode status = HttpStatusCode.OK)
	{
		_responses.Add((match, body, status));
		return this;
	}

	public StubHttpHandler WhenPathContains(string fragment, string body, HttpStatusCode status = HttpStatusCode.OK)
	{
		return When(r => r.RequestUri!.PathAndQuery.Contains(fragment, StringComparison.Ordinal), body, status);
	}

	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
	{
		Requests.Add(request);

		var match = _responses.FirstOrDefault(r => r.Match(request));
		if (match.Body is null)
		{
			throw new InvalidOperationException($"No stub for {request.RequestUri}");
		}

		return Task.FromResult(new HttpResponseMessage(match.Status)
		{
			Content = new StringContent(match.Body, Encoding.UTF8, "application/json"),
		});
	}
}
