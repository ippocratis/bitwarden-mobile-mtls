﻿using System;
using System.Threading.Tasks;
using Bit.App.Models;
using Bit.App.Utilities;
using Bit.Core;
using Bit.Core.Abstractions;
using Bit.Core.Models.Domain;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Xamarin.CommunityToolkit.ObjectModel;
using Xamarin.Forms;

namespace Bit.App.Pages
{
    public partial class LoginPage : BaseContentPage
    {
        private readonly IBroadcasterService _broadcasterService;
        private readonly LoginPageViewModel _vm;
        private readonly AppOptions _appOptions;

        private bool _inputFocused;

        readonly LazyResolve<ILogger> _logger = new LazyResolve<ILogger>("logger");

        public LoginPage(string email = null, AppOptions appOptions = null)
        {
            _appOptions = appOptions;
            _broadcasterService = ServiceContainer.Resolve<IBroadcasterService>("broadcasterService");
            InitializeComponent();
            _broadcasterService = ServiceContainer.Resolve<IBroadcasterService>();
            _vm = BindingContext as LoginPageViewModel;
            _vm.Page = this;
            _vm.StartTwoFactorAction = () => Device.BeginInvokeOnMainThread(async () => await StartTwoFactorAsync());
            _vm.LogInSuccessAction = () => Device.BeginInvokeOnMainThread(async () => await LogInSuccessAsync());
            _vm.LogInWithDeviceAction = () => StartLoginWithDeviceAsync().FireAndForget();
            _vm.StartSsoLoginAction = () => Device.BeginInvokeOnMainThread(async () => await StartSsoLoginAsync());
            _vm.UpdateTempPasswordAction = () => Device.BeginInvokeOnMainThread(async () => await UpdateTempPasswordAsync());
            _vm.CloseAction = async () =>
            {
                await _accountListOverlay.HideAsync();
                await Navigation.PopModalAsync();
            };
            _vm.IsEmailEnabled = string.IsNullOrWhiteSpace(email);
            _vm.IsIosExtension = _appOptions?.IosExtension ?? false;

            if (_vm.IsEmailEnabled)
            {
                _vm.ShowCancelButton = true;
            }
            _vm.Email = email;
            MasterPasswordEntry = _masterPassword;

            if (Device.RuntimePlatform == Device.iOS)
            {
                ToolbarItems.Add(_moreItem);
            }
            else
            {
                ToolbarItems.Add(_getPasswordHint);
            }

            if (_appOptions?.IosExtension ?? false)
            {
                _vm.ShowCancelButton = true;
            }

            if (_appOptions?.HideAccountSwitcher ?? false)
            {
                ToolbarItems.Remove(_accountAvatar);
            }
        }

        public Entry MasterPasswordEntry { get; set; }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            _broadcasterService.Subscribe(nameof(LoginPage), async (message) =>
            {
                if(message.Command == "selectFileResult")
                {
                    FileSelected_InstallCertificate(message);
                }
                else if(message.Command == "installCertificateResult")
                {
                    if(await _vm.PickCertificate())
                        await _vm.LoadCertificateAndLogin();
                }
            });

            //_broadcasterService.Subscribe(nameof(LoginPage), message =>
            //{
            //    if (message.Command == Constants.ClearSensitiveFields)
            //    {
            //        Device.BeginInvokeOnMainThread(_vm.ResetPasswordField);
            //    }
            //});
            _mainContent.Content = _mainLayout;
            _accountAvatar?.OnAppearing();

            await _vm.InitAsync();
            if (!_appOptions?.HideAccountSwitcher ?? false)
            {
                _vm.AvatarImageSource = await GetAvatarImageSourceAsync(_vm.EmailIsInSavedAccounts);
            }
            if (!_inputFocused)
            {
                RequestFocus(_masterPassword);
                _inputFocused = true;
            }
            if (Device.RuntimePlatform == Device.Android && !_vm.CanRemoveAccount)
            {
                ToolbarItems.Add(_removeAccount);
            }
        }
        
         private void FileSelected_InstallCertificate(Message message)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                var data = message.Data as Tuple<byte[], string>;
                var fileData = data.Item1;
                var fileName = data.Item2;
                _vm.PromptInstallCertificate(fileData);
            });
        }

        protected override bool OnBackButtonPressed()
        {
            if (_accountListOverlay.IsVisible)
            {
                _accountListOverlay.HideAsync().FireAndForget();
                return true;
            }
            return false;
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            _accountAvatar?.OnDisappearing();
            _broadcasterService.Unsubscribe(nameof(LoginPage));
        }

        private async void LogIn_Clicked(object sender, EventArgs e)
        {
            if (DoOnce())
            {
                await _vm.LogInAsync(true, _vm.IsEmailEnabled);
            }
        }

        private void LogInSSO_Clicked(object sender, EventArgs e)
        {
            if (DoOnce())
            {
                _vm.StartSsoLoginAction();
            }
        }

        private async Task StartLoginWithDeviceAsync()
        {
            var page = new LoginPasswordlessRequestPage(_vm.Email, _appOptions);
            await Navigation.PushModalAsync(new NavigationPage(page));
        }

        private async Task StartSsoLoginAsync()
        {
            var page = new LoginSsoPage(_appOptions);
            await Navigation.PushModalAsync(new NavigationPage(page));
        }

        private void Hint_Clicked(object sender, EventArgs e)
        {
            if (DoOnce())
            {
                _vm.ShowMasterPasswordHintAsync().FireAndForget();
            }
        }

        private async void RemoveAccount_Clicked(object sender, EventArgs e)
        {
            await _accountListOverlay.HideAsync();
            if (DoOnce())
            {
                await _vm.RemoveAccountAsync();
            }
        }

        private void Cancel_Clicked(object sender, EventArgs e)
        {
            if (DoOnce())
            {
                _vm.CloseAction();
            }
        }

        private async void More_Clicked(object sender, EventArgs e)
        {
            try
            {
                await _accountListOverlay.HideAsync();
                _vm.MoreCommand.Execute(null);
            }
            catch (Exception ex)
            {
                _logger.Value.Exception(ex);
            }
        }

        private async Task StartTwoFactorAsync()
        {
            var page = new TwoFactorPage(false, _appOptions);
            await Navigation.PushModalAsync(new NavigationPage(page));
        }

        private async Task LogInSuccessAsync()
        {
            if (AppHelpers.SetAlternateMainPage(_appOptions))
            {
                return;
            }
            var previousPage = await AppHelpers.ClearPreviousPage();
            Application.Current.MainPage = new TabsPage(_appOptions, previousPage);
        }

        private async Task UpdateTempPasswordAsync()
        {
            var page = new UpdateTempPasswordPage();
            await Navigation.PushModalAsync(new NavigationPage(page));
        }
    }
}
