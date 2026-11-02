using System.Net;
using System.Text;

namespace DiscoTrack.Infrastructure.Tests;

public sealed class StubHttpHandler : HttpMessageHandler
{
	private readonly List<(Func<HttpRequestMessage, bool> Match, string Body)> _responses = [];

	public List<HttpRequestMessage> Requests { get; } = [];

	public StubHttpHandler When(Func<HttpRequestMessage, bool> match, string body)
	{
		_responses.Add((match, body));
		return this;
	}

	public StubHttpHandler WhenPathContains(string fragment, string body)
	{
		return When(r => r.RequestUri!.PathAndQuery.Contains(fragment, StringComparison.Ordinal), body);
	}

	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
	{
		Requests.Add(request);

		var match = _responses.FirstOrDefault(r => r.Match(request));
		if (match.Body is null)
		{
			throw new InvalidOperationException($"No stub for {request.RequestUri}");
		}

		return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(match.Body, Encoding.UTF8, "application/json"),
		});
	}
}
