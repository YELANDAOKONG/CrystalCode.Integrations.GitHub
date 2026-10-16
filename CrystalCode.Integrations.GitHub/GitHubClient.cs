using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CrystalCode.Integrations.GitHub;

/// <summary>
/// Sends GitHub REST requests. Call <see cref="SendAsync"/> for an endpoint
/// that has no helper. The client does not follow redirects.
/// </summary>
public sealed class GitHubClient
{
    private readonly HttpClient _http;
    private readonly Uri _api;
    private readonly string _token;

    public GitHubClient(HttpClient http, Uri api, string token)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(api);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        if (!string.Equals(api.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("GitHub API URL must use HTTPS.", nameof(api));
        }

        if (!string.IsNullOrEmpty(api.UserInfo))
        {
            throw new ArgumentException("GitHub API URL must not include credentials.", nameof(api));
        }

        if (token.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("GitHub token must not contain whitespace.", nameof(token));
        }

        _http = http;
        _api = api;
        _token = token;
    }

    public Uri Api => _api;

    public static HttpClient CreateHttp()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = GitHubLimits.ConnectionLifetime
        };
        return new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    public async Task<GitHubResponse> SendAsync(GitHubRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(GitHubLimits.RequestTimeout);
        using var message = new HttpRequestMessage(request.Method, BuildUri(request.Path));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        message.Headers.UserAgent.ParseAdd(GitHubLimits.UserAgent);
        message.Headers.Accept.ParseAdd(request.Accept ?? GitHubLimits.DefaultAccept);
        message.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", GitHubLimits.ApiVersion);
        if (request.JsonBody is not null)
        {
            message.Content = new StringContent(request.JsonBody, Encoding.UTF8, "application/json");
        }

        try
        {
            using var response = await _http
                .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);
            var (text, truncated) = await ReadBodyAsync(response, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var detail = truncated ? null : TryMessage(text);
                throw new GitHubException(
                    detail is null
                        ? $"GitHub returned HTTP status {(int)response.StatusCode}."
                        : $"GitHub returned HTTP status {(int)response.StatusCode}: {detail}");
            }

            var media = response.Content?.Headers.ContentType?.MediaType ?? string.Empty;
            if (truncated && media.Contains("json", StringComparison.OrdinalIgnoreCase))
            {
                throw new GitHubException("GitHub response exceeded 1 MiB.");
            }

            if (truncated)
            {
                text += GitHubLimits.TruncationNote;
            }

            return new GitHubResponse((int)response.StatusCode, text, truncated, CopyHeaders(response));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GitHubException("GitHub request failed or timed out.");
        }
        catch (HttpRequestException)
        {
            throw new GitHubException("GitHub request failed or timed out.");
        }
    }

    private Uri BuildUri(string path)
    {
        var baseUri = _api.AbsoluteUri.EndsWith('/') ? _api.AbsoluteUri : _api.AbsoluteUri + "/";
        if (!Uri.TryCreate(baseUri + path.TrimStart('/'), UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, _api.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(uri.Authority, _api.Authority, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("GitHub path must stay on the API host.", nameof(path));
        }

        return uri;
    }

    private Dictionary<string, string> CopyHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Add(response.Headers);
        if (response.Content is not null)
        {
            Add(response.Content.Headers);
        }

        return headers;

        void Add(IEnumerable<KeyValuePair<string, IEnumerable<string>>> source)
        {
            foreach (var header in source)
            {
                var value = string.Join(", ", header.Value);
                if (value.Contains(_token, StringComparison.Ordinal))
                {
                    continue;
                }

                headers[header.Key] = value;
            }
        }
    }

    private static async Task<(string Text, bool Truncated)> ReadBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content is null)
        {
            return (string.Empty, false);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[GitHubLimits.ReadBufferBytes];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return (Decode(buffer), false);
            }

            var room = GitHubLimits.MaximumResponseBytes - (int)buffer.Length;
            if (read > room)
            {
                if (room > 0)
                {
                    buffer.Write(chunk, 0, room);
                }

                return (Decode(buffer), true);
            }

            buffer.Write(chunk, 0, read);
            if (buffer.Length != GitHubLimits.MaximumResponseBytes)
            {
                continue;
            }

            var extra = await stream.ReadAsync(chunk.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
            return (Decode(buffer), extra > 0);
        }
    }

    private static string Decode(MemoryStream buffer) =>
        Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);

    private static string? TryMessage(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("message", out var message)
                || message.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var text = message.GetString();
            if (string.IsNullOrWhiteSpace(text)
                || text.Length > GitHubLimits.MaximumErrorDetailCharacters
                || text.Contains('\n')
                || text.Contains('\r')
                || text.Contains("ghp_", StringComparison.Ordinal)
                || text.Contains("github_pat_", StringComparison.Ordinal))
            {
                return null;
            }

            return text;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
