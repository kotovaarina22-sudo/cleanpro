// DataPage.xaml.cs — админка: слева список 14 таблиц базы, справа таблица
// с поиском и кнопками добавления/изменения/удаления записей (для менеджера и администратора).
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CleanPro.AppData;

namespace CleanPro.Pages
{
    public partial class DataPage : Page
    {
        private TableDef _table; // текущая выбранная таблица

        public DataPage(string tableKey = null)
        {
            InitializeComponent();

            if (!AppConnect.CanManageData)
            {
                Msg.Info("Раздел доступен менеджеру и администратору.", "Доступ ограничен");
                AppFrame.mainFrame.Navigate(new CatalogPage());
                return;
            }

            navList.ItemsSource = Tables.All;
            navList.SelectedItem = tableKey != null
                ? Tables.All.FirstOrDefault(x => x.Key == tableKey)
                : Tables.All.FirstOrDefault();
            if (navList.SelectedItem == null) navList.SelectedIndex = 0;
        }

        // Права на изменение: администратор всё, менеджер — всё, кроме пользователей.
        private bool CanModify => AppConnect.IsAdmin ||
                                  (_table != null && _table.Key != "users");
        // Удаление записей — только администратор.
        private bool CanDelete => AppConnect.IsAdmin;

        // Смена таблицы в списке слева — перестраиваем и заполняем грид.
        private void Nav_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!(navList.SelectedItem is TableDef table)) return;
            _table = table;
            LoadGrid();
        }

        // Создаёт колонки грида по описанию полей таблицы (TableDef) и загружает строки.
        private void LoadGrid()
        {
            grid.Columns.Clear();

            foreach (var field in _table.Fields)
            {
                string path = field.Kind == FieldKind.FK
                    ? (field.DisplayProp ?? field.Name)
                    : field.Name;

                var binding = new Binding(path);
                if (field.Kind == FieldKind.Decimal) binding.StringFormat = "{0:N2}";
                if (field.Kind == FieldKind.Date) binding.StringFormat = "{0:dd.MM.yyyy}";
                if (field.Kind == FieldKind.DateTime) binding.StringFormat = "{0:dd.MM.yy HH:mm}";
                if (field.Kind == FieldKind.Bool)
                    binding.Converter = (BoolTextConverter)Application.Current.Resources["BoolText"];

                var column = new DataGridTextColumn
                {
                    Header = field.Title,
                    Binding = binding
                };
                if (field.Kind == FieldKind.Text && (field.MaxLength ?? 0) >= 500)
                    column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);

                grid.Columns.Add(column);
            }

            txtTitle.Text = _table.Title;
            UpdateButtons();
            FillItems();
        }

        // Включает/выключает кнопки и пишет подсказку по правам для текущей таблицы.
        private void UpdateButtons()
        {
            btnAdd.IsEnabled = CanModify;
            btnEdit.IsEnabled = CanModify;
            btnDelete.IsEnabled = CanDelete;

            if (AppConnect.IsAdmin)
                txtHint.Text = "Добавление, изменение и удаление доступны";
            else if (CanModify)
                txtHint.Text = "Добавление и изменение доступны, удаление — только администратору";
            else
                txtHint.Text = "Только просмотр";
        }

        // Заполняет грид строками таблицы с учётом строки поиска.
        private void FillItems()
        {
            if (_table == null) return;

            var rows = _table.Load().Cast<object>().ToList();

            string query = txtSearch.Text.Trim().ToLower();
            if (query.Length > 0 && _table.RowText != null)
                rows = rows.Where(x => (_table.RowText(x) ?? "").ToLower().Contains(query)).ToList();

            grid.ItemsSource = rows;
        }

        private void Search_Changed(object sender, TextChangedEventArgs e)
        {
            FillItems();
        }

        // «Добавить» — открывает пустую форму новой записи.
        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (_table == null) return;
            if (!CanModify)
            {
                Msg.Warn("Добавление доступно только администратору.", "Доступ ограничен");
                return;
            }

            AppFrame.mainFrame.Navigate(new EntityEditPage(_table, null));
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            OpenSelected();
        }

        // Двойной клик по строке — то же самое, что кнопка «Изменить».
        private void Grid_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenSelected();
        }

        // Открывает выбранную строку в форме редактирования (с проверкой прав).
        private void OpenSelected()
        {
            if (_table == null || grid.SelectedItem == null)
            {
                Msg.Info("Выберите запись в таблице.");
                return;
            }
            if (!CanModify)
            {
                Msg.Warn("Изменение доступно только администратору и, кроме пользователей, менеджеру.",
                    "Доступ ограничен");
                return;
            }

            AppFrame.mainFrame.Navigate(new EntityEditPage(_table, grid.SelectedItem));
        }

        // Удаление выбранной строки с подтверждением и проверкой связей (CheckDelete).
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_table == null || grid.SelectedItem == null)
            {
                Msg.Info("Выберите запись в таблице.");
                return;
            }
            if (!CanDelete)
            {
                Msg.Warn("Удаление доступно только администратору.", "Доступ ограничен");
                return;
            }

            var row = grid.SelectedItem;
            string text = _table.RowText != null ? _table.RowText(row) : "";
            if (!Msg.Confirm($"Удалить запись «{text}»?")) return;

            try
            {
                string error = _table.CheckDelete != null ? _table.CheckDelete(row) : null;
                if (error != null)
                {
                    Msg.Warn(error, "Удаление запрещено");
                    return;
                }

                _table.Delete(row);
                FillItems();
            }
            catch (Exception ex)
            {
                Msg.Error("Не удалось удалить запись.\n" + DbError.Text(ex));
            }
        }

        private void Catalog_Click(object sender, RoutedEventArgs e)
        {
            AppFrame.mainFrame.Navigate(new CatalogPage());
        }
    }
}
