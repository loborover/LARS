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

        // 비즈니스 서비스
        services.AddSingleton<BomReportService>();
        services.AddSingleton<DailyPlanService>();
        services.AddSingleton<PartListService>();
        services.AddSingleton<ItemCounterService>();
        services.AddSingleton<FeederService>();

        // ViewModel
        services.AddSingleton<MainViewModel>();

        // MainWindow
        services.AddSingleton<Views.MainWindow>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var mainWindow = _serviceProvider.GetRequiredService<Views.MainWindow>();
        mainWindow.Show();
    }
}
