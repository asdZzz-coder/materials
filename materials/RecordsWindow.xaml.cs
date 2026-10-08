using System.Windows;
using System.Windows.Controls;
using materials.Models;
using materials.Services;

namespace materials
{
    public partial class RecordsWindow : Window
    {
        /// <summary>「動作」下拉選單的選項；Kind 為 null 表示全部。</summary>
        private record KindOption(RecordKind? Kind, string Display)
        {
            public override string ToString() => Display;
        }

        private readonly IReadOnlyList<StockRecord> _records;
        private readonly IReadOnlyList<Loan> _loans;
        private readonly Func<Loan, bool> _returnLoan;
        private MaterialItem? _item; // 只看這項物料；null 表示全部
        private bool _loansTab;

        private RecordsWindow(IReadOnlyList<StockRecord> records, IReadOnlyList<Loan> loans, MaterialItem? item, bool loansTab,
            Func<Loan, bool> returnLoan)
        {
            InitializeComponent();
            _records = records;
            _loans = loans;
            _item = item;
            _returnLoan = returnLoan;
            KindFilter.ItemsSource = new[] { new KindOption(null, "全部動作") }
                .Concat(Enum.GetValues<RecordKind>().Select(k => new KindOption(k, StockService.KindName(k)))).ToList();
            KindFilter.SelectedIndex = 0;
            UpdateItemFilter();
            ShowTab(loansTab);
        }

        /// <param name="records">出入紀錄（與主視窗共用同一份清單，歸還後重新整理就看得到新紀錄）。</param>
        /// <param name="item">只看這項物料；null 表示全部。</param>
        /// <param name="returnLoan">處理歸還（開歸還對話框並存檔）；有歸還時回傳 true。</param>
        public static void Open(Window owner, IReadOnlyList<StockRecord> records, IReadOnlyList<Loan> loans, MaterialItem? item,
            bool loansTab, Func<Loan, bool> returnLoan)
        {
            new RecordsWindow(records, loans, item, loansTab, returnLoan) { Owner = owner }.ShowDialog();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            WindowTheme.ApplyTitleBar(this);
        }

        private void ShowTab(bool loans)
        {
            _loansTab = loans;
            LoansPage.Visibility = loans ? Visibility.Visible : Visibility.Collapsed;
            RecordsPage.Visibility = loans ? Visibility.Collapsed : Visibility.Visible;
            KindFilter.Visibility = loans ? Visibility.Collapsed : Visibility.Visible;
            foreach (var (tab, selected) in new[] { (LoansTab, loans), (RecordsTab, !loans) })
            {
                if (selected)
                {
                    tab.SetResourceReference(BackgroundProperty, "AccentSoftBrush");
                    tab.SetResourceReference(ForegroundProperty, "AccentBrush");
                }
                else
                {
                    tab.Background = System.Windows.Media.Brushes.Transparent;
                    tab.SetResourceReference(ForegroundProperty, "TextBrush");
                }
            }
            Refresh();
        }

        private void LoansTab_Click(object sender, RoutedEventArgs e) => ShowTab(true);

        private void RecordsTab_Click(object sender, RoutedEventArgs e) => ShowTab(false);

        private void UpdateItemFilter()
        {
            ItemFilterButton.Visibility = _item == null ? Visibility.Collapsed : Visibility.Visible;
            if (_item != null) ItemFilterText.Text = "只看：" + _item.Name;
        }

        private void ItemFilter_Click(object sender, RoutedEventArgs e)
        {
            _item = null;
            UpdateItemFilter();
            Refresh();
        }

        private void Filter_Changed(object sender, EventArgs e)
        {
            if (IsInitialized) Refresh();
        }

        private bool ItemMatches(IItemRef r) => _item == null || r.ItemId == _item.Id;

        private bool TextMatches(string keyword, params string[] fields) =>
            keyword.Length == 0 || fields.Any(f => f.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        private void Refresh()
        {
            if (LoanList == null || RecordList == null) return;
            var keyword = SearchBox.Text.Trim();

            var open = _loans.Where(l => l.IsOpen && ItemMatches(l)).ToList();
            var loans = open.Where(l => TextMatches(keyword, l.ItemName, l.ItemSpec, l.Borrower, l.Purpose))
                            .OrderBy(l => l.LentAt).ToList();
            LoanList.ItemsSource = loans;
            LoansTabText.Text = open.Count > 0 ? $"借出中 {open.Count}" : "借出中";
            LoansEmpty.Visibility = loans.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            LoansEmpty.Text = open.Count == 0 ? "目前沒有借出未還的物料" : "沒有符合的借出";

            var kind = (KindFilter.SelectedItem as KindOption)?.Kind;
            var all = _records.Where(ItemMatches).ToList();
            var records = all.Where(r => (kind == null || r.Kind == kind) &&
                                         TextMatches(keyword, r.ItemName, r.ItemSpec, r.Borrower, r.Purpose))
                             .Reverse().OrderByDescending(r => r.Time).ToList(); // 最新的在上面；同一時間的後做的在上面
            RecordList.ItemsSource = records;
            RecordsEmpty.Visibility = records.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            RecordsEmpty.Text = all.Count == 0 ? "還沒有出入紀錄\n在主畫面選取物料後，按「存入」「拿出」「借出」「歸還」就會記在這裡" : "沒有符合的紀錄";

            SummaryText.Text = _loansTab
                ? $"共 {loans.Count} 筆借出未還"
                : records.Count == all.Count ? $"共 {all.Count} 筆紀錄" : $"符合 {records.Count} 筆（共 {all.Count} 筆紀錄）";
        }

        private void ReturnLoan_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: Loan loan } && _returnLoan(loan))
                Refresh();
        }
    }
}
