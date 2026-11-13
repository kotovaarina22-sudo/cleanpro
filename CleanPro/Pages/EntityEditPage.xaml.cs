// EntityEditPage.xaml.cs — форма добавления/изменения записи любой таблицы:
// поля создаются кодом по описанию TableDef, проверяются и сохраняются в базу.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using CleanPro.AppData;

namespace CleanPro.Pages
{
    public partial class EntityEditPage : Page
    {
        private readonly TableDef _table;   // описание текущей таблицы
        private readonly object _row;       // редактируемая запись (или новая)
        private readonly bool _isNew;
        private readonly List<FieldControl> _controls = new List<FieldControl>(); // связь поля и его значения
        private StackPanel _previewHost;    // место под превью изображения

        public EntityEditPage(TableDef table, object item)
        {
            InitializeComponent();

            _table = table;
            _isNew = item == null;
            _row = item ?? Tables.Create(table);

            txtTitle.Text = (_isNew ? "Добавление записи — " : "Изменение записи — ") + table.Title;
            BuildForm();
        }

        // Собирает форму: подпись + контрол для каждого поля таблицы (пропускает служебные № при добавлении).
        private void BuildForm()
        {
            foreach (var field in _table.Fields)
            {
                if (field.ReadOnly && _isNew) continue;

                AddLabel(field.Title + (field.Required ? " *" : ""));

                if (field.ReadOnly)
                {
                    formPanel.Children.Add(new TextBlock
                    {
                        Text = Tables.Display(_row, field.Name),
                        Margin = new Thickness(0, 2, 0, 8)
                    });
                    continue;
                }

                switch (field.Kind)
                {
                    case FieldKind.FK:
                        BuildCombo(field);
                        break;
                    case FieldKind.Bool:
                        BuildCheck(field);
                        break;
                    case FieldKind.Date:
                    case FieldKind.DateTime:
                        BuildDate(field);
                        break;
                    case FieldKind.Image:
                        BuildImage(field);
                        break;
                    default:
                        BuildText(field);
                        break;
                }
            }
        }

        // Подпись поля над контролом.
        private void AddLabel(string text)
        {
            formPanel.Children.Add(new TextBlock
            {
                Text = text,
                Style = (Style)Application.Current.Resources["FieldLabel"]
            });
        }

        private const double FieldWidth = 520;

        // Добавляет контрол на форму и подписывает его для служб доступности.
        private void AddControl(FrameworkElement control, FieldDef field)
        {
            System.Windows.Automation.AutomationProperties.SetName(control, field.Title);
            System.Windows.Automation.AutomationProperties.SetAutomationId(control, field.Name);
            formPanel.Children.Add(control);
        }

        // Текстовое поле (или пароль) с текущим значением записи.
        private void BuildText(FieldDef field)
        {
            object current = Tables.GetProp(_row, field.Name);

            if (field.Kind == FieldKind.Password)
            {
                var box = new PasswordBox { Width = FieldWidth };
                if (current is string password) box.Password = password;
                AddControl(box, field);
                _controls.Add(new FieldControl(field, () => box.Password));
                return;
            }

            var textBox = new TextBox { Width = FieldWidth };
            if (current != null) textBox.Text = Convert.ToString(current) ?? "";
            if (field.MaxLength.HasValue) textBox.MaxLength = field.MaxLength.Value;
            AddControl(textBox, field);
            _controls.Add(new FieldControl(field, () => textBox.Text));
        }

        // Чекбокс «Да/Нет».
        private void BuildCheck(FieldDef field)
        {
            var check = new CheckBox { Content = "Да" };
            check.IsChecked = Tables.GetProp(_row, field.Name) is bool value && value;
            AddControl(check, field);
            _controls.Add(new FieldControl(field, () => check.IsChecked == true));
        }

        // Выбор даты.
        private void BuildDate(FieldDef field)
        {
            var picker = new DatePicker { Width = FieldWidth };
            object current = Tables.GetProp(_row, field.Name);
            if (current is DateTime date && date != default(DateTime)) picker.SelectedDate = date;
            AddControl(picker, field);
            _controls.Add(new FieldControl(field, () => picker.SelectedDate));
        }

