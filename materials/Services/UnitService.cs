namespace materials.Services
{
    public enum UnitNameError { None, Empty, TooLong, Duplicate }

    /// <summary>
    /// 預設單位：編輯表單「單位」欄的下拉清單，可自行新增、刪除、調整順序。
    /// 只是方便點選的清單，單位欄仍可直接輸入清單以外的文字。
    /// 這裡只放不碰畫面與檔案的邏輯，方便單元測試。
    /// </summary>
    public static class UnitService
    {
        public const int MaxNameLength = 10;

        /// <summary>第一次使用（或按「恢復預設」）時的單位清單。</summary>
        public static readonly IReadOnlyList<string> Defaults =
            ["個", "盒", "包", "箱", "支", "張", "本", "卷", "瓶", "罐", "組", "套", "台", "條", "片", "雙", "袋", "桶"];

        /// <summary>檢查新單位名稱（不分大小寫比對重複）。</summary>
        public static UnitNameError Validate(string? name, IEnumerable<string> existing)
        {
            var n = CategoryService.Normalize(name);
            if (n.Length == 0) return UnitNameError.Empty;
            if (n.Length > MaxNameLength) return UnitNameError.TooLong;
            if (existing.Any(u => CategoryService.SameName(u, n))) return UnitNameError.Duplicate;
            return UnitNameError.None;
        }

        /// <summary>
        /// 整理存檔裡的單位清單：去掉空白與重複（不分大小寫，保留先出現的寫法），保留使用者排的順序。
        /// 舊版存檔沒有這個欄位（null）時使用預設清單；使用者刻意清空則維持空清單。
        /// </summary>
        public static List<string> Clean(IEnumerable<string>? units)
        {
            var result = new List<string>();
            foreach (var name in (units ?? Defaults).Select(CategoryService.Normalize))
                if (name.Length > 0 && !result.Any(u => CategoryService.SameName(u, name)))
                    result.Add(name);
            return result;
        }

        /// <summary>把第 index 個單位往前（offset = -1）或往後（+1）移一格；超出範圍則不動。</summary>
        public static bool Move(IList<string> units, int index, int offset)
        {
            int target = index + offset;
            if (index < 0 || index >= units.Count || target < 0 || target >= units.Count) return false;
            (units[index], units[target]) = (units[target], units[index]);
            return true;
        }
    }
}
