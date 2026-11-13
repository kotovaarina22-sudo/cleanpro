// MainWindow.xaml.cs — главное окно: шапка с пользователем и кнопками
// (Профиль / Управление / Выход) и Frame, в котором открываются все страницы.
using System.Windows;
using CleanPro.AppData;
using CleanPro.Pages;

namespace CleanPro
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            AppConnect.MainWindow = this;
            AppFrame.mainFrame = MainFrame;
            UpdateHeader();
            // Стартовое окно — вход в систему.
            MainFrame.Navigate(new AuthorizationPage());
        }

        // Обновляет шапку (ФИО, роль, видимость кнопок). Вызывается после входа, выхода и смены пользователя.
        public void UpdateHeader()
        {
            bool logged = AppConnect.user != null;
            txtFio.Text = logged ? AppConnect.user.FIO : "Гость";
            txtRole.Text = logged ? AppConnect.user.RoleName : "Вход не выполнен";
            SetVisible(logged, btnProfile, btnExit);
            SetVisible(AppConnect.CanManageData, btnManage);
        }

        // Показывает или прячет кнопки шапки одной строкой.
        private static void SetVisible(bool show, params UIElement[] elements)
        {
            foreach (var element in elements)
                element.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        // Профиль доступен только вошедшему пользователю.
        private void Profile_Click(object sender, RoutedEventArgs e)
        {
            if (AppConnect.user == null)
            {
                Msg.Info("Сначала войдите в систему.");
                return;
            }
            MainFrame.Navigate(new ProfilePage());
        }

        // «Управление» (админка таблиц) — только менеджеру и администратору.
        private void Manage_Click(object sender, RoutedEventArgs e)
        {
            if (!AppConnect.CanManageData)
            {
                Msg.Info("Раздел доступен менеджеру и администратору.", "Доступ ограничен");
                return;
            }
            MainFrame.Navigate(new DataPage());
        }

        // Выход: сбрасываем пользователя и корзину, возвращаемся на страницу входа.
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            AppConnect.SetUser(null);
            Cart.Clear();
            MainFrame.Navigate(new AuthorizationPage());
        }
    }
}
