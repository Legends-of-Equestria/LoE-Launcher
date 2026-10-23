using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace LoE_Launcher.Services;

public class DevTokenHandler(Settings settings, HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        AddToken(request);
        return base.SendAsync(request, cancellationToken);
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        AddToken(request);
        return base.Send(request, cancellationToken);
    }

    private void AddToken(HttpRequestMessage request)
    {
        if (settings.ShouldSendDevToken(request.RequestUri))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.DevToken.Trim());
        }
    }
}
