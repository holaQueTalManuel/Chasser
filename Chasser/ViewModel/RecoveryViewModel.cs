using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Chasser.Common.Network;
using Chasser.Logic;
using Chasser.Logic.Network;

namespace Chasser.ViewModel
{
    public class RecoveryViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string _email;
        private string _newPassword;
        private string _confirmPassword;
        private string _token;

        public string Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(); }
        }

        public string NewPassword
        {
            get => _newPassword;
            set { _newPassword = value; OnPropertyChanged(); }
        }

        public string ConfirmPassword
        {
            get => _confirmPassword;
            set { _confirmPassword = value; OnPropertyChanged(); }
        }

        public ICommand ChangePasswordCommand { get; }
        public ICommand CancelCommand { get; }

        private readonly Window _window;

        public RecoveryViewModel(Window window)
        {
            _window = window;

            ChangePasswordCommand = new RelayCommand(ChangePassword);
            CancelCommand = new RelayCommand(Cancel);
        }

        private async void ChangePassword()
        {
            if (string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(NewPassword) ||
                string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                MessageBox.Show("Completa todos los campos.");
                return;
            }

            if (NewPassword != ConfirmPassword)
            {
                MessageBox.Show("Las contraseñas no coinciden.");
                return;
            }

            try
            {
                var request = new RequestMessage
                {
                    Command = "RECOVERY_PASSWORD",
                    Data = new Dictionary<string, string>
                    {
                        { "email", Email },
                        { "new_password", NewPassword },
                    }
                };

                await TCPClient.SendOnlyMessageAsync(request);
                var response = await TCPClient.ReceiveMessageAsync();

                if (response.Status == "SUCCESS")
                {
                    MessageBox.Show("Contraseña actualizada correctamente.");
                    _window.DialogResult = true;
                    _window.Close();
                }
                else
                {
                    MessageBox.Show("Error: " + response.Message);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error de conexión: " + ex.Message);
            }
        }

        private void Cancel()
        {
            _window.DialogResult = false;
            _window.Close();
        }

        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
