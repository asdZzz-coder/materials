using System.Windows;
using System.Windows.Controls;
using materials.Services;

namespace materials
{
    public partial class InputDialog : Window
    {
        private readonly Func<string, string?> _validate;

        public string Value => InputBox.Text.Trim();

        /// <param name="validate">回傳錯誤訊息（顯示在輸入框下方、不關閉視窗），或 null 表示可以接受。</param>
        private InputDialog(string title, string prompt, string initial, Func<string, string?> validate)
        {
            InitializeComponent();
            Title = title;
            PromptText.Text = prompt;
            InputBox.Text = initial;
            _validate = validate;
            Loaded += (_, _) => { InputBox.Focus(); InputBox.SelectAll(); };
        }

        /// <summary>顯示對話框；按「確定」且通過檢查時回傳輸入的文字，取消則回傳 null。</summary>
        public static string? Ask(Window owner, string title, string prompt, string initial, Func<string, string?> validate)
        {
            var dlg = new InputDialog(title, prompt, initial, validate) { Owner = owner };
            return dlg.ShowDialog() == true ? dlg.Value : null;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            WindowTheme.ApplyTitleBar(this, "CardBrush");
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var error = _validate(Value);
            if (error != null)
            {
                ErrorText.Text = error;
                ErrorText.Visibility = Visibility.Visible;
                InputBox.Focus();
                return;
            }
            DialogResult = true;
        }

        private void InputBox_TextChanged(object sender, TextChangedEventArgs e) => ErrorText.Visibility = Visibility.Collapsed;
    }
}
