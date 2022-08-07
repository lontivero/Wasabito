using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using NBitcoin;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Backend.Banning;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend;

public class UtxoPrisonWardenTests
{
	[Fact]
	public async Task CanStartAndStopAsync()
	{
		var workDir = Common.GetWorkDir(nameof(CanStartAndStopAsync));
		await IoHelpers.TryDeleteDirectoryAsync(workDir);
		var mockDoSOptions = CreateDoSOptions();
		using var w = new Warden(mockDoSOptions.Object);
		await w.StartAsync(CancellationToken.None);
		await w.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task PrisonSerializationAsync()
	{
		var workDir = Common.GetWorkDir(nameof(PrisonSerializationAsync));
		await IoHelpers.TryDeleteDirectoryAsync(workDir);

		// Create prison.
		var mockDoSOptions = CreateDoSOptions();
		using var w = new Warden(mockDoSOptions.Object);
		await w.StartAsync(CancellationToken.None);
		var i1 = new Inmate(BitcoinFactory.CreateOutPoint(), Punishment.Noted, DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()), uint256.Zero);
		var i2 = new Inmate(BitcoinFactory.CreateOutPoint(), Punishment.Banned, DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()), uint256.Zero);
		w.Prison.Punish(i1);
		w.Prison.Punish(i2);

		// Wait until serializes.
		await w.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(7));
		await w.StopAsync(CancellationToken.None);

		// See if prev UTXOs are loaded.
		using var w2 = new Warden(mockDoSOptions.Object);
		await w2.StartAsync(CancellationToken.None);

		Assert.True(w2.Prison.TryGet(i1.Utxo, out var sameI1));
		Assert.True(w2.Prison.TryGet(i2.Utxo, out var sameI2));
		Assert.Equal(i1.LastDisruptedRoundId, sameI1!.LastDisruptedRoundId);
		Assert.Equal(i2.LastDisruptedRoundId, sameI2!.LastDisruptedRoundId);
		Assert.Equal(i1.Punishment, sameI1!.Punishment);
		Assert.Equal(i2.Punishment, sameI2!.Punishment);
		Assert.Equal(i1.Started, sameI1!.Started);
		Assert.Equal(i2.Started, sameI2!.Started);

		await w2.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task NoPrisonSerializationAsync()
	{
		// Don't serialize when there's no change.
		var workDir = Common.GetWorkDir(nameof(NoPrisonSerializationAsync));
		await IoHelpers.TryDeleteDirectoryAsync(workDir);

		// Create prison.
		var mockDoSOptions = new TesteableOptionsMonitor<DoSOptions>(new DoSOptions());
		using var w = new Warden(mockDoSOptions);
		await w.StartAsync(CancellationToken.None);
		var i1 = new Inmate(BitcoinFactory.CreateOutPoint(), Punishment.Noted, DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()), uint256.Zero);
		var i2 = new Inmate(BitcoinFactory.CreateOutPoint(), Punishment.Banned, DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()), uint256.Zero);
		w.Prison.Punish(i1);
		w.Prison.Punish(i2);

		// Wait until serializes.
		await w.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(7));

		// Make sure it does not serialize again as there was no change.
		File.Delete(w.DoSOptions.CurrentValue.PrisonFilePath);
		await w.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(7));
		Assert.False(File.Exists(w.DoSOptions.CurrentValue.PrisonFilePath));
		await w.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task ReleasesInmatesAsync()
	{
		var workDir = Common.GetWorkDir(nameof(ReleasesInmatesAsync));
		await IoHelpers.TryDeleteDirectoryAsync(workDir);

		// Create prison.
		var mockDoSOptions = new Mock<IOptionsMonitor<DoSOptions>>();
		mockDoSOptions.Setup(x => x.CurrentValue).Returns(
			new DoSOptions
			{
				PrisonFilePath = "prision.txt",
				ReleaseUtxoFromPrisonAfter = TimeSpan.FromMilliseconds(1)
			});

		using var w = new Warden(mockDoSOptions.Object);
		await w.StartAsync(CancellationToken.None);
		var i1 = new Inmate(BitcoinFactory.CreateOutPoint(), Punishment.Noted, DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()), uint256.Zero);
		var i2 = new Inmate(BitcoinFactory.CreateOutPoint(), Punishment.Banned, DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()), uint256.Zero);
		var p = w.Prison;
		p.Punish(i1);
		p.Punish(i2);
		Assert.NotEmpty(p.GetInmates());

		// Wait until releases from prison.
		await w.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(7));
		Assert.Empty(p.GetInmates());
		await w.StopAsync(CancellationToken.None);
	}

	private static Mock<IOptionsMonitor<DoSOptions>> CreateDoSOptions()
	{
		DoSOptions options = new();
		var mockDoSOptions = new Mock<IOptionsMonitor<DoSOptions>>();
		mockDoSOptions.Setup(o => o.CurrentValue).Returns(options);
		return mockDoSOptions;
	}
}
