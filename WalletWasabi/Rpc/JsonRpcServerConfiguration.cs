namespace WalletWasabi.Rpc;

public class JsonRpcServerConfiguration
{
	public JsonRpcServerConfiguration()
	{
		
	} public JsonRpcServerConfiguration(bool enabled, string jsonRpcUser, string jsonRpcPassword, string[] jsonRpcServerPrefixes)
	{
		IsEnabled = enabled;
		JsonRpcUser = jsonRpcUser;
		JsonRpcPassword = jsonRpcPassword;
		JsonRpcServerPrefixes = jsonRpcServerPrefixes;
	}

	public bool IsEnabled { get; set; } = false;
	public string JsonRpcUser { get; set; }
	public string JsonRpcPassword { get; set; }
	public string[] JsonRpcServerPrefixes { get; set; } = Array.Empty<string>();

	public bool RequiresCredentials => !string.IsNullOrEmpty(JsonRpcUser) && !string.IsNullOrEmpty(JsonRpcPassword);
}
