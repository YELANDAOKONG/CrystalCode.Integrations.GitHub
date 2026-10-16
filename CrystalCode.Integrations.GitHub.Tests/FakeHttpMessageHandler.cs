namespace CrystalCode.Integrations.GitHub.Tests;

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<CapturedRequest> Requests { get; } = [];

    public FakeHttpMessageHandler Respond(Func<HttpRequestMessage, HttpResponseMessage> response)
    {
        _responses.Enqueue(response);
        return this;
    }

    public FakeHttpMessageHandler Respond(HttpResponseMessage response) =>
        Respond(_ => response);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in request.Headers)
        {
            headers[header.Key] = string.Join(", ", header.Value);
        }

        if (request.Content is not null)
        {
            foreach (var header in request.Content.Headers)
            {
                headers[header.Key] = string.Join(", ", header.Value);
            }
        }

        Requests.Add(new CapturedRequest(request.Method.Method, request.RequestUri, headers, body));
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("The fake handler has no scripted response.");
        }

        return _responses.Dequeue().Invoke(request);
    }
}
