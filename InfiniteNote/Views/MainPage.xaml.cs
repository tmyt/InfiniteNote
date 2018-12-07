using Windows.UI.Xaml.Controls;
using InfiniteNote.ViewModels;

namespace InfiniteNote.Views
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private readonly MainViewModel viewModel = new MainViewModel();

        public MainPage()
        {
            this.InitializeComponent();
        }
    }
}
