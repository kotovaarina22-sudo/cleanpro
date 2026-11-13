// RegistrationPage.xaml.cs — страница регистрации: проверяет поля новой учётной
// записи, создаёт пользователя в базе и сразу выполняет вход.
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CleanPro.AppData;

namespace CleanPro.Pages
{
    public partial class RegistrationPage : Page
    {
        public RegistrationPage()
        {
            InitializeComponent();
        }

        // Регистрация: общие проверки полей → пароль → уникальность логина/email/телефона → сохранение.
        private void Register_Click(object sender, RoutedEventArgs e)
        {
            Ui.HideError(txtError);

            string lastName = txtLastName.Text.Trim();
            string firstName = txtFirstName.Text.Trim();
            string login = txtLogin.Text.Trim();
            string email = txtEmail.Text.Trim();
            string phone = txtPhone.Text.Trim();
            string password = txtPassword.Password;
            string password2 = txtPassword2.Password;

            string error = UserRules.CheckFields(lastName, firstName, login, email, phone);
            if (error != null)
            {
                Ui.ShowError(txtError, error);
                return;
            }
            if (password.Length < 6)
            {
                Ui.ShowError(txtError, "Пароль должен содержать минимум 6 символов.");
                return;
            }
            if (password != password2)
            {
                Ui.ShowError(txtError, "Пароли не совпадают.");
                return;
            }

            error = UserRules.CheckUnique(0, login, email, phone);
            if (error != null)
            {
                Ui.ShowError(txtError, error);
                return;
            }

            try
            {
                var user = new User
                {
                    Login = login,
                    Password = password,
                    FirstName = firstName,
                    LastName = lastName,
                    Phone = phone,
                    Email = email,
                    DefaultAddress = txtAddress.Text.Trim(),
                    RoleId = UserRoleId(),
                    UserStatusId = Tables.Rows<UserStatuse>().FirstOrDefault()?.UserStatusId ?? 1,
                    RegistrationDate = DateTime.Now
                };

                AppConnect.model0db.Users.Add(user);
                AppConnect.model0db.SaveChanges();

                AppConnect.SetUser(user);
                Cart.Clear();
                AppFrame.mainFrame.Navigate(new CatalogPage());
            }
            catch (Exception ex)
            {
                Msg.Error("Не удалось зарегистрироваться.\n" + DbError.Text(ex));
            }
        }

        // Id роли «Пользователь» для нового пользователя (или первая роль, если такой нет).
        private int UserRoleId()
        {
            var roles = Tables.Rows<Role>();
            var role = roles.FirstOrDefault(x => (x.RoleName ?? "").Contains("Пользователь"));
            if (role == null) role = roles.FirstOrDefault();
            return role != null ? role.RoleId : 1;
        }

        // Возврат на страницу входа.
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            AppFrame.mainFrame.Navigate(new AuthorizationPage());
        }
    }
}
