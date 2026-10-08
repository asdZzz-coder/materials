using materials.Models;

namespace materials.Services
{
    public enum StockError { None, QuantityNotPositive, NotEnoughStock, TooMuchStock, PurposeRequired, BorrowerRequired, MoreThanOutstanding, TextTooLong }

    /// <summary>
    /// 出入庫：存入、拿出、借出、歸還。每個動作都會改物料的數量並留下一筆紀錄；
    /// 借出另外建立借出單，歸還時從借出單扣（可以分次還）。
    /// 這裡只放不碰畫面與檔案的邏輯，方便單元測試。
    /// </summary>
    public static class StockService
    {
        public const int MaxTextLength = 100;

        public static string KindName(RecordKind kind) => kind switch
        {
            RecordKind.In => "存入",
            RecordKind.Out => "拿出",
            RecordKind.Lend => "借出",
            _ => "歸還",
        };

        /// <summary>存入、歸還會增加庫存；拿出、借出會減少。</summary>
        public static bool Adds(RecordKind kind) => kind is RecordKind.In or RecordKind.Return;

        public static string ItemDisplay(string name, string spec) =>
            spec.Trim().Length > 0 ? $"{name.Trim()}（{spec.Trim()}）" : name.Trim();

        public static string DaysAgo(DateTime then, DateTime now) => (now.Date - then.Date).Days switch
        {
            <= 0 => "今天",
            1 => "昨天",
            var d => $"{d} 天前",
        };

        /// <summary>
        /// 檢查一個動作能不能做。onHand 是目前庫存；歸還時 outstanding 是借出單還沒還的數量。
        /// 拿出一定要寫用在哪裡，借出一定要寫借給誰。
        /// </summary>
        public static StockError Check(RecordKind kind, decimal onHand, decimal quantity,
            string? purpose = null, string? borrower = null, decimal outstanding = 0)
        {
            purpose = CategoryService.Normalize(purpose);
            borrower = CategoryService.Normalize(borrower);
            if (quantity <= 0) return StockError.QuantityNotPositive;
            if (purpose.Length > MaxTextLength || borrower.Length > MaxTextLength) return StockError.TextTooLong;
            switch (kind)
            {
                case RecordKind.In:
                    if (onHand + quantity > QuantityText.Max) return StockError.TooMuchStock;
                    break;
                case RecordKind.Out:
                    if (quantity > onHand) return StockError.NotEnoughStock;
                    if (purpose.Length == 0) return StockError.PurposeRequired;
                    break;
                case RecordKind.Lend:
                    if (quantity > onHand) return StockError.NotEnoughStock;
                    if (borrower.Length == 0) return StockError.BorrowerRequired;
                    break;
                case RecordKind.Return:
                    if (quantity > outstanding) return StockError.MoreThanOutstanding;
                    if (onHand + quantity > QuantityText.Max) return StockError.TooMuchStock;
                    break;
            }
            return StockError.None;
        }

        private static void Ensure(StockError error)
        {
            if (error != StockError.None) throw new InvalidOperationException(error.ToString());
        }

        private static T Ref<T>(T target, MaterialItem item) where T : IItemRef
        {
            target.ItemId = item.Id;
            target.ItemName = item.Name;
            target.ItemSpec = item.Spec;
            target.Unit = item.Unit;
            return target;
        }

        private static void Touch(MaterialItem item, decimal delta, DateTime now)
        {
            item.Quantity = QuantityText.Adjust(item.Quantity, delta);
            item.UpdatedAt = now;
        }

        public static StockRecord StockIn(MaterialItem item, decimal quantity, string? note, DateTime now)
        {
            Ensure(Check(RecordKind.In, item.Quantity, quantity, note));
            Touch(item, quantity, now);
            return Ref(new StockRecord
            {
                Kind = RecordKind.In, Quantity = quantity, Purpose = CategoryService.Normalize(note), Time = now, Balance = item.Quantity,
            }, item);
        }

        public static StockRecord TakeOut(MaterialItem item, decimal quantity, string purpose, DateTime now)
        {
            Ensure(Check(RecordKind.Out, item.Quantity, quantity, purpose));
            Touch(item, -quantity, now);
            return Ref(new StockRecord
            {
                Kind = RecordKind.Out, Quantity = quantity, Purpose = CategoryService.Normalize(purpose), Time = now, Balance = item.Quantity,
            }, item);
        }

