using System.IO;
using materials.Services;

namespace materials.Tests
{
    /// <summary>預設單位：名稱檢查、整理清單、調整順序，以及舊版存檔的相容性。</summary>
    public class UnitServiceTests
    {
        [Theory]
        [InlineData("", UnitNameError.Empty)]
        [InlineData("  ", UnitNameError.Empty)]
        [InlineData("盒", UnitNameError.Duplicate)]
        [InlineData(" 盒 ", UnitNameError.Duplicate)]
        [InlineData("PCS", UnitNameError.Duplicate)]
        [InlineData("打", UnitNameError.None)]
        public void Validate_ChecksEmptyAndDuplicate(string name, UnitNameError expected) =>
            Assert.Equal(expected, UnitService.Validate(name, ["盒", "pcs"]));

        [Fact]
        public void Validate_RejectsTooLongName() =>
            Assert.Equal(UnitNameError.TooLong, UnitService.Validate(new string('包', UnitService.MaxNameLength + 1), []));

        [Fact]
        public void Clean_NullMeansOldSaveFile_UsesDefaults() =>
            Assert.Equal(UnitService.Defaults, UnitService.Clean(null));

        [Fact]
        public void Clean_EmptyListStaysEmpty() =>
            Assert.Empty(UnitService.Clean([]));

        [Fact]
        public void Clean_RemovesBlanksAndDuplicates_KeepsOrder() =>
            Assert.Equal(["箱", "pcs", "個"], UnitService.Clean([" 箱 ", "", "pcs", "PCS", "個", "箱"]));

        [Fact]
        public void Move_SwapsWithNeighbour_IgnoresOutOfRange()
        {
            var units = new List<string> { "個", "盒", "包" };

            Assert.True(UnitService.Move(units, 2, -1));
            Assert.Equal(["個", "包", "盒"], units);

            Assert.False(UnitService.Move(units, 0, -1));
            Assert.False(UnitService.Move(units, 2, 1));
            Assert.Equal(["個", "包", "盒"], units);
        }

        [Fact]
        public void OldSaveFileWithoutUnits_LoadsAsNull()
        {
            var path = Path.Combine(Path.GetTempPath(), "MaterialsKeeper-Tests-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllText(path, """{ "Items": [], "Categories": ["文具"] }""");
                Assert.Null(DataStore.Load(path).Units);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
