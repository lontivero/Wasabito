using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using WalletWasabi.Microservices;

namespace WalletWasabi.Tor;

/// <summary>
/// All Tor-related settings.
/// </summary>
public class TorSettings
{
	/// <summary>Tor binary file name without extension.</summary>
	public const string TorBinaryFileName = "tor";

	/// <param name="dataDir">Application data directory.</param>
	/// <param name="distributionFolderPath">Full path to folder containing Tor installation files.</param>
	public TorSettings(string dataDir, bool terminateOnExit, int? owningProcessId = null)
	{
		TorBinaryFilePath = GetTorBinaryFilePath();

		TorDataDir = Path.Combine(dataDir, "tordata2");
		CookieAuthFilePath = Path.Combine(dataDir, "control_auth_cookie");
		LogFilePath = Path.Combine(dataDir, "TorLogs.txt");
		IoHelpers.EnsureContainingDirectoryExists(LogFilePath);
		TerminateOnExit = terminateOnExit;
		OwningProcessId = owningProcessId;
	}

	/// <summary>Full directory path where Tor stores its data.</summary>
	public string TorDataDir { get; }

	/// <summary>Full path. Directory may not necessarily exist.</summary>
	public string LogFilePath { get; }

	/// <summary>Whether Tor should be terminated when Wasabi Wallet terminates.</summary>
	public bool TerminateOnExit { get; }

	/// <summary>Owning process ID for Tor program.</summary>
	public int? OwningProcessId { get; }

	/// <summary>Full path to executable file that is used to start Tor process.</summary>
	public string TorBinaryFilePath { get; }

	/// <summary>Full path to Tor cookie file.</summary>
	public string CookieAuthFilePath { get; }

	/// <summary>Tor SOCKS5 endpoint.</summary>
	public IPEndPoint SocksEndpoint { get; } = new(IPAddress.Loopback, 37150);

	/// <summary>Tor control endpoint.</summary>
	public IPEndPoint ControlEndpoint { get; } = new(IPAddress.Loopback, 37151);

	/// <returns>Full path to Tor binary for selected <paramref name="platform"/>.</returns>
	public static string GetTorBinaryFilePath(OSPlatform? platform = null)
	{
		platform ??= MicroserviceHelpers.GetCurrentPlatform();

		string binaryPath = MicroserviceHelpers.GetBinaryPath("tor", platform);
		return platform == OSPlatform.OSX ? $"{binaryPath}.real" : binaryPath;
	}

	public string GetCmdArguments()
	{
		// `--SafeLogging 0` is useful for debugging to avoid "[scrubbed]" redactions in Tor log.
		List<string> arguments = new()
		{
			$"--LogTimeGranularity 1",
			$"--SOCKSPort \"{SocksEndpoint} ExtendedErrors\"",
			$"--CookieAuthentication 1",
			$"--ControlPort {ControlEndpoint.Port}",
			$"--CookieAuthFile \"{CookieAuthFilePath}\"",
			$"--DataDirectory \"{TorDataDir}\"",
			$"--Log \"notice file {LogFilePath}\""
		};

		if (TerminateOnExit && OwningProcessId is not null)
		{
			arguments.Add($"__OwningControllerProcess {OwningProcessId}");
		}

		return string.Join(" ", arguments);
	}
}
