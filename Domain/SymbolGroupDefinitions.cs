using Domain.Enums;

namespace Domain;

/// <summary>
/// Curated lists of symbols for bulk seeding. These are static reference data
/// owned by the domain — no external dependencies required.
/// </summary>
public static class SymbolGroupDefinitions
{
    private static readonly IReadOnlyDictionary<SymbolGroup, IReadOnlyList<string>> Groups =
        new Dictionary<SymbolGroup, IReadOnlyList<string>>
        {
            [SymbolGroup.UsBluechip] =
            [
                "AAPL", "MSFT", "GOOGL", "AMZN", "NVDA", "META", "TSLA", "JPM", "V", "JNJ",
                "UNH", "WMT", "PG", "MA", "HD", "BAC", "XOM", "KO", "PFE", "ABBV",
                "CVX", "MRK", "COST", "PEP", "TMO", "AVGO", "LLY", "ORCL", "CSCO", "ACN"
            ],
            [SymbolGroup.AsxBluechip] =
            [
                "BHP.AX", "CBA.AX", "CSL.AX", "WBC.AX", "ANZ.AX", "NAB.AX", "WES.AX",
                "MQG.AX", "FMG.AX", "TLS.AX", "WOW.AX", "RIO.AX", "ALL.AX", "STO.AX",
                "COL.AX", "QBE.AX", "NCM.AX", "WPL.AX", "TCL.AX", "AMC.AX"
            ],
            [SymbolGroup.Crypto] =
            [
                "BTC-USD", "ETH-USD", "SOL-USD", "XRP-USD", "ADA-USD", "DOGE-USD",
                "AVAX-USD", "DOT-USD", "MATIC-USD", "LINK-USD", "ATOM-USD", "UNI-USD",
                "LTC-USD", "BCH-USD", "NEAR-USD"
            ],
            [SymbolGroup.Tech] =
            [
                "AAPL", "MSFT", "GOOGL", "META", "NVDA", "AMD", "INTC", "CRM", "ORCL", "ADBE",
                "CSCO", "AVGO", "TXN", "QCOM", "NOW", "PANW", "SNPS", "CDNS", "MRVL", "KLAC"
            ],
            [SymbolGroup.Etfs] =
            [
                "SPY", "QQQ", "IWM", "DIA", "VTI", "VOO", "VEA", "VWO", "BND", "GLD"
            ]
        };

    /// <summary>
    /// Returns the symbols in the given group, or null if the group is not defined.
    /// </summary>
    public static IReadOnlyList<string>? GetSymbols(SymbolGroup group) =>
        Groups.GetValueOrDefault(group);

    /// <summary>
    /// Returns all available group names.
    /// </summary>
    public static IReadOnlyList<SymbolGroup> AvailableGroups => Groups.Keys.ToList().AsReadOnly();
}
