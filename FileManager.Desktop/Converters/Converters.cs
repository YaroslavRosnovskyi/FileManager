using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace FileManager.Desktop.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            var result = boolValue ? Visibility.Visible : Visibility.Collapsed;
            System.Diagnostics.Debug.WriteLine($"BoolToVisibilityConverter: {boolValue} -> {result}");
            return result;
        }
        
        System.Diagnostics.Debug.WriteLine($"BoolToVisibilityConverter: Unexpected value type: {value?.GetType()}");
        return Visibility.Collapsed;
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
        
        return false;
    }
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }
        
        return Visibility.Visible;
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility != Visibility.Visible;
        }
        
        return true;
    }
}

public class FileSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long sizeInBytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = sizeInBytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            
            return $"{len:0.##} {sizes[order]}";
        }
        
        return "0 B";
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class ColumnVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isVisible && parameter is string columnName)
        {
            if (columnName == "Name")
                return Visibility.Visible;
                
            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }
        
        return Visibility.Visible;
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility && parameter is string columnName)
        {
            if (columnName == "Name")
                return true;
                
            return visibility == Visibility.Visible;
        }
        
        return true;
    }
}