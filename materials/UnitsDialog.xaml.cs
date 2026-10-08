using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using materials.Services;

namespace materials
{
    public partial class UnitsDialog : Window
    {
        // 在副本上編輯，按「確定」才交回主視窗存檔；按「取消」不會改到原本的清單
        private readonly ObservableCollection<string> _units;

        private UnitsDialog(IEnumerable<string> units)
        {
            InitializeComponent();
            _units = new ObservableCollection<string>(units);
            _units.CollectionChanged += (_, _) => UpdateCount();
            UnitList.ItemsSource = _units;
            UpdateCount();
            Loaded += (_, _) => NewUnitBox.Focus();
        }

        /// <summary>顯示對話框；按「確定」時回傳新的單位清單，取消則回傳 null。</summary>
        public static List<string>? Edit(Window owner, IEnumerable<string> units)
        {
            var dlg = new UnitsDialog(units) { Owner = owner };
            return dlg.ShowDialog() == true ? UnitService.Clean(dlg._units) : null;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            WindowTheme.ApplyTitleBar(this, "CardBrush");
        }

        private void UpdateCount()
        {
            CountText.Text = $"{_units.Count} 個";
            EmptyText.Visibility = _units.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
            NewUnitBox.Focus();
            NewUnitBox.SelectAll();
        }

        private void AddUnit()
        {
            var name = CategoryService.Normalize(NewUnitBox.Text);
            switch (UnitService.Validate(name, _units))
            {
                case UnitNameError.Empty: ShowError("請輸入單位名稱。"); return;
                case UnitNameError.TooLong: ShowError($"單位名稱最多 {UnitService.MaxNameLength} 個字。"); return;
                case UnitNameError.Duplicate: ShowError($"清單裡已經有「{name}」。"); return;
            }
            _units.Add(name);
            UnitList.SelectedItem = name;
            UnitList.ScrollIntoView(name);
            NewUnitBox.Clear();
            NewUnitBox.Focus();
        }

        private void Add_Click(object sender, RoutedEventArgs e) => AddUnit();

        // 在輸入框按 Enter 是「新增」，不是關閉對話框
        private void NewUnitBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            AddUnit();
        }

        private void NewUnitBox_TextChanged(object sender, TextChangedEventArgs e) => ErrorText.Visibility = Visibility.Collapsed;

        private static string? UnitOf(object sender) => (sender as FrameworkElement)?.DataContext as string;

        private void Move(object sender, int offset)
        {
            if (UnitOf(sender) is not { } unit) return;
            if (UnitService.Move(_units, _units.IndexOf(unit), offset))
                UnitList.SelectedItem = unit;
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e) => Move(sender, -1);

        private void MoveDown_Click(object sender, RoutedEventArgs e) => Move(sender, 1);

        // 刪除單位只影響下拉清單，已經填了這個單位的物料不會被改動
        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (UnitOf(sender) is { } unit) _units.Remove(unit);
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            var ok = MessageBox.Show(this, "要把單位清單換回內建的預設單位嗎？\n（自己新增的單位會被移除，按「確定」後才會儲存）",
                "恢復預設", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
            if (ok != MessageBoxResult.Yes) return;
            _units.Clear();
            foreach (var u in UnitService.Defaults) _units.Add(u);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            // 輸入框裡還有沒按「新增」的文字：一併加進去，免得以為加了其實沒有
            if (CategoryService.Normalize(NewUnitBox.Text).Length > 0)
            {
                int before = _units.Count;
                AddUnit();
                if (_units.Count == before) return; // 名稱有問題，留在對話框讓使用者修正
            }
            DialogResult = true;
        }
    }
}