        // Список-связь (ForeignKey) с значениями из связанной таблицы.
        private void BuildCombo(FieldDef field)
        {
            string keyProp = Tables.PkOf(field.ListKey);
            var combo = new ComboBox
            {
                Width = FieldWidth,
                ItemsSource = Options(field),
                DisplayMemberPath = field.OptionProp,
                SelectedValuePath = keyProp
            };

            if (field.Nullable) combo.Items.Insert(0, EmptyOption(field));

            object value = Tables.GetProp(_row, field.Name);
            if (value != null)
                combo.SelectedValue = value;
            else
                combo.SelectedIndex = field.Nullable ? 0 : -1;

            AddControl(combo, field);
            _controls.Add(new FieldControl(field, () =>
            {
                object id = combo.SelectedValue;
                if (id == null) return null;
                int number = Convert.ToInt32(id);
                return number == 0 ? null : (object)number;
            }));
        }

        // Пустой вариант «— не выбрано —» для списка, допускающего null.
        private object EmptyOption(FieldDef field)
        {
            var option = Activator.CreateInstance(Tables.Get(field.ListKey).EntityType);
            Tables.SetProp(option, Tables.PkOf(field.ListKey), 0);
            return option;
        }

        private List<object> Options(FieldDef field)
        {
            return Tables.Options(field.ListKey).Cast<object>().ToList();
        }

        // Поле изображения: путь + кнопка «Обзор...» и превью под ними.
        private void BuildImage(FieldDef field)
        {
            string current = Convert.ToString(Tables.GetProp(_row, field.Name)) ?? "";

            var row = new Grid { Margin = new Thickness(0, 2, 0, 0), Width = FieldWidth };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var pathBox = new TextBox
            {
                IsReadOnly = true,
                Text = current,
                Margin = new Thickness(0),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(pathBox, 0);

            var browse = new Button
            {
                Content = "Обзор...",
                MinWidth = 90,
                Margin = new Thickness(6, 0, 0, 0)
            };
            Grid.SetColumn(browse, 1);
            browse.Click += (s, e) => BrowseImage(pathBox);

            row.Children.Add(pathBox);
            row.Children.Add(browse);
            AddControl(row, field);

            _previewHost = new StackPanel();
            formPanel.Children.Add(_previewHost);
            UpdatePreview(current);

            _controls.Add(new FieldControl(field, () => pathBox.Text));
        }

        // Кнопка «Обзор...»: выбираем файл, копируем в проект и показываем превью.
        private void BrowseImage(TextBox pathBox)
        {
            string file = ImageFiles.SelectFile();
            if (file == null) return;
            try
            {
                string path = ImageFiles.CopyToProject(file);
                pathBox.Text = path;
                UpdatePreview(path);
            }
            catch (Exception ex)
            {
                Msg.Error("Не удалось загрузить изображение.\n" + ex.Message);
            }
        }

        // Обновляет превью картинки под полем (пустой путь — превью очищается).
        private void UpdatePreview(string path)
        {
            if (_previewHost == null) return;
            _previewHost.Children.Clear();
            if (string.IsNullOrWhiteSpace(path)) return;

            try
            {
                _previewHost.Children.Add(new Image
                {
                    Source = new BitmapImage(ImageUri(path)),
                    Height = 160,
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 6, 0, 4)
                });
            }
            catch
            {
                // картинка не загрузилась — просто не показываем превью
            }
        }

        // Путь из базы → Uri: путь ресурса («/» или «pack:») или файл рядом с программой.
        private Uri ImageUri(string path)
        {
            if (path.StartsWith("/") || path.StartsWith("pack:", StringComparison.OrdinalIgnoreCase))
            {
                // В базе пути вида /img/имя.jpg, а картинки лежат в папке Images.
                string pack = path.TrimStart('/');
                if (pack.StartsWith("img/", StringComparison.OrdinalIgnoreCase))
                    pack = "Images/" + pack.Substring(4);
                return new Uri("pack://application:,,,/" + pack);
            }
            return new Uri(
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path),
                UriKind.Absolute);
        }

        // Проверка всех полей формы перед сохранением; при ошибке показывает окно и возвращает false.
        private bool Validate()
        {
            foreach (var control in _controls)
            {
                string error = ValidateField(control);
                if (error != null)
                    return Validators.Error(error);
            }

            if (_table.Key == "users" && !ValidateUsers()) return false;
            return true;
        }

