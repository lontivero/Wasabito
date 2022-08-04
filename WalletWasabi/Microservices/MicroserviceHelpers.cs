using System.IO;
using System.Runtime.InteropServices;
using WalletWasabi.Helpers;

namespace WalletWasabi.Microservices;

public static class MicroserviceHelpers
{
	public static OSPlatform GetCurrentPlatform()
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			return OSPlatform.Windows;
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			return OSPlatform.Linux;
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			return OSPlatform.OSX;
		}
		else
		{
			throw new NotSupportedException("Platform is not supported.");
		}
	}

	public static string GetBinaryPath(string binaryNameWithoutExtension, OSPlatform? platform = null)
	{
		platform ??= GetCurrentPlatform();
		return platform.Value == OSPlatform.Windows
			? $"{binaryNameWithoutExtension}.exe" 
			: $"{binaryNameWithoutExtension}";
	}
}
