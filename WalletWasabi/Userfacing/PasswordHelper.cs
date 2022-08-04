using System.Collections.Generic;
using System.Text;
using NBitcoin;
using System.Security;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Blockchain.Keys;
using System.Diagnostics.CodeAnalysis;

namespace WalletWasabi.Userfacing;

public static class PasswordHelper
{
	public const int MaxPasswordLength = 150;
	public static readonly string PasswordTooLongMessage = $"Password is too long.";
	public const string TrimWarnMessage = "Leading and trailing white spaces will be removed!";
	public const string MatchingMessage = "Passwords don't match.";
	public const string WhitespaceMessage = "Leading and trailing white spaces are not allowed!";

	public static bool IsTooLong(string? password, out string? limitedPassword)
	{
		limitedPassword = password;
		if (password is null)
		{
			return false;
		}

		if (IsTooLong(password.Length))
		{
			limitedPassword = password[..MaxPasswordLength];
			return true;
		}

		return false;
	}

	public static bool IsTooLong(int length)
	{
		return length > MaxPasswordLength;
	}

	public static bool IsTrimmable(string? password, [NotNullWhen(true)] out string? trimmedPassword)
	{
		if (password is { } && password.IsTrimmable())
		{
			trimmedPassword = password.Trim();
			return true;
		}

		trimmedPassword = password;
		return false;
	}

	public static bool TryPassword(KeyManager keyManager, string password)
	{
		try
		{
			GetMasterExtKey(keyManager, password);
		}
		catch
		{
			return false;
		}

		return true;
	}

	public static void Guard(string password)
	{
		if (IsTooLong(password, out _)) // Password should be formatted, before entering here.
		{
			throw new FormatException(PasswordTooLongMessage);
		}

		if (IsTrimmable(password, out _)) // Password should be formatted, before entering here.
		{
			throw new FormatException("Leading and trailing white spaces are not allowed!");
		}
	}

	public static ExtKey GetMasterExtKey(KeyManager keyManager, string password)
	{
		password = Helpers.Guard.Correct(password); // Correct the password to ensure compatibility. User will be notified about this through TogglePasswordBox.

		Guard(password);

		return keyManager.GetMasterExtKey(password);
	}

	public static void ValidatePassword(IValidationErrors errors, string password)
	{
		if (IsTrimmable(password, out _))
		{
			errors.Add(ErrorSeverity.Warning, TrimWarnMessage);
		}

		if (IsTooLong(password, out _))
		{
			errors.Add(ErrorSeverity.Error, PasswordTooLongMessage);
		}
	}
}
