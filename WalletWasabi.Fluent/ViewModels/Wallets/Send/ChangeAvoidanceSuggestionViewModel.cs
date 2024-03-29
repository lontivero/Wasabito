using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class ChangeAvoidanceSuggestionViewModel : SuggestionViewModel
{
	[AutoNotify] private string _amount;

	public ChangeAvoidanceSuggestionViewModel(
		decimal originalAmount,
		BuildTransactionResult transactionResult)
	{
		TransactionResult = transactionResult;

		var totalAmount = transactionResult.CalculateDestinationAmount();
		_amount = $"{totalAmount.ToFormattedString()} BTC";
	}

	public BuildTransactionResult TransactionResult { get; }

	public static async IAsyncEnumerable<ChangeAvoidanceSuggestionViewModel> GenerateSuggestionsAsync(
		TransactionInfo transactionInfo,
		BitcoinAddress destination,
		Wallet wallet,
		ImmutableArray<SmartCoin> coinsToUse,
		int maxInputCount,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var selections = ChangelessTransactionCoinSelector.GetAllStrategyResultsAsync(
			coinsToUse,
			transactionInfo.FeeRate,
			new TxOut(transactionInfo.Amount, destination),
			maxInputCount,
			cancellationToken).ConfigureAwait(false);

		HashSet<Money> foundSolutionsByAmount = new();

		await foreach (var selection in selections)
		{
			if (selection.Any())
			{
				BuildTransactionResult? transaction = null;

				try
				{
					transaction = TransactionHelpers.BuildChangelessTransaction(
						wallet,
						destination,
						transactionInfo.UserLabels,
						transactionInfo.FeeRate,
						selection,
						tryToSign: false);
				}
				catch (Exception ex)
				{
					Logger.LogError($"Failed to build changeless transaction. Exception: {ex}");
				}

				if (transaction is not null)
				{
					var destinationAmount = transaction.CalculateDestinationAmount();

					// If BnB solutions become the same transaction somehow, do not show the same suggestion twice.
					if (!foundSolutionsByAmount.Contains(destinationAmount))
					{
						foundSolutionsByAmount.Add(destinationAmount);

						yield return new ChangeAvoidanceSuggestionViewModel(
							transactionInfo.Amount.ToDecimal(MoneyUnit.BTC),
							transaction);
					}
				}
			}
		}
	}
}
