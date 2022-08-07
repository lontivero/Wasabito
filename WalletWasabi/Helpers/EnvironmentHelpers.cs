using System.Collections.Concurrent;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Microservices;

namespace WalletWasabi.Helpers;

public static class EnvironmentHelpers
{
	public static string GetDefaultDataDir() =>
		RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
			? "%APPDATA%\\wwl"
			: "%HOME%/wwl";
		
		

	public static string ExpandDirectory(string dir)
	{
		var directory = Environment.ExpandEnvironmentVariables(dir);

		if (!Directory.Exists(directory))
		{
			Directory.CreateDirectory(directory);
		}
		return directory;
	}

	/// <summary>
	/// Gets Bitcoin <c>datadir</c> parameter from:
	/// <list type="bullet">
	/// <item><c>APPDATA</c> environment variable on Windows, and</item>
	/// <item><c>HOME</c> environment variable on other platforms.</item>
	/// </list>
	/// </summary>
	/// <returns><c>datadir</c> or empty string.</returns>
	/// <seealso href="https://en.bitcoin.it/wiki/Data_directory"/>
	public static string GetDefaultBitcoinCoreDataDirOrEmptyString()
	{
		string directory = "";

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			var localAppData = Environment.GetEnvironmentVariable("APPDATA");
			if (!string.IsNullOrEmpty(localAppData))
			{
				directory = Path.Combine(localAppData, "Bitcoin");
			}
			else
			{
				Logger.LogDebug($"Could not find suitable default {Constants.BuiltinBitcoinNodeName} datadir.");
			}
		}
		else
		{
			var home = Environment.GetEnvironmentVariable("HOME");
			if (!string.IsNullOrEmpty(home))
			{
				directory = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
					? Path.Combine(home, "Library", "Application Support", "Bitcoin")
					: Path.Combine(home, ".bitcoin"); // Linux
			}
			else
			{
				Logger.LogDebug($"Could not find suitable default {Constants.BuiltinBitcoinNodeName} datadir.");
			}
		}

		return directory;
	}

	/// <summary>
	/// Executes a command with Bourne shell.
	/// https://stackoverflow.com/a/47918132/2061103
	/// </summary>
	public static async Task ShellExecAsync(string cmd, bool waitForExit = true)
	{
		var escapedArgs = cmd.Replace("\"", "\\\"");

		var startInfo = new ProcessStartInfo
		{
			FileName = "/usr/bin/env",
			Arguments = $"sh -c \"{escapedArgs}\"",
			RedirectStandardOutput = true,
			UseShellExecute = false,
			CreateNoWindow = true,
			WindowStyle = ProcessWindowStyle.Hidden
		};

		if (waitForExit)
		{
			using var process = new ProcessAsync(startInfo);
			process.Start();

			await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

			if (process.ExitCode != 0)
			{
				Logger.LogError($"{nameof(ShellExecAsync)} command: {cmd} exited with exit code: {process.ExitCode}, instead of 0.");
			}
		}
		else
		{
			using var process = Process.Start(startInfo);
		}
	}

	public static bool IsFileTypeAssociated(string fileExtension)
	{
		// Source article: https://edi.wang/post/2019/3/4/read-and-write-windows-registry-in-net-core

		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			throw new InvalidOperationException("Operation only supported on windows.");
		}

		fileExtension = fileExtension.TrimStart('.'); // Remove . if added by the caller.

		using (var key = Registry.ClassesRoot.OpenSubKey($".{fileExtension}"))
		{
			// Read the (Default) value.
			if (key?.GetValue(null) is not null)
			{
				return true;
			}
		}
		return false;
	}

	public static string GetFullBaseDirectory()
	{
		return Path.GetFullPath(AppContext.BaseDirectory);
	}

	public static string GetExecutablePath()
	{
		var fullBaseDir = GetFullBaseDirectory();
		var wassabeeFileName = Path.Combine(fullBaseDir, Constants.ExecutableName);
		wassabeeFileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"{wassabeeFileName}.exe" : $"{wassabeeFileName}";
		if (File.Exists(wassabeeFileName))
		{
			return wassabeeFileName;
		}
		var assemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? throw new NullReferenceException("Assembly or Assembly's Name was null.");
		var fluentExecutable = Path.Combine(fullBaseDir, assemblyName);
		return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"{fluentExecutable}.exe" : $"{fluentExecutable}";
	}

	/// <summary>
	/// Reset the system sleep timer, this method has to be called from time to time to prevent sleep.
	/// It does not prevent the display to turn off.
	/// </summary>
	public static async Task ProlongSystemAwakeAsync()
	{
	}
}
