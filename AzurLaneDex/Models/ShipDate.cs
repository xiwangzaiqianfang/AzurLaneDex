using System;
using System.Globalization;

namespace AzurLaneDex.Models
{
    /// <summary>
    /// 日期文本（yyyy-MM-dd）与 Unix 毫秒的互转。
    /// 数据文件中的日期统一存**文本**（可读、可直接手改），需要毫秒时现算；
    /// 解析兼容 yyyy/MM/dd、yyyy.MM.dd、带时间的写法以及纯数字（按旧毫秒时间戳处理）。
    /// </summary>
    public static class ShipDate
    {
        /// <summary>规范格式。</summary>
        public const string Format = "yyyy-MM-dd";

        /// <summary>文本 → Unix 毫秒（UTC 零点）；无法解析返回 0。</summary>
        public static long ToUnixMs(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            string t = text.Trim();

            // 纯数字：按旧毫秒时间戳处理（兼容手填）
            if (long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out long raw))
                return raw > 0 ? raw : 0;

            if (!DateTime.TryParse(t, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
                return 0;

            return new DateTimeOffset(dt.Date, TimeSpan.Zero).ToUnixTimeMilliseconds();
        }

        /// <summary>Unix 毫秒 → yyyy-MM-dd（UTC）；&lt;=0 返回空串。</summary>
        public static string FromUnixMs(long ms)
        {
            if (ms <= 0) return "";
            try
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime.ToString(Format, CultureInfo.InvariantCulture);
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// 任意常见写法 → 规范 yyyy-MM-dd；空白返回空串；无法解析时返回去空白后的原文本（交由调用方校验提示）。
        /// </summary>
        public static string Normalize(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            string t = text.Trim();

            long ms = ToUnixMs(t);
            if (ms > 0)
            {
                string formatted = FromUnixMs(ms);
                if (!string.IsNullOrEmpty(formatted)) return formatted;
            }
            return t;
        }

        /// <summary>是否是可解析的日期（用于保存前校验）。</summary>
        public static bool IsValid(string? text)
            => string.IsNullOrWhiteSpace(text) || ToUnixMs(text) > 0;
    }
}
