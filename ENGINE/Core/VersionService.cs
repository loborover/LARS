using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace ENGINE;

/// <summary>
/// 로컬/원격 버전 정보를 읽어오고, Version 형식으로 반환하는 서비스
/// </summary>
public class VersionService
{
    private readonly string _localVersionFilePath;
    private readonly string? _remoteVersionUrl;

    public VersionService(string localVersionFilePath, string? remoteVersionUrl = null)
    {
        _localVersionFilePath = localVersionFilePath;
        _remoteVersionUrl = remoteVersionUrl;
    }

    /// <summary>
    /// 로컬 Version.txt 등에서 버전을 읽어옵니다.
    /// 파일이 없으면 0.0.0 으로 간주합니다.
    /// </summary>
    public Version GetLocalVersion()
    {
        if (!File.Exists(_localVersionFilePath))
            return new Version(0, 0, 0);

        var text = File.ReadAllText(_localVersionFilePath).Trim();
        if (string.IsNullOrWhiteSpace(text))
            return new Version(0, 0, 0);

        return Version.Parse(text);
    }

    /// <summary>
    /// 서버(예: GitHub Raw, 사내 웹서버)에 있는 최신 버전을 읽어옵니다.
    /// _remoteVersionUrl 이 없으면 null 반환.
    /// </summary>
    public async Task<Version?> GetRemoteVersionAsync()
    {
        if (string.IsNullOrWhiteSpace(_remoteVersionUrl))
            return null;

        using var client = new HttpClient();

        // version.txt 안에 "1.2.3" 한 줄 있다고 가정
        var text = await client.GetStringAsync(_remoteVersionUrl);

        return Version.Parse(text.Trim());
    }
}
