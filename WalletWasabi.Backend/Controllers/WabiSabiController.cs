using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WalletWasabi.Backend.Controllers.WabiSabi;
using WalletWasabi.Backend.Filters;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Backend.Statistics;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.Backend.Controllers;

[ApiController]
[ExceptionTranslate]
[LateResponseLoggerFilter]
[Route("[controller]")]
[Produces("application/json")]
public class WabiSabiController : ControllerBase, IWabiSabiApiRequestHandler
{
	public WabiSabiController(IdempotencyRequestCache idempotencyRequestCache, Arena arena, CoinJoinFeeRateStatStore coinJoinFeeRateStatStore)
	{
		IdempotencyRequestCache = idempotencyRequestCache;
		Arena = arena;
		CoinJoinFeeRateStatStore = coinJoinFeeRateStatStore;
	}

	private IdempotencyRequestCache IdempotencyRequestCache { get; }
	private Arena Arena { get; }
	private CoinJoinFeeRateStatStore CoinJoinFeeRateStatStore { get; }

	[HttpPost("status")]
	public async Task<RoundStateResponse> GetStatusAsync(RoundStateRequest request, CancellationToken cancellationToken)
	{
		var response = await Arena.GetStatusAsync(request, cancellationToken);
		var medians = CoinJoinFeeRateStatStore.GetDefaultMedians();
		return new RoundStateResponse(response.RoundStates, medians);
	}

	[HttpPost("connection-confirmation")]
	public async Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request, CancellationToken cancellationToken)
	{
		return await IdempotencyRequestCache.GetCachedResponseAsync(request, action: (request, token) => Arena.ConfirmConnectionAsync(request, token), cancellationToken);
	}

	[HttpPost("input-registration")]
	public async Task<InputRegistrationResponse> RegisterInputAsync(InputRegistrationRequest request, CancellationToken cancellationToken)
	{
		return await IdempotencyRequestCache.GetCachedResponseAsync(request, action: (request, token) => Arena.RegisterInputAsync(request, token), cancellationToken);
	}

	[HttpPost("output-registration")]
	public async Task RegisterOutputAsync(OutputRegistrationRequest request, CancellationToken cancellationToken)
	{
		var before = DateTimeOffset.UtcNow;
		await IdempotencyRequestCache.GetCachedResponseAsync(request, action: (request, token) => Arena.RegisterOutputCoreAsync(request, token), cancellationToken);

		var duration = DateTimeOffset.UtcNow - before;
		RequestTimeStatista.Instance.Add("output-registration", duration);
	}

	[HttpPost("credential-issuance")]
	public async Task<ReissueCredentialResponse> ReissuanceAsync(ReissueCredentialRequest request, CancellationToken cancellationToken)
	{
		return await IdempotencyRequestCache.GetCachedResponseAsync(request, action: (request, token) => Arena.ReissuanceAsync(request, token), cancellationToken);
	}

	[HttpPost("input-unregistration")]
	public async Task RemoveInputAsync(InputsRemovalRequest request, CancellationToken cancellableToken)
	{
		await Arena.RemoveInputAsync(request, cancellableToken);
	}

	[HttpPost("transaction-signature")]
	public async Task SignTransactionAsync(TransactionSignaturesRequest request, CancellationToken cancellableToken)
	{
		await Arena.SignTransactionAsync(request, cancellableToken);
	}

	[HttpPost("ready-to-sign")]
	public async Task ReadyToSignAsync(ReadyToSignRequestRequest request, CancellationToken cancellableToken)
	{
		await Arena.ReadyToSignAsync(request, cancellableToken);

		var duration = DateTimeOffset.UtcNow - before;
		RequestTimeStatista.Instance.Add("ready-to-sign", duration);
	}

	/// <summary>
	/// Information about the current Rounds designed for the human eyes.
	/// </summary>
	[HttpGet("human-monitor")]
	public HumanMonitorResponse GetHumanMonitor()
	{
		var response = Arena.Rounds
			.Where(r => r.Phase is not Phase.Ended)
			.OrderByDescending(x => x.InputCount)
			.Select(r =>
				new HumanMonitorRoundResponse(
					RoundId: r.Id,
					IsBlameRound: r is BlameRound,
					InputCount: r.InputCount,
					Phase: r.Phase.ToString(),
					MaxSuggestedAmount: r.Parameters.MaxSuggestedAmount.ToDecimal(NBitcoin.MoneyUnit.BTC),
					InputRegistrationRemaining: r.InputRegistrationTimeFrame.EndTime - DateTimeOffset.UtcNow));

		return new HumanMonitorResponse(response.ToArray());
	}
}
