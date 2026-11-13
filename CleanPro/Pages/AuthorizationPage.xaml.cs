// AuthorizationPage.xaml.cs — страница входа: проверяет логин/пароль
// и при успешном входе открывает каталог услуг.
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CleanPro.AppData;

namespace CleanPro.Pages
{
    public partial class AuthorizationPage : Page
    {
        public AuthorizationPage()
        {
            InitializeComponent();
        }

        // Вход: ищем пользователя по логину, Email или телефону + паролю, затем открываем каталог.
        private void Login_Click(object sender, RoutedEventArgs e)
        {
            Ui.HideError(txtError);

            string login = txtLogin.Text.Trim();
            string password = txtPassword.Password;

            if (login.Length == 0 || password.Length == 0)
            {
                Ui.ShowError(txtError, "Заполните логин и пароль.");
                return;
            }

            try
            {
                var user = AppConnect.model0db.Users.FirstOrDefault(x =>
                    (x.Login == login || x.Email == login || x.Phone == login) &&
                    x.Password == password);

                if (user == null)
                {
                    Ui.ShowError(txtError, "Пользователь не найден. Проверьте логин и пароль.");
                    return;
                }
                if (user.UserStatusId != 1)
                {
                    Ui.ShowError(txtError, "Учётная запись заблокирована.");
                    return;
                }

                AppConnect.SetUser(user);
                Cart.Clear();
                AppFrame.mainFrame.Navigate(new CatalogPage());
            }
            catch (Exception ex)
            {
                Msg.Error("Не удалось войти в систему.\n" + DbError.Text(ex));
            }
        }

        // Ссылка на страницу регистрации.
        private void Register_Click(object sender, RoutedEventArgs e)
        {
            AppFrame.mainFrame.Navigate(new RegistrationPage());
        }
    }
}
