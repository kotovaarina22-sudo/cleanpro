// ProfilePage.xaml.cs — личный кабинет: показывает роль и статус пользователя,
// позволяет изменить ФИО, логин, контакты и адрес с проверками на уникальность.
using System;
using System.Windows;
using System.Windows.Controls;
using CleanPro.AppData;

namespace CleanPro.Pages
{
    public partial class ProfilePage : Page
    {
        private User _user;

        public ProfilePage()
        {
            InitializeComponent();

            _user = AppConnect.user;
            if (_user == null)
            {
                AppFrame.mainFrame.Navigate(new AuthorizationPage());
                return;
            }

            FillForm();
        }

        // Заполняет поля формы данными текущего пользователя.
        private void FillForm()
        {
            txtRole.Text = _user.RoleName;
            txtStatus.Text = _user.StatusName;
            txtLastName.Text = _user.LastName;
            txtFirstName.Text = _user.FirstName;
            txtLogin.Text = _user.Login;
            txtEmail.Text = _user.Email;
            txtPhone.Text = _user.Phone;
            txtAddress.Text = _user.DefaultAddress ?? "";
        }

        // Сохранение изменений: общие проверки полей и уникальности, затем запись в базу.
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Ui.HideError(txtError);

            string lastName = txtLastName.Text.Trim();
            string firstName = txtFirstName.Text.Trim();
            string login = txtLogin.Text.Trim();
            string email = txtEmail.Text.Trim();
            string phone = txtPhone.Text.Trim();

            string error = UserRules.CheckFields(lastName, firstName, login, email, phone);
            if (error == null)
                error = UserRules.CheckUnique(_user.UserId, login, email, phone);
            if (error != null)
            {
                Ui.ShowError(txtError, error);
                return;
            }

            try
            {
                _user.LastName = lastName;
                _user.FirstName = firstName;
                _user.Login = login;
                _user.Email = email;
                _user.Phone = phone;
                _user.DefaultAddress = txtAddress.Text.Trim();

                AppConnect.Save();
                AppConnect.SetUser(_user);
                Msg.Info("Изменения сохранены.", "Готово");
                AppFrame.mainFrame.Navigate(new CatalogPage());
            }
            catch (Exception ex)
            {
                Msg.Error("Не удалось сохранить изменения.\n" + DbError.Text(ex));
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            AppFrame.mainFrame.Navigate(new CatalogPage());
        }
    }
}
