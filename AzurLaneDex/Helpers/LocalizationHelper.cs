using AzurLaneDex.Models;
using AzurLaneDex.Services;
using System;
using Windows.ApplicationModel.Resources;
using Windows.ApplicationModel.Resources.Core;

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
                    // 不再读取 PrimaryLanguageOverride（部分环境下会抛 0x80073DE1），
                    // 直接用系统首选语言兜底；启动时会由 App 从 config 覆盖为已保存语言
                    try
                    {
                        var langs = Windows.System.UserProfile.GlobalizationPreferences.Languages;
                        _currentLanguage = langs.Count > 0 ? langs[0] : "zh-Hans";
                    }
                    catch
                    {
                        _currentLanguage = "zh-Hans";
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

        /// <summary>
        /// 尝试设置全局资源语言限定符（替代 PrimaryLanguageOverride 的安全方案）。
        /// 优点：打包/未打包环境均可用，不会抛 0x80073DE1。
        /// 局限：只影响“之后加载”的 .resw 资源；已加载到 XAML 的文本需重建页面/重启才刷新。
        /// </summary>
        public static bool TrySetSystemLanguageOverride(string language)
        {
            if (string.IsNullOrWhiteSpace(language)) return false;
            try
            {
                ResourceContext.SetGlobalQualifierValue("Language", language);

                // 读回校验：限定符可能被规范化（zh-hans）或未被接受（回退到系统语言）
                try
                {
                    var context = ResourceContext.GetForViewIndependentUse();
                    string actual = context.QualifierValues.TryGetValue("Language", out var value) ? value : "";
                    if (string.IsNullOrEmpty(actual))
                        return true;   // 某些环境不回报限定符，视为已提交

                    if (!string.Equals(actual, language, StringComparison.OrdinalIgnoreCase))
                    {
                        LogService.Warning(
                            $"资源语言限定符与请求不一致（请求 {language}，实际 {actual}），框架文案可能未按预期切换",
                            "Localization");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    LogService.Warning($"读回资源语言限定符失败（不影响设置）：{ex.Message}", "Localization");
                }
                return true;
            }
            catch (Exception ex)
            {
                LogService.Warning(
                    $"设置资源语言限定符失败（{language}，HRESULT 0x{ex.HResult:X8}）：{ex.Message}",
                    "Localization");
                return false;
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
            if (loc.TryGetValue("en", out var enValue) && !string.IsNullOrEmpty(enValue))
                return enValue;
            return fallback;
        }

        public static bool HasAnyText(this LocalizedString? loc)
        {
            if (loc == null) return false;
            foreach (var v in loc.Values)
                if (!string.IsNullOrEmpty(v)) return true;
            return false;
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

        public static string GetEnumString<TEnum>(TEnum value) where TEnum : Enum
        {
            return GetEnumString(typeof(TEnum).Name, Convert.ToInt32(value));
        }
    }
}