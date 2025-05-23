using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using Chasser.Common.Logic.Enums;
using Chasser.Common.Network;
using Chasser.Logic;
using Chasser.Logic.Network;
using Chasser.View;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Chasser.ViewModels
{
    public class LoginViewModel : BaseViewModel
    {
        private string _username;
        private string _password;
        private bool _isPasswordVisible;
        private string _passwordText;
        private readonly INavigationService _navigationService;

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public string PasswordText
        {
            get => _passwordText;
            set => SetProperty(ref _passwordText, value);
        }

        public bool IsPasswordVisible
        {
            get => _isPasswordVisible;
            set
            {
                if (SetProperty(ref _isPasswordVisible, value))
                {
                    OnPropertyChanged(nameof(PasswordVisibility));
                    OnPropertyChanged(nameof(PasswordBoxVisibility));
                    OnPropertyChanged(nameof(TogglePasswordButtonContent));
                }
            }
        }

        public Visibility PasswordVisibility => IsPasswordVisible ? Visibility.Visible : Visibility.Collapsed;
        public Visibility PasswordBoxVisibility => IsPasswordVisible ? Visibility.Collapsed : Visibility.Visible;
        public string TogglePasswordButtonContent => IsPasswordVisible ? "🙈" : "👁️";

        public ICommand LoginCommand { get; }
        public ICommand ForgotPasswordCommand { get; }
        public ICommand RegisterCommand { get; }
        public ICommand TogglePasswordVisibilityCommand { get; }

        public LoginViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService;

            LoginCommand = new RelayCommand(async () => await LoginAsync());
            ForgotPasswordCommand = new RelayCommand(ForgotPassword);
            RegisterCommand = new RelayCommand(Register);
            TogglePasswordVisibilityCommand = new RelayCommand(TogglePasswordVisibility);
        }

        private async Task LoginAsync()
        {
            if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(IsPasswordVisible ? PasswordText : Password))
            {
                PopUpInfo.ShowMessage("Ambos campos deben estar completos.", Application.Current.MainWindow, MessageType.Warning);
                return;
            }

            var actualPassword = IsPasswordVisible ? PasswordText : Password;

            var request = new RequestMessage
            {
                Command = "LOGIN",
                Data = new Dictionary<string, string>
                {
                    { "username", Username },
                    { "password", actualPassword }
                }
            };

            try
            {
                await TCPClient.SendOnlyMessageAsync(request);
                var response = await TCPClient.ReceiveMessageAsync();

                if (response.Status == "LOGIN_SUCCESS")
                {
                    var token = response.Data["token"];
                    AuthHelper.SetToken(token);
                    _navigationService.NavigateTo("MainPage");
                }
                else
                {
                    PopUpInfo.ShowMessage($"Inicio de sesión fallido. Motivo: {response.Message}",
                        Application.Current.MainWindow, MessageType.Error);
                }
            }
            catch (Exception ex)
            {
                PopUpInfo.ShowMessage("Error de conexión: " + ex.Message,
                    Application.Current.MainWindow, MessageType.Warning);
            }
        }

        private void ForgotPassword()
        {
            new RecoveryWindow { Owner = Application.Current.MainWindow }.ShowDialog();
        }

        private void Register()
        {
            _navigationService.NavigateTo("Register");
        }

        private void TogglePasswordVisibility()
        {
            if (IsPasswordVisible)
            {
                Password = PasswordText;
            }
            else
            {
                PasswordText = Password;
            }

            IsPasswordVisible = !IsPasswordVisible;
        }
    }
}