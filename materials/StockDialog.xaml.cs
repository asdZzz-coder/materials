using System.Windows;
using System.Windows.Controls;
using materials.Models;
using materials.Services;

namespace materials
{
    /// <summary>存入 / 拿出 / 借出 對話框填的內容。</summary>
    public record StockInput(decimal Quantity, string Purpose, string Borrower);

    public partial class StockDialog : Window
    {
        private readonly RecordKind _kind;
        private readonly MaterialItem _item;
        private StockInput? _result;

        private StockDialog(RecordKind kind, MaterialItem item, IReadOnlyList<string> recentPurposes, IReadOnlyList<string> recentBorrowers)
        {
            InitializeComponent();
            _kind = kind;
            _item = item;
            var name = StockService.KindName(kind);
            var unit = item.Unit.Trim();
            Title = $"{name}物料";
            ItemText.Text = StockService.ItemDisplay(item.Name, item.Spec);
            ItemText.ToolTip = ItemText.Text;
            StockText.Text = $"目前庫存 {item.QuantityDisplay}" + (item.HasLent ? $" · {item.LentDisplay}" : "");
            KindText.Text = name;
            var (soft, strong) = StockStyles.Brushes(kind);
            KindBadge.SetResourceReference(Border.BackgroundProperty, soft);
            KindText.SetResourceReference(TextBlock.ForegroundProperty, strong);
            QtyLabel.Text = unit.Length > 0 ? $"{name}數量（{unit}）*" : $"{name}數量 *";
            PurposeLabel.Text = kind switch
            {
                RecordKind.Out => "用在哪裡 *",
                RecordKind.Lend => "用途（選填）",
                _ => "說明（選填，例如：採購、其他部門給的）",
            };
            BorrowerPanel.Visibility = kind == RecordKind.Lend ? Visibility.Visible : Visibility.Collapsed;
            OkButton.Content = name;

            // 預設 1；要拿走的東西庫存不到 1（例如剩 0.5 卷）就預設全部
            bool removes = !StockService.Adds(kind);
            QtyBox.Text = removes && item.Quantity < 1 ? QuantityText.Format(item.Quantity) : "1";

            Chips.Fill(PurposeChips, recentPurposes, v => Pick(PurposeBox, v), caption: "最近：", small: true);
            Chips.Fill(BorrowerChips, recentBorrowers, v => Pick(BorrowerBox, v), caption: "最近：", small: true);
            UpdatePreview();
            Loaded += (_, _) => { QtyBox.Focus(); QtyBox.SelectAll(); };
        }

        /// <summary>顯示對話框；按確定且通過檢查時回傳填的內容，取消則回傳 null。</summary>
        public static StockInput? Ask(Window owner, RecordKind kind, MaterialItem item,
            IReadOnlyList<string> recentPurposes, IReadOnlyList<string> recentBorrowers)
        {
            var dlg = new StockDialog(kind, item, recentPurposes, recentBorrowers) { Owner = owner };
            return dlg.ShowDialog() == true ? dlg._result : null;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            WindowTheme.ApplyTitleBar(this, "CardBrush");
        }

        private static void Pick(TextBox box, string value)
        {
            box.Text = value;
            box.Focus();
            box.CaretIndex = box.Text.Length;
        }

        private void Step(decimal delta)
        {
            if (QuantityText.TryParse(QtyBox.Text, out var q))
                QtyBox.Text = QuantityText.Format(QuantityText.Adjust(q, delta));
        }

        private void QtyPlus_Click(object sender, RoutedEventArgs e) => Step(1);

        private void QtyMinus_Click(object sender, RoutedEventArgs e) => Step(-1);

        private void Field_TextChanged(object sender, TextChangedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;
            if (sender == QtyBox) UpdatePreview();
        }

        /// <summary>數量下方預告做完之後的庫存，例如「拿出後剩 10 包」。</summary>
        private void UpdatePreview()
        {
            if (PreviewText == null) return; // InitializeComponent 期間就會觸發 TextChanged
            if (!QuantityText.TryParse(QtyBox.Text, out var q) || q <= 0) { PreviewText.Text = ""; return; }
            bool adds = StockService.Adds(_kind);
            if (!adds && q > _item.Quantity)
            {
                PreviewText.Text = $"超過目前庫存（{_item.QuantityDisplay}）";
                return;
            }
            var after = QuantityText.Adjust(_item.Quantity, adds ? q : -q);
            PreviewText.Text = $"{StockService.KindName(_kind)}後庫存 {(QuantityText.Format(after) + " " + _item.Unit.Trim()).Trim()}";
        }

        private void ShowError(string message, Control focus)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
            focus.Focus();
            if (focus is TextBox t) t.SelectAll();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (!QuantityText.TryParse(QtyBox.Text, out var qty))
            {
                ShowError("數量請輸入正數（可以有小數，例如 0.5）。", QtyBox);
                return;
            }
            var name = StockService.KindName(_kind);
            switch (StockService.Check(_kind, _item.Quantity, qty, PurposeBox.Text, BorrowerBox.Text))
            {
                case StockError.QuantityNotPositive: ShowError("請輸入大於 0 的數量。", QtyBox); return;
                case StockError.NotEnoughStock: ShowError($"目前庫存只有 {_item.QuantityDisplay}，不夠{name}這麼多。", QtyBox); return;
                case StockError.TooMuchStock: ShowError("數量太大了。", QtyBox); return;
                case StockError.PurposeRequired: ShowError("請寫上拿去用在哪裡。", PurposeBox); return;
                case StockError.BorrowerRequired: ShowError("請寫上借給誰。", BorrowerBox); return;
                case StockError.TextTooLong: ShowError($"文字最多 {StockService.MaxTextLength} 個字。", PurposeBox); return;
            }
            _result = new StockInput(qty, CategoryService.Normalize(PurposeBox.Text), CategoryService.Normalize(BorrowerBox.Text));
            DialogResult = true;
        }
    }

    /// <summary>四種動作的代表色：存入、歸還綠色，拿出主色，借出琥珀色。</summary>
    internal static class StockStyles
    {
        public static (string Soft, string Strong) Brushes(RecordKind kind) => kind switch
        {
            RecordKind.In or RecordKind.Return => ("SuccessSoftBrush", "SuccessBrush"),
            RecordKind.Lend => ("WarnSoftBrush", "WarnBrush"),
            _ => ("AccentSoftBrush", "AccentBrush"),
        };
    }
}
