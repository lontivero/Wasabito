using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using WalletWasabi.Microservices;

namespace WalletWasabi.Tor;

public class TorOptions
{
	/// <summary>Tor binary file name without extension.</summary>
	public const string TorBinaryFileName = "tor";

	public TorOptions()
	{
	}
	
	public TorOptions(string dataDir, bool terminateOnExit, int? owningProcessId = null)
	{
		TorBinaryFilePath = GetTorBinaryFilePath();

		DataDirectory = Path.Combine(dataDir, "tordata2");
		CookieAuthFilePath = Path.Combine(dataDir, "control_auth_cookie");
		LogFilePath = Path.Combine(dataDir, "TorLogs.txt");
		IoHelpers.EnsureContainingDirectoryExists(LogFilePath);
		TerminateOnExit = terminateOnExit;
		OwningProcessId = owningProcessId;
	}

	public bool UseTor { get; set; } = true;
	
	public string DataDirectory { get; set; }

	public string LogFilePath { get; set; }

	public bool TerminateOnExit { get; set; }

	public int? OwningProcessId { get; set; }

	public string TorBinaryFilePath { get; set; }

	public string CookieAuthFilePath { get; set; }


	public IPEndPoint SocksEndpoint { get; set; } = new(IPAddress.Loopback, 37150);

	public IPEndPoint ControlEndpoint { get; set; } = new(IPAddress.Loopback, 37151);

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
			$"--DataDirectory \"{DataDirectory}\"",
			$"--Log \"notice file {LogFilePath}\""
		};

		if (TerminateOnExit && OwningProcessId is not null)
		{
			arguments.Add($"__OwningControllerProcess {OwningProcessId}");
		}

		return string.Join(" ", arguments);
	}
}
