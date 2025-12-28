/*
 * LTX Boost – Un optimizador para Windows 10 y 11 diseñado
 * para mejorar el rendimiento del sistema.
 *
 * Copyright (C) 2024 LaitaX
 *
 * Este programa es software libre: puedes redistribuirlo
 * y/o modificarlo bajo los términos de la Licencia Pública
 * General Affero de GNU, publicada por la Free Software
 * Foundation, ya sea la versión 3 de la Licencia o
 * (a tu elección) cualquier versión posterior.
 *
 * Este programa se distribuye con la esperanza de que
 * sea útil, pero SIN NINGUNA GARANTÍA; sin siquiera la
 * garantía implícita de COMERCIALIZACIÓN o IDONEIDAD
 * PARA UN PROPÓSITO PARTICULAR.
 *
 * Consulta la Licencia Pública General Affero de GNU
 * para más detalles.
 *
 * Deberías haber recibido una copia de la Licencia
 * Pública General Affero de GNU junto con este programa.
 * Si no es así, visita: https://www.gnu.org/licenses/
 *
 * Contacto: a342510021@unidem.edu.mx
 */

using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using RyTuneX.Activation;
using RyTuneX.Contracts.Services;
using RyTuneX.Core.Contracts.Services;
using RyTuneX.Core.Services;
using RyTuneX.Models;
using RyTuneX.Services;
using RyTuneX.ViewModels;
using RyTuneX.Views;
using Windows.Storage;

namespace RyTuneX;

public partial class App : Application
{
    public IHost Host
    {
        get;
    }

    public static void ShowNotification(string title, string message, Microsoft.UI.Xaml.Controls.InfoBarSeverity severity, int duration) =>
        ShellPage.ShowNotification(title, message, severity, duration);

    public static T GetService<T>()
        where T : class
    {
        if ((App.Current as App)!.Host.Services.GetService(typeof(T)) is not T service)
        {
            throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
        }

        return service;
    }

    public static WindowEx MainWindow { get; } = new MainWindow();

    public static UIElement? AppTitlebar
    {
        get; set;
    }

    public App()
    {
        InitializeComponent();
        LogHelper.Log("___________ New Session ___________");

        // Catch unhandled exceptions early to avoid silent activation failures
        UnhandledException += async (sender, e) =>
        {
            try { await LogHelper.LogError($"UnhandledException: {e.Exception}"); } catch { }
        };

        Host = Microsoft.Extensions.Hosting.Host.
        CreateDefaultBuilder().
        UseContentRoot(AppContext.BaseDirectory).
        ConfigureServices((context, services) =>
        {
            // Default Activation Handler
            services.AddTransient<ActivationHandler<LaunchActivatedEventArgs>, DefaultActivationHandler>();

            // Services
            services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
            services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
            services.AddTransient<INavigationViewService, NavigationViewService>();

            services.AddSingleton<IActivationService, ActivationService>();
            services.AddSingleton<IPageService, PageService>();
            services.AddSingleton<INavigationService, NavigationService>();

            // Core Services
            services.AddSingleton<IFileService, FileService>();

            // Views and ViewModels
            services.AddTransient<SettingsPage>();
            services.AddTransient<OptimizeSystemPage>();
            services.AddTransient<SystemInfoPage>();
            services.AddTransient<DebloatSystemPage>();
            services.AddTransient<HomePage>();
            services.AddTransient<NetworkPage>();
            services.AddTransient<SecurityPage>();
            services.AddTransient<RepairPage>();
            services.AddTransient<ShellPage>();
            services.AddTransient<ShellViewModel>();

            // Configuration
            services.Configure<LocalSettingsOptions>(context.Configuration.GetSection(nameof(LocalSettingsOptions)));
        }).
        Build();
    }
    private static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void RestartElevated(string? arguments = null)
    {
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule!.FileName!;

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = true,
                Verb = "runas" // triggers UAC
            };

            Process.Start(startInfo);
        }
        catch
        {
            // User cancelled UAC
        }

        Environment.Exit(0);
    }

    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Ensure admin
        if (!IsRunningAsAdministrator())
        {
            RestartElevated();
            return;
        }
        base.OnLaunched(args);

        // setting custom title bar when the app starts to prevent it from briefly show the standard titlebar
        try
        {
            MainWindow.AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            MainWindow.AppWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        }
        catch (Exception ex)
        {
            await LogHelper.LogError($"TitleBar init failed: {ex}");
        }

        await App.GetService<IActivationService>().ActivateAsync(args);
    }

    // Sets flow direction based on the current language.

    public static Microsoft.UI.Xaml.FlowDirection FlowDirectionSetting
    {
        get
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("SelectedLanguage", out var langObj)
                && langObj is string lang)
            {
                if (lang.StartsWith("ar", StringComparison.OrdinalIgnoreCase) ||
                    lang.StartsWith("he", StringComparison.OrdinalIgnoreCase))
                    return Microsoft.UI.Xaml.FlowDirection.RightToLeft;
            }
            return Microsoft.UI.Xaml.FlowDirection.LeftToRight;
        }
    }
}
