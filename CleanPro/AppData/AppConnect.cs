// AppConnect.cs — «состояние» приложения и общие классы (всё в одном файле, как в референсе):
// подключение к базе EF (CleanProEntities3), текущий пользователь и права, навигация,
// partial-классы сущностей, корзина, хелперы и описание таблиц для админки.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace CleanPro.AppData
{
    public static class AppConnect
    {
        // Единственное подключение к базе данных на всё приложение.
        public static CleanProEntities3 model0db = new CleanProEntities3();
        public static User user;
        public static MainWindow MainWindow;

        static AppConnect()
        {
            // работаем с обычными классами сущностей (без прокси-классов EF)
            model0db.Configuration.ProxyCreationEnabled = false;
            model0db.Configuration.LazyLoadingEnabled = false;
        }

        // Уровень прав: 0 — гость, 1 — пользователь, 2 — менеджер, 3 — администратор.
        public static int Rank => user == null ? 0 : Roles.Rank(user.RoleName);

        // Права ролей по уровню прав (используются всеми страницами и админкой).
        public static bool IsUser => Rank >= 1;
        public static bool CanManageData => Rank >= 2;
        public static bool IsAdmin => Rank >= 3;

        // Сохраняет текущего пользователя и обновляет шапку главного окна.
        public static void SetUser(User value)
        {
            user = value;
            MainWindow?.UpdateHeader();
        }

        public static void Save()
        {
            model0db.SaveChanges();
        }
    }

    // Переводит название роли в числовой уровень прав.
    public static class Roles
    {
        public static int Rank(string roleName)
        {
            if (string.Equals(roleName, "Администратор", StringComparison.OrdinalIgnoreCase)) return 3;
            if (string.Equals(roleName, "Менеджер", StringComparison.OrdinalIgnoreCase)) return 2;
            if (!string.IsNullOrWhiteSpace(roleName)) return 1;
            return 0;
        }
    }

    // Навигация: единый Frame главного окна, через который открываются все страницы.
    public static class AppFrame
    {
        public static Frame mainFrame;

        // Проверка входа перед открытием страницы: если пользователь не вошёл —
        // открываем страницу авторизации и возвращаем false (вызывающий прекращает работу).
        public static bool RequireLogin()
        {
            if (AppConnect.Rank >= 1) return true;
            mainFrame.Navigate(new CleanPro.Pages.AuthorizationPage());
            return false;
        }
    }

    // ============================================================
    //  РАСШИРЕНИЯ СУЩНОСТЕЙ МОДЕЛИ (partial-классы)
    //  Подписи и связанные значения, которые удобно биндить в списках.
    // ============================================================

    public partial class Role
    {
        public string Info => RoleName;
    }

    public partial class User
    {
        public string FIO => ((LastName ?? "") + " " + (FirstName ?? "")).Trim();
        public string RoleName => Tables.ById<Role>(RoleId)?.RoleName ?? "";
        public string StatusName => Tables.ById<UserStatuse>(UserStatusId)?.StatusName ?? "";
        public string Info => FIO + " (" + Login + ")";
    }

    public partial class Service
    {
        public string CategoryName => Tables.ById<Category>(CategoryId)?.CategoryName ?? "";
        public string UnitName => Tables.ById<ServiceUnit>(UnitId)?.UnitName ?? "";
        public bool HasDiscount => DiscountPercent.HasValue && DiscountPercent.Value > 0;
        public decimal FinalPrice => HasDiscount && OldPrice.HasValue
            ? Math.Round(OldPrice.Value * (100 - DiscountPercent.Value) / 100, 2)
            : BasePrice;
        public bool IsExpensive => FinalPrice > 1000;
        // Признак недоступности — чтобы на карточке была видна пометка (а не только ошибка по клику).
        public bool IsUnavailable => IsAvailable != true;
        public string Info => ServiceName;
    }

    public partial class Employee
    {
        public string StatusName => Tables.ById<EmployeeStatu>(EmployeeStatusId)?.StatusName ?? "";
        public string Info => LastName + " " + FirstName;
    }

    public partial class Order
    {
        public string UserName => Tables.ById<User>(UserId)?.FIO ?? "";
        public string ManagerName => ManagerId.HasValue ? Tables.ById<User>(ManagerId.Value)?.FIO ?? "" : "";
        public string EmployeeName => EmployeeId.HasValue ? Tables.ById<Employee>(EmployeeId.Value)?.Info ?? "" : "";
        public string StatusName => Tables.ById<OrderStatu>(OrderStatusId)?.StatusName ?? "";
        public string Info => "№ " + OrderNumber + " от " + OrderDate.ToString("dd.MM.yyyy");
    }

    public partial class OrderItem
    {
        public string Info => OrderNumber + " — " + ServiceName;
        public string OrderNumber => Tables.ById<Order>(OrderId)?.OrderNumber ?? "";
        public string ServiceName => Tables.ById<Service>(ServiceId)?.ServiceName ?? "";
    }

    public partial class Payment
    {
        public string Info => "№ " + OrderNumber + " — " + Amount + " ₽";
        public string OrderNumber => Tables.ById<Order>(OrderId)?.OrderNumber ?? "";
        public string MethodName => Tables.ById<PaymentMethod>(PaymentMethodId)?.MethodName ?? "";
    }

    public partial class Review
    {
        public string Info => Rating + " ★ — " + UserName;
        public string OrderNumber => Tables.ById<Order>(OrderId)?.OrderNumber ?? "";
        public string UserName => Tables.ById<User>(UserId)?.FIO ?? "";
        public string EmployeeInfo => Tables.ById<Employee>(EmployeeId)?.Info ?? "";
    }

    // ============================================================
    //  КОРЗИНА
    // ============================================================

    public class CartItem
    {
        public int ServiceId { get; set; }
        public string Name { get; set; }
        public string UnitName { get; set; }
        public decimal Quantity { get; set; } = 1;
        public decimal Price { get; set; }
        public decimal Sum => Quantity * Price;
    }

    public static class Cart
    {
        public static List<CartItem> Items = new List<CartItem>();

        // Общая стоимость всех позиций корзины.
        public static decimal TotalSum()
        {
            return Items.Sum(x => x.Sum);
        }

        // Добавляет услугу: если она уже в корзине — увеличивает количество.
        public static void Add(Service service, decimal quantity = 1)
        {
            var exist = Items.FirstOrDefault(x => x.ServiceId == service.ServiceId);
            if (exist != null)
            {
                exist.Quantity += quantity;
                return;
            }
            Items.Add(new CartItem
            {
                ServiceId = service.ServiceId,
                Name = service.ServiceName,
                UnitName = service.UnitName,
                Price = service.BasePrice,
                Quantity = quantity
            });
        }

        public static void Clear()
        {
            Items.Clear();
        }
    }
    // ============================================================
    //  ХЕЛПЕРЫ ДЛЯ СТРАНИЦ (конвертеры, валидация, сообщения)
    // ============================================================
    // Превращает путь к картинке (из базы или из ресурсов) в BitmapImage для <Image Source="...">.
    public class ImagePathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string path = value as string;
            if (string.IsNullOrWhiteSpace(path))
                path = "/Images/placeholder.png";

            try
            {
                if (path.StartsWith("pack:", StringComparison.OrdinalIgnoreCase))
                    return new BitmapImage(new Uri(path));
                if (path.StartsWith("/") || path.StartsWith("Images/", StringComparison.OrdinalIgnoreCase))
                {
                    // В базе пути вида /img/имя.jpg, а картинки лежат в папке Images.
                    string pack = path.TrimStart('/');
                    if (pack.StartsWith("img/", StringComparison.OrdinalIgnoreCase))
                        pack = "Images/" + pack.Substring(4);
                    return new BitmapImage(new Uri("pack://application:,,,/" + pack));
                }
                return new BitmapImage(new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path), UriKind.Absolute));
            }
            catch
            {
                return new BitmapImage(new Uri("pack://application:,,,/Images/placeholder.png"));
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    // Логическое значение → текст «Да»/«Нет» для колонок и подписей.
    public class BoolTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? "Да" : "Нет";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }

    // Общие правила проверки текста, чисел и телефона/email.
    public static class Validators
    {
        public static bool IsEmail(string text)
        {
            return !string.IsNullOrWhiteSpace(text) &&
                   Regex.IsMatch(text.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$");
        }

        public static bool IsPhone(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string digits = Regex.Replace(text, @"\D", "");
            return digits.Length >= 10 && digits.Length <= 15;
        }

        // Разбор decimal из поля формы: убирает «₽» и пробелы, пробует две культуры.
        // Запятая без точки («1234,56») — десятичный разделитель русской записи:
        // иначе InvariantCulture читает её как разделитель разрядов и выдаёт 123456.
        public static bool ParseDecimal(string text, out decimal result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim().Replace("₽", "").Replace(" ", "");
            if (text.Contains(",") && !text.Contains("."))
                text = text.Replace(',', '.');
            if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out result)) return true;
            return decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out result);
        }

        public static bool ParseInt(string text, out int result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            return int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }

        // Показывает сообщение об ошибке валидации и сразу возвращает false (удобно в if).
        public static bool Error(string message)
        {
            Msg.Warn(message, "Проверьте введённые данные");
            return false;
        }
    }

    // Короткие обёртки над MessageBox: не повторяем заголовки и кнопки в каждом обработчике.
    public static class Msg
    {
        // Простое информационное окно (OK).
        public static void Info(string text, string title = "Внимание")
        {
            MessageBox.Show(text, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Окно с предупреждением (OK).
        public static void Warn(string text, string title = "Внимание")
        {
            MessageBox.Show(text, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // Окно об ошибке (OK).
        public static void Error(string text, string title = "Ошибка")
        {
            MessageBox.Show(text, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        // Подтверждение «Да/Нет»: true, если пользователь нажал «Да».
        public static bool Confirm(string text, string title = "Подтверждение")
        {
            return MessageBox.Show(text, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }
    }

    // Работа с текстом ошибки на формах (txtError на страницах входа, регистрации, корзина, профиль).
    public static class Ui
    {
        public static void ShowError(TextBlock box, string message)
        {
            box.Text = message;
            box.Visibility = Visibility.Visible;
        }

        public static void HideError(TextBlock box)
        {
            box.Visibility = Visibility.Collapsed;
        }
    }

    // Общие проверки учётной записи: одинаковые правила при регистрации,
    // в личном кабинете и в админке при редактировании пользователя.
    public static class UserRules
    {
        // Проверка полей формы. Возвращает текст ошибки или null, если всё верно.
        public static string CheckFields(string lastName, string firstName, string login,
            string email, string phone)
        {
            if (lastName.Length == 0 || firstName.Length == 0 || login.Length == 0 ||
                email.Length == 0 || phone.Length == 0)
                return "Заполните обязательные поля.";
            if (login.Length < 3)
                return "Логин должен содержать минимум 3 символа.";
            if (!Validators.IsEmail(email))
                return "Некорректный Email. Пример: user@mail.ru.";
            if (!Validators.IsPhone(phone))
                return "Некорректный телефон. Используйте 10–15 цифр, пример: +79991234567.";
            return null;
        }

        // Проверка занятости логина, Email и телефона (excludeId — текущий пользователь, 0 при регистрации).
        // Сравнение регистронезависимое: база сравнивает по CI-коллации, и в C# должно быть так же,
        // иначе «Admin» при существующем «admin» обходит проверку и падает на индексе базы.
        // Возвращает текст ошибки или null.
        public static string CheckUnique(int excludeId, string login, string email, string phone)
        {
            var users = AppConnect.model0db.Users.ToList();
            if (users.Any(x => x.UserId != excludeId &&
                string.Equals(x.Login, login, StringComparison.OrdinalIgnoreCase)))
                return "Такой логин уже занят.";
            if (users.Any(x => x.UserId != excludeId &&
                string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase)))
                return "Такой Email уже используется.";
            if (users.Any(x => x.UserId != excludeId && x.Phone == phone))
                return "Такой телефон уже используется.";
            return null;
        }
    }

    // Дружелюбные сообщения об ошибках базы данных (вместо текста исключения EF/SQL).
    public static class DbError
    {
        public static string Text(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (current is System.Data.Entity.Validation.DbEntityValidationException validation)
                    return FromValidation(validation);
                if (current is System.Data.SqlClient.SqlException sql)
                    return FromSql(sql);
            }
            return ex.Message;
        }

        // Ошибка проверки свойств EF: берём первое сообщение из первой невалидной сущности.
        private static string FromValidation(System.Data.Entity.Validation.DbEntityValidationException validation)
        {
            foreach (var entityError in validation.EntityValidationErrors)
                foreach (var error in entityError.ValidationErrors)
                    return "Проверьте данные записи. " + error.ErrorMessage;
            return validation.Message;
        }

        // Типовые ошибки SQL Server: дубликат, ссылка на связанные записи, пустое поле.
        private static string FromSql(System.Data.SqlClient.SqlException sql)
        {
            if (sql.Number == 2601 || sql.Number == 2627)
                return "Запись с такими значениями уже есть в базе данных.";
            if (sql.Number == 547)
                return "Операция нарушает связи с другими таблицами, сначала уберите зависимые записи.";
            if (sql.Number == 515)
                return "Не заполнено обязательное поле.";
            return sql.Message;
        }
    }

    // Копирование картинки из выбранного файла в папку приложения (ProductImages).
    public static class ImageFiles
    {
        public static string Folder => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ProductImages");

        public static string CopyToProject(string sourcePath)
        {
            Directory.CreateDirectory(Folder);
            string fileName = Guid.NewGuid().ToString("N") + Path.GetExtension(sourcePath);
            File.Copy(sourcePath, Path.Combine(Folder, fileName), true);
            return "ProductImages/" + fileName;
        }

        // Диалог выбора файла-картинки; null, если пользователь отменил.
        public static string SelectFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Выберите изображение",
                Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Все файлы|*.*"
            };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }
    // ============================================================
    //  ТАБЛИЦЫ ДЛЯ АДМИНКИ (описание полей и работа с БД)
    // ============================================================
    // Вид поля формы: от него зависит, какой контрол создать и как проверять значение.
    public enum FieldKind
    {
        Text,
        Email,
        Phone,
        Password,
        Int,
        Decimal,
        Bool,
        Date,
        DateTime,
        FK,
        Image
    }

    public class FieldDef
    {
        public string Name { get; set; }
        public string Title { get; set; }
        public FieldKind Kind { get; set; }
        public bool Required { get; set; }
        public bool Nullable { get; set; }
        public string ListKey { get; set; }
        public string OptionProp { get; set; }
        public string DisplayProp { get; set; }
        public decimal? Min { get; set; }
        public decimal? Max { get; set; }
        public int? MaxLength { get; set; }
        public bool ReadOnly { get; set; }
    }

    public class TableDef
    {
        public string Key { get; set; }
        public string Title { get; set; }
        public string SingleTitle { get; set; }
        public Type EntityType { get; set; }
        public string KeyProp { get; set; }
        public List<FieldDef> Fields { get; set; } = new List<FieldDef>();
        public Func<object, string> RowText { get; set; }
        public Func<object, string> CheckDelete { get; set; }

        public FieldDef Field(string name) => Fields.FirstOrDefault(f => f.Name == name);

        public IList Load()
        {
            // Set(Type) возвращает не-generic DbSet — перебираем как IEnumerable
            var rows = ((IEnumerable)AppConnect.model0db.Set(EntityType)).Cast<object>().ToList();

            // сортировка по первичному ключу — стабильный порядок строк в таблице
            if (!string.IsNullOrEmpty(KeyProp))
                rows = rows.OrderBy(Tables.GetId).ToList();

            return rows;
        }

        public void Insert(object row)
        {
            AppConnect.model0db.Set(EntityType).Add(row);
            try
            {
                AppConnect.model0db.SaveChanges();
            }
            catch
            {
                AppConnect.model0db.Entry(row).State = System.Data.Entity.EntityState.Detached;
                throw;
            }
        }

        public void Delete(object row)
        {
            AppConnect.model0db.Set(EntityType).Remove(row);
            try
            {
                AppConnect.model0db.SaveChanges();
            }
            catch
            {
                AppConnect.model0db.Entry(row).State = System.Data.Entity.EntityState.Unchanged;
                throw;
            }
        }
    }

    public static class Tables
    {
        public static readonly List<TableDef> All = new List<TableDef>();

        static Tables()
        {
            TablesDef.Register();
        }

        public static TableDef Get(string key) => All.FirstOrDefault(t => t.Key == key);

        // Ищет описание таблицы по типу сущности (или по прокси-классу EF).
        public static TableDef Get(Type type)
        {
            if (type == null) return null;
            var table = All.FirstOrDefault(t => t.EntityType == type);
            if (table != null) return table;
            // на случай, если тип пришёл как прокси-класс EF
            return All.FirstOrDefault(t => t.EntityType.IsAssignableFrom(type));
        }

        public static T ById<T>(int id) where T : class
        {
            var table = Get(typeof(T));
            if (table == null) return null;
            return AppConnect.model0db.Set(typeof(T)).Find(id) as T;
        }

        public static int GetId(object row)
        {
            if (row == null) return 0;
            var table = Get(row.GetType());
            if (table == null) return 0;
            object value = GetProp(row, table.KeyProp);
            if (value == null) return 0;
            return Convert.ToInt32(value);
        }

        // Читает свойство сущности по имени (для привязок и заполнения формы).
        public static object GetProp(object row, string name)
        {
            var prop = row.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            return prop?.GetValue(row);
        }

        // Записывает значение из поля формы в свойство сущности (с приведением типа).
        public static void SetProp(object row, string name, object value)
        {
            var prop = row.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null || !prop.CanWrite) return;

            var type = prop.PropertyType;
            bool isNullable = Nullable.GetUnderlyingType(type) != null;
            var baseType = Nullable.GetUnderlyingType(type) ?? type;
            prop.SetValue(row, ConvertValue(value, baseType, isNullable));
        }

        // Приводит значение из формы к типу свойства: null/пустая строка, готовое значение или строка.
        private static object ConvertValue(object value, Type baseType, bool isNullable)
        {
            bool empty = value == null ||
                         (value is string text && string.IsNullOrWhiteSpace(text) && baseType != typeof(string));

            if (empty)
            {
                if (isNullable) return null;
                if (baseType == typeof(string)) return string.Empty;
                return Activator.CreateInstance(baseType);
            }
            if (baseType.IsInstanceOfType(value)) return value;
            if (baseType == typeof(string)) return Convert.ToString(value, CultureInfo.InvariantCulture);

            return ConvertText(Convert.ToString(value, CultureInfo.InvariantCulture).Trim(), baseType);
        }

        // Разбор строки в конкретный тип (число, дата, «Да/Нет»). Бросает FormatException при ошибке.
        private static object ConvertText(string input, Type baseType)
        {
            if (baseType == typeof(decimal))
            {
                if (Validators.ParseDecimal(input, out var number)) return number;
                throw new FormatException("Некорректное число: " + input);
            }
            if (baseType == typeof(int))
            {
                if (Validators.ParseInt(input, out var number)) return number;
                throw new FormatException("Некорректное целое число: " + input);
            }
            if (baseType == typeof(bool))
                return input == "Да" || input == "1" || input.Equals("True", StringComparison.OrdinalIgnoreCase);

            if (baseType == typeof(DateTime))
            {
                if (DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
                    DateTime.TryParse(input, CultureInfo.CurrentCulture, DateTimeStyles.None, out date))
                    return date;
                throw new FormatException("Некорректная дата: " + input);
            }
            return Convert.ChangeType(input, baseType, CultureInfo.InvariantCulture);
        }

        public static List<T> Rows<T>() where T : class
        {
            return AppConnect.model0db.Set<T>().ToList();
        }

        public static object Create(TableDef table)
        {
            var row = Activator.CreateInstance(table.EntityType);

            // даты по умолчанию — текущее время
            foreach (var field in table.Fields)
            {
                if (field.Kind != FieldKind.Date && field.Kind != FieldKind.DateTime) continue;
                var prop = row.GetType().GetProperty(field.Name, BindingFlags.Public | BindingFlags.Instance);
                if (prop == null || Nullable.GetUnderlyingType(prop.PropertyType) != null) continue;
                prop.SetValue(row, DateTime.Now);
            }

            return row;
        }

        public static void Insert(TableDef table, object row)
        {
            table.Insert(row);
        }

        public static void Save()
        {
            AppConnect.model0db.SaveChanges();
        }

        public static IList Options(string listKey)
        {
            var table = Get(listKey);
            if (table == null) return new List<object>();
            return table.Load();
        }

        public static string PkOf(string listKey)
        {
            var table = Get(listKey);
            return table == null ? "" : table.KeyProp;
        }

        public static string DisplayValue(object row, FieldDef field)
        {
            if (row == null || field == null) return string.Empty;
            if (field.Kind == FieldKind.FK)
            {
                string prop = !string.IsNullOrEmpty(field.DisplayProp) ? field.DisplayProp : field.Name;
                return Convert.ToString(GetProp(row, prop)) ?? string.Empty;
            }
            if (field.Kind == FieldKind.Bool)
            {
                object value = GetProp(row, field.Name);
                return value is bool b && b ? "Да" : "Нет";
            }
            return Convert.ToString(GetProp(row, field.Name)) ?? string.Empty;
        }

        public static string Display(object row, string propName)
        {
            return Convert.ToString(GetProp(row, propName)) ?? string.Empty;
        }
    }
    public static class TablesDef
    {
        // Короткая запись поля: склеивает параметры в объект FieldDef.
        private static FieldDef F(string name, string title, FieldKind kind,
            bool required = false, string listKey = null, string optionProp = null,
            string displayProp = null, decimal? min = null, decimal? max = null,
            int? maxLength = null, bool nullable = false, bool readOnly = false)
        {
            return new FieldDef
            {
                Name = name,
                Title = title,
                Kind = kind,
                Required = required,
                ListKey = listKey,
                OptionProp = optionProp,
                DisplayProp = displayProp,
                Min = min,
                Max = max,
                MaxLength = maxLength,
                Nullable = nullable,
                ReadOnly = readOnly
            };
        }

        // Регистрирует таблицу: ключ, заголовки, тип сущности, колонка-идентификатор,
        // подпись строки в списках, проверка удаления и описание полей.
        private static void Add(string key, string title, string singleTitle, Type type,
            string keyProp, Func<object, string> rowText, Func<object, string> checkDelete,
            params FieldDef[] fields)
        {
            Tables.All.Add(new TableDef
            {
                Key = key,
                Title = title,
                SingleTitle = singleTitle,
                EntityType = type,
                KeyProp = keyProp,
                RowText = rowText,
                CheckDelete = checkDelete,
                Fields = new List<FieldDef>(fields)
            });
        }

        // Проверка удаления: used получает Id записи и возвращает true, если на неё
        // есть ссылки в других таблицах — тогда показывается текст message.
        private static Func<object, string> Guard(string message, Func<int, bool> used)
        {
            return o => used(Tables.GetId(o)) ? message : null;
        }

        public static void Register()
        {
            Tables.All.Clear();

            Add("roles", "Роли", "роль", typeof(Role), "RoleId",
                o => ((Role)o).RoleName,
                Guard("Невозможно удалить роль, так как она присвоена пользователям.",
                    id => Tables.Rows<User>().Any(u => u.RoleId == id)),
                F("RoleId", "№", FieldKind.Int, readOnly: true),
                F("RoleName", "Название", FieldKind.Text, required: true, maxLength: 50));

            Add("user-statuses", "Статусы пользователей", "статус", typeof(UserStatuse), "UserStatusId",
                o => ((UserStatuse)o).StatusName,
                Guard("Невозможно удалить статус, так как он используется пользователями.",
                    id => Tables.Rows<User>().Any(u => u.UserStatusId == id)),
                F("UserStatusId", "№", FieldKind.Int, readOnly: true),
                F("StatusName", "Название", FieldKind.Text, required: true, maxLength: 50));

            Add("users", "Пользователи", "пользователя", typeof(User), "UserId",
                o => ((User)o).Info,
                Guard("Невозможно удалить пользователя, так как он есть в заказах и отзывах.",
                    id => Tables.Rows<Order>().Any(x => x.UserId == id || x.ManagerId == id)
                       || Tables.Rows<Review>().Any(r => r.UserId == id)),
                F("UserId", "№", FieldKind.Int, readOnly: true),
                F("Login", "Логин", FieldKind.Text, required: true, maxLength: 100),
                F("Password", "Пароль", FieldKind.Password, required: true, maxLength: 100),
                F("FirstName", "Имя", FieldKind.Text, required: true, maxLength: 100),
                F("LastName", "Фамилия", FieldKind.Text, required: true, maxLength: 100),
                F("Phone", "Телефон", FieldKind.Phone, required: true, maxLength: 20),
                F("Email", "Email", FieldKind.Email, required: true, maxLength: 100),
                F("DefaultAddress", "Адрес", FieldKind.Text, maxLength: 500),
                F("RoleId", "Роль", FieldKind.FK, required: true, listKey: "roles", optionProp: "RoleName", displayProp: "RoleName"),
                F("UserStatusId", "Статус", FieldKind.FK, required: true, listKey: "user-statuses", optionProp: "StatusName", displayProp: "StatusName"),
                F("RegistrationDate", "Дата регистрации", FieldKind.DateTime, required: true));

            Add("categories", "Категории", "категорию", typeof(Category), "CategoryId",
                o => ((Category)o).CategoryName,
                Guard("Невозможно удалить категорию, так как она используется в услугах.",
                    id => Tables.Rows<Service>().Any(s => s.CategoryId == id)),
                F("CategoryId", "№", FieldKind.Int, readOnly: true),
                F("CategoryName", "Название", FieldKind.Text, required: true, maxLength: 100));

            Add("services-unit", "Единицы измерения", "единицу измерения", typeof(ServiceUnit), "UnitId",
                o => ((ServiceUnit)o).UnitName,
                Guard("Невозможно удалить единицу измерения, так как она используется в услугах.",
                    id => Tables.Rows<Service>().Any(s => s.UnitId == id)),
                F("UnitId", "№", FieldKind.Int, readOnly: true),
                F("UnitName", "Название", FieldKind.Text, required: true, maxLength: 50));

            Add("services", "Услуги", "услугу", typeof(Service), "ServiceId",
                o => ((Service)o).Info,
                Guard("Невозможно удалить товар, так как он присутствует в одном или нескольких заказах.",
                    id => Tables.Rows<OrderItem>().Any(i => i.ServiceId == id)),
                F("ServiceId", "№", FieldKind.Int, readOnly: true),
                F("ServiceName", "Название", FieldKind.Text, required: true, maxLength: 200),
                F("CategoryId", "Категория", FieldKind.FK, required: true, listKey: "categories", optionProp: "CategoryName", displayProp: "CategoryName"),
                F("BasePrice", "Цена", FieldKind.Decimal, required: true, min: 0, max: 1000000),
                F("OldPrice", "Старая цена", FieldKind.Decimal, min: 0, max: 1000000, nullable: true),
                F("DiscountPercent", "Процент скидки", FieldKind.Decimal, min: 0, max: 100, nullable: true),
                F("UnitId", "Единица измерения", FieldKind.FK, required: true, listKey: "services-unit", optionProp: "UnitName", displayProp: "UnitName"),
                F("ImagePath", "Изображение", FieldKind.Image),
                F("IsAvailable", "Доступна", FieldKind.Bool));

            Add("employee-statuses", "Статусы сотрудников", "статус", typeof(EmployeeStatu), "EmployeeStatusId",
                o => ((EmployeeStatu)o).StatusName,
                Guard("Невозможно удалить статус, так как он используется сотрудниками.",
                    id => Tables.Rows<Employee>().Any(e => e.EmployeeStatusId == id)),
                F("EmployeeStatusId", "№", FieldKind.Int, readOnly: true),
                F("StatusName", "Название", FieldKind.Text, required: true, maxLength: 50));

            Add("employees", "Сотрудники", "сотрудника", typeof(Employee), "EmployeeId",
                o => ((Employee)o).Info,
                Guard("Невозможно удалить сотрудника, так как он есть в заказах и отзывах.",
                    id => Tables.Rows<Order>().Any(x => x.EmployeeId == id)
                       || Tables.Rows<Review>().Any(r => r.EmployeeId == id)),
                F("EmployeeId", "№", FieldKind.Int, readOnly: true),
                F("FirstName", "Имя", FieldKind.Text, required: true, maxLength: 100),
                F("LastName", "Фамилия", FieldKind.Text, required: true, maxLength: 100),
                F("Phone", "Телефон", FieldKind.Phone, required: true, maxLength: 20),
                F("Specialty", "Специализация", FieldKind.Text, maxLength: 200),
                F("EmployeeStatusId", "Статус", FieldKind.FK, required: true, listKey: "employee-statuses", optionProp: "StatusName", displayProp: "StatusName"),
                F("Rating", "Рейтинг", FieldKind.Decimal, min: 0, max: 5, nullable: true),
                F("HireDate", "Дата приёма", FieldKind.Date, required: true),
                F("MaxOrdersPerDay", "Заказов в день", FieldKind.Int, min: 0, max: 1000, nullable: true),
                F("CurrentOrdersCount", "Заказов сегодня", FieldKind.Int, min: 0, max: 1000, nullable: true));

            Add("order-statuses", "Статусы заказов", "статус", typeof(OrderStatu), "OrderStatusId",
                o => ((OrderStatu)o).StatusName,
                Guard("Невозможно удалить статус, так как он используется в заказах.",
                    id => Tables.Rows<Order>().Any(x => x.OrderStatusId == id)),
                F("OrderStatusId", "№", FieldKind.Int, readOnly: true),
                F("StatusName", "Название", FieldKind.Text, required: true, maxLength: 50),
                F("IsFinal", "Финальный", FieldKind.Bool));

            Add("orders", "Заказы", "заказ", typeof(Order), "OrderId",
                o => ((Order)o).Info,
                Guard("Невозможно удалить заказ, так как по нему есть позиции, оплаты или отзывы.",
                    id => Tables.Rows<OrderItem>().Any(i => i.OrderId == id)
                       || Tables.Rows<Payment>().Any(p => p.OrderId == id)
                       || Tables.Rows<Review>().Any(r => r.OrderId == id)),
                F("OrderId", "№", FieldKind.Int, readOnly: true),
                F("OrderNumber", "Номер заказа", FieldKind.Text, required: true, maxLength: 50),
                F("OrderStatusId", "Статус", FieldKind.FK, required: true, listKey: "order-statuses", optionProp: "StatusName", displayProp: "StatusName"),
                F("UserId", "Клиент", FieldKind.FK, required: true, listKey: "users", optionProp: "Info", displayProp: "UserName"),
                F("ManagerId", "Менеджер", FieldKind.FK, listKey: "users", optionProp: "Info", displayProp: "ManagerName", nullable: true),
                F("EmployeeId", "Исполнитель", FieldKind.FK, listKey: "employees", optionProp: "Info", displayProp: "EmployeeName", nullable: true),
                F("OrderDate", "Дата создания", FieldKind.DateTime, required: true),
                F("ScheduledDate", "Дата выполнения", FieldKind.DateTime, required: true),
                F("TargetAddress", "Адрес", FieldKind.Text, required: true, maxLength: 500),
                F("TotalAmount", "Сумма", FieldKind.Decimal, required: true, min: 0, max: 1000000),
                F("Comment", "Комментарий", FieldKind.Text, maxLength: 1000));

            Add("orders-items", "Позиции заказов", "позицию", typeof(OrderItem), "OrderItemId",
                o => ((OrderItem)o).Info,
                o => null,
                F("OrderItemId", "№", FieldKind.Int, readOnly: true),
                F("OrderId", "Заказ", FieldKind.FK, required: true, listKey: "orders", optionProp: "Info", displayProp: "OrderNumber"),
                F("ServiceId", "Услуга", FieldKind.FK, required: true, listKey: "services", optionProp: "Info", displayProp: "ServiceName"),
                F("Quantity", "Количество", FieldKind.Decimal, required: true, min: 0.01m, max: 10000),
                F("UnitPrice", "Цена за единицу", FieldKind.Decimal, required: true, min: 0, max: 1000000),
                F("FinalPrice", "Сумма", FieldKind.Decimal, required: true, min: 0, max: 1000000));

            Add("payments-methods", "Способы оплаты", "способ оплаты", typeof(PaymentMethod), "PaymentMethodId",
                o => ((PaymentMethod)o).MethodName,
                Guard("Невозможно удалить способ оплаты, так как он используется в оплатах.",
                    id => Tables.Rows<Payment>().Any(p => p.PaymentMethodId == id)),
                F("PaymentMethodId", "№", FieldKind.Int, readOnly: true),
                F("MethodName", "Название", FieldKind.Text, required: true, maxLength: 50));

            Add("payments", "Оплаты", "оплату", typeof(Payment), "PaymentId",
                o => ((Payment)o).Info,
                o => null,
                F("PaymentId", "№", FieldKind.Int, readOnly: true),
                F("OrderId", "Заказ", FieldKind.FK, required: true, listKey: "orders", optionProp: "Info", displayProp: "OrderNumber"),
                F("PaymentMethodId", "Способ оплаты", FieldKind.FK, required: true, listKey: "payments-methods", optionProp: "MethodName", displayProp: "MethodName"),
                F("Amount", "Сумма", FieldKind.Decimal, required: true, min: 0.01m, max: 1000000),
                F("PaymentDate", "Дата оплаты", FieldKind.DateTime, required: true),
                F("IsFullPayment", "Полная оплата", FieldKind.Bool));

            Add("reviews", "Отзывы", "отзыв", typeof(Review), "ReviewId",
                o => ((Review)o).Info,
                o => null,
                F("ReviewId", "№", FieldKind.Int, readOnly: true),
                F("OrderId", "Заказ", FieldKind.FK, required: true, listKey: "orders", optionProp: "Info", displayProp: "OrderNumber"),
                F("EmployeeId", "Сотрудник", FieldKind.FK, required: true, listKey: "employees", optionProp: "Info", displayProp: "EmployeeInfo"),
                F("UserId", "Клиент", FieldKind.FK, required: true, listKey: "users", optionProp: "Info", displayProp: "UserName"),
                F("Rating", "Оценка", FieldKind.Int, required: true, min: 1, max: 5),
                F("Comment", "Комментарий", FieldKind.Text, maxLength: 1000),
                F("ReviewDate", "Дата отзыва", FieldKind.DateTime, required: true));
        }
    }
}
