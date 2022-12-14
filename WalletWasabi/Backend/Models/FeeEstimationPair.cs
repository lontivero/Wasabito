using System.ComponentModel.DataAnnotations;

namespace WalletWasabi.Backend.Models
{
	/// <summary>
	/// Satoshi per bytes.
	/// </summary>
	public class FeeEstimationPair
	{
		[Required]
		public long Economical { get; set; }

		[Required]
		public long Conservative { get; set; }
	}
}
