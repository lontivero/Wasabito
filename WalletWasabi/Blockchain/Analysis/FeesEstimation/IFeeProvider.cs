namespace WalletWasabi.Blockchain.Analysis.FeesEstimation;

public interface IFeeProvider
{
	event EventHandler<AllFeeEstimate>? AllFeeEstimateArrived;

	AllFeeEstimate? AllFeeEstimate { get; }
	bool InError { get; }
}
