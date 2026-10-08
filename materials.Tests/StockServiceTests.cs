using System.IO;
using materials.Models;
using materials.Services;

namespace materials.Tests
{
    /// <summary>出入庫：存入、拿出（要寫用途）、借出（要寫借給誰）、歸還（可分次），以及存檔相容性。</summary>
    public class StockServiceTests
    {
        private static readonly DateTime Now = new(2026, 10, 8, 9, 30, 0);

        private static MaterialItem Item(decimal qty = 10) => new() { Name = "A4 紙", Spec = "80g", Unit = "包", Quantity = qty };

        [Theory]
        [InlineData(RecordKind.In, 0, "", "", StockError.QuantityNotPositive)]
        [InlineData(RecordKind.In, 5, "", "", StockError.None)]
        [InlineData(RecordKind.Out, 5, "", "", StockError.PurposeRequired)]
        [InlineData(RecordKind.Out, 5, "  ", "", StockError.PurposeRequired)]
        [InlineData(RecordKind.Out, 11, "印表機", "", StockError.NotEnoughStock)]
        [InlineData(RecordKind.Out, 10, "印表機", "", StockError.None)]
        [InlineData(RecordKind.Lend, 3, "會議", "", StockError.BorrowerRequired)]
        [InlineData(RecordKind.Lend, 3, "", "小王", StockError.None)]
        [InlineData(RecordKind.Lend, 11, "", "小王", StockError.NotEnoughStock)]
        public void Check_ValidatesQuantityPurposeAndBorrower(RecordKind kind, int qty, string purpose, string borrower, StockError expected) =>
            Assert.Equal(expected, StockService.Check(kind, 10, qty, purpose, borrower));

        [Fact]
        public void Check_RejectsTooLongText() =>
            Assert.Equal(StockError.TextTooLong, StockService.Check(RecordKind.Out, 10, 1, new string('字', StockService.MaxTextLength + 1)));

        [Fact]
        public void Check_ReturnCannotExceedOutstanding()
        {
            Assert.Equal(StockError.MoreThanOutstanding, StockService.Check(RecordKind.Return, 0, 3, outstanding: 2));
            Assert.Equal(StockError.None, StockService.Check(RecordKind.Return, 0, 2, outstanding: 2));
        }

        [Fact]
        public void StockIn_AddsQuantityAndRecords()
        {
            var item = Item();
            var r = StockService.StockIn(item, 5, " 採購 ", Now);

            Assert.Equal(15, item.Quantity);
            Assert.Equal(Now, item.UpdatedAt);
            Assert.Equal((RecordKind.In, 5m, "採購", 15m), (r.Kind, r.Quantity, r.Purpose, r.Balance));
            Assert.Equal((item.Id, "A4 紙", "80g", "包"), (r.ItemId, r.ItemName, r.ItemSpec, r.Unit));
            Assert.Equal("+5 包", r.QuantityDisplay);
        }

        [Fact]
        public void TakeOut_SubtractsAndKeepsPurpose()
        {
            var item = Item();
            var r = StockService.TakeOut(item, 2.5m, "3F 印表機", Now);

            Assert.Equal(7.5m, item.Quantity);
            Assert.Equal("3F 印表機", r.Purpose);
            Assert.Equal("−2.5 包", r.QuantityDisplay);
            Assert.Equal("用在：3F 印表機", r.Detail);
        }

        [Fact]
        public void TakeOut_WithoutPurpose_Throws()
        {
            var item = Item();
            Assert.Throws<InvalidOperationException>(() => StockService.TakeOut(item, 1, "", Now));
            Assert.Equal(10, item.Quantity); // 失敗時不會動到庫存
        }

        [Fact]
        public void LendThenReturnInParts_ClosesLoan()
        {
            var item = Item();
            var (loan, lend) = StockService.Lend(item, 3, "小王", "會議", Now);

            Assert.Equal(7, item.Quantity);
            Assert.Equal((3m, 0m, true), (loan.Quantity, loan.Returned, loan.IsOpen));
            Assert.Equal(loan.Id, lend.LoanId);
            Assert.Equal(3m, StockService.Outstanding([loan])[item.Id]);

            var first = StockService.Return(loan, item, 1, "", Now.AddDays(1));
            Assert.Equal(8, item.Quantity);
            Assert.Equal(2, loan.Outstanding);
            Assert.True(loan.IsOpen);
            Assert.Null(loan.ReturnedAt);
            Assert.Equal((RecordKind.Return, "小王", loan.Id), (first.Kind, first.Borrower, first.LoanId));

            StockService.Return(loan, item, 2, "完好", Now.AddDays(2));
            Assert.Equal(10, item.Quantity);
            Assert.False(loan.IsOpen);
            Assert.Equal(Now.AddDays(2), loan.ReturnedAt);
            Assert.Empty(StockService.Outstanding([loan]));
        }

