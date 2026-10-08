using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using materials.Services;

namespace materials.Models
{
    /// <summary>
    /// 一項部門物料。只記整理需要的欄位：名稱、分類、規格、數量、單位、備註
    /// （刻意不放儲位、料號、經手人、安全庫存、金額）。
    /// 會通知畫面更新，清單上按「＋／－」調整數量時能即時看到。
    /// </summary>
    public class MaterialItem : INotifyPropertyChanged
    {
        private string _name = "";
        private string _category = "";
        private string _spec = "";
        private decimal _quantity;
        private string _unit = "";
        private string _note = "";
        private DateTime _updatedAt = DateTime.Now;
        private decimal _lentQuantity;

        /// <summary>固定識別碼，出入庫紀錄與借出單靠它對應物料（舊版存檔沒有，載入時自動補上）。</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Name { get => _name; set => Set(ref _name, value, nameof(Initial), nameof(AvatarColor)); }

        /// <summary>所屬分類；空字串表示「未分類」。</summary>
        public string Category { get => _category; set => Set(ref _category, value); }

        public string Spec { get => _spec; set => Set(ref _spec, value, nameof(Subtitle)); }

        public decimal Quantity { get => _quantity; set => Set(ref _quantity, value, nameof(QuantityDisplay), nameof(IsEmpty)); }

        public string Unit { get => _unit; set => Set(ref _unit, value, nameof(QuantityDisplay), nameof(LentDisplay)); }

        public string Note { get => _note; set => Set(ref _note, value, nameof(Subtitle)); }

        /// <summary>最後修改時間（新增、儲存、調整數量時更新）。</summary>
        public DateTime UpdatedAt { get => _updatedAt; set => Set(ref _updatedAt, value); }

        /// <summary>借出未還的數量（由借出單算出，不存檔）；不算在 Quantity（在庫數量）裡。</summary>
        [JsonIgnore]
        public decimal LentQuantity { get => _lentQuantity; set => Set(ref _lentQuantity, value, nameof(LentDisplay), nameof(HasLent)); }

        // 螢幕閱讀器與 UI 自動化讀到的名稱（不影響存檔）
        public override string ToString() => Name;

        // ---------- 僅供畫面使用，不存檔 ----------

        /// <summary>清單右側的數量徽章，例如「12 盒」。</summary>
        [JsonIgnore]
        public string QuantityDisplay => (QuantityText.Format(Quantity) + " " + Unit.Trim()).Trim();

        /// <summary>清單上的借出徽章，例如「借出 2 盒」。</summary>
        [JsonIgnore]
        public string LentDisplay => ("借出 " + QuantityText.Format(LentQuantity) + " " + Unit.Trim()).Trim();

        [JsonIgnore]
        public bool HasLent => LentQuantity > 0;

        /// <summary>數量為 0 時徽章改成紅色提醒。</summary>
        [JsonIgnore]
        public bool IsEmpty => Quantity <= 0;

        /// <summary>清單第二行：規格，沒有規格時顯示備註。</summary>
        [JsonIgnore]
        public string Subtitle => Spec.Trim().Length > 0 ? Spec.Trim() : Note.Trim().ReplaceLineEndings(" ");

        /// <summary>清單頭像上顯示的第一個字。</summary>
        [JsonIgnore]
        public string Initial => string.IsNullOrWhiteSpace(Name)
            ? "?"
            : Name.Trim().EnumerateRunes().First().ToString().ToUpperInvariant();

        private static readonly string[] AvatarColors =
        {
            "#4F46E5", "#0EA5E9", "#10B981", "#F59E0B", "#EF4444",
            "#EC4899", "#8B5CF6", "#14B8A6", "#F97316", "#6366F1",
        };

        /// <summary>依名稱固定挑一個頭像底色（同名永遠同色）。</summary>
        [JsonIgnore]
        public string AvatarColor
        {
            get
            {
                uint h = 2166136261; // FNV-1a，跨次啟動結果一致（string.GetHashCode 每次啟動都不同）
                foreach (var c in Name.Trim().ToUpperInvariant()) { h ^= c; h *= 16777619; }
                return AvatarColors[h % (uint)AvatarColors.Length];
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>設定欄位並通知畫面；also 是跟著改變的顯示用屬性。</summary>
        private void Set<T>(ref T field, T value, string? also1 = null, string? also2 = null, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            foreach (var n in new[] { name, also1, also2 })
                if (n != null) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        }
    }
}
