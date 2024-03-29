using Moq;
using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;

namespace WalletWasabi.Tests.Helpers;

/// <summary>
/// Builder class for <see cref="Arena"/>.
/// </summary>
public class ArenaBuilder
{
	public static ArenaBuilder Default => new();

	public TimeSpan? Period { get; set; }
	public Network? Network { get; set; }
	public WabiSabiConfig? Config { get; set; }
	public IRPCClient? Rpc { get; set; }
	public Prison? Prison { get; set; }
	public RoundParameterFactory? RoundParameterFactory { get; set;  }

	/// <param name="rounds">Rounds to initialize <see cref="Arena"/> with.</param>
	public Arena Create(params Round[] rounds)
	{
		TimeSpan period = Period ?? TimeSpan.FromHours(1);
		Prison prison = Prison ?? new();
		WabiSabiConfig config = Config ?? new();
		IRPCClient rpc = Rpc ?? WabiSabiFactory.CreatePreconfiguredRpcClient().Object;
		Network network = Network ?? Network.Main;
		RoundParameterFactory roundParameterFactory = RoundParameterFactory ?? CreateRoundParameterFactory(config, network);

		var testeableCfg = new TesteableOptionsMonitor<WabiSabiConfig>(config);
		Arena arena = new(network, testeableCfg, rpc, prison, roundParameterFactory, NullLogger<Arena>.Instance,  period: period);

		foreach (var round in rounds)
		{
			arena.Rounds.Add(round);
		}

		return arena;
	}

	public Task<Arena> CreateAndStartAsync(params Round[] rounds)
		=> CreateAndStartAsync(rounds, CancellationToken.None);

	public async Task<Arena> CreateAndStartAsync(Round[] rounds, CancellationToken cancellationToken = default)
	{
		Arena? toDispose = null;

		try
		{
			toDispose = Create(rounds);
			Arena arena = toDispose;
			await arena.StartAsync(cancellationToken).ConfigureAwait(false);
			toDispose = null;
			return arena;
		}
		finally
		{
			toDispose?.Dispose();
		}
	}

	public ArenaBuilder With(IMock<IRPCClient> rpc) =>
		With(rpc.Object);

	public ArenaBuilder With(IRPCClient rpc)
	{
		Rpc = rpc;
		return this;
	}

	public ArenaBuilder With(RoundParameterFactory roundParameterFactory)
	{
		RoundParameterFactory = roundParameterFactory;
		return this;
	}

	public static ArenaBuilder From(WabiSabiConfig cfg) => new() { Config = cfg };

	public static ArenaBuilder From(WabiSabiConfig cfg, Prison prison) => new() { Config = cfg, Prison = prison };

	public static ArenaBuilder From(WabiSabiConfig cfg, IMock<IRPCClient> mockRpc, Prison prison) => new() { Config = cfg, Rpc = mockRpc.Object, Prison = prison };

	private static RoundParameterFactory CreateRoundParameterFactory(WabiSabiConfig cfg, Network network) =>
		WabiSabiFactory.CreateRoundParametersFactory(cfg, network, maxVsizeAllocationPerAlice: 11 + 31 + MultipartyTransactionParameters.SharedOverhead);

	private static IOptionsMonitor<WabiSabiConfig> CreateMockOptionsMonitor(WabiSabiConfig wabiSabiConfig)
	{
		var mockOptionsMonitor = new Mock<IOptionsMonitor<WabiSabiConfig>>();
		mockOptionsMonitor.SetupGet(x => x.CurrentValue).Returns(wabiSabiConfig);
		return mockOptionsMonitor.Object;
	}
}
