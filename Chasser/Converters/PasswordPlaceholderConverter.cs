using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Chasser.Converters
{
    public class PasswordPlaceholderConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 3 && values[0] is bool isPasswordVisible)
            {
                var passwordText = values[1] as string;
                var password = values[2] as string;

                if (isPasswordVisible)
                {
                    return string.IsNullOrEmpty(passwordText) ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    return string.IsNullOrEmpty(password) ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            return Visibility.Visible;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}