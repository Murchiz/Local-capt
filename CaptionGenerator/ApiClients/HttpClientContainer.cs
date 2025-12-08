using System.Net.Http;

namespace CaptionGenerator.ApiClients;

public static class HttpClientContainer
{
    public static readonly HttpClient Client = new();
}
