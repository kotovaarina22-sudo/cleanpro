// CartPage.xaml.cs — корзина: изменение количества, удаление позиций
// и оформление заказа (создаёт заказ и его позиции в базе).
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CleanPro.AppData;

namespace CleanPro.Pages
{
    public partial class CartPage : Page
    {
        public CartPage()
        {
            InitializeComponent();

            if (!AppFrame.RequireLogin()) return;

            listCart.ItemsSource = Cart.Items;
            txtAddress.Text = AppConnect.user.DefaultAddress ?? "";
            dtDate.SelectedDate = DateTime.Now.Date;
            UpdateTotal();
        }

        // Пересчёт итоговой суммы корзины.
        private void UpdateTotal()
        {
            txtTotal.Text = $"Итого: {Cart.TotalSum():N2} ₽";
        }

        // Потеря фокуса у поля количества: проверяем число и обновляем сумму.
        private void Qty_Changed(object sender, RoutedEventArgs e)
        {
            if (!(sender is TextBox box) || !(box.DataContext is CartItem item))
                return;

            if (!Validators.ParseDecimal(box.Text, out decimal quantity) || quantity <= 0)
            {
                Msg.Warn("Количество должно быть числом больше нуля.", "Ошибка");
                box.Text = item.Quantity.ToString();
                return;
            }

            item.Quantity = quantity;
            RefreshList();
        }

        // Убирает выбранную позицию из корзины.
        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is CartItem item))
                return;

            Cart.Items.Remove(item);
            RefreshList();
        }

        // Очистка всей корзины с подтверждением.
        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (Cart.Items.Count == 0) return;
            if (!Msg.Confirm("Очистить корзину?")) return;

            Cart.Items.Clear();
            RefreshList();
        }

        // Обновляет список позиций и итог после любого изменения корзины.
        private void RefreshList()
        {
            listCart.Items.Refresh();
            UpdateTotal();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            AppFrame.mainFrame.Navigate(new CatalogPage());
        }

        // Оформление заказа: проверяет форму, создаёт заказ с номером и позиции, очищает корзину.
        private void Checkout_Click(object sender, RoutedEventArgs e)
        {
            Ui.HideError(txtError);

            string error = CheckForm();
            if (error != null)
            {
                Ui.ShowError(txtError, error);
                return;
            }

            try
            {
                // Заказ и его позиции сохраняются в одной транзакции: если позиции записать
                // не удалось (например, услугу удалили в момент оформления), заказ не «повиснет» в базе.
                using (var transaction = AppConnect.model0db.Database.BeginTransaction())
                {
                    var order = CreateOrder();

                    foreach (var cartItem in Cart.Items)
                    {
                        AppConnect.model0db.OrderItems.Add(new OrderItem
                        {
                            OrderId = order.OrderId,
                            ServiceId = cartItem.ServiceId,
                            Quantity = cartItem.Quantity,
                            UnitPrice = cartItem.Price,
                            FinalPrice = cartItem.Sum
                        });
                    }

                    AppConnect.model0db.SaveChanges();
                    transaction.Commit();

                    Cart.Items.Clear();
                    AppFrame.mainFrame.Navigate(new OrdersPage());
                    Msg.Info($"Заказ №{order.OrderNumber} оформлен.", "Готово");
                }
            }
            catch (Exception ex)
            {
                // Откат транзакции уже произошёл; убираем «висящие» добавленные записи
                // из контекста, чтобы следующая попытка не сохранила их вместе с новыми.
                foreach (var entry in AppConnect.model0db.ChangeTracker.Entries()
                    .Where(x => x.State == System.Data.Entity.EntityState.Added)
                    .ToList())
                {
                    entry.State = System.Data.Entity.EntityState.Detached;
                }
                Msg.Error("Не удалось оформить заказ.\n" + DbError.Text(ex));
            }
        }

        // Проверка формы оформления: корзина, адрес, дата (не в прошлом). Возвращает текст ошибки или null.
        private string CheckForm()
        {
            if (Cart.Items.Count == 0)
                return "Корзина пуста. Добавьте услуги в каталоге.";
            if (txtAddress.Text.Trim().Length == 0)
                return "Укажите адрес выполнения заказа.";
            if (dtDate.SelectedDate == null)
                return "Укажите дату выполнения заказа.";
            if (dtDate.SelectedDate.Value.Date < DateTime.Today)
                return "Дата выполнения не может быть в прошлом.";
            return null;
        }

        // Сохраняет заказ в базе и присваивает ему номер (ORD-год-№);
        // номер и позиции запишутся общим SaveChanges после добавления позиций.
        private Order CreateOrder()
        {
            var order = new Order
            {
                OrderNumber = "", // номер проставим после сохранения, когда будет Id
                UserId = AppConnect.user.UserId,
                OrderStatusId = Tables.Rows<OrderStatu>().FirstOrDefault()?.OrderStatusId ?? 1,
                OrderDate = DateTime.Now,
                ScheduledDate = dtDate.SelectedDate.Value,
                TargetAddress = txtAddress.Text.Trim(),
                Comment = txtComment.Text.Trim(),
                TotalAmount = Cart.TotalSum()
            };

            AppConnect.model0db.Orders.Add(order);
            AppConnect.model0db.SaveChanges();

            order.OrderNumber = "ORD-" + order.OrderDate.Year + "-" + order.OrderId.ToString("D3");
            return order;
        }
    }
}
