using NBitcoin;
using NBitcoin.RPC;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using WalletWasabi.Bases;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using WalletWasabi.WabiSabi.Backend.Statistics;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;

namespace WalletWasabi.WabiSabi.Backend.Rounds;

public partial class Arena : PeriodicRunner
{
	private readonly ILogger<Arena> _logger;

	public Arena(
		Network network,
		IOptionsMonitor<WabiSabiConfig> config,
		IRPCClient rpc,
		Prison prison,
		ICoinJoinIdStore coinJoinIdStore,
		RoundParameterFactory roundParameterFactory,
		ILogger<Arena> logger,
		CoinJoinScriptStore? coinJoinScriptStore = null,
		TimeSpan? period = null
		) : base(period ?? TimeSpan.FromSeconds(10))
	{
		_logger = logger;
		Network = network;
		Config = config;
		Rpc = rpc;
		Prison = prison;
		CoinJoinIdStore = coinJoinIdStore;
		CoinJoinScriptStore = coinJoinScriptStore;
		RoundParameterFactory = roundParameterFactory;
		MaxSuggestedAmountProvider = new(Config.CurrentValue);
	}

	public event EventHandler<Transaction>? CoinJoinBroadcast;

	public HashSet<Round> Rounds { get; } = new();
	private IEnumerable<RoundState> RoundStates { get; set; } = Enumerable.Empty<RoundState>();
	private AsyncLock AsyncLock { get; } = new();
	private Network Network { get; }
	private IOptionsMonitor<WabiSabiConfig> Config { get; }
	internal IRPCClient Rpc { get; }
	private Prison Prison { get; }
	public CoinJoinScriptStore? CoinJoinScriptStore { get; }
	private ICoinJoinIdStore CoinJoinIdStore { get; set; }
	private RoundParameterFactory RoundParameterFactory { get; }
	public MaxSuggestedAmountProvider MaxSuggestedAmountProvider { get; }

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		var before = DateTimeOffset.UtcNow;
		using (await AsyncLock.LockAsync(cancel).ConfigureAwait(false))
		{
			TimeoutRounds();

			TimeoutAlices();

			await StepTransactionSigningPhaseAsync(cancel).ConfigureAwait(false);

			await StepOutputRegistrationPhaseAsync(cancel).ConfigureAwait(false);

			await StepConnectionConfirmationPhaseAsync(cancel).ConfigureAwait(false);

			await StepInputRegistrationPhaseAsync(cancel).ConfigureAwait(false);

			cancel.ThrowIfCancellationRequested();

			// Ensure there's at least one non-blame round in input registration.
			await CreateRoundsAsync(cancel).ConfigureAwait(false);

			// RoundStates have to contain all states. Do not change stateId=0.
			SetRoundStates();
		}
		var duration = DateTimeOffset.UtcNow - before;
		RequestTimeStatista.Instance.Add("arena-period", duration);
	}

	private void SetRoundStates()
	{
		// Order rounds ascending by max suggested amount, then ascending by input count.
		// This will make sure WW2.0.1 clients register according to our desired order.
		var rounds = Rounds
						.OrderBy(x => x.Parameters.MaxSuggestedAmount)
						.ThenBy(x => x.InputCount)
						.ToList();

		var standardRegistrableRounds = rounds
			.Where(x =>
				x.Phase == Phase.InputRegistration
				&& x is not BlameRound
				&& !x.IsInputRegistrationEnded(x.Parameters.MaxInputCountByRound))
			.ToArray();

		// Let's make sure WW2.0.1 clients prefer rounds that WW2.0.0 clients don't.
		// In WW2.0.0 on client side we accidentally order rounds by calling .ToImmutableDictionary(x => x.Id, x => x)
		// therefore whichever round ToImmutableDictionary would make to be the first round, we send it to the back of our list.
		// With this we can achieve that WW2.0.0 and WW2.0.1 clients prefer different rounds in parallel round configuration.
		if (standardRegistrableRounds.Any())
		{
			var firstRegistrableRoundAccordingToWW200 = standardRegistrableRounds
				.ToImmutableDictionary(x => x.Id, x => x)
				.First()
				.Value;

			// Remove from wherever WW2.0.0's most preferred round is, then add it back to the end of our list.
			rounds.Remove(firstRegistrableRoundAccordingToWW200);
			rounds.Add(firstRegistrableRoundAccordingToWW200);
		}

		RoundStates = rounds.Select(r => RoundState.FromRound(r, stateId: 0));
	}

	private async Task StepInputRegistrationPhaseAsync(CancellationToken cancel)
	{
		foreach (var round in Rounds.Where(x =>
			x.Phase == Phase.InputRegistration
			&& x.IsInputRegistrationEnded(Config.CurrentValue.MaxInputCountByRound))
			.ToArray())
		{
			try
			{
				await foreach (var offendingAlices in CheckTxoSpendStatusAsync(round, cancel).ConfigureAwait(false))
				{
					if (offendingAlices.Any())
					{
						round.Alices.RemoveAll(x => offendingAlices.Contains(x));
					}
				}

				if (round.InputCount < Config.CurrentValue.MinInputCountByRound)
				{
					if (!round.InputRegistrationTimeFrame.HasExpired)
					{
						continue;
					}

					MaxSuggestedAmountProvider.StepMaxSuggested(round, false);
					round.EndRound(EndRoundState.AbortedNotEnoughAlices);

					_logger.LogInformation("Not enough inputs ({inputs}) in {phase} phase. Minimum is {minimum}.", round.InputCount, nameof(Phase.InputRegistration), Config.CurrentValue.MinInputCountByRound); // FIXME: use round parameters instead of Config
				}
				else if (round.IsInputRegistrationEnded(Config.CurrentValue.MaxInputCountByRound))
				{
					MaxSuggestedAmountProvider.StepMaxSuggested(round, true);
					round.SetPhase(Phase.ConnectionConfirmation);
				}
			}
			catch (Exception ex)
			{
				round.EndRound(EndRoundState.AbortedWithError);
				_logger.LogError(ex, "Unexpected error.");
			}
		}
	}

	private async Task StepConnectionConfirmationPhaseAsync(CancellationToken cancel)
	{
		foreach (var round in Rounds.Where(x => x.Phase == Phase.ConnectionConfirmation).ToArray())
		{
			try
			{
				if (round.Alices.All(x => x.ConfirmedConnection))
				{
					round.SetPhase(Phase.OutputRegistration);
				}
				else if (round.ConnectionConfirmationTimeFrame.HasExpired)
				{
					var alicesDidntConfirm = round.Alices.Where(x => !x.ConfirmedConnection).ToArray();
					foreach (var alice in alicesDidntConfirm)
					{
						Prison.Note(alice, round.Id);
					}
					var removedAliceCount = round.Alices.RemoveAll(x => alicesDidntConfirm.Contains(x));
					_logger.LogInformation("{removedAliceCount} alices removed because they didn't confirm.", removedAliceCount);

					// Once an input is confirmed and non-zero credentials are issued, it must be included and must provide a
					// a signature for a valid transaction to be produced, therefore this is the last possible opportunity to
					// remove any spent inputs.
					if (round.InputCount >= Config.CurrentValue.MinInputCountByRound)
					{
						await foreach (var offendingAlices in CheckTxoSpendStatusAsync(round, cancel).ConfigureAwait(false))
						{
							if (offendingAlices.Any())
							{
								var removed = round.Alices.RemoveAll(x => offendingAlices.Contains(x));
								_logger.LogInformation("There were {removedAliceCount} alices removed because they spent the registered UTXO.", removed);
							}
						}
					}

					if (round.InputCount < Config.CurrentValue.MinInputCountByRound)
					{
						round.EndRound(EndRoundState.AbortedNotEnoughAlices);
						_logger.LogInformation("Not enough inputs ({inputs}) in {phase} phase. Minimum is {minimum}.", round.InputCount, nameof(Phase.ConnectionConfirmation), Config.CurrentValue.MinInputCountByRound);
					}
					else
					{
						round.OutputRegistrationTimeFrame = TimeFrame.Create(Config.CurrentValue.FailFastOutputRegistrationTimeout);
						round.SetPhase(Phase.OutputRegistration);
						_logger.LogInformation($"Phase changed: {round.Phase} -> {Phase.OutputRegistration}");
					}
				}
			}
			catch (Exception ex)
			{
				round.EndRound(EndRoundState.AbortedWithError);
				_logger.LogError(ex, "Unexpected error.");
			}
		}
	}

	private async Task StepOutputRegistrationPhaseAsync(CancellationToken cancellationToken)
	{
		foreach (var round in Rounds.Where(x => x.Phase == Phase.OutputRegistration).ToArray())
		{
			try
			{
				var allReady = round.Alices.All(a => a.ReadyToSign);
				bool phaseExpired = round.OutputRegistrationTimeFrame.HasExpired;

				if (allReady || phaseExpired)
				{
					var coinjoin = round.Assert<ConstructionState>();

					_logger.LogInformation("{inputCount} inputs were added.", coinjoin.Inputs.Count());
					_logger.LogInformation("{outputCount} outputs were added.", coinjoin.Outputs.Count());

					round.CoordinatorScript = GetCoordinatorScriptPreventReuse(round);

					coinjoin = await TryAddBlameScriptAsync(round, coinjoin, allReady, round.CoordinatorScript, cancellationToken).ConfigureAwait(false);

					round.CoinjoinState = coinjoin.Finalize();

					if (!allReady && phaseExpired)
					{
						round.TransactionSigningTimeFrame = TimeFrame.Create(Config.CurrentValue.FailFastTransactionSigningTimeout);
					}

					round.SetPhase(Phase.TransactionSigning);
					_logger.LogInformation($"Phase changed: {round.Phase} -> {Phase.TransactionSigning}");
				}
			}
			catch (Exception ex)
			{
				round.EndRound(EndRoundState.AbortedWithError);
				_logger.LogError(ex, "Unexpected error.");
			}
		}
	}

	private async Task StepTransactionSigningPhaseAsync(CancellationToken cancellationToken)
	{
		foreach (var round in Rounds.Where(x => x.Phase == Phase.TransactionSigning).ToArray())
		{
			var state = round.Assert<SigningState>();

			try
			{
				if (state.IsFullySigned)
				{
					Transaction coinjoin = state.CreateTransaction();

					// Logging.
					_logger.LogInformation("Trying to broadcast coinjoin.");
					Coin[]? spentCoins = round.Alices.Select(x => x.Coin).ToArray();
					Money networkFee = coinjoin.GetFee(spentCoins);
					uint256 roundId = round.Id;
					FeeRate feeRate = coinjoin.GetFeeRate(spentCoins);
					_logger.LogInformation("Trying to broadcast coinjoin. Fee: {fee} FeeRate: {feeRate}", networkFee, feeRate);
					_logger.LogDebug("coinjoin: {transaction}.", coinjoin.ToHex());
					
					var indistinguishableOutputs = coinjoin.GetIndistinguishableOutputs(includeSingle: true);
					foreach (var (value, count) in indistinguishableOutputs.Where(x => x.count > 1))
					{
						_logger.LogInformation("There are {count} occurrences of {value} outputs.", count, value.ToString(true, false));
					}
					_logger.LogInformation("There are {indistinguishableOutputCount} occurrences of unique outputs.", indistinguishableOutputs.Count());

					// Broadcasting.
					await Rpc.SendRawTransactionAsync(coinjoin, cancellationToken).ConfigureAwait(false);

					var coordinatorScriptPubKey = Config.CurrentValue.GetNextCleanCoordinatorScript();
					if (round.CoordinatorScript == coordinatorScriptPubKey)
					{
						Config.CurrentValue.MakeNextCoordinatorScriptDirty();
					}

					foreach (var address in coinjoin.Outputs
								 .Select(x => x.ScriptPubKey)
								 .Where(script => CoinJoinScriptStore?.Contains(script) is true))
					{
						if (address == round.CoordinatorScript)
						{
							_logger.LogError("Coordinator script pub key reuse detected: {coordinatorScript}", round.CoordinatorScript.ToHex());
						}
						else
						{
							_logger.LogError("Output script pub key reuse detected: {address}", address);
						}
					}

					round.EndRound(EndRoundState.TransactionBroadcasted);
					_logger.LogInformation("Successfully broadcast the coinjoin: {hash}", coinjoin.GetHash());

					CoinJoinScriptStore?.AddRange(coinjoin.Outputs.Select(x => x.ScriptPubKey));
					CoinJoinBroadcast?.Invoke(this, coinjoin);
				}
				else if (round.TransactionSigningTimeFrame.HasExpired)
				{
					_logger.LogWarning($"Signing phase failed with timed out after {round.TransactionSigningTimeFrame.Duration.TotalSeconds} seconds.");
					await FailTransactionSigningPhaseAsync(round, cancellationToken).ConfigureAwait(false);
				}
			}
			catch (RPCException ex)
			{
				_logger.LogWarning($"Transaction broadcasting failed: '{ex}'.");
				round.EndRound(EndRoundState.TransactionBroadcastFailed);
			}
			catch (Exception ex)
			{
				_logger.LogWarning("Signing phase failed, reason: '{ex}'.", ex);
				round.EndRound(EndRoundState.AbortedWithError);
			}
		}
	}

	private async IAsyncEnumerable<Alice[]> CheckTxoSpendStatusAsync(Round round, [EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		foreach (var chunckOfAlices in round.Alices.ToList().ChunkBy(16))
		{
			var batchedRpc = Rpc.PrepareBatch();

			var aliceCheckingTaskPairs = chunckOfAlices
				.Select(x => (Alice: x, StatusTask: Rpc.GetTxOutAsync(x.Coin.Outpoint.Hash, (int)x.Coin.Outpoint.N, includeMempool: true, cancellationToken)))
				.ToList();

			await batchedRpc.SendBatchAsync(cancellationToken).ConfigureAwait(false);

			var spendStatusCheckingTasks = aliceCheckingTaskPairs.Select(async x => (x.Alice, Status: await x.StatusTask.ConfigureAwait(false)));
			var alices = await Task.WhenAll(spendStatusCheckingTasks).ConfigureAwait(false);
			yield return alices.Where(x => x.Status is null).Select(x => x.Alice).ToArray();
		}
	}

	private async Task FailTransactionSigningPhaseAsync(Round round, CancellationToken cancellationToken)
	{
		var state = round.Assert<SigningState>();

		var unsignedOutpoints = state.UnsignedInputs.Select(c => c.Outpoint).ToHashSet();

		var alicesWhoDidntSign = round.Alices
			.Where(alice => unsignedOutpoints.Contains(alice.Coin.Outpoint))
			.ToHashSet();

		foreach (var alice in alicesWhoDidntSign)
		{
			Prison.Note(alice, round.Id);
		}

		var cnt = round.Alices.RemoveAll(alice => unsignedOutpoints.Contains(alice.Coin.Outpoint));
		_logger.LogInformation("Removed {removeAlicesCount} alices, because they didn't sign. Remainig: {remainingAlicesCount}", cnt, round.InputCount);

		if (round.InputCount >= Config.CurrentValue.MinInputCountByRound)
		{
			round.EndRound(EndRoundState.NotAllAlicesSign);
			await CreateBlameRoundAsync(round, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			round.EndRound(EndRoundState.AbortedNotEnoughAlicesSigned);
		}
	}

	private async Task CreateBlameRoundAsync(Round round, CancellationToken cancellationToken)
	{
		var feeRate = (await Rpc.EstimateSmartFeeAsync((int)Config.CurrentValue.ConfirmationTarget, EstimateSmartFeeMode.Conservative, simulateIfRegTest: true, cancellationToken).ConfigureAwait(false)).FeeRate;
		var blameWhitelist = round.Alices
			.Select(x => x.Coin.Outpoint)
			.Where(x => !Prison.IsBanned(x))
			.ToHashSet();

		RoundParameters parameters = RoundParameterFactory.CreateBlameRoundParameter(feeRate, round);
		BlameRound blameRound = new(parameters, round, blameWhitelist, SecureRandom.Instance);
		Rounds.Add(blameRound);
		// blameRound.LogInfo("Blame round created."); FIXME
	}

	private async Task CreateRoundsAsync(CancellationToken cancellationToken)
	{
		FeeRate? feeRate = null;

		// Have rounds to split the volume around minimum input counts if load balance is required.
		// Only do things if the load balancer compatibility is configured.
		if (Config.CurrentValue.WW200CompatibleLoadBalancing)
		{
			// Destroy the round when it reaches this input count and create 2 new ones instead.
			var roundDestroyerInputCount = Config.CurrentValue.MinInputCountByRound * 2 + Config.CurrentValue.MinInputCountByRound / 2;

			feeRate = (await Rpc.EstimateSmartFeeAsync((int)Config.CurrentValue.ConfirmationTarget, EstimateSmartFeeMode.Conservative, simulateIfRegTest: true, cancellationToken).ConfigureAwait(false)).FeeRate;

			foreach (var round in Rounds.Where(x =>
				x.Phase == Phase.InputRegistration
				&& x is not BlameRound
				&& !x.IsInputRegistrationEnded(x.Parameters.MaxInputCountByRound)
				&& x.InputCount >= roundDestroyerInputCount).ToArray())
			{

				var allInputs = round.Alices.Select(y => y.Coin.Amount).OrderBy(x => x).ToArray();

				// 0.75 to bias towards larger numbers as larger input owners often have many smaller inputs too.
				var smallSuggestion = allInputs.Skip((int)(allInputs.Length * Config.CurrentValue.WW200CompatibleLoadBalancingInputSplit)).First();
				var largeSuggestion = MaxSuggestedAmountProvider.AbsoluteMaximumInput;

				var roundWithoutThis = Rounds.Except(new[] { round });
				RoundParameters parameters = RoundParameterFactory.CreateRoundParameter(feeRate, largeSuggestion);
				Round? foundLargeRound = roundWithoutThis
					.FirstOrDefault(x =>
									x.Phase == Phase.InputRegistration
									&& x is not BlameRound
									&& !x.IsInputRegistrationEnded(x.Parameters.MaxInputCountByRound)
									&& x.Parameters.MaxSuggestedAmount >= allInputs.Max()
									&& x.InputRegistrationTimeFrame.Remaining > TimeSpan.FromSeconds(60));
				var largeRound = foundLargeRound ?? TryMineRound(parameters, roundWithoutThis.ToArray());

				if (largeRound is not null)
				{
					parameters = RoundParameterFactory.CreateRoundParameter(feeRate, smallSuggestion);
					var smallRound = TryMineRound(parameters, roundWithoutThis.Concat(new[] { largeRound }).ToArray());

					// If creation is successful destory round only.
					if (smallRound is not null)
					{
						Rounds.Add(largeRound);
						Rounds.Add(smallRound);

						if (foundLargeRound is null)
						{
							// largeRound.LogInfo($"Mined round with params: {nameof(largeRound.Parameters.MaxSuggestedAmount)}:'{largeRound.Parameters.MaxSuggestedAmount}' BTC."); FIXME
						}
						// smallRound.LogInfo($"Mined round with params: {nameof(smallRound.Parameters.MaxSuggestedAmount)}:'{smallRound.Parameters.MaxSuggestedAmount}' BTC."); FIXME

						// If it can't create the large round, then don't abort.
						round.EndRound(EndRoundState.AbortedLoadBalancing);
						// Logger.LogInfo($"Destroyed round with {allInputs.Length} inputs. Threshold: {roundDestroyerInputCount}");
					}
				}
			}
		}

		// Add more rounds if not enough.
		var registrableRoundCount = Rounds.Count(x => x is not BlameRound && x.Phase == Phase.InputRegistration && x.InputRegistrationTimeFrame.Remaining > TimeSpan.FromMinutes(1));
		int roundsToCreate = Config.CurrentValue.RoundParallelization - registrableRoundCount;
        feeRate ??= (await Rpc.EstimateSmartFeeAsync((int)Config.CurrentValue.ConfirmationTarget, EstimateSmartFeeMode.Conservative, simulateIfRegTest: true, cancellationToken).ConfigureAwait(false)).FeeRate;
		for (int i = 0; i < roundsToCreate; i++)
		{
			RoundParameters parameters = RoundParameterFactory.CreateRoundParameter(feeRate, MaxSuggestedAmountProvider.MaxSuggestedAmount);

			var r = new Round(parameters, SecureRandom.Instance);
			Rounds.Add(r);
			_logger.LogInformation("Created round with params: {paramName}:'{maxSuggestedAmount}' BTC.",nameof(r.Parameters.MaxSuggestedAmount), r.Parameters.MaxSuggestedAmount);
		}
	}

	private Round? TryMineRound(RoundParameters parameters, Round[] rounds)
	{
		// Huge HACK to keep it compatible with WW2.0.0 client version, which's
		// round preference is based on the ordering of ToImmutableDictionary.
		// Add round until ToImmutableDictionary orders it to be the first round
		// so old clients will prefer that one.
		IOrderedEnumerable<Round>? orderedRounds;
		Round r;
		var before = DateTimeOffset.UtcNow;
		var times = 0;
		var maxCycleTimes = 300;
		do
		{
			var roundsCopy = rounds.ToList();
			r = new Round(parameters, SecureRandom.Instance);
			roundsCopy.Add(r);
			orderedRounds = roundsCopy
				.Where(x => x.Phase == Phase.InputRegistration && x is not BlameRound && !x.IsInputRegistrationEnded(x.Parameters.MaxInputCountByRound))
				.OrderBy(x => x.Parameters.MaxSuggestedAmount)
				.ThenBy(x => x.InputCount);
			times++;
		}
		while (times <= maxCycleTimes && orderedRounds.ToImmutableDictionary(x => x.Id, x => x).First().Key != r.Id);

		_logger.LogDebug("First ordered round creator did {times} cycles.", times);

		if (times > maxCycleTimes)
		{
			_logger.LogInformation("First ordered round creation too expensive. Skipping...");
			return null;
		}
		else
		{
			return r;
		}
	}

	private void TimeoutRounds()
	{
		foreach (var expiredRound in Rounds.Where(
			x =>
			x.Phase == Phase.Ended
			&& x.End + Config.CurrentValue.RoundExpiryTimeout < DateTimeOffset.UtcNow).ToArray())
		{
			Rounds.Remove(expiredRound);
		}
	}

	private void TimeoutAlices()
	{
		foreach (var round in Rounds.Where(x => !x.IsInputRegistrationEnded(Config.CurrentValue.MaxInputCountByRound)).ToArray())
		{
			var removedAliceCount = round.Alices.RemoveAll(x => x.Deadline < DateTimeOffset.UtcNow);
			if (removedAliceCount > 0)
			{
				_logger.LogInformation("{removedAliceCount} alices timed out and removed.", removedAliceCount);
			}
		}
	}

	private async Task<ConstructionState> TryAddBlameScriptAsync(Round round, ConstructionState coinjoin, bool allReady, Script blameScript, CancellationToken cancellationToken)
	{
		long aliceSum = round.Alices.Sum(x => x.CalculateRemainingAmountCredentials(round.Parameters.MiningFeeRate));
		long bobSum = round.Bobs.Sum(x => x.CredentialAmount);
		var diff = aliceSum - bobSum;

		// If timeout we must fill up the outputs to build a reasonable transaction.
		// This won't be signed by the alice who failed to provide output, so we know who to ban.
		var diffMoney = Money.Satoshis(diff) - round.Parameters.MiningFeeRate.GetFee(blameScript.EstimateOutputVsize());
		if (diffMoney > round.Parameters.AllowedOutputAmounts.Min)
		{
			// If diff is smaller than max fee rate of a tx, then add it as fee.
			var highestFeeRate = (await Rpc.EstimateSmartFeeAsync(2, EstimateSmartFeeMode.Conservative, simulateIfRegTest: true, cancellationToken).ConfigureAwait(false)).FeeRate;

			// ToDo: This condition could be more sophisticated by always trying to max out the miner fees to target 2 and only deal with the remaining diffMoney.
			if (coinjoin.EffectiveFeeRate > highestFeeRate)
			{
				coinjoin = coinjoin.AddOutput(new TxOut(diffMoney, blameScript));

				if (allReady)
				{
					_logger.LogInformation("Filled up the outputs to build a reasonable transaction, all Alices signalled ready. Added amount: {diffMoney}.", diffMoney);
				}
				else
				{
					_logger.LogWarning("Filled up the outputs to build a reasonable transaction because some alice failed to provide its output. Added amount: {diffMoney}.", diffMoney);
				}
			}
			else
			{
				_logger.LogWarning("There were some leftover satoshis. Added amount to miner fees: {diffMoney}.", diffMoney);
			}
		}
		else if (!allReady)
		{
			_logger.LogWarning("Could not add blame script, because the amount was too small: {diffMoney}.", diffMoney);
		}

		return coinjoin;
	}

	private Script GetCoordinatorScriptPreventReuse(Round round)
	{
		var coordinatorScriptPubKey = Config.CurrentValue.GetNextCleanCoordinatorScript();

		// Prevent coordinator script reuse.
		if (Rounds.Any(r => r.CoordinatorScript == coordinatorScriptPubKey))
		{
			Config.CurrentValue.MakeNextCoordinatorScriptDirty();
			coordinatorScriptPubKey = Config.CurrentValue.GetNextCleanCoordinatorScript();
			_logger.LogWarning("Coordinator script pub key was already used by another round, making it dirty and taking a new one.");
		}

		return coordinatorScriptPubKey;
	}
}
