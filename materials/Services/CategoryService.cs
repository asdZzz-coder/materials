using materials.Models;

namespace materials.Services
{
    public enum CategoryFilterKind { All, Uncategorized, Category }

    /// <summary>左側分類清單目前選的是哪一個：全部物料 / 某個分類 / 未分類。</summary>
    public readonly record struct CategoryFilter(CategoryFilterKind Kind, string Name = "")
    {
        public static readonly CategoryFilter All = new(CategoryFilterKind.All);
        public static readonly CategoryFilter Uncategorized = new(CategoryFilterKind.Uncategorized);
        public static CategoryFilter Of(string name) => new(CategoryFilterKind.Category, CategoryService.Normalize(name));

        public bool Matches(MaterialItem item) => Kind switch
        {
            CategoryFilterKind.All => true,
            CategoryFilterKind.Uncategorized => CategoryService.Normalize(item.Category).Length == 0,
            _ => CategoryService.SameName(item.Category, Name),
        };

        /// <summary>兩個篩選是否指向同一個分類（名稱不分大小寫）。</summary>
        public bool SameAs(CategoryFilter other) => Kind == other.Kind && CategoryService.SameName(Name, other.Name);
    }

    public enum CategoryNameError { None, Empty, TooLong, Duplicate }

    /// <summary>
    /// 物料分類：每項物料屬於一個分類或「未分類」。
    /// 分類以名稱識別、不分大小寫；清單另外存一份，所以還沒放物料的空分類也會保留。
    /// 這裡只放不碰畫面與檔案的邏輯，方便單元測試。
    /// </summary>
    public static class CategoryService
    {
        public const int MaxNameLength = 40;

        public static string Normalize(string? name) => (name ?? "").Trim();

        public static bool SameName(string? a, string? b) =>
            string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);

        /// <summary>檢查新名稱；重新命名時傳入原名稱，只改大小寫不算重複。</summary>
        public static CategoryNameError Validate(string? name, IEnumerable<string> existing, string? renaming = null)
        {
            var n = Normalize(name);
            if (n.Length == 0) return CategoryNameError.Empty;
            if (n.Length > MaxNameLength) return CategoryNameError.TooLong;
            if (existing.Any(c => SameName(c, n) && !(renaming != null && SameName(c, renaming))))
                return CategoryNameError.Duplicate;
            return CategoryNameError.None;
        }

        /// <summary>
        /// 整理分類清單：去掉空白與重複（不分大小寫，保留先出現的寫法），
        /// 並補上物料用到、但清單裡沒有的分類（例如從 Excel 匯入的），最後依名稱排序。
        /// </summary>
        public static List<string> Merge(IEnumerable<string> categories, IEnumerable<MaterialItem> items)
        {
            var result = new List<string>();
            foreach (var name in categories.Concat(items.Select(i => i.Category)).Select(Normalize))
                if (name.Length > 0 && !result.Any(c => SameName(c, name)))
                    result.Add(name);
            result.Sort(StringComparer.CurrentCultureIgnoreCase);
            return result;
        }

        /// <summary>清單裡與 name 同名（不分大小寫）的分類寫法；沒有則回傳空字串（＝未分類）。</summary>
        public static string Canonical(IEnumerable<string> categories, string? name) =>
            categories.FirstOrDefault(c => SameName(c, name)) ?? "";

        /// <summary>重新命名分類，裡面的物料一起改過去；回傳新清單與搬動的筆數。</summary>
        public static (List<string> Categories, int Moved) Rename(
            IEnumerable<string> categories, IEnumerable<MaterialItem> items, string oldName, string newName)
        {
            newName = Normalize(newName);
            int moved = 0;
            foreach (var i in items)
                if (SameName(i.Category, oldName)) { i.Category = newName; moved++; }
            return (Merge(categories.Where(c => !SameName(c, oldName)).Append(newName), []), moved);
        }

        /// <summary>刪除分類：物料不會被刪掉，而是移到「未分類」；回傳新清單與搬動的筆數。</summary>
        public static (List<string> Categories, int Moved) Delete(
            IEnumerable<string> categories, IEnumerable<MaterialItem> items, string name)
        {
            int moved = 0;
            foreach (var i in items)
                if (SameName(i.Category, name)) { i.Category = ""; moved++; }
            return (Merge(categories.Where(c => !SameName(c, name)), []), moved);
        }
    }
}
