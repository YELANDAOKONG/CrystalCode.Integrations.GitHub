namespace CrystalCode.Integrations.GitHub.Tests;

internal static class TestClients
{
    public const string Token = "ghp_testtoken";

    public static readonly Uri Api = new("https://api.github.test");

    public static GitHubClient Create(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler), Api, Token);
}
