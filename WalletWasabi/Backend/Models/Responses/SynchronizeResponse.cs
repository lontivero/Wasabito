using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using WalletWasabi.Blockchain.Analysis.FeesEstimation;
using WalletWasabi.JsonConverters;

namespace WalletWasabi.Backend.Models.Responses;

public class SynchronizeResponse
{
	public FiltersResponseState FiltersResponseState { get; set; }

	[JsonProperty(ItemConverterType = typeof(FilterModelJsonConverter))] // Do not use the default jsonifyer, because that's too much data.
	public IEnumerable<FilterModel> Filters { get; set; }

	public int BestHeight { get; set; }

	public AllFeeEstimate? AllFeeEstimate { get; set; }
}
