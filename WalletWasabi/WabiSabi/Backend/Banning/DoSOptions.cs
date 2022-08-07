namespace WalletWasabi.WabiSabi.Backend.Banning;

public class DoSOptions
{
	public TimeSpan ReleaseUtxoFromPrisonAfter { get; set; } = TimeSpan.FromHours(3);
	public string PrisonFilePath { get; set; } = "prision.txt";

}
