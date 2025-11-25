using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;


namespace LARS.ENGINE.Core.Documents;

    /// <summary> 문서작성에 필요한 공유 기능들을 모아둔 클래스임 </summary>
internal class Util
{    
    internal static enum DocTypes : sbyte
        {
            DailyPlan = -1,
            BOM = -2,
            PartList = -3,
            itemCounter = -4
        }
    internal static Util()
    {
        string DirPath = Directories.asdf; // Directories.json 주소 지정 필요
    }
    /// <summary> 사용자가 만든 Column List를 활용함 User_Columns.json </summary>
    internal class GetColumnList()
    {
        // 내부로직 필요

        //<summary> Key만 반환함 </summary>
        List<string> Key()
        {
            
        }
        //<summary> Value만 반환함 </summary>
        List<string> Val()
        {
            
        }
    }
    // <summary> 사용자가 지정한 Title의 Column들을 기록함. </summary>
    internal void SetColumnList(string keyTarget, string ColumnTitle)
    {
        // .json 저장 기능
    }
}