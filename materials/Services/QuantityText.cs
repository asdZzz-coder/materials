using System.Globalization;

namespace materials.Services
{
    /// <summary>數量的文字轉換：可輸入小數（例如 0.5 卷），顯示時去掉多餘的 0。</summary>
    public static class QuantityText
    {
        public const decimal Max = 999_999_999m;

        /// <summary>12.50 → "12.5"，3.0 → "3"。</summary>
        public static string Format(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);

        /// <summary>
        /// 解析使用者輸入的數量：允許空白（當作 0）、全形數字、千分位逗號；
        /// 負數、超過上限或看不懂時回傳 false。
        /// </summary>
        public static bool TryParse(string? text, out decimal value)
        {
            value = 0;
            var s = (text ?? "").Trim().Normalize(System.Text.NormalizationForm.FormKC).Replace(",", "");
            if (s.Length == 0) return true;
            if (!decimal.TryParse(s, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v)) return false;
            if (v > Max) return false;
            value = Math.Round(v, 4);
            return true;
        }

        /// <summary>加減數量，結果不會小於 0。</summary>
        public static decimal Adjust(decimal value, decimal delta) => Math.Clamp(value + delta, 0, Max);
    }
}