        public static (Loan Loan, StockRecord Record) Lend(MaterialItem item, decimal quantity, string borrower, string? purpose, DateTime now)
        {
            Ensure(Check(RecordKind.Lend, item.Quantity, quantity, purpose, borrower));
            Touch(item, -quantity, now);
            var loan = Ref(new Loan
            {
                Borrower = CategoryService.Normalize(borrower), Purpose = CategoryService.Normalize(purpose), Quantity = quantity, LentAt = now,
            }, item);
            var record = Ref(new StockRecord
            {
                Kind = RecordKind.Lend, Quantity = quantity, Borrower = loan.Borrower, Purpose = loan.Purpose,
                LoanId = loan.Id, Time = now, Balance = item.Quantity,
            }, item);
            return (loan, record);
        }

        /// <summary>
        /// 歸還（可以只還一部分）。物料已經被刪掉時 item 傳 null：只把借出單結清，不加回庫存。
        /// </summary>
        public static StockRecord Return(Loan loan, MaterialItem? item, decimal quantity, string? note, DateTime now)
        {
            Ensure(Check(RecordKind.Return, item?.Quantity ?? 0, quantity, note, outstanding: loan.Outstanding));
            loan.Returned += quantity;
            if (!loan.IsOpen) loan.ReturnedAt = now;
            if (item != null) Touch(item, quantity, now);
            var record = new StockRecord
            {
                ItemId = loan.ItemId, ItemName = item?.Name ?? loan.ItemName, ItemSpec = item?.Spec ?? loan.ItemSpec, Unit = loan.Unit,
                Kind = RecordKind.Return, Quantity = quantity, Borrower = loan.Borrower, Purpose = CategoryService.Normalize(note),
                LoanId = loan.Id, Time = now, Balance = item?.Quantity ?? 0,
            };
            return record;
        }

        /// <summary>
        /// 讓紀錄與借出單重新對上物料：找不到原本的物料時（例如「清空後匯入」換了一批物料），
        /// 改用名稱 + 規格找同一項；還沒還清的借出單順便更新成物料目前的名稱、規格、單位。
        /// </summary>
        public static void Relink(IReadOnlyCollection<MaterialItem> items, IEnumerable<IItemRef> refs)
        {
            var byId = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
            foreach (var r in refs)
            {
                if (!byId.TryGetValue(r.ItemId, out var item))
                {
                    item = items.FirstOrDefault(i => ExcelService.SameMaterial(i, new MaterialItem { Name = r.ItemName, Spec = r.ItemSpec }));
                    if (item == null) continue;
                    r.ItemId = item.Id;
                }
                if (r is Loan { IsOpen: true })
                {
                    r.ItemName = item.Name;
                    r.ItemSpec = item.Spec;
                    r.Unit = item.Unit;
                }
            }
        }

        /// <summary>每項物料目前借出未還的總數（沒有借出的不在字典裡）。</summary>
        public static Dictionary<string, decimal> Outstanding(IEnumerable<Loan> loans) =>
            loans.Where(l => l.IsOpen).GroupBy(l => l.ItemId).ToDictionary(g => g.Key, g => g.Sum(l => l.Outstanding));

        /// <summary>最近用過、不重複（不分大小寫）的前幾個值（例如用途、借用人），最新的在前。</summary>
        public static List<string> Recent(IEnumerable<StockRecord> records, Func<StockRecord, bool> filter, Func<StockRecord, string> pick, int count = 6)
        {
            var result = new List<string>();
            foreach (var v in records.Where(filter).Reverse().OrderByDescending(r => r.Time).Select(r => CategoryService.Normalize(pick(r))))
            {
                if (v.Length == 0 || result.Any(x => CategoryService.SameName(x, v))) continue;
                result.Add(v);
                if (result.Count == count) break;
            }
            return result;
        }

        public static MaterialItem? FindItem(IEnumerable<MaterialItem> items, IItemRef r) =>
            items.FirstOrDefault(i => i.Id == r.ItemId);
    }
}
