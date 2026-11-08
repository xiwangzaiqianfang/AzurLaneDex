using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using System;

namespace AzurLaneDex.Converters
{
    public class StringToImageSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string uriString && !string.IsNullOrEmpty(uriString))
            {
                try
                {
                    return new BitmapImage(new Uri(uriString));
                }
                catch
                {
                    // 加载失败返回默认占位
                }
            }
            // 默认头像（占位图：Assets/icon/square/unknown.png）
            return new BitmapImage(new Uri("ms-appx:///Assets/icon/square/unknown.png"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }
}