// CatalogPage.xaml.cs — каталог услуг: список карточек с поиском, фильтром
// по категории, сортировкой и добавлением выбранной услуги в корзину.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CleanPro.AppData;

namespace CleanPro.Pages
{
    public partial class CatalogPage : Page
    {
        private List<Service> _all = new List<Service>();

        public CatalogPage()
        {
            InitializeComponent();

            // Каталог смотрят только вошедшие пользователи.
            if (!AppFrame.RequireLogin()) return;

            Load();
        }

        // Загружает услуги и варианты фильтров, затем применяет текущие условия.
        private void Load()
        {
            _all = Tables.Rows<Service>().ToList();

            cmbCategory.Items.Clear();
            cmbCategory.Items.Add("Все категории");
            foreach (var category in Tables.Rows<Category>())
                cmbCategory.Items.Add(category.CategoryName);
            cmbCategory.SelectedIndex = 0;

            cmbSort.Items.Clear();
            cmbSort.Items.Add("Без сортировки");
            cmbSort.Items.Add("Цена ↑");
            cmbSort.Items.Add("Цена ↓");
            cmbSort.Items.Add("Название А-Я");
            cmbSort.SelectedIndex = 0;

            Apply();
        }

        // Фильтрует и сортирует услуги по полям поиска, категории, доступности и сортировке.
        private void Apply()
        {
            var items = _all.AsEnumerable();

            string query = txtSearch.Text.Trim().ToLower();
            if (query.Length > 0)
                items = items.Where(x => (x.ServiceName ?? "").ToLower().Contains(query));

            if (cmbCategory.SelectedIndex > 0)
            {
                string category = cmbCategory.SelectedItem.ToString();
                items = items.Where(x => x.CategoryName == category);
            }

            if (chkAvailable.IsChecked == true)
                items = items.Where(x => x.IsAvailable == true);

            if (cmbSort.SelectedIndex == 1) items = items.OrderBy(x => x.FinalPrice);
            if (cmbSort.SelectedIndex == 2) items = items.OrderByDescending(x => x.FinalPrice);
            if (cmbSort.SelectedIndex == 3) items = items.OrderBy(x => x.ServiceName);

            var list = items.ToList();
            listServices.ItemsSource = list;
            txtInfo.Text = list.Count == 0
                ? "Ничего не найдено — измените запрос или фильтры."
                : $"Услуг в каталоге: {list.Count}";
        }

        // Любое изменение фильтра/поиска — перерисовываем список.
        private void Filters_Changed(object sender, EventArgs e)
        {
            Apply();
        }

        // Кнопка «В корзину»: добавляет выбранную доступную услугу в корзину.
        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (!(listServices.SelectedItem is Service service))
            {
                Msg.Info("Выберите услугу в каталоге.");
                return;
            }

            if (service.IsAvailable != true)
            {
                Msg.Warn("Эта услуга сейчас недоступна.");
                return;
            }

            Cart.Add(service);
            Msg.Info($"«{service.ServiceName}» добавлена в корзину.", "Корзина");
        }

        private void Cart_Click(object sender, RoutedEventArgs e)
        {
            AppFrame.mainFrame.Navigate(new CartPage());
        }

        private void Orders_Click(object sender, RoutedEventArgs e)
        {
            AppFrame.mainFrame.Navigate(new OrdersPage());
        }
    }
}
