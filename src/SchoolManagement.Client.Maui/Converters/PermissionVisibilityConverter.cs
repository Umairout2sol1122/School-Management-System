using System.Globalization;
using SchoolManagement.Client.Core.Auth;

namespace SchoolManagement.Client.Maui.Converters;

public class PermissionVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Try getting permission string from parameter, or from value
        string? permission = parameter as string ?? value as string;

        if (string.IsNullOrWhiteSpace(permission))
            return true;

        try
        {
            var authState = IPlatformApplication.Current?.Services.GetService<ClientAuthState>();
            if (authState == null)
                return false;

            return authState.HasPermission(permission);
        }
        catch
        {
            return false;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw freshNotSupportedException();

        static NotSupportedException freshNotSupportedException() => new();
    }
}
