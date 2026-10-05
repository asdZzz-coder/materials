using materials.Services;

namespace materials.Models
{
    /// <summary>左側分類清單的一列（全部物料 / 各分類 / 未分類），每次資料變動時重新產生。</summary>
    public record CategoryItem(CategoryFilter Filter, string Display, int Count)
    {
        public bool IsUserCategory => Filter.Kind == CategoryFilterKind.Category;

        public string Icon => Filter.Kind switch
        {
            CategoryFilterKind.All => "",           // 全部
            CategoryFilterKind.Uncategorized => "", // 未分類（文件）
            _ => "",                                // 資料夾
        };

        // 螢幕閱讀器與 UI 自動化讀到的名稱
        public override string ToString() => Display;
    }

    /// <summary>編輯表單「分類」下拉選單的選項；Name 為空字串表示未分類。</summary>
    public record CategoryOption(string Name, string Display)
    {
        public override string ToString() => Display;
    }
}
