using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LARS.ENGINE.Documents;

/// <summary>
/// 폴더를 스캔해서 DailyPlan/BOM/기타 파일을
/// RandomFile / DailyPlanFile / BOMFile / PartList 로 분류해주는 엔진
/// </summary>
public sealed class FileScanner
{
    /// <summary>검색의 기준이 되는 루트 폴더</summary>
    public string RootPath { get; }

    public FileScanner(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("rootPath가 비어 있습니다.", nameof(rootPath));

        RootPath = rootPath;
    }

    /// <summary>
    /// 루트 폴더 아래 모든 파일을 스캔하여
    /// RandomFile (또는 자식타입) 리스트로 반환
    /// </summary>
    public IEnumerable<RandomFile> ScanAll(bool recursive = true)
    {
        if (!Directory.Exists(RootPath))
            yield break;

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        foreach (var path in Directory.EnumerateFiles(RootPath, "*.*", option))
        {
            yield return Classify(path);
        }
    }

    /// <summary>
    /// 파일 경로 하나를 받아서 DailyPlanFile/BOMFile/RandomFile 중 하나로 분류
    /// </summary>
    private RandomFile Classify(string path)
    {
        var fileName = Path.GetFileName(path);

        // TODO: 실제 규칙으로 교체 (예: 접두사, 상위 폴더명, 패턴 등)
        if (fileName.Contains("DP_", StringComparison.OrdinalIgnoreCase))
        {
            return new DailyPlanFile(path, File.GetLastWriteTime(path));
        }
        else if (fileName.Contains("BOM_", StringComparison.OrdinalIgnoreCase))
        {
            return new BOMFile(path, File.GetLastWriteTime(path));
        }
        else
        {
            return new RandomFile(path, File.GetLastWriteTime(path));
        }
    }

    /// <summary>
    /// DailyPlan 전용 파일만 반환
    /// </summary>
    public IEnumerable<DailyPlanFile> ScanDailyPlans(bool recursive = true)
        => ScanAll(recursive).OfType<DailyPlanFile>();

    /// <summary>
    /// BOM 전용 파일만 반환
    /// </summary>
    public IEnumerable<BOMFile> ScanBomFiles(bool recursive = true)
        => ScanAll(recursive).OfType<BOMFile>();
}