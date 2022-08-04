using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.ViewModels.Wallets;
using WalletWasabi.Userfacing;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Login;

[NavigationMetaData(Title = "")]
public partial class LoginViewModel : RoutableViewModel
{
	[AutoNotify] private string _password;
	[AutoNotify] private bool _isPasswordNeeded;
	[AutoNotify] private string _errorMessage;

	public LoginViewModel(ClosedWalletViewModel closedWalletViewModel)
	{
		var wallet = closedWalletViewModel.Wallet;
		IsPasswordNeeded = !wallet.KeyManager.IsWatchOnly;
		WalletName = wallet.WalletName;
		_password = "";
		_errorMessage = "";
		WalletType = WalletHelpers.GetType(closedWalletViewModel.Wallet.KeyManager);

		NextCommand = ReactiveCommand.CreateFromTask(async () => await OnNextAsync(closedWalletViewModel, wallet));

		OkCommand = ReactiveCommand.Create(OnOk);

		EnableAutoBusyOn(NextCommand);
	}

	public WalletType WalletType { get; }

	public string WalletName { get; }

	public ICommand OkCommand { get; }

	private async Task OnNextAsync(ClosedWalletViewModel closedWalletViewModel, Wallet wallet)
	{
		var isPasswordCorrect = await Task.Run(() => wallet.TryLogin(Password));

		if (!isPasswordCorrect)
		{
			ErrorMessage = "The password is incorrect! Try Again.";
			return;
		}

		LoginWallet(closedWalletViewModel);
	}

	private void OnOk()
	{
		Password = "";
		ErrorMessage = "";
	}

	private void LoginWallet(ClosedWalletViewModel closedWalletViewModel)
	{
		closedWalletViewModel.RaisePropertyChanged(nameof(WalletViewModelBase.IsLoggedIn));
		Navigate().To(closedWalletViewModel, NavigationMode.Clear);
	}
}
