using System.IO;

namespace LARS.Utils;

public static class DirectoryHelper
{
    // 기본 루트 디렉토리 (실행 파일 위치 기준)
    public static string RootPath => AppDomain.CurrentDomain.BaseDirectory;

    // 소스 파일 폴더 경로 (기본값: 실행 파일 위치)
    // 필요 시 설정 파일에서 읽어오도록 변경 가능
    public static string SourcePath { get; set; } = RootPath;

    // BOM 파일 저장 경로
    public static string BomPath => Path.Combine(RootPath, "BOM");

    // PartList 파일 저장 경로
    public static string PartListPath => Path.Combine(RootPath, "PartList");

    // 초기화: 필요한 폴더가 없으면 생성
    public static void InitializeDirectories()
    {
        EnsureDirectory(BomPath);
        EnsureDirectory(PartListPath);
    }

    // 폴더 존재 확인 및 생성
    private static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }
}
