Attribute VB_Name = "BCA_PLIV_Feeder"
Option Explicit
#Const COM = True
Private ws As Worksheet
Private Feeders As New Collection  ' FeederUnit들을 담기 위한 컬렉션
Private LV_F As New ListView, LV_Fi As New ListView, LV_PLfF As New ListView

Public Sub SetUp_FeederTrackers()
    Set LV_F = UI.ListView_Feeders
    Set LV_Fi = UI.ListView_Feeder_item
    Set LV_PLfF = UI.ListView_PLfF_item
End Sub
Public Property Set FeedersWS(ByRef TargetWorkSheet As Worksheet)
    Set ws = TargetWorkSheet
End Property

Public Sub SortColumnByFeeder(ByRef Feeder As Collection)
    #If COM = True Then
        Set ws = ActiveWorkbook.ActiveSheet
    #End If
    If ws Is Nothing Then Debug.Print "Err.WorkSheet is Nothing": Exit Sub
    Dim Chk As Range, itemRange As Range: Set itemRange = ws.Rows(1).Find("-Line", LookIn:=xlValues, lookAt:=xlPart)
    Dim FirstCol As Long, LastCol As Long, i As Long
    ' 초기화
    FirstCol = itemRange.Column + 2: LastCol = ws.Cells(1, ws.Columns.Count).End(xlToLeft).Column
    Set itemRange = ws.Range(ws.Cells(1, FirstCol), ws.Cells(1, LastCol))
    ws.Columns.EntireColumn.Hidden = False
    ws.Columns(ws.Rows(1).Find("Tool", LookIn:=xlValues, lookAt:=xlWhole).Column).Hidden = True
    Replacing_Feeder2item Feeder
    
    For Each Chk In itemRange
        ' Feeder 컬렉션내의 항목인지 검사 후 목록 중 없을 경우 해당 열을 숨김처리 하는 코드
        ws.Columns(Chk.Column).Hidden = Not IsInCollection(Chk.Value, Feeder)
    Next Chk
End Sub

' item 규칙 " " -> vbLf // VBA 내부 연산시 변환 필요
' Feeder 규칙 vbLf -> " ", "_" -> " " // VBA 외부 표현시 변환 필요. 예를 들면 UI/UX 또는 워크시트 셀에서 표기할때 사용됨.
Private Sub Replacing_Feeder2item(ByRef Target As Collection)
    Dim Copied As New Collection, i As Long
    For i = 1 To Target.Count
        If InStr(Target(i), " ") > 0 Then
            Copied.Add Replace(Target(i), " ", vbLf)
        Else
            Copied.Add Target(i)
        End If
    Next i
    Set Target = Copied
End Sub
Private Sub Replacing_item2Feeder(ByRef Target As Collection)
    Dim Copied As New Collection, i As Long
    For i = 1 To Target.Count
        If InStr(Target(i), vbLf) > 0 Then
            Copied.Add Replace(Target(i), vbLf, " ")
        ElseIf InStr(Target(i), "_") Then
            Copied.Add Replace(Target(i), "_", " ")
        Else
            Copied.Add Target(i)
        End If
    Next i
    Set Target = Copied
End Sub

Public Sub A_Delete_Feeder()
    ' 선택된 값과 콤보 리스트 중 중복되는 인덱스를 찾아 해당 피더를 삭제하는 코드
    If UI.CbBx_Feeder.ListCount = 0 Then UI.CbBx_Feeder.Value = "": Exit Sub
    Dim i As Long
    Dim Target As String: Target = UI.CbBx_Feeder.Value
    Feeders.Remove Target
    UI.CbBx_Feeder.Value = ""
    For i = 0 To UI.CbBx_Feeder.ListCount - 1
        If UI.CbBx_Feeder.List(i) = Target Then UI.CbBx_Feeder.RemoveItem i: Exit Sub
    Next i