        // Проверка одного поля по его типу. Возвращает текст ошибки или null.
        private string ValidateField(FieldControl control)
        {
            var field = control.Field;
            object value = control.GetValue();

            if (field.Kind == FieldKind.Text || field.Kind == FieldKind.Password ||
                field.Kind == FieldKind.Email || field.Kind == FieldKind.Phone)
                return ValidateText(field, value as string ?? "");

            if (field.Kind == FieldKind.Int || field.Kind == FieldKind.Decimal)
                return ValidateNumber(field, value);

            if ((field.Kind == FieldKind.Date || field.Kind == FieldKind.DateTime) &&
                field.Required && !(value is DateTime))
                return $"Укажите поле «{field.Title}».";

            if (field.Kind == FieldKind.FK && field.Required && value == null)
                return $"Выберите «{field.Title}».";

            return null;
        }

        // Текстовые поля: обязательность, длина и формат Email/телефона.
        private string ValidateText(FieldDef field, string text)
        {
            if (field.Required && text.Trim().Length == 0)
                return $"Заполните поле «{field.Title}».";
            if (field.MaxLength.HasValue && text.Length > field.MaxLength.Value)
                return $"Поле «{field.Title}» длиннее {field.MaxLength.Value} символов.";
            if (field.Kind == FieldKind.Email && text.Length > 0 && !Validators.IsEmail(text))
                return $"Поле «{field.Title}» должно содержать корректный Email.";
            if (field.Kind == FieldKind.Phone && text.Length > 0 && !Validators.IsPhone(text))
                return $"Поле «{field.Title}» должно содержать корректный телефон (10–15 цифр).";
            return null;
        }

        // Числовые поля: обязательность, формат числа и диапазон min/max.
        private string ValidateNumber(FieldDef field, object value)
        {
            bool empty = value == null || (value is string s && s.Trim().Length == 0);
            if (empty)
                return field.Required ? $"Заполните поле «{field.Title}»." : null;

            string input = Convert.ToString(value);
            bool isInt = field.Kind == FieldKind.Int;

            bool isNumber;
            decimal number = 0;
            if (isInt)
            {
                isNumber = Validators.ParseInt(input, out int parsedInt);
                number = parsedInt;
            }
            else
            {
                isNumber = Validators.ParseDecimal(input, out number);
            }

            if (!isNumber)
                return isInt ? $"Поле «{field.Title}» должно быть целым числом."
                             : $"Поле «{field.Title}» должно быть числом.";

            if (field.Min.HasValue && number < field.Min.Value)
                return $"Поле «{field.Title}» должно быть не меньше {field.Min.Value}.";
            if (field.Max.HasValue && number > field.Max.Value)
                return $"Поле «{field.Title}» должно быть не больше {field.Max.Value}.";
            return null;
        }

        // Для таблицы пользователей дополнительно проверяем занятость логина, Email и телефона.
        private bool ValidateUsers()
        {
            string error = UserRules.CheckUnique(Tables.GetId(_row),
                ControlValue("Login"), ControlValue("Email"), ControlValue("Phone"));
            if (error == null) return true;
            return Validators.Error(error);
        }

        // Значение поля формы по его имени (для проверки пользователей).
        private string ControlValue(string fieldName)
        {
            var control = _controls.FirstOrDefault(x => x.Field.Name == fieldName);
            return control == null ? "" : Convert.ToString(control.GetValue()) ?? "";
        }

        // Сохранение: проверка формы → перенос значений в запись → добавление или обновление в базе.
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!Validate()) return;

                foreach (var control in _controls)
                    Tables.SetProp(_row, control.Field.Name, control.GetValue());

                if (_isNew)
                    Tables.Insert(_table, _row);
                else
                    Tables.Save();

                AppFrame.mainFrame.Navigate(new DataPage(_table.Key));
            }
            catch (Exception ex)
            {
                Msg.Error("Не удалось сохранить запись.\n" + DbError.Text(ex));
            }
        }

        // Отмена — возврат в админку без сохранения.
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            AppFrame.mainFrame.Navigate(new DataPage(_table.Key));
        }

        // Связка «описание поля + способ получить текущее значение из контрола».
        private class FieldControl
        {
            public FieldDef Field { get; }
            public Func<object> GetValue { get; }

            public FieldControl(FieldDef field, Func<object> getValue)
            {
                Field = field;
                GetValue = getValue;
            }
        }
    }
}
