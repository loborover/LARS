using System;
using System.IO;
using System.Text.Json;

namespace ENGINE.Core;

/// <summary>
/// LARS에서 사용하는 주요 폴더 경로를 중앙에서 관리하는 클래스
/// - OwnPath        : 실행 파일이 있는 폴더
/// - DocumentsPath  : 내 문서\LARS
/// - DownloadPath   : 사용자 설정 가능, 없으면 기본값 사용
/// </summary>
public static class Directories
{
    // 읽기 전용 프로퍼티들
    public static string OwnPath { get; }
    public static string DocumentsPath { get; }
    public static string DefaultDownloadPath { get; }
    public static string ConfigFilePath { get; }

    // 내부에서 쓰는 설정 객체
    private static DirectorySettings _settings;

    /// <summary>
    /// 실제로 사용하는 다운로드 폴더.
    /// 설정에 DownloadPath가 있으면 그것을, 없으면 DefaultDownloadPath를 사용.
    /// </summary>
    public static string DownloadPath
        => string.IsNullOrWhiteSpace(_settings.DownloadPath)
           ? DefaultDownloadPath
           : _settings.DownloadPath!;

    // 정적 생성자: 프로그램 시작 시 한 번만 실행
    static Directories()
    {
        // 1) 실행 파일 위치 (예: C:\Apps\LARS\ )
        OwnPath = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // 2) 내 문서\LARS
        var myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        DocumentsPath = Path.Combine(myDocuments, "LARS");

        // 3) 기본 다운로드 경로: OwnPath\Downloads
        DefaultDownloadPath = Path.Combine(OwnPath, "Downloads");

        // 4) 설정 파일 위치: OwnPath\LARS.directories.json
        ConfigFilePath = Path.Combine(OwnPath, "LARS.directories.json");

        // 5) 설정 읽기
        _settings = LoadSettings();

        // 6) 기본 폴더 존재 보장
        EnsureDirectoryExists(DocumentsPath);
        EnsureDirectoryExists(DownloadPath);
    }

    /// <summary>
    /// 다운로드 폴더를 변경하고, 설정 파일에 저장합니다.
    /// </summary>
    public static void SetDownloadPath(string newPath)
    {
        if (string.IsNullOrWhiteSpace(newPath))
            throw new ArgumentException("Download 경로가 비어 있습니다.", nameof(newPath));

        // 경로 정규화
        var fullPath = Path.GetFullPath(newPath);

        _settings.DownloadPath = fullPath;
        SaveSettings();

        EnsureDirectoryExists(fullPath);
    }

    // ----- 내부 메서드들 -----

    private static DirectorySettings LoadSettings()
    {
        try
        {
            if (!File.Exists(ConfigFilePath))
                return new DirectorySettings(); // 빈 설정 (DownloadPath = null)

            var json = File.ReadAllText(ConfigFilePath);
            var settings = JsonSerializer.Deserialize<DirectorySettings>(json);

            return settings ?? new DirectorySettings();
        }
        catch
        {
            // 설정 파일이 깨져있거나 읽기 실패하면, 새 설정 사용
            return new DirectorySettings();
        }
    }

    private static void SaveSettings()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(_settings, options);
        File.WriteAllText(ConfigFilePath, json);
    }

    private static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }
}

/// <summary>
/// 설정 파일에 저장되는 구조
/// </summary>
public class DirectorySettings
{
    public string? DownloadPath { get; set; }
}
