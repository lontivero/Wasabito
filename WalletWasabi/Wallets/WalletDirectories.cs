using NBitcoin;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Options;
using WalletWasabi.Helpers;

namespace WalletWasabi.Wallets;

public class WalletDirectories
{
	private const string WalletFileExtension = "json";

	public WalletDirectories(Network network, string workDir)
	{
		Network = network;
		workDir = Guard.NotNullOrEmptyOrWhitespace(nameof(workDir), workDir, true);
		var correctedWorkDir = Path.Combine(workDir, "Wallets");
		WalletsDir = (network == Network.Main)
			? Path.Combine(correctedWorkDir)
			: Path.Combine(correctedWorkDir, network.ToString());

		Directory.CreateDirectory(WalletsDir);
	}

	public string WalletsDir { get; }

	public Network Network { get; }

	public string GetWalletFilePaths(string walletName)
	{
		if (!walletName.EndsWith($".{WalletFileExtension}", StringComparison.OrdinalIgnoreCase))
		{
			walletName = $"{walletName}.{WalletFileExtension}";
		}
		return Path.Combine(WalletsDir, walletName);
	}

	public IEnumerable<FileInfo> EnumerateWalletFiles()
	{
		var walletsDirInfo = new DirectoryInfo(WalletsDir);
		var walletsDirExists = walletsDirInfo.Exists;
		var searchPattern = $"*.{WalletFileExtension}";
		var searchOption = SearchOption.TopDirectoryOnly;
		IEnumerable<FileInfo> result;

		if (!walletsDirExists)
		{
			return Enumerable.Empty<FileInfo>();
		}

		result = walletsDirInfo.EnumerateFiles(searchPattern, searchOption);

		return result.OrderByDescending(t => t.LastAccessTimeUtc);
	}

	public string GetNextWalletName(string prefix = "Random Wallet")
	{
		int i = 1;
		var walletNames = EnumerateWalletFiles().Select(x => Path.GetFileNameWithoutExtension(x.Name));
		while (true)
		{
			var walletName = i == 1 ? prefix : $"{prefix} {i}";

			if (!walletNames.Contains(walletName))
			{
				return walletName;
			}

			i++;
		}
	}
}