End Sub
Public Sub A_New_Feeder()
    ' 콤보박스 리스트와 중복되지 않게끔 피더 이름을 추가하고 피더유닛을 생성함
    If UI.CbBx_Feeder.Value = "" Then Exit Sub
    Dim NewFeeder As New FeederUnit
    If Not FOTFC(UI.CbBx_Feeder.Value, UI.CbBx_Feeder) Then
        UI.CbBx_Feeder.Additem UI.CbBx_Feeder.Value
        NewFeeder.Name = UI.CbBx_Feeder.Value
        Feeders.Add NewFeeder, NewFeeder.Name
    Else
        MsgBox "중복된 Feeder 추가", vbCritical
    End If
End Sub
Public Sub A_Save_Feeder()
    ' 실시간 수정중인 사항들을 저장하는 코드
End Sub
Public Sub Select_Feeder_Target()
    ' 선택된 피더 이름을 참조하여, 피더유닛의 아이템 컬렉션을 리스트뷰에 적재하는 코드
    ' 콤보박스를 변경하는 이벤트 발생시 실행됨.
    
End Sub
Public Sub B_Read_Feeder()
    ' 외부로 송출된 피더목록을 불러와 콤보박스를 채우는 코드
End Sub
Public Sub B_Send_Feeder()
    ' 실시간 편집중인 피더유닛들을 엑셀 외부로 송출하여 지정된 디렉토리에 저장하는 코드
End Sub

Public Sub C_Additem_List()
    If Feeders Is Nothing Or Feeders.Count = 0 Then Exit Sub
    Dim FmLV As ListView: Set FmLV = LV_PLfF
    Dim ToLV As ListView: Set ToLV = LV_Fi
    If FmLV.ListItems.Count = 0 Then Exit Sub
    Dim i As Long, Target As String: Target = FmLV.SelectedItem.text
    With UI.CbBx_Feeder
        
    End With
    For i = 1 To ToLV.ListItems.Count
        If StrComp(ToLV.ListItems(i), Target, vbTextCompare) = 0 Then Exit Sub ' 중복된 아이템 추가시 Exit
    Next i
    ToLV.ListItems.Add(, , Target).Checked = True ' 리스트뷰에 추가
End Sub
Public Sub C_Removeitem_List()
    With LV_Fi
        If .SelectedItem Is Nothing Then Exit Sub
        Dim i As Long, Target As String: Target = .SelectedItem.text
        For i = 1 To .ListItems.Count
            If .ListItems(i) = Target Then .ListItems.Remove i: Exit Sub
        Next i
    End With
End Sub
Public Sub D_ListView_Feeder_Updater()
    If Feeders Is Nothing Then Exit Sub
    Dim i As Long
    LV_F.ListItems.Clear
    For i = 1 To Feeders.Count
        Dim Target As New FeederUnit: Set Target = Feeders(i)
        With LV_F.ListItems.Add(, , Target.Name)
            .SubItems(1) = Target.itemBox.Count
        End With
    Next i
End Sub
Public Sub D_ListView_Feeder_item_Updater(ByVal Feeder_Name As String)
    If Feeders Is Nothing Then Exit Sub
    Dim i As Long, Target As New FeederUnit
    For i = 1 To Feeders.Count
        Set Target = Feeders(i)
        If Feeder_Name = Target.Name Then GoTo Pass
    Next i
    Exit Sub
Pass:
    LV_Fi.ListItems.Clear
    Set Target = Feeders(Feeder_Name)
    For i = 1 To Target.itemBox.Count
        LV_Fi.ListItems.Add , , Target.itemBox(i)
    Next i
End Sub
' Feeder Utillity
Private Function FOTFC(ByVal Target As String, ByRef From As MSForms.ComboBox) As Boolean
    ' Find Out Target From Combobox
    Dim i As Long
    For i = 0 To From.ListCount - 1
        If StrComp(From.List(i), Target, vbTextCompare) = 0 Then FOTFC = True: Exit Function
    Next i
End Function
