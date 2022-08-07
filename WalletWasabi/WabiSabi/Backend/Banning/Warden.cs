using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using WalletWasabi.Bases;
using WalletWasabi.Logging;

namespace WalletWasabi.WabiSabi.Backend.Banning;

/// <summary>
/// Serializes and releases the prison population periodically.
/// </summary>
public class Warden : PeriodicRunner
{
	public Warden(IOptionsMonitor<DoSOptions> cfg) : base(TimeSpan.FromMinutes(1))
	{
		DoSOptions = cfg;
		Prison = DeserializePrison(DoSOptions.CurrentValue.PrisonFilePath);
		LastKnownChange = Prison.ChangeId;
	}

	public Prison Prison { get; }
	public Guid LastKnownChange { get; private set; }

	public IOptionsMonitor<DoSOptions> DoSOptions { get; }
	
	private static Prison DeserializePrison(string prisonFilePath)
	{
		IoHelpers.EnsureContainingDirectoryExists(prisonFilePath);
		var inmates = new List<Inmate>();
		if (File.Exists(prisonFilePath))
		{
			try
			{
				foreach (var inmate in File.ReadAllLines(prisonFilePath).Select(Inmate.FromString))
				{
					inmates.Add(inmate);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
				Logger.LogWarning($"Deleting {prisonFilePath}");
				File.Delete(prisonFilePath);
			}
		}

		var prison = new Prison(inmates);

		var (noted, banned) = prison.CountInmates();
		if (noted > 0)
		{
			Logger.LogInfo($"{noted} noted UTXOs are found in prison.");
		}

		if (banned > 0)
		{
			Logger.LogInfo($"{banned} banned UTXOs are found in prison.");
		}

		return prison;
	}

	public async Task SerializePrisonAsync()
	{
		IoHelpers.EnsureContainingDirectoryExists(DoSOptions.CurrentValue.PrisonFilePath);
		await File.WriteAllLinesAsync(DoSOptions.CurrentValue.PrisonFilePath, Prison.GetInmates().Select(x => x.ToString())).ConfigureAwait(false);
	}

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		var count = Prison.ReleaseEligibleInmates(DoSOptions.CurrentValue.ReleaseUtxoFromPrisonAfter).Count();
		if (count > 0)
		{
			Logger.LogInfo($"{count} UTXOs are released from prison.");
		}

		// If something changed, send prison to file.
		if (LastKnownChange != Prison.ChangeId)
		{
			await SerializePrisonAsync().ConfigureAwait(false);
			LastKnownChange = Prison.ChangeId;
		}
	}
}
