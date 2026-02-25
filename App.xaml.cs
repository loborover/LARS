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
        services.AddSingleton<PdfExportService>();
        services.AddSingleton<SettingsService>();

        // 비즈니스 서비스
        services.AddSingleton<BomReportService>();
        services.AddSingleton<DailyPlanService>();
        services.AddSingleton<PartListService>();
        services.AddSingleton<ItemCounterService>();
        services.AddSingleton<FeederService>();
        services.AddSingleton<MultiDocService>();

        // 매크로 서비스
        services.AddSingleton<MacroRunner>();
        services.AddSingleton<MacroStorageService>();

        // 뷰모델 등록
        services.AddSingleton<MainViewModel>();
        services.AddTransient<MacroEditorViewModel>();

        // 뷰 등록
        services.AddTransient<Views.MainWindow>();
        services.AddTransient<Views.MacroEditorWindow>();

#if DEBUG
        // AI Agent 테스트 API (Debug 전용)
        services.AddSingleton<TestApiService>();
#endif
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

#if DEBUG
        // Debug HTTP API 서버 시작 (localhost:19840)
        var testApi = ServiceProvider.GetRequiredService<TestApiService>();
        testApi.Start();
#endif
    }

    protected override void OnExit(ExitEventArgs e)
    {
#if DEBUG
        ServiceProvider.GetService<TestApiService>()?.Dispose();
#endif
        base.OnExit(e);
    }
}
