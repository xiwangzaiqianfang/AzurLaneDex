using AzurLaneDex.Models;
using System;
using Windows.ApplicationModel.Resources;
using Windows.Globalization;

namespace AzurLaneDex.Helpers
{
    public static class LocalizationHelper
    {
        private static string _currentLanguage;
        private static ResourceLoader _resourceLoader;

        public static string CurrentLanguage
        {
            get
            {
                if (string.IsNullOrEmpty(_currentLanguage))
                {
                    _currentLanguage = ApplicationLanguages.PrimaryLanguageOverride;
                    if (string.IsNullOrEmpty(_currentLanguage))
                    {
                        var langs = Windows.System.UserProfile.GlobalizationPreferences.Languages;
                        _currentLanguage = langs.Count > 0 ? langs[0] : "zh-Hans";
                    }
                }
                return _currentLanguage;
            }
            set
            {
                _currentLanguage = value;
                _resourceLoader = null;
            }
        }

        private static ResourceLoader GetResourceLoader()
        {
            if (_resourceLoader == null)
                _resourceLoader = ResourceLoader.GetForViewIndependentUse();
            return _resourceLoader;
        }

        public static string GetLocalized(this LocalizedString loc, string fallback = "")
        {
            if (loc == null) return fallback;
            if (loc.TryGetValue(CurrentLanguage, out var value) && !string.IsNullOrEmpty(value))
                return value;
            if (CurrentLanguage.Contains('-'))
            {
                var parent = CurrentLanguage.Split('-')[0];
                if (loc.TryGetValue(parent, out var parentValue) && !string.IsNullOrEmpty(parentValue))
                    return parentValue;
            }
            if (loc.TryGetValue("zh-Hans", out var zhValue) && !string.IsNullOrEmpty(zhValue))
                return zhValue;
            return fallback;
        }

        public static string GetEnumString(string prefix, int id)
        {
            if (id == 0) return "未知";
            var key = $"{prefix}_{id}";
            try
            {
                var result = GetResourceLoader().GetString(key);
                return string.IsNullOrEmpty(result) ? $"{prefix}_{id}" : result;
            }
            catch
            {
                return $"{prefix}_{id}";
            }
        }


        // 扩展：支持枚举类型直接传入
        public static string GetEnumString<TEnum>(TEnum value) where TEnum : Enum
        {
            return GetEnumString(typeof(TEnum).Name, Convert.ToInt32(value));
        }
    }
}