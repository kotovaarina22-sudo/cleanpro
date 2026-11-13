// App.xaml.cs — точка входа WPF-приложения: перехватывает все необработанные
// исключения (UI, потоки, задачи) и показывает одно понятное окно ошибки.
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CleanPro.AppData;

namespace CleanPro
{
    public partial class App : Application
    {
        // Подписываемся на обработчики ошибок при старте приложения.
        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += Domain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            base.OnStartup(e);
        }

        // Ошибка в потоке интерфейса: показываем и считаем обработанной, чтобы приложение не падало.
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ShowError(e.Exception);
            e.Handled = true;
        }

        private void Domain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex) ShowError(ex);
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            ShowError(e.Exception);
        }

        // Единое окно с текстом ошибки для всех перехваченных исключений.
        private void ShowError(Exception ex)
        {
            Msg.Error("Произошла непредвиденная ошибка. Попробуйте ещё раз.\n\n" + ex.Message);
        }
    }
}
