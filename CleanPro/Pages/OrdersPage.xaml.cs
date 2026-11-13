// OrdersPage.xaml.cs — «Мои заказы»: список заказов текущего пользователя,
// детали выбранного заказа и его позиции (услуги).
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CleanPro.AppData;

namespace CleanPro.Pages
{
    public partial class OrdersPage : Page
    {
        public OrdersPage()
        {
            InitializeComponent();

            if (!AppFrame.RequireLogin()) return;

            Load();
        }

        // Показывает только заказы вошедшего пользователя, новые — сверху.
        private void Load()
        {
            var list = Tables.Rows<Order>()
                .Where(x => x.UserId == AppConnect.user.UserId)
                .OrderByDescending(x => x.OrderDate)
                .ToList();

            listOrders.ItemsSource = list;
            txtTitle.Text = "Мои заказы";
            txtInfo.Text = $"Заказов: {list.Count}";
        }

        // Выбор заказа в списке: слева детали, справа его позиции.
        private void Orders_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!(listOrders.SelectedItem is Order order))
            {
                txtDetails.Text = "";
                listItems.ItemsSource = null;
                return;
            }

            listItems.ItemsSource = Tables.Rows<OrderItem>()
                .Where(x => x.OrderId == order.OrderId)
                .ToList();

            txtDetails.Text =
                $"Номер: {order.OrderNumber}\n" +
                $"Статус: {order.StatusName}\n" +
                $"Клиент: {order.UserName}\n" +
                $"Исполнитель: {order.EmployeeName}\n" +
                $"Создан: {order.OrderDate:dd.MM.yyyy HH:mm}\n" +
                $"Выполнение: {order.ScheduledDate:dd.MM.yyyy}\n" +
                $"Адрес: {order.TargetAddress}\n" +
                $"Сумма: {order.TotalAmount:N2} ₽" +
                (string.IsNullOrWhiteSpace(order.Comment) ? "" : $"\nКомментарий: {order.Comment}");
        }

        // Возврат в каталог.
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            AppFrame.mainFrame.Navigate(new CatalogPage());
        }
    }
}
