using System.IO;
using System.Text.Json;
using ClosedXML.Excel;
using materials.Models;
using materials.Services;

namespace materials.Tests
{
    /// <summary>存檔（JSON）與 Excel 匯出 / 匯入。全部在暫存資料夾進行，不會動到真正的資料。</summary>
    public sealed class StorageTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "MaterialsKeeper-Tests-" + Guid.NewGuid().ToString("N"));

        public StorageTests() => Directory.CreateDirectory(_root);

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static List<MaterialItem> Sample() =>
        [
            new() { Name = "A4 影印紙", Category = "文具", Spec = "80g", Quantity = 12, Unit = "包", Note = "放在影印機下方" },
            new() { Name = "螺絲", Category = "五金", Spec = "M3x10", Quantity = 0.5m, Unit = "盒", Note = "第一行\n第二行" },
            new() { Name = "'引號開頭", Category = "", Spec = "007", Quantity = 0, Unit = "", Note = "" },
        ];

        private static void AssertSame(IList<MaterialItem> expected, IList<MaterialItem> actual)
        {
            Assert.Equal(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i].Name, actual[i].Name);
                Assert.Equal(expected[i].Category, actual[i].Category);
                Assert.Equal(expected[i].Spec, actual[i].Spec);
                Assert.Equal(expected[i].Quantity, actual[i].Quantity);
                Assert.Equal(expected[i].Unit, actual[i].Unit);
                Assert.Equal(expected[i].Note, actual[i].Note);
            }
        }

        // ---------- JSON 存檔 ----------

        [Fact]
        public void DataStore_SaveThenLoad_RoundTrips()
        {
            var path = Path.Combine(_root, "sub", DataStore.FileName);
            var items = Sample();

            DataStore.Save(path, new MaterialData { Items = items, Categories = ["文具", "五金", "空分類"], Units = ["包", "盒"] });
            var data = DataStore.Load(path);

            AssertSame(items, data.Items);
            Assert.Equal(["文具", "五金", "空分類"], data.Categories);
            Assert.Equal(["包", "盒"], data.Units);
            Assert.False(File.Exists(path + ".tmp"));
        }

        [Fact]
        public void DataStore_StoresChineseAsReadableText()
        {
            var path = Path.Combine(_root, DataStore.FileName);
            DataStore.Save(path, new MaterialData { Items = Sample() });
            Assert.Contains("影印紙", File.ReadAllText(path));
        }

        [Fact]
        public void DataStore_MissingFile_ReturnsEmpty()
        {
            var data = DataStore.Load(Path.Combine(_root, "none.json"));
            Assert.Empty(data.Items);
            Assert.Empty(data.Categories);
        }

        [Fact]
        public void DataStore_CorruptFile_IsBackedUpAndStartsEmpty()
        {
            var path = Path.Combine(_root, DataStore.FileName);
            File.WriteAllText(path, "{ 這不是 JSON");

            var data = DataStore.Load(path);

            Assert.Empty(data.Items);
            Assert.False(File.Exists(path));
            Assert.Single(Directory.GetFiles(_root, DataStore.FileName + ".corrupt-*"));
        }

        [Fact]
        public void DataStore_DoesNotSaveDisplayOnlyProperties()
        {
            var path = Path.Combine(_root, DataStore.FileName);
            DataStore.Save(path, new MaterialData { Items = Sample() });
            var json = File.ReadAllText(path);
            Assert.DoesNotContain(nameof(MaterialItem.QuantityDisplay), json);
            Assert.DoesNotContain(nameof(MaterialItem.AvatarColor), json);
            using var _ = JsonDocument.Parse(json);
        }

        // ---------- Excel ----------

        [Fact]
        public void Excel_ExportThenImport_RoundTrips()
        {
            var path = Path.Combine(_root, "export.xlsx");
            var items = Sample();

            ExcelService.Export(items, path);
            var imported = ExcelService.Import(path);

            AssertSame(items, imported);
        }

        [Fact]
        public void Excel_Export_WritesQuantityAsNumber()
        {
            var path = Path.Combine(_root, "export.xlsx");
            ExcelService.Export(Sample(), path);

            using var wb = new XLWorkbook(path);
            var ws = wb.Worksheets.First();
            Assert.Equal(ExcelService.Headers, Enumerable.Range(1, 6).Select(c => ws.Cell(1, c).GetString()));
            Assert.Equal(XLDataType.Number, ws.Cell(2, 4).DataType);
            Assert.Equal("007", ws.Cell(4, 3).GetString()); // 規格保持文字，前導 0 不會消失
        }

        [Fact]
        public void Excel_Export_AddsRecordAndLoanSheets_ImportStillReadsItems()
        {
            var path = Path.Combine(_root, "export.xlsx");
            var items = Sample();
            var now = new DateTime(2026, 10, 8, 9, 0, 0);
            var takeOut = StockService.TakeOut(items[0], 2, "會議室", now);
            var (loan, lend) = StockService.Lend(items[0], 1, "小王", "", now);

            ExcelService.Export(items, path, [takeOut, lend], [loan]);

            using (var wb = new XLWorkbook(path))
            {
                Assert.Equal(["物料", "出入紀錄", "借出中"], wb.Worksheets.Select(w => w.Name));
                var rs = wb.Worksheet("出入紀錄");
                Assert.Equal("拿出", rs.Cell(2, 2).GetString());
                Assert.Equal(-2, rs.Cell(2, 5).GetDouble()); // 拿出記成負數
                Assert.Equal("會議室", rs.Cell(2, 7).GetString());
                Assert.Equal("小王", wb.Worksheet("借出中").Cell(2, 4).GetString());
            }
            AssertSame(items, ExcelService.Import(path)); // 匯入只讀第一個工作表
        }

        [Fact]
        public void Excel_Import_HandlesHandMadeSheet()
        {
            var path = Path.Combine(_root, "hand.xlsx");
            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("任意名稱");
                ws.Cell(1, 1).Value = "名稱";
                ws.Cell(2, 1).Value = "原子筆"; ws.Cell(2, 4).Value = "２０"; ws.Cell(2, 5).Value = "支";
                ws.Cell(3, 1).Value = "";       ws.Cell(3, 4).Value = 5;     // 沒有名稱 → 略過
                ws.Cell(4, 1).Value = "膠水";   ws.Cell(4, 4).Value = "很多"; // 看不懂的數量 → 0
                ws.Cell(5, 1).Value = "釘書針"; ws.Cell(5, 4).Value = -3;     // 負數 → 0
                wb.SaveAs(path);
            }

            var imported = ExcelService.Import(path);

            Assert.Equal(["原子筆", "膠水", "釘書針"], imported.Select(i => i.Name));
            Assert.Equal([20m, 0m, 0m], imported.Select(i => i.Quantity));
            Assert.Equal("支", imported[0].Unit);
            Assert.Equal("", imported[0].Category);
        }

        [Fact]
        public void SameMaterial_ComparesNameAndSpecIgnoringCase()
        {
            var a = new MaterialItem { Name = "USB 線", Spec = "Type-C" };
            Assert.True(ExcelService.SameMaterial(a, new MaterialItem { Name = " usb 線 ", Spec = "type-c" }));
            Assert.False(ExcelService.SameMaterial(a, new MaterialItem { Name = "USB 線", Spec = "Micro" }));
        }
    }
}
