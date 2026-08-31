using AzurLaneDex.Helpers;
using AzurLaneDex.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using System;

namespace AzurLaneDex.Converters
{
    public class BoolToCheckMarkConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool b && b) ? "✓" : "✗";
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }

    public class BreakthroughToDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is int breakthrough)
            {
                // 满破显示实心星
                if (breakthrough >= 3)
                    return "⭐";
                else
                    return breakthrough.ToString();
            }
            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /*
    public class BoolToBreakthroughTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool b && b) ? "⭐" : "";
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }
    */

    public class BoolToStarConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool b && b) ? "⭐" : "☆";
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }
    public class BoolToHeartConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool b && b) ? "❤" : "♡";
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }
    public class BoolToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool b && b) ? "是" : "否";
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }
    public class BoolToRoleTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool b && b) ? "管理员" : "普通用户";
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    public class ImagePathToBitmapConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string path = value as string;
            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
            {
                try
                {
                    return new BitmapImage(new Uri(path));
                }
                catch { }
            }
            // 默认头像
            return new BitmapImage(new Uri("ms-appx:///Assets/default_avatar.png"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class EnumToDisplayStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return "";
            var enumType = value.GetType();
            if (!enumType.IsEnum) return value.ToString();
            return LocalizationHelper.GetEnumString(enumType.Name, (int)value);
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class DoublePercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null) return "0.00%";
            try
            {
                double d = System.Convert.ToDouble(value);
                return d.ToString("F2") + "%";
            }
            catch
            {
                return "0.00%";
            }
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    public class AcquisitionMethodTypeToTemplateConverter : IValueConverter
    {
        public DataTemplate ConstructionTemplate { get; set; }
        public DataTemplate DropTemplate { get; set; }
        public DataTemplate ExchangeTemplate { get; set; }
        public DataTemplate ResearchTemplate { get; set; }
        public DataTemplate OtherTemplate { get; set; }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is AcquisitionMethodType type)
            {
                return type switch
                {
                    AcquisitionMethodType.Construction => ConstructionTemplate,
                    AcquisitionMethodType.Drop => DropTemplate,
                    AcquisitionMethodType.Exchange => ExchangeTemplate,
                    AcquisitionMethodType.Research => ResearchTemplate,
                    AcquisitionMethodType.Other => OtherTemplate,
                    _ => ConstructionTemplate
                };
            }
            return ConstructionTemplate;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }

    public class EnumToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value != null && parameter is string expected)
            {
                return value.ToString().Equals(expected, StringComparison.OrdinalIgnoreCase)
                       ? Visibility.Visible
                       : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}