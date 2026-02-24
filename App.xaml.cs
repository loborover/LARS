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
    public ServiceProvider ServiceProvider { get; }

    public App()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();
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

        // 뷰모델 등록
        services.AddTransient<MainViewModel>();

        // 뷰 등록
        services.AddTransient<Views.MainWindow>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 새 환경: AppSettings 전체를 넘겨서 DirectoryManager 구성
        var settings = ServiceProvider.GetRequiredService<SettingsService>().Load();
        if (!string.IsNullOrWhiteSpace(settings.BasePath))
        {
            var dirs = ServiceProvider.GetRequiredService<DirectoryManager>();
            dirs.Setup(settings);

            var vm = ServiceProvider.GetRequiredService<MainViewModel>();
            vm.BasePath = settings.BasePath;
        }

        var mainWindow = ServiceProvider.GetRequiredService<Views.MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // 런타임 중 변경된 경로들은 다시 OnExit 바인딩 시점에서 SettingsService를 통해 직접 저장 처리됨
        // (MainViewModel 측에서 UpdateSettingsAndSave 등의 메서드로 실시간 저장)
        base.OnExit(e);
    }
}
