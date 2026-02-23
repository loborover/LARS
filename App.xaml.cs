using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using LARS.Services;
using LARS.ViewModels;

namespace LARS;

/// <summary>
/// LARS 애플리케이션 진입점
/// DI(Dependency Injection) 컨테이너를 구성하고 서비스를 등록합니다.
/// </summary>
public partial class App : Application
{
    private readonly ServiceProvider _serviceProvider;

    public App()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    /// <summary>
    /// 서비스 컨테이너에 의존성을 등록합니다.
    /// </summary>
    private void ConfigureServices(IServiceCollection services)
    {
        // 인프라 서비스
        services.AddSingleton<DirectoryManager>();
        services.AddSingleton<ExcelReaderService>();
        services.AddSingleton<PrintService>();
        services.AddSingleton<PdfExportService>();
        services.AddSingleton<SettingsService>();

        // 비즈니스 서비스
        services.AddSingleton<BomReportService>();
        services.AddSingleton<DailyPlanService>();
        services.AddSingleton<PartListService>();
        services.AddSingleton<ItemCounterService>();
        services.AddSingleton<FeederService>();
        services.AddSingleton<MultiDocService>();

        // ViewModel
        services.AddSingleton<MainViewModel>();

        // MainWindow
        services.AddSingleton<Views.MainWindow>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Sprint 5: 저장된 경로 설정 자동 복원
        var settings = _serviceProvider.GetRequiredService<SettingsService>().Load();
        if (!string.IsNullOrWhiteSpace(settings.BasePath))
        {
            var dirs = _serviceProvider.GetRequiredService<DirectoryManager>();
            dirs.Setup(settings.BasePath, settings.SourcePath);

            var vm = _serviceProvider.GetRequiredService<MainViewModel>();
            vm.BasePath = settings.BasePath;
        }

        var mainWindow = _serviceProvider.GetRequiredService<Views.MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Sprint 5: 현재 경로 설정 저장
        var dirs     = _serviceProvider.GetRequiredService<DirectoryManager>();
        var vm       = _serviceProvider.GetRequiredService<MainViewModel>();
        var settings = _serviceProvider.GetRequiredService<SettingsService>();
        settings.Save(new AppSettings
        {
            BasePath       = dirs.BasePath,
            SourcePath     = dirs.Source,
            LastFeederName = vm.SelectedFeeder?.Name ?? string.Empty
        });
        base.OnExit(e);
    }
}
