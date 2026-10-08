using System.Windows;
using System.Windows.Controls;
using materials.Models;
using materials.Services;

namespace materials
{
    /// <summary>歸還對話框填的內容。</summary>
    public record ReturnInput(Loan Loan, decimal Quantity, string Note);

    public partial class ReturnDialog : Window
    {
        private ReturnInput? _result;

        private ReturnDialog(IReadOnlyList<Loan> loans, Loan? selected)
        {
            InitializeComponent();
            LoanList.ItemsSource = loans;
            LoanList.SelectedItem = selected ?? (loans.Count == 1 ? loans[0] : null);
            UpdateQuantity();
            Loaded += (_, _) =>
            {
                if (Selected == null) { LoanList.Focus(); return; }
                LoanList.ScrollIntoView(Selected);
                QtyBox.Focus();
                QtyBox.SelectAll();
            };
        }

        /// <param name="loans">還沒還清的借出單。</param>
        /// <param name="selected">預先選好的一筆；null 時只有一筆就自動選它。</param>
        public static ReturnInput? Ask(Window owner, IReadOnlyList<Loan> loans, Loan? selected = null)
        {
            var dlg = new ReturnDialog(loans, selected) { Owner = owner };
            return dlg.ShowDialog() == true ? dlg._result : null;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            WindowTheme.ApplyTitleBar(this, "CardBrush");
        }

        private Loan? Selected => LoanList.SelectedItem as Loan;

        /// <summary>選了哪一筆，數量就預設為那筆還沒還的全部。</summary>
        private void UpdateQuantity()
        {
            var loan = Selected;
            QtyLabel.Text = loan != null && loan.Unit.Trim().Length > 0 ? $"歸還數量（{loan.Unit.Trim()}）*" : "歸還數量 *";
            QtyBox.Text = loan == null ? "" : QuantityText.Format(loan.Outstanding);
        }

        private void LoanList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateQuantity();

        private void Step(decimal delta)
        {
            if (QuantityText.TryParse(QtyBox.Text, out var q))
                QtyBox.Text = QuantityText.Format(QuantityText.Adjust(q, delta));
        }

        private void QtyPlus_Click(object sender, RoutedEventArgs e) => Step(1);

        private void QtyMinus_Click(object sender, RoutedEventArgs e) => Step(-1);

        private void Field_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ErrorText == null) return;
            ErrorText.Visibility = Visibility.Collapsed;
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (PreviewText == null) return;
            var loan = Selected;
            if (loan == null || !QuantityText.TryParse(QtyBox.Text, out var q) || q <= 0 || q > loan.Outstanding)
            {
                PreviewText.Text = "";
                return;
            }
            var left = loan.Outstanding - q;
            PreviewText.Text = left > 0
                ? $"還完這次後，{loan.Borrower} 還有 {(QuantityText.Format(left) + " " + loan.Unit.Trim()).Trim()} 未還"
                : "這筆借出會全部還清";
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
            if (Selected is not { } loan)
            {
                ShowError("請先在上方選擇要歸還的是哪一筆借出。", LoanList);
                return;
            }
            if (!QuantityText.TryParse(QtyBox.Text, out var qty))
            {
                ShowError("數量請輸入正數（可以有小數，例如 0.5）。", QtyBox);
                return;
            }
            switch (StockService.Check(RecordKind.Return, 0, qty, NoteBox.Text, outstanding: loan.Outstanding))
            {
                case StockError.QuantityNotPositive: ShowError("請輸入大於 0 的數量。", QtyBox); return;
                case StockError.MoreThanOutstanding: ShowError($"這筆只有 {loan.OutstandingDisplay} 未還。", QtyBox); return;
                case StockError.TextTooLong: ShowError($"說明最多 {StockService.MaxTextLength} 個字。", NoteBox); return;
                case StockError.TooMuchStock: ShowError("數量太大了。", QtyBox); return;
            }
            _result = new ReturnInput(loan, qty, CategoryService.Normalize(NoteBox.Text));
            DialogResult = true;
        }
    }
}
