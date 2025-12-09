using System.Text.Json;
using LARS.ENGINE.Core;
namespace LARS.ENGINE.Documents;

/// <summary> 문서작성에 필요한 공유 기능들을 모아둔 클래스임 </summary>
internal class DocUtils
{    
    internal enum DocTypes : sbyte
        {
            BOM = -1,
            DailyPlan = -2,
            PartList = -3,
            itemCounter = -4
        }
    internal static readonly string SourcePath = Directories.OwnPath; // Directories.json 주소 지정 필요
    ///<summary> 사용자가 만든 Column List를 활용함 User_Columns.json </summary>
    internal static TargetList GetColumnList(DocTypes? types=null)
    {
        User_Columns Target = LoadConfig(SourcePath);
        TargetList targetList= new TargetList();
        switch (types)
        {
            case DocTypes.BOM:
                break;
            case DocTypes.DailyPlan:
                break;
            case DocTypes.PartList:
                break;
            case DocTypes.itemCounter:
                break;
            default:
                break;
        }
        return targetList;
    }
    internal class TargetList
    {
        internal List<string> Key{get; init;} = new List<string>();
        internal List<string> Val{get; init;} = new List<string>();
        internal static string? TargetPath=null;
        internal TargetList()
        {
            Key.Add("");
            Val.Add("");
        }
    }
    
    /// <summary> 사용자가 지정한 Title의 Column들을 기록함. </summary>
    internal void SetColumnList(string keyTarget, string ColumnTitle)
    {
        // .json 저장 기능
    }
    internal void Reset2Default()
    {
        
    }
    internal class User_Columns
    {
        internal required Dictionary<string, Dictionary<string, string>> Default{get; set;}
        internal required Dictionary<string, Dictionary<string, string>> UserSet{get; set;}
    }
    /// <summary>
    /// 지정된 경로에 있는 JSON 파일을 읽어 User_Columns 객체로 반환합니다.
    /// </summary>
    internal static User_Columns LoadConfig(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"JSON 파일을 찾을 수 없습니다: {filePath}");

        string json = File.ReadAllText(filePath);

        // System.Text.Json 옵션 (키 이름을 그대로 사용)
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,   // 대소문자 구분 없이 매핑
            ReadCommentHandling = JsonCommentHandling.Skip, // // 주석 무시
            AllowTrailingCommas = true
        };

        User_Columns config = JsonSerializer.Deserialize<User_Columns>(json, options)
                         ?? throw new InvalidOperationException("JSON 파싱에 실패했습니다.");

        return config;
    }
}