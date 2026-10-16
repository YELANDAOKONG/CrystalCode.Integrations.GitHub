using System.Net;
using System.Text;

namespace CrystalCode.Integrations.GitHub.Tests;

internal static class TestResponses
{
    public static HttpResponseMessage Json(string body, string? link = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        if (link is not null)
        {
            response.Headers.TryAddWithoutValidation("Link", link);
        }

        return response;
    }

    public static HttpResponseMessage Text(string body, string mediaType)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
        response.Content = content;
        return response;
    }

    public static HttpResponseMessage Status(HttpStatusCode status, string body)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        return response;
    }
}
