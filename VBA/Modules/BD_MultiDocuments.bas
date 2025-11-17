Attribute VB_Name = "BD_MultiDocuments"
Option Explicit

Private isParsed As Boolean
Private tWB As Workbook, tWS As Worksheet
Private rWB As Workbook, rWS As Worksheet
Public Sub Read_Documents(Optional Handle As Boolean = False)
    Dim DPCount As Long, PLCount As Long, MDCount As Long, i As Long, c As Long, Cycle As Long
    Dim vDate(1 To 2) As String, vLine(1 To 2) As String
    Dim Dir_Main As String: Dir_Main = Replace(ThisWorkbook.FullName, ThisWorkbook.Name, "")
    Dim Dir_DP As String: Dir_DP = Dir_Main & "DailyPlan"
    Dim Dir_PLi As String: Dir_PLi = Dir_Main & "PartList"
    Dim Clt_DP As New Collection: Set Clt_DP = FindFilesWithTextInName(Dir_DP, "DailyPlan", ".xlsx")
    Dim Clt_PLi As New Collection: Set Clt_PLi = FindFilesWithTextInName(Dir_PLi, "PartList", ".xlsx")
    Dim LV_MD As ListView: Set LV_MD = AutoReportHandler.ListView_MD_Own: LV_MD.ListItems.Clear
    
    FillListView_Intersection Clt_DP, Clt_PLi, LV_MD, 2025, "³¯Â¥", "¶óÀÎ", "DailyPlan", "PartList"

    DPCount = Clt_DP.Count: PLCount = Clt_PLi.Count: MDCount = LV_MD.ListItems.Count
    If Handle Then MsgBox "DailyPlan : " & DPCount & "Àå ¿¬°áµÊ" & vbLf & _
                                "PartList : " & PLCount & "Àå ¿¬°áµÊ" & vbLf & _
                                "Multi Documents : " & MDCount & "Àå ¿¬°áµÊ" & vbLf & _
                                Cycle
End Sub

Private Sub SetUp_Targets(ByRef Target_WorkBook As Workbook, ByRef Target_WorkSheet As Worksheet, _
                            ByRef Reference_WorkBook As Workbook, ByRef Reference_WorkSheet As Worksheet)
    Set tWB = Target_WorkBook: Set tWS = Target_WorkSheet: Set rWB = Reference_WorkBook: Set rWS = Reference_WorkSheet
End Sub
                            
Private Sub Parse_wbwsPointer()
    Dim Linked(1 To 4) As Boolean
    Linked(1) = Not tWB Is Nothing: Linked(2) = Not tWS Is Nothing: Linked(3) = Not rWB Is Nothing: Linked(4) = Not rWS Is Nothing
    If Linked(1) And Linked(2) And Linked(3) And Linked(4) Then Exit Sub
    Set tWB = Nothing: Set tWS = Nothing: Set rWB = Nothing: Set rWS = Nothing
    
    isParsed = True ' Parsing Boolean
End Sub

Public Sub MixMatching(ByVal Target_item As String)
    
End Sub
