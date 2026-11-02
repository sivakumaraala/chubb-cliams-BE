using Microsoft.Extensions.Configuration;

namespace Chubb.Claims.Application;

public sealed record DemoMarket(string Code, string Name, string[] Currencies);

public sealed class DemoMarketCatalog
{
    public DemoMarketCatalog(IConfiguration configuration)
    {
        Markets = configuration.GetSection("DemoMarkets").Get<DemoMarket[]>()
            ?? throw new InvalidOperationException("Configure the six illustrative DemoMarkets.");
        if (Markets.Length != 6 || Markets.Select(x => x.Code).Distinct().Count() != 6 ||
            Markets.Any(x => string.IsNullOrWhiteSpace(x.Code) || x.Currencies.Length == 0 ||
                x.Currencies.Any(c => c is not ("SGD" or "AUD"))))
            throw new InvalidOperationException("Demo markets require six distinct codes and supported two-decimal currencies (SGD/AUD).");
    }

    public DemoMarket[] Markets { get; }
}
