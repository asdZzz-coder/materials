using materials.Models;
using materials.Services;

namespace materials.Tests
{
    /// <summary>數量的輸入解析、顯示格式與加減。</summary>
    public class QuantityTextTests
    {
        [Theory]
        [InlineData("12", 12)]
        [InlineData(" 3.5 ", 3.5)]
        [InlineData("", 0)]
        [InlineData("１２", 12)]      // 全形數字
        [InlineData("1,200", 1200)]   // 千分位
        [InlineData("0.123456", 0.1235)]
        public void TryParse_AcceptsValidInput(string text, double expected)
        {
            Assert.True(QuantityText.TryParse(text, out var v));
            Assert.Equal((decimal)expected, v);
        }

        [Theory]
        [InlineData("-1")]
        [InlineData("abc")]
        [InlineData("1e5")]
        [InlineData("9999999999")]
        public void TryParse_RejectsInvalidInput(string text) => Assert.False(QuantityText.TryParse(text, out _));

        [Theory]
        [InlineData(12, "12")]
        [InlineData(12.5, "12.5")]
        [InlineData(0, "0")]
        [InlineData(0.25, "0.25")]
        public void Format_TrimsTrailingZeros(double value, string expected) =>
            Assert.Equal(expected, QuantityText.Format((decimal)value));

        [Fact]
        public void Adjust_NeverGoesBelowZero()
        {
            Assert.Equal(4m, QuantityText.Adjust(3m, 1));
            Assert.Equal(0m, QuantityText.Adjust(0m, -1));
            Assert.Equal(0m, QuantityText.Adjust(0.5m, -1));
        }

        [Fact]
        public void MaterialItem_DisplaysQuantityWithUnit()
        {
            var item = new MaterialItem { Name = "A4 紙", Quantity = 12.50m, Unit = "包" };
            Assert.Equal("12.5 包", item.QuantityDisplay);
            Assert.False(item.IsEmpty);

            item.Quantity = 0;
            item.Unit = "";
            Assert.Equal("0", item.QuantityDisplay);
            Assert.True(item.IsEmpty);
        }

        [Fact]
        public void MaterialItem_NotifiesDisplayPropertiesWhenQuantityChanges()
        {
            var item = new MaterialItem { Name = "膠帶" };
            var changed = new List<string?>();
            item.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            item.Quantity = 3;

            Assert.Contains(nameof(MaterialItem.Quantity), changed);
            Assert.Contains(nameof(MaterialItem.QuantityDisplay), changed);
            Assert.Contains(nameof(MaterialItem.IsEmpty), changed);
        }
    }
}