        [Fact]
        public void Return_MoreThanOutstanding_Throws()
        {
            var item = Item();
            var (loan, _) = StockService.Lend(item, 2, "小王", "", Now);
            Assert.Throws<InvalidOperationException>(() => StockService.Return(loan, item, 3, "", Now));
            Assert.Equal(8, item.Quantity);
        }

        [Fact]
        public void Return_ItemDeleted_ClosesLoanWithoutStock()
        {
            var item = Item();
            var (loan, _) = StockService.Lend(item, 2, "小王", "", Now);

            var r = StockService.Return(loan, null, 2, "", Now);

            Assert.False(loan.IsOpen);
            Assert.Equal("A4 紙", r.ItemName);
            Assert.Equal(0, r.Balance);
        }

        [Fact]
        public void Relink_FindsItemByNameAndSpec_AndUpdatesOpenLoans()
        {
            var old = Item();
            var (open, lendRecord) = StockService.Lend(old, 1, "小王", "", Now);
            var (closed, _) = StockService.Lend(old, 1, "小李", "", Now);
            StockService.Return(closed, old, 1, "", Now);

            // 「清空後匯入」：同名同規格、但是新的物料（Id 不同），單位也改了
            var fresh = new MaterialItem { Name = "a4 紙", Spec = "80G", Unit = "令" };
            StockService.Relink([fresh], [open, closed, lendRecord]);

            Assert.Equal(fresh.Id, open.ItemId);
            Assert.Equal(fresh.Id, closed.ItemId);
            Assert.Equal(fresh.Id, lendRecord.ItemId);
            Assert.Equal(("a4 紙", "令"), (open.ItemName, open.Unit)); // 還沒還清的借出單跟著物料更新
            Assert.Equal(("A4 紙", "包"), (closed.ItemName, closed.Unit)); // 已還清的保留當時的寫法
        }

        [Fact]
        public void Relink_UnknownItem_KeepsReference()
        {
            var (loan, _) = StockService.Lend(Item(), 1, "小王", "", Now);
            var id = loan.ItemId;
            StockService.Relink([new MaterialItem { Name = "別的東西" }], [loan]);
            Assert.Equal(id, loan.ItemId);
        }

        [Fact]
        public void Recent_ReturnsDistinctNewestFirst()
        {
            var item = Item(100);
            var records = new List<StockRecord>
            {
                StockService.TakeOut(item, 1, "印表機", Now),
                StockService.TakeOut(item, 1, "會議室", Now.AddMinutes(1)),
                StockService.TakeOut(item, 1, "印表機", Now.AddMinutes(2)),
                StockService.StockIn(item, 1, "採購", Now.AddMinutes(3)),
            };

            Assert.Equal(["印表機", "會議室"], StockService.Recent(records, r => r.Kind == RecordKind.Out, r => r.Purpose));
        }

        [Fact]
        public void Loan_DisplaysPartialReturn()
        {
            var item = Item();
            var (loan, _) = StockService.Lend(item, 3, "小王", "", Now);
            Assert.Equal("借 3 包", loan.QuantityDisplay);
            StockService.Return(loan, item, 1, "", Now);
            Assert.Equal("未還 2 / 借 3 包", loan.QuantityDisplay);
        }

        [Theory]
        [InlineData(0, "今天")]
        [InlineData(1, "昨天")]
        [InlineData(5, "5 天前")]
        public void DaysAgo_IsFriendly(int days, string expected) =>
            Assert.Equal(expected, StockService.DaysAgo(Now.AddDays(-days), Now));

        [Fact]
        public void DataStore_RoundTripsRecordsAndLoans_AndOldFilesGetItemIds()
        {
            var dir = Path.Combine(Path.GetTempPath(), "MaterialsKeeper-Tests-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(dir, DataStore.FileName);
            try
            {
                var item = Item();
                var (loan, record) = StockService.Lend(item, 2, "小王", "會議", Now);
                DataStore.Save(path, new MaterialData { Items = [item], Records = [record], Loans = [loan] });

                var json = File.ReadAllText(path);
                Assert.Contains("\"Lend\"", json); // 動作存成文字，用記事本打開也看得懂
                Assert.DoesNotContain(nameof(MaterialItem.LentDisplay), json);

                var data = DataStore.Load(path);
                Assert.Equal(item.Id, data.Items[0].Id);
                Assert.Equal((RecordKind.Lend, "小王", 2m), (data.Records[0].Kind, data.Records[0].Borrower, data.Records[0].Quantity));
                Assert.Equal((loan.Id, 2m, true), (data.Loans[0].Id, data.Loans[0].Outstanding, data.Loans[0].IsOpen));

                // 舊版存檔：物料沒有 Id、也沒有紀錄
                File.WriteAllText(path, """{ "Items": [ { "Name": "膠帶" }, { "Name": "剪刀" } ] }""");
                var old = DataStore.Load(path);
                Assert.All(old.Items, i => Assert.Equal(32, i.Id.Length));
                Assert.NotEqual(old.Items[0].Id, old.Items[1].Id);
                Assert.Empty(old.Records);
                Assert.Empty(old.Loans);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
        }
    }
}
