#if DEBUG
using System.Data;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LARS.ViewModels;

namespace LARS.Services;

/// <summary>
/// Debug 전용 경량 HTTP API 서버. AI 에이전트가 터미널에서 LARS 상태를 조회·검증합니다.
/// 릴리스 빌드에는 포함되지 않습니다 (#if DEBUG).
/// 기본 포트: 19840
/// </summary>
public class TestApiService : IDisposable
{
    private readonly HttpListener _listener;
    private readonly MainViewModel _mainVm;
    private readonly int _port;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TestApiService(MainViewModel mainVm, int port = 19840)
    {
        _mainVm = mainVm;
        _port = port;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listener.Start();
        _listenTask = Task.Run(() => ListenLoop(_cts.Token));
        System.Diagnostics.Debug.WriteLine($"[TestApiService] Listening on http://localhost:{_port}/");
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener.IsListening)
        {
            try
            {
                var ctx = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(ctx));
            }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }
        }
    }

    private void HandleRequest(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url?.AbsolutePath?.ToLower() ?? "";
        try
        {
            switch (path)
            {
                case "/api/status":
                    RespondJson(ctx, new
                    {
                        status = "running",
                        isProcessing = _mainVm.IsProcessing,
                        statusText = _mainVm.StatusText,
                        port = _port,
                        timestamp = DateTime.Now.ToString("o")
                    });
                    break;

                case "/api/vm/main":
                    RespondJson(ctx, GetMainVmSnapshot());
                    break;

                case "/api/bom/data":
                    RespondDataTable(ctx, _mainVm.BomDataTable, "bom");
                    break;

                case "/api/dp/data":
                    RespondDataTable(ctx, _mainVm.DailyPlanDataTable, "dailyPlan");
                    break;

                case "/api/pl/data":
                    RespondDataTable(ctx, _mainVm.PartListDataTable, "partList");
                    break;

                case "/api/screenshot":
                    HandleScreenshot(ctx);
                    break;

                default:
                    RespondJson(ctx, new
                    {
                        error = "unknown endpoint",
                        available = new[]
                        {
                            "/api/status", "/api/vm/main",
                            "/api/bom/data", "/api/dp/data", "/api/pl/data",
                            "/api/screenshot"
                        }
                    }, 404);
                    break;
            }
        }
        catch (Exception ex)
        {
            RespondJson(ctx, new { error = ex.Message }, 500);
        }
    }

    // ==========================================
    // ViewModel 스냅샷
    // ==========================================

    private object GetMainVmSnapshot()
    {
        return new
        {
            isProcessing = _mainVm.IsProcessing,
            statusText = _mainVm.StatusText,
            progress = _mainVm.Progress,
            // BOM
            bomInfoText = _mainVm.BomInfoText,
            bomRowCount = _mainVm.BomDataTable?.Rows.Count ?? 0,
            bomColCount = _mainVm.BomDataTable?.Columns.Count ?? 0,
            // DailyPlan
            dpInfoText = _mainVm.DpInfoText,
            dpRowCount = _mainVm.DailyPlanDataTable?.Rows.Count ?? 0,
            dpColCount = _mainVm.DailyPlanDataTable?.Columns.Count ?? 0,
            // PartList
            plInfoText = _mainVm.PlInfoText,
            plRowCount = _mainVm.PartListDataTable?.Rows.Count ?? 0,
            plColCount = _mainVm.PartListDataTable?.Columns.Count ?? 0,
            // BasePath
            basePath = _mainVm.BasePath
        };
    }

    // ==========================================
    // DataTable → JSON
    // ==========================================

    private void RespondDataTable(HttpListenerContext ctx, DataTable? dt, string name)
    {
        if (dt == null || dt.Rows.Count == 0)
        {
            RespondJson(ctx, new { name, loaded = false, rowCount = 0 });
            return;
        }

        var headers = new List<string>();
        foreach (DataColumn col in dt.Columns)
            headers.Add(col.ColumnName);

        var rows = new List<List<string>>();
        foreach (DataRow row in dt.Rows)
        {
            var r = new List<string>();
            foreach (var item in row.ItemArray)
                r.Add(item?.ToString() ?? "");
            rows.Add(r);
        }

        RespondJson(ctx, new
        {
            name,
            loaded = true,
            rowCount = dt.Rows.Count,
            colCount = dt.Columns.Count,
            headers,
            rows
        });
    }

    // ==========================================
    // 스크린샷
    // ==========================================

    private void HandleScreenshot(HttpListenerContext ctx)
    {
        byte[]? pngBytes = null;

        Application.Current.Dispatcher.Invoke(() =>
        {
            var window = Application.Current.MainWindow;
            if (window == null) return;

            var bounds = VisualTreeHelper.GetDescendantBounds(window);
            if (bounds.IsEmpty) return;

            var rtb = new RenderTargetBitmap(
                (int)window.ActualWidth, (int)window.ActualHeight,
                96, 96, PixelFormats.Pbgra32);
            rtb.Render(window);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            using var ms = new MemoryStream();
            encoder.Save(ms);
            pngBytes = ms.ToArray();
        });

        if (pngBytes != null)
        {
            ctx.Response.ContentType = "image/png";
            ctx.Response.ContentLength64 = pngBytes.Length;
            ctx.Response.OutputStream.Write(pngBytes, 0, pngBytes.Length);
            ctx.Response.Close();
        }
        else
        {
            RespondJson(ctx, new { error = "no window available" }, 500);
        }
    }

    // ==========================================
    // 응답 헬퍼
    // ==========================================

    private static void RespondJson(HttpListenerContext ctx, object data, int statusCode = 200)
    {
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");

        string json = JsonSerializer.Serialize(data, _json);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        ctx.Response.ContentLength64 = bytes.Length;
        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        ctx.Response.Close();
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _listener.Close();
        _listenTask?.Wait(TimeSpan.FromSeconds(2));
        _cts?.Dispose();
    }
}
#endif
