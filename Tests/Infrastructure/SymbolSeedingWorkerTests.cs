using Domain.Entities;
using Domain.Interfaces;
using FluentAssertions;
using Infrastructure.Ingestion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Tests.Infrastructure;

public class SymbolSeedingWorkerTests
{
    private readonly ITrackedSymbolRepository _repository = Substitute.For<ITrackedSymbolRepository>();
    private readonly ILogger<SymbolSeedingWorker> _logger = Substitute.For<ILogger<SymbolSeedingWorker>>();

    private SymbolSeedingWorker CreateWorker(List<string> seedGroups)
    {
        var options = Options.Create(new IngestionOptions { SeedGroups = seedGroups });
        var services = new ServiceCollection();
        services.AddSingleton(_repository);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        return new SymbolSeedingWorker(scopeFactory, options, _logger);
    }

    private static async Task RunWorkerAsync(SymbolSeedingWorker worker)
    {
        await worker.StartAsync(CancellationToken.None);
        // ExecuteAsync is fire-and-forget from StartAsync — wait for it to complete
        if (worker.ExecuteTask is not null)
            await worker.ExecuteTask;
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoSeedGroups_DoesNothing()
    {
        var worker = CreateWorker([]);
        await RunWorkerAsync(worker);

        await _repository.DidNotReceive().ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().AddAsync(Arg.Any<TrackedSymbol>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithCryptoGroup_SeedsAllNewSymbols()
    {
        _repository.ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var worker = CreateWorker(["crypto"]);

        await RunWorkerAsync(worker);

        await _repository.Received(15).AddAsync(Arg.Any<TrackedSymbol>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SkipsExistingSymbols()
    {
        _repository.ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _repository.ExistsAsync("BTC-USD", Arg.Any<CancellationToken>()).Returns(true);
        _repository.ExistsAsync("ETH-USD", Arg.Any<CancellationToken>()).Returns(true);

        var worker = CreateWorker(["crypto"]);

        await RunWorkerAsync(worker);

        await _repository.Received(13).AddAsync(Arg.Any<TrackedSymbol>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithKebabCaseGroupName_ParsesCorrectly()
    {
        _repository.ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var worker = CreateWorker(["us-bluechip"]);

        await RunWorkerAsync(worker);

        await _repository.Received(30).AddAsync(Arg.Any<TrackedSymbol>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownGroup_SkipsIt()
    {
        var worker = CreateWorker(["nonexistent"]);

        await RunWorkerAsync(worker);

        await _repository.DidNotReceive().AddAsync(Arg.Any<TrackedSymbol>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleGroups_SeedsAll()
    {
        _repository.ExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var worker = CreateWorker(["crypto", "etfs"]);

        await RunWorkerAsync(worker);

        // 15 crypto + 10 etfs = 25
        await _repository.Received(25).AddAsync(Arg.Any<TrackedSymbol>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void NormaliseGroupName_KebabCase_ReturnsPascalCase()
    {
        SymbolSeedingWorker.NormaliseGroupName("us-bluechip").Should().Be("UsBluechip");
        SymbolSeedingWorker.NormaliseGroupName("asx-bluechip").Should().Be("AsxBluechip");
        SymbolSeedingWorker.NormaliseGroupName("crypto").Should().Be("Crypto");
    }
}
