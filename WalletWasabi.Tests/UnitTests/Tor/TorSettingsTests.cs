using System.IO;
using WalletWasabi.Tor;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Tor;

/// <summary>
/// Tests for <see cref="Tor.TorSettings"/> class.
/// </summary>
public class TorSettingsTests
{
	[Fact]
	public void GetCmdArgumentsTest()
	{
		string dataDir = Path.Combine("temp", "tempDataDir");

		TorSettings settings = new(dataDir, terminateOnExit: true, owningProcessId: 7);

		string arguments = settings.GetCmdArguments();

		string expected = string.Join(
			" ",
			$"--LogTimeGranularity 1",
			$"--SOCKSPort \"127.0.0.1:37150 ExtendedErrors\"",
			$"--CookieAuthentication 1",
			$"--ControlPort 37151",
			$"--CookieAuthFile \"{Path.Combine("temp", "tempDataDir", "control_auth_cookie")}\"",
			$"--DataDirectory \"{Path.Combine("temp", "tempDataDir", "tordata2")}\"",
			$"--Log \"notice file {Path.Combine("temp", "tempDataDir", "TorLogs.txt")}\"",
			$"__OwningControllerProcess 7");

		Assert.Equal(expected, arguments);
	}
}
