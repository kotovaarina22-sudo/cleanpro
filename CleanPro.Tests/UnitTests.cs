using System;
using CleanPro.AppData;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CleanPro.Tests
{
    // Модульные тесты чистой логики CleanPro: валидация и корзина.
    // Не трогают базу данных и интерфейс — только функции без побочных эффектов.
    [TestClass]
    public class UnitTests
    {
        // --- Validators.IsEmail ---
        [TestMethod]
        public void IsEmail_ValidAndInvalid_ReturnsExpected()
        {
            Assert.IsTrue(Validators.IsEmail("user@mail.ru"));
            Assert.IsTrue(Validators.IsEmail("a.b+c@sub.domain.ru"));
            Assert.IsFalse(Validators.IsEmail("usermail.ru"));   // нет @
            Assert.IsFalse(Validators.IsEmail("user@ru"));       // нет домена
            Assert.IsFalse(Validators.IsEmail(""));              // пусто
            Assert.IsFalse(Validators.IsEmail("user @mail.ru")); // пробел в локальной части
        }

        // --- Validators.IsPhone ---
        [TestMethod]
        public void IsPhone_DigitCountBounds_ReturnsExpected()
        {
            Assert.IsTrue(Validators.IsPhone("+79991234567"));   // 11 цифр
            Assert.IsTrue(Validators.IsPhone("8 (812) 123-45-67")); // 11 цифр с маской
            Assert.IsFalse(Validators.IsPhone("12345"));         // 5 цифр — мало
            Assert.IsFalse(Validators.IsPhone("+7999123456789012")); // 16 цифр — много
            Assert.IsFalse(Validators.IsPhone("нет телефона"));  // не цифры
            Assert.IsFalse(Validators.IsPhone(""));
        }

        // --- Validators.ParseDecimal ---
        [TestMethod]
        public void ParseDecimal_CurrencyAndGarbage_ParsesOrFails()
        {
            Assert.IsTrue(Validators.ParseDecimal("1234,56", out decimal v1));
            Assert.AreEqual(1234.56m, v1, 0.001m);

            Assert.IsTrue(Validators.ParseDecimal("1 234 ₽", out decimal v2));
            Assert.AreEqual(1234m, v2);

            Assert.IsFalse(Validators.ParseDecimal("не число", out decimal _));
            Assert.IsFalse(Validators.ParseDecimal("", out decimal _));
            Assert.IsFalse(Validators.ParseDecimal(null, out decimal _));
        }

        // --- UserRules.CheckFields ---
        [TestMethod]
        public void CheckFields_ValidationRules_ReturnsErrorOrNull()
        {
            // пустые обязательные поля
            Assert.IsNotNull(UserRules.CheckFields("", "Иван", "login", "a@mail.ru", "+79991234567"));
            // логин короче 3 символов
            Assert.IsNotNull(UserRules.CheckFields("Иванов", "Иван", "ab", "a@mail.ru", "+79991234567"));
            // некорректный email
            Assert.IsNotNull(UserRules.CheckFields("Иванов", "Иван", "login", "не-почта", "+79991234567"));
            // некорректный телефон
            Assert.IsNotNull(UserRules.CheckFields("Иванов", "Иван", "login", "a@mail.ru", "12"));
            // всё верно
            Assert.IsNull(UserRules.CheckFields("Иванов", "Иван", "login", "a@mail.ru", "+79991234567"));
        }

        // --- Cart: итог и очистка ---
        [TestMethod]
        public void Cart_TotalSumAndClear_Works()
        {
            Cart.Clear();
            Cart.Items.Add(new CartItem { ServiceId = 1, Name = "Уборка", UnitName = "шт", Price = 1500m, Quantity = 2 });
            Cart.Items.Add(new CartItem { ServiceId = 2, Name = "Мойка", UnitName = "кв.м", Price = 300m, Quantity = 5m });

            Assert.AreEqual(2, Cart.Items.Count);
            Assert.AreEqual(1500m * 2 + 300m * 5m, Cart.TotalSum());
            Assert.AreEqual(300m * 5m, Cart.Items[1].Sum);

            Cart.Clear();
            Assert.AreEqual(0, Cart.Items.Count);
            Assert.AreEqual(0m, Cart.TotalSum());
        }
    }
}
