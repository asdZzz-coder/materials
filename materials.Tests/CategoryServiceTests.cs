using materials.Models;
using materials.Services;

namespace materials.Tests
{
    /// <summary>分類邏輯：名稱檢查、整理清單、篩選、重新命名與刪除。</summary>
    public class CategoryServiceTests
    {
        private static MaterialItem Item(string name, string category = "") => new() { Name = name, Category = category };

        [Theory]
        [InlineData("", CategoryNameError.Empty)]
        [InlineData("   ", CategoryNameError.Empty)]
        [InlineData("文具", CategoryNameError.Duplicate)]
        [InlineData(" 文具 ", CategoryNameError.Duplicate)]
        [InlineData("Tools", CategoryNameError.Duplicate)]
        [InlineData("清潔用品", CategoryNameError.None)]
        public void Validate_ChecksEmptyAndDuplicate(string name, CategoryNameError expected) =>
            Assert.Equal(expected, CategoryService.Validate(name, ["文具", "tools"]));

        [Fact]
        public void Validate_RejectsTooLongName() =>
            Assert.Equal(CategoryNameError.TooLong, CategoryService.Validate(new string('物', CategoryService.MaxNameLength + 1), []));

        [Fact]
        public void Validate_RenamingToDifferentCaseIsAllowed() =>
            Assert.Equal(CategoryNameError.None, CategoryService.Validate("Tools", ["tools", "文具"], renaming: "tools"));

        [Fact]
        public void Merge_RemovesBlanksAndDuplicates_AddsCategoriesUsedByItems()
        {
            var merged = CategoryService.Merge(["文具", " ", "tools", "Tools"], [Item("剪刀", "五金"), Item("膠帶", "文具"), Item("紙", "")]);

            Assert.Equal(3, merged.Count);
            Assert.Contains("文具", merged);
            Assert.Contains("五金", merged);
            Assert.Contains("tools", merged); // 保留先出現的寫法
        }

        [Fact]
        public void Filter_MatchesAllCategoryAndUncategorized()
        {
            var a = Item("A", "文具");
            var b = Item("B", "");
            var c = Item("C", " 文具 ");

            Assert.True(CategoryFilter.All.Matches(a) && CategoryFilter.All.Matches(b));
            Assert.True(CategoryFilter.Uncategorized.Matches(b));
            Assert.False(CategoryFilter.Uncategorized.Matches(a));
            Assert.True(CategoryFilter.Of("文具").Matches(a));
            Assert.True(CategoryFilter.Of("文具").Matches(c));
            Assert.False(CategoryFilter.Of("文具").Matches(b));
        }

        [Fact]
        public void Rename_MovesItemsToNewName()
        {
            var items = new[] { Item("A", "文具"), Item("B", "五金"), Item("C", "文具") };

            var (categories, moved) = CategoryService.Rename(["文具", "五金"], items, "文具", "辦公用品");

            Assert.Equal(2, moved);
            Assert.Equal(["五金", "辦公用品"], categories.Order());
            Assert.Equal(["辦公用品", "五金", "辦公用品"], items.Select(i => i.Category));
        }

        [Fact]
        public void Delete_KeepsItemsAsUncategorized()
        {
            var items = new[] { Item("A", "文具"), Item("B", "五金") };

            var (categories, moved) = CategoryService.Delete(["文具", "五金"], items, "文具");

            Assert.Equal(1, moved);
            Assert.Equal(["五金"], categories);
            Assert.Equal(["", "五金"], items.Select(i => i.Category));
        }

        [Fact]
        public void Canonical_ReturnsStoredSpellingOrEmpty()
        {
            Assert.Equal("Tools", CategoryService.Canonical(["Tools"], "tools"));
            Assert.Equal("", CategoryService.Canonical(["Tools"], "不存在"));
        }
    }
}
