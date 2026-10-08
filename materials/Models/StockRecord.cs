using System.Text.Json.Serialization;
using materials.Services;

namespace materials.Models
{
    /// <summary>出入庫動作：存入、拿出（要寫用在哪裡）、借出（要寫借給誰）、歸還。</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<RecordKind>))]
    public enum RecordKind { In, Out, Lend, Return }

    /// <summary>紀錄與借出單共用：指向哪一項物料，並另存當時的名稱、規格、單位。</summary>
    public interface IItemRef
    {
        string ItemId { get; set; }
        string ItemName { get; set; }
        string ItemSpec { get; set; }
        string Unit { get; set; }
    }

    /// <summary>
    /// 一筆出入庫紀錄（只增不改）。物料之後改名或被刪掉，紀錄仍保留當時的名稱看得懂。
    /// </summary>
    public class StockRecord : IItemRef
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string ItemId { get; set; } = "";
        public string ItemName { get; set; } = "";
        public string ItemSpec { get; set; } = "";
        public string Unit { get; set; } = "";
        public RecordKind Kind { get; set; }
        public decimal Quantity { get; set; }

        /// <summary>拿出：用在哪裡；借出：用途；存入 / 歸還：說明。</summary>
        public string Purpose { get; set; } = "";

        /// <summary>借出 / 歸還：借用人。</summary>
        public string Borrower { get; set; } = "";

        /// <summary>借出 / 歸還：對應的借出單。</summary>
        public string LoanId { get; set; } = "";

        public DateTime Time { get; set; } = DateTime.Now;

        /// <summary>這筆動作之後的庫存數量。</summary>
        public decimal Balance { get; set; }

        // ---------- 僅供畫面使用，不存檔 ----------

        [JsonIgnore]
        public string KindDisplay => StockService.KindName(Kind);

        /// <summary>例如「+5 包」「−2 包」：存入、歸還是加，拿出、借出是減。</summary>
        [JsonIgnore]
        public string QuantityDisplay =>
            (StockService.Adds(Kind) ? "+" : "−") + (QuantityText.Format(Quantity) + " " + Unit.Trim()).Trim();

        [JsonIgnore]
        public string ItemDisplay => StockService.ItemDisplay(ItemName, ItemSpec);

        /// <summary>第二行說明，例如「借給 小王 · 會議用」「用在：3F 印表機」。</summary>
        [JsonIgnore]
        public string Detail
        {
            get
            {
                var parts = new List<string>();
                if (Borrower.Trim().Length > 0) parts.Add((Kind == RecordKind.Return ? "歸還人 " : "借給 ") + Borrower.Trim());
                if (Purpose.Trim().Length > 0) parts.Add((Kind == RecordKind.Out ? "用在：" : "") + Purpose.Trim().ReplaceLineEndings(" "));
                return string.Join(" · ", parts);
            }
        }

        [JsonIgnore]
        public string TimeDisplay => Time.ToString("yyyy/MM/dd HH:mm");

        [JsonIgnore]
        public string BalanceDisplay => "剩 " + (QuantityText.Format(Balance) + " " + Unit.Trim()).Trim();
    }

    /// <summary>借出單：借出時建立，歸還到數量全部還清為止（可以分次還）。</summary>
    public class Loan : IItemRef
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string ItemId { get; set; } = "";
        public string ItemName { get; set; } = "";
        public string ItemSpec { get; set; } = "";
        public string Unit { get; set; } = "";
        public string Borrower { get; set; } = "";
        public string Purpose { get; set; } = "";
        public decimal Quantity { get; set; }
        public decimal Returned { get; set; }
        public DateTime LentAt { get; set; } = DateTime.Now;

        /// <summary>全部還清的時間；還沒還清為 null。</summary>
        public DateTime? ReturnedAt { get; set; }

        // ---------- 僅供畫面使用，不存檔 ----------

        [JsonIgnore]
        public decimal Outstanding => Math.Max(0, Quantity - Returned);

        [JsonIgnore]
        public bool IsOpen => Outstanding > 0;

        [JsonIgnore]
        public string ItemDisplay => StockService.ItemDisplay(ItemName, ItemSpec);

        /// <summary>例如「未還 2 / 借 3 盒」或「借 3 盒」。</summary>
        [JsonIgnore]
        public string QuantityDisplay
        {
            get
            {
                var unit = Unit.Trim();
                var total = (QuantityText.Format(Quantity) + " " + unit).Trim();
                return Returned > 0 && IsOpen ? $"未還 {QuantityText.Format(Outstanding)} / 借 {total}" : $"借 {total}";
            }
        }

        [JsonIgnore]
        public string OutstandingDisplay => (QuantityText.Format(Outstanding) + " " + Unit.Trim()).Trim();

        [JsonIgnore]
        public string Detail
        {
            get
            {
                var s = $"借給 {Borrower.Trim()} · {LentAt:yyyy/MM/dd}（{StockService.DaysAgo(LentAt, DateTime.Now)}）";
                return Purpose.Trim().Length > 0 ? s + " · " + Purpose.Trim().ReplaceLineEndings(" ") : s;
            }
        }

        public override string ToString() => $"{ItemDisplay} {Borrower}";
    }
}
