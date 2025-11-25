using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;


namespace LARS.ENGINE.Core.Documents;

/// <summary> 문서작성에 필요한 공유 기능들을 모아둔 클래스임 </summary>
internal class Util
{    
    internal enum DocTypes : sbyte
        {
            BOM = -1,
            DailyPlan = -2,
            PartList = -3,
            itemCounter = -4
        }
    private readonly string SourcePath = Directories.OwnPath; // Directories.json 주소 지정 필요
    static Util()
    {
        
    }
    ///<summary> 사용자가 만든 Column List를 활용함 User_Columns.json </summary>
    internal class GetColumnList
    {
        // 내부로직 필요
        

        ///<summary> Key만 반환함 </summary>
        internal List<string> Key(DocTypes documentType)
        {
            List<string> ColumnList = new List<string>();

            return ColumnList;
        }
        ///<summary> Value만 반환함 </summary>
        internal List<string> Val(DocTypes documentType)
        {
            List<string> ColumnList = new List<string>();

            return ColumnList;
        }
    }
    /// <summary> 사용자가 지정한 Title의 Column들을 기록함. </summary>
    internal void SetColumnList(string keyTarget, string ColumnTitle)
    {
        // .json 저장 기능
    }
    internal void Set2Default()
    {
        
    }
}