Attribute VB_Name = "BB_DailyPlan_Viewer"
Option Explicit

Public Yoil_DP As Boolean ' Day of the week On/Off Switch
Public MRB_DP As Boolean ' Manual_Reporting_Bool_DailyPlan

Private BoW As Single ' Black or White
Private DP_Processing_WB As New Workbook ' 모듈내 전역변수로 선언함
Private Target_WorkSheet As New Worksheet
Private PrintArea As Range ' 모듈내 프린트영역
Private Brush As New Painter
Private Title As String, wLine As String, vDate As String
Private vCFR As Collection ' Columns For Report

Public Sub Read_DailyPlan(Optional Handle As Boolean, Optional ByRef Target_Listview As ListView)
    Dim i As Long
    Dim DailyPlan As New Collection
        
    If Target_Listview Is Nothing Then Set Target_Listview = AutoReportHandler.ListView_DailyPlan: Target_Listview.ListItems.Clear
    Set DailyPlan = FindFilesWithTextInName(Z_Directory.Source, "Excel_Export_")
    If DailyPlan.Count = 0 Then: If Handle Then MsgBox "연결된 주소에 DailyPlan 파일이 없음": Exit Sub
    
    With Target_Listview
        For i = 1 To DailyPlan.Count
AutoReportHandler.UpdateProgressBar AutoReportHandler.PB_BOM, (i - i / 2) / DailyPlan.Count * 100
            ' Dim vDate As String
            Dim DPCount As Long
            vDate = GetDailyPlanWhen(DailyPlan(i))
            If vDate = "It's Not a DailyPlan" Then GoTo SkipLoop
            DPCount = DPCount + 1
            With .ListItems.Add(, , vDate)
                .SubItems(1) = wLine
                .SubItems(2) = DailyPlan(i)
                .SubItems(3) = "Ready" 'Print
                .SubItems(4) = CheckFileAlreadyWritten_PDF("DailyPlan " & vDate & "_" & wLine, dc_DailyPlan) 'PDF
            End With
        .ListItems(DPCount).Checked = True ' 체크박스 체크
AutoReportHandler.UpdateProgressBar AutoReportHandler.PB_BOM, i / DailyPlan.Count * 100
SkipLoop:
        Next i
    End With
    
    If Handle Then MsgBox "일일생산계획서 " & DPCount & "장 연결완료"
End Sub
' 문서 자동화, 출력까지 한번에 실행하는 Sub
Public Sub Print_DailyPlan(Optional Handle As Boolean)
    Dim DPLV As ListView
    Dim DPitem As listItem
    Dim Chkditem As New Collection
    Dim i As Long, PaperCopies As Long, ListCount As Long
    Dim SavedPath As String
    Dim ws As Worksheet
    
    BoW = AutoReportHandler.Brightness
    Set Brush = New Painter
    
    PaperCopies = CInt(AutoReportHandler.DP_PN_Copies_TB.text)
    Set DPLV = AutoReportHandler.ListView_DailyPlan
    ListCount = DPLV.ListItems.Count: If ListCount = 0 Then MsgBox "연결된 데이터 없음": Exit Sub

    For i = 1 To ListCount ' 체크박스 활성화된 아이템 선별
        Set DPitem = DPLV.ListItems.Item(i)
        If DPitem.Checked Then Chkditem.Add DPitem.Index 'SubItems(1)
    Next i
    
    If Chkditem.Count < 1 Then MsgBox "선택된 문서 없음": Exit Sub
    
    ListCount = Chkditem.Count
    For i = 1 To ListCount
AutoReportHandler.UpdateProgressBar AutoReportHandler.PB_BOM, (i - 0.99) / ListCount * 100
        Set DPitem = DPLV.ListItems.Item(Chkditem(i))
        Set DP_Processing_WB = Workbooks.Open(DPitem.SubItems(2))
AutoReportHandler.UpdateProgressBar AutoReportHandler.PB_BOM, (i - 0.91) / ListCount * 100
        wLine = DPitem.SubItems(1) ' Line 이름 인계
        Set Target_WorkSheet = DP_Processing_WB.Worksheets(1): Set ws = Target_WorkSheet: Set Brush.DrawingWorksheet = Target_WorkSheet ' 워크시트 타게팅
        DP_Processing_WB.Windows(1).WindowState = xlMinimized ' 최소화
        AutoReport_DailyPlan DP_Processing_WB '자동화 서식작성 코드
AutoReportHandler.UpdateProgressBar AutoReportHandler.PB_BOM, (i - 0.87) / ListCount * 100
        If PrintNow.DailyPlan Then
            Printer.PrinterNameSet  ' 기본프린터 이름 설정, 유지되는지 확인
            ws.PrintOut ActivePrinter:=DefaultPrinter, From:=1, To:=2, copies:=PaperCopies
            DPitem.SubItems(3) = "Done" 'Print
        Else
            DPitem.SubItems(3) = "Pass" 'Print
        End If
AutoReportHandler.UpdateProgressBar AutoReportHandler.PB_BOM, (i - 0.73) / ListCount * 100
'저장을 위해 타이틀 수정
        Title = "DailyPlan " & DPLV.ListItems.Item(i).text & "_" & wLine
AutoReportHandler.UpdateProgressBar AutoReportHandler.PB_BOM, (i - 0.65) / ListCount * 100
'저장여부 결정
        SavedPath = SaveFilesWithCustomDirectory("DailyPlan", DP_Processing_WB, PS_DPforPDF(PrintArea), Title, True, True, OriginalKiller.DailyPlan)
AutoReportHandler.UpdateProgressBar AutoReportHandler.PB_BOM, (i - 0.45) / ListCount * 100
        DPitem.SubItems(4) = "Done" 'PDF
AutoReportHandler.UpdateProgressBar AutoReportHandler.PB_BOM, (i - 0.35) / ListCount * 100
        If MRB_DP Then Workbooks.Open (SavedPath & ".xlsx") ' 메뉴얼 모드일 때 열기
'Progress Update
AutoReportHandler.UpdateProgressBar AutoReportHandler.PB_BOM, i / ListCount * 100
    Next i
    
    If Handle Then MsgBox ListCount & "장의 DailyPlan 출력 완료"
    
End Sub
' 문서 서식 자동화

Private Sub AutoReport_DailyPlan(ByRef Wb As Workbook)
    ' 초기화 변수
    Set Target_WorkSheet = Wb.Worksheets(1)
    Set vCFR = New Collection
    
    Dim LastCol As Long, LastRow As Long ' DailyPlan 데이터가 있는 마지막 행
    Dim Begin As Range, Finish As Range
    
    SetUsingColumns vCFR ' 사용하는 열 선정
    AR_1_EssentialDataExtraction LastCol, LastRow  ' 필수데이터 추출
    Interior_Set_DailyPlan , LastRow, PrintArea ' Range 서식 설정
    AutoPageSetup Target_WorkSheet, PS_DailyPlan(PrintArea)  ' PrintPageSetup
    MarkingUp AR_2_ModelGrouping4 ' 모델 그루핑
    
    Set vCFR = Nothing
End Sub
Private Sub AR_1_EssentialDataExtraction(Optional ByRef LastCol As Long = 0, Optional ByRef LastRow As Long = 0) ' AutoReport 초반 설정 / 필수 데이터 영역만 추출함
    '月火水木金土日 요일입력 코드 月火水木金土日
    Dim i As Long, startRow As Long
    Dim DelCell As Range
    Dim CopiedData As New Collection ', TimeKeeper As New Collection
    Dim ws As Worksheet: Set ws = Target_WorkSheet
    
    Application.DisplayAlerts = False ' 경고문 비활성화
    
    ' 투입시점 시작시간 추출
    Set DelCell = ws.Cells.Find("Planned Start Time", lookAt:=xlWhole, MatchCase:=True) ' 투입시점 Range추출
    i = DelCell.Column: startRow = DelCell.Row + 3: LastRow = Target_WorkSheet.Cells(ws.Rows.Count, 1).End(xlUp).Row
    MergeDateTime_Flexible ws, i, 1, , startRow, "", "h:mm"
    
    ' 필요없는 행열 삭제/숨기기
    ws.Rows(1).Delete: ws.Columns("B:D").Delete ' 잉여 행열 삭제
    ws.Cells(1, 1).Value = "투입" & vbLf & "시점"
    LastCol = ws.Cells(1, ws.Columns.Count).End(xlToLeft).Column ' 데이터가 존재하는 마지막 열

    For i = LastCol To 2 Step -1
        Set DelCell = ws.Cells(2, i)
        ' 현재 열이 vCFR에 없거나, 숫자(날짜)면서 생산량이 0일 경우 삭제
        If Not IsInCollection(DelCell.Value, vCFR) Xor _
            (IsNumeric(DelCell.Value) And DelCell.Offset(1, 0).Value > 0) Then ws.Columns(i).Delete ' 숨기려면 .Hidden = True
    Next i
    ' 새로운 서식 적용을 위한 열 추가 및 수정작업
    Set DelCell = ws.Rows(2).Find(What:="W/O 계획수량", lookAt:=xlWhole)
    If DelCell Is Nothing Then Stop ' 오류나면 정지
    DelCell.Value = "계획" ' 원래의 열 제목이 너무 길어서 수정
    DelCell.Offset(0, 1).Value = "IN" ' 원래의 열 제목이 너무 길어서 수정
    DelCell.Offset(0, 2).Value = "OUT" ' 원래의 열 제목이 너무 길어서 수정
    startRow = DelCell.Offset(2, 0).Row ' StartRow 추출
    Set DelCell = DelCell.Offset(0, 3) ' 계획 셀에서 오른쪽으로 열이동 3번 하면 금일 날짜 나옴
    ws.Columns(DelCell.Column).Insert Shift:=xlShiftToRight, CopyOrigin:=xlFormatFromLeftOrAbove ' Connecter 2*2셀로 만듦
    ws.Columns(DelCell.Column).Insert Shift:=xlShiftToRight, CopyOrigin:=xlFormatFromLeftOrAbove
    DelCell.Offset(0, -1).Value = "Connecter"
    ws.Range(DelCell.Offset(-1, -2), DelCell.Offset(0, -1)).Merge
    
    Do Until ws.Cells(1, DelCell.Offset(0, 3).Column + 1).Value = ""
        ws.Columns(DelCell.Offset(0, 3).Column + 1).Delete ' D-day 기준, +3일까지 살리고 싸그리 삭제
    Loop
    LastCol = ws.Cells(1, ws.Columns.Count).End(xlToLeft).Column ' 데이터가 존재하는 마지막 열
    ws.Cells(2, LastCol + 1).Value = wLine & "-Line" ' 라인 데이터 기입
    ws.Range(ws.Cells(1, LastCol + 1), ws.Cells(2, LastCol + 2)).Merge
    
    LastCol = ws.Cells(1, ws.Columns.Count).End(xlToLeft).Column ' 마지막날짜 열 찾기
    LastRow = ws.Cells(ws.Rows.Count, LastCol - 1).End(xlUp).Row ' 마지막 날짜의 마지막 행 찾기
    Title = DelCell.Column ' D-Day 셀의 열 값을 Title변수로 옮김.
    
    Do Until ws.Cells(LastRow + 1, 2).Value = "" ' 마지막 밑으로 값이 있으면 삭제
        ws.Rows(LastRow + 1).Delete
    Loop
    
    Set DelCell = ws.Rows(2).Find(What:="계획", lookAt:=xlWhole)
    For i = 0 To 8
        If i <> 3 And i <> 4 Then
            DelCell.Offset(1, i).Formula = "=Sum(" & ws.Range(DelCell.Offset(2, i), ws.Cells(LastRow, DelCell.Offset(2, i).Column)).Address & ")"
            If DelCell.Offset(1, i).Value > 9999 Then DelCell.Offset(1, i).Value = Format(DelCell.Offset(1, i).Value / 1000, "0.0") & "k"
        End If
    Next i
    
    For i = startRow To LastRow
        ws.Cells(i, 20).Value = Time_Filtering(ws.Cells(i, 1).Value, ws.Cells(i + 1, 1).Value)
        ws.Cells(i, 21).Value = ws.Cells(i, 20).Value / ws.Cells(i, 4).Value
    Next i
    ws.Cells(2, 16).Value = "Meta_Data"
    Dim arr As Variant: arr = Array("3001", "2101", "2102", "3304", "TPL", "UPPH")
    For i = LBound(arr) To UBound(arr)
        ws.Cells(startRow - 1, 16 + i).Value = CStr(arr(i))
    Next i
    ws.Range(ws.Columns(20), ws.Columns(21)).NumberFormat = "[m]:ss"
    
    With ws.Range(ws.Cells(1, 1), ws.Cells(2, LastCol))
        .HorizontalAlignment = xlCenter
        .VerticalAlignment = xlCenter
    End With
    
    Application.DisplayAlerts = True ' 경고문 활성화
    
End Sub
' Grouping for each LOT Models
Public Function AR_2_ModelGrouping4(Optional ByRef startRow As Long = 4, Optional ByRef StartCol As Long = 3, Optional ByRef TargetWorkSheet As Worksheet, Optional MainOrSub As MorS = -1) As D_Maps
    Dim tWS As Worksheet: If TargetWorkSheet Is Nothing Then Set tWS = Target_WorkSheet Else Set tWS = TargetWorkSheet
    Dim Marker As New D_Maps
    Dim Checker As New ProductModel2
    Dim CurrRow As Long: CurrRow = startRow
    Dim StartRow_Prcss As Long: StartRow_Prcss = 0
    Dim EndRow As Long
    Dim LastRow As Long: LastRow = tWS.Cells(tWS.Rows.Count, StartCol).End(xlUp).Row
    Dim CriterionField As ModelinfoFeild

    Checker.SetModel tWS.Cells(CurrRow, StartCol), tWS.Cells(CurrRow + 1, StartCol)
    If MainOrSub = -1 Or MainOrSub = SubG Then
        Do While CurrRow <= LastRow + 1
            If StartRow_Prcss = 0 Then StartRow_Prcss = CurrRow
            If CurrRow <> startRow Then
                Checker.NextModel tWS.Cells(CurrRow + 1, StartCol)
            End If
    
            If Checker.Crr.Number <> Checker.Nxt.Number Then
                EndRow = CurrRow
                Marker.Set_Lot tWS.Cells(StartRow_Prcss, StartCol), tWS.Cells(EndRow, StartCol), SubG
                StartRow_Prcss = 0
            End If
            CurrRow = CurrRow + 1
        Loop
    End If
    If MainOrSub = -1 Or MainOrSub = MainG Then
        ' Main Group
        Dim vCurr As ModelInfo, vNext As ModelInfo
        CurrRow = 1: StartRow_Prcss = 0
    
        Do While CurrRow < Marker.Count(SubG)
            Set vCurr = Marker.Sub_Lot(CurrRow).info
            Set vNext = Marker.Sub_Lot(CurrRow + 1).info
    
            If StartRow_Prcss = 0 Then
                If Checker.Compare2Models(vCurr, vNext, mif_SpecNumber) Then
                    StartRow_Prcss = Marker.Sub_Lot(CurrRow).Start_R.Row
                    CriterionField = mif_SpecNumber
                ElseIf vCurr.Species <> "LS63" Then
                    If Checker.Compare2Models(vCurr, vNext, mif_TySpec) Then
                        StartRow_Prcss = Marker.Sub_Lot(CurrRow).Start_R.Row
                        CriterionField = mif_TySpec
                    ElseIf Checker.Compare2Models(vCurr, vNext, mif_Species) Then
                        StartRow_Prcss = Marker.Sub_Lot(CurrRow).Start_R.Row
                        CriterionField = mif_Species
                    End If
                End If
            ElseIf Not Checker.Compare2Models(vCurr, vNext, CriterionField) Then
                EndRow = Marker.Sub_Lot(CurrRow).End_R.Row
                Marker.Set_Lot tWS.Cells(StartRow_Prcss, StartCol), tWS.Cells(EndRow, StartCol)
                StartRow_Prcss = 0
            End If
            CurrRow = CurrRow + 1
        Loop
    End If
    Set AR_2_ModelGrouping4 = Marker
End Function
Private Sub MarkingUp(ByRef Target As D_Maps)
    Dim i As Long
    Set Brush.DrawingWorksheet = Target_WorkSheet
    
    For i = 1 To Target.Count(SubG) ' SubGroups 윗라인 라이닝
        With ForLining(Target.Sub_Lot(i).Start_R, Row).Borders(xlEdgeTop)
            .LineStyle = xlContinuous
            .Weight = xlThin
        End With
    Next i
    
    With Target
        For i = .Count(SubG) To 1 Step -1 ' Sub Group Stamp it
            With .Sub_Lot(i)
                Brush.Stamp_it_Auto SetRangeForDraw(Target_WorkSheet.Range(.Start_R, .End_R)), dsRight, True
            End With
            .Remove i, SubG
        Next i
        
        For i = .Count(MainG) To 1 Step -1   ' Main Group Stamp it
            With .Main_Lot(i)
                Brush.Stamp_it_Auto SetRangeForDraw(Target_WorkSheet.Range(.Start_R, .End_R))
            End With
            .Remove i, MainG
        Next i
    End With
    
End Sub

Private Sub SetUsingColumns(ByRef UsingCol As Collection) ' 살릴 열 선정
    UsingCol.Add "W/O"
    UsingCol.Add "부품번호"
    UsingCol.Add "W/O 계획수량"
    UsingCol.Add "W/O Input"
    UsingCol.Add "W/O실적"
End Sub

Private Sub Interior_Set_DailyPlan(Optional ByRef FirstRow As Long = 3, Optional LastRow As Long, Optional ByRef PR As Range)
    
    Dim ws As Worksheet: Set ws = Target_WorkSheet
    Dim SetEdge(1 To 6) As XlBordersIndex
    Dim colWidth As New Collection
    Dim i As Long, LastCol As Long
    
    Set PR = ws.Cells(1, 1).CurrentRegion
    
    SetEdge(1) = xlEdgeLeft
    SetEdge(2) = xlEdgeRight
    SetEdge(3) = xlEdgeTop
    SetEdge(4) = xlEdgeBottom
    SetEdge(5) = xlInsideHorizontal
    SetEdge(6) = xlInsideVertical
    
    LastCol = ws.Cells(1, ws.Columns.Count).End(xlToLeft).Column ' 데이터가 존재하는 마지막 열
    With ws.Range(ws.Cells(1, 1), ws.Cells(2, LastCol)) ' 제목부분 interior
        .WrapText = True
        .Interior.Color = RGB(199, 253, 240)
        .Font.Bold = True
    End With
    
    If Yoil_DP Then
        Dim Target As Range
        Set Target = ws.Range("2:2").Find(What:="계획", LookIn:=xlValues, lookAt:=xlWhole).Offset(0, 5)
        For i = 0 To 3
            DecodeDate Target.Offset(0, i)
        Next i
    End If
    
    With PR ' PrintRange 인쇄영역의 인테리어 세팅
        .Font.Name = "LG스마트체2.0 Regular"
        .Font.Size = 12
        
        For i = LBound(SetEdge) To UBound(SetEdge)
            With .Borders(SetEdge(i))
                .LineStyle = xlContinuous
                .Color = RGB(0, 0, 0)
                .Weight = xlThin
            End With
        Next i
        
        .Rows.rowHeight = 15.75 ' 행 높이 지정
    End With
    
    'Connecter Col 7, 8 / Finish Line Col 13, 14
    'Need a Sub for Search Connecter and Finish Line automatical
    
    Dim tempRange As Range, xCell As Range
    Dim arrr(1 To 2) As Long
    Dim ACol As Variant
    
    arrr(1) = 7 ' Connecter Col is 7
    arrr(2) = 13 ' Finish_Line Col is 13
    
    For i = FirstRow To LastRow ' Connecter, FinishLine 중간선 삭제 코드
        For Each ACol In arrr
            With ws
                Set tempRange = .Range(.Cells(i, ACol), .Cells(i, ACol + 1))
                tempRange.Borders(xlInsideVertical).LineStyle = xlNone
            End With
        Next ACol
    Next i
    
    ACol = ws.Range("1:2").Find(What:="Line", LookIn:=xlValues, lookAt:=xlPart).Column
    Set tempRange = ws.Range(ws.Cells(FirstRow + 1, 1), ws.Cells(LastRow, ACol + 1))
    
    With tempRange.Borders(xlInsideHorizontal)
        .LineStyle = xlDot
        .Weight = xlHairline
    End With
    
    For Each xCell In tempRange
        If xCell.Value = "" Then xCell.Interior.Color = RGB(BoW, BoW, BoW) ' Brightness
    Next xCell
    
' 열 너비 지정
    colWidth.Add 6.5   ' 투입시점 10.1
    colWidth.Add 13   ' W/O 11
    colWidth.Add 28   ' 부품번호 27
    colWidth.Add 6  ' 수량/계획
    colWidth.Add 6  ' 수량/IN
    colWidth.Add 6  ' 수량/OUT
    colWidth.Add 7.5  ' Connect_1 Connect Width = 13.5 // 6.5
    colWidth.Add 6  ' Connect_2 ' Day 너비랑 맞춰야함
    colWidth.Add 6 ' D-Day
    colWidth.Add 6 ' D+1
    colWidth.Add 6 ' D+2
    colWidth.Add 6 ' D+3
    colWidth.Add 6 ' Finish_Line_1 Finish Line Width = 12.5
    colWidth.Add 6.5 ' Finish_Line_2
    
    'DayColumn = 6
    
    For i = 1 To colWidth.Count
    ws.Columns(i).ColumnWidth = colWidth(i)
    Next i
End Sub

Private Function GetDailyPlanWhen(DailyPlanDirectiory As String) As String
    ' Excel 애플리케이션을 새로운 인스턴스로 생성
    Dim xlApp As Excel.Application: Set xlApp = New Excel.Application: xlApp.Visible = False
    Dim Wb As Workbook: Set Wb = xlApp.Workbooks.Open(DailyPlanDirectiory) ' 워크북 열기
    Dim ws As Worksheet: Set ws = Wb.Worksheets(1) ' 워크시트 선택

    ' 값을 읽어오기
    Dim col(1 To 2) As Long, smallestValue As Long: smallestValue = 31
    Dim cell As Range, Finder As Range
        
    For Each Finder In ws.Rows(2).Cells ' DP에서 날짜를 찾는 줄
        If Finder.Value Like "*월" And Finder.Offset(2, 0).Value > 0 Then Set cell = Finder: Exit For
        col(1) = col(1) + 1: If col(1) > 70 Then Exit For
    Next Finder
    If cell Is Nothing Then GetDailyPlanWhen = "It's Not a DailyPlan": GoTo NAD ' 열람한 문서가 DailyPlan이 아닐시 오류처리 단
    Title = cell.Value ' 생산 월
    col(1) = cell.MergeArea.Cells(1, 1).Column: col(2) = cell.MergeArea.Cells(1, cell.MergeArea.Columns.Count).Column ' 생산 일 Range 지정을 위한 열 값 추적
    For Each cell In ws.Range(ws.Cells(3, col(1)), ws.Cells(3, col(2)))
        If IsNumeric(cell.Value) And cell.Offset(1, 0).Value > 0 And cell.Value < smallestValue Then smallestValue = cell.Value
    Next cell
    Title = Title & "-" & smallestValue & "일" ' Title = *월-*일
    GetDailyPlanWhen = Title ' 날짜형 제목값 인계
    Title = smallestValue ' 날짜값
    Set cell = ws.Rows("2:3").Find(What:="생산 라인", lookAt:=xlWhole, LookIn:=xlValues)
    wLine = cell.Offset(2, 0).Value
NAD:
    Wb.Close SaveChanges:=False: Set Wb = Nothing ' 워크북 닫기
    xlApp.Quit: Set xlApp = Nothing ' Excel 애플리케이션 종료
End Function

Public Sub MMG_Do() ' Manual Model Grouping
    Dim CritR As Range ' Criterion Range
    Dim ws As Worksheet ' Worksheet
    Dim CritCol As Long ' Criterion Column
    If Brush Is Nothing Then Set Brush = New Painter
    If vCFR Is Nothing Then Set vCFR = New Collection
    
    On Error Resume Next
        Set ws = DP_Processing_WB.Worksheets(1) ' 연산 완료된 워크시트 우선 참조
        If Err.Number <> 0 Then
            Set ws = ActiveWorkbook.ActiveSheet ' 워크시트 참조 실패시 활성화 워크시트 참조
            Set CritR = ws.Range(Selection.Address) ' 모델번호 영역 참조
            Err.Clear
        Else
            Set CritR = ws.Range(Selection.Address) ' 워크시트 참조 성공 시 모델번호 영역 참조
        End If
    On Error GoTo 0
    Set Brush.DrawingWorksheet = ws
    
    CritCol = ws.Cells.Find("부품번호", lookAt:=xlWhole, MatchCase:=True).Column
    If CritR.Column <> CritCol Then MsgBox ("잘못된 참조"): Exit Sub
    
    Brush.Stamp_it_Auto SetRangeForDraw(CritR), CollectionForUndo:=vCFR
End Sub

Public Sub MMG_Undo() ' Manual Model Grouping
    If vCFR Is Nothing Or vCFR.Count = 0 Then MsgBox "로딩된 데이터 없음", vbDefaultButton4: Exit Sub
    vCFR.Item(vCFR.Count).Delete
    vCFR.Remove (vCFR.Count)
End Sub

Public Sub Re_Grouping()
    Set Target_WorkSheet = Selection.Worksheet
    Set Brush.DrawingWorksheet = Target_WorkSheet
    Brush.DeleteShapes
    Dim CriterionCell As Range: Set CriterionCell = Target_WorkSheet.Rows("1:10").Find("계획", lookAt:=xlWhole, MatchCase:=True)
    Dim CritRow As Long, CritCol As Long: CritRow = CriterionCell.Row + 2: CritCol = CriterionCell.Column - 1
    MarkingUp AR_2_ModelGrouping4(CritRow, CritCol, Target_WorkSheet)
End Sub

Private Function SetRangeForDraw(ByRef Criterion_Target As Range) As Range
    Dim ws As Worksheet
    Dim FirstCol As Long, LastCol As Long, FirstRow As Long, LastRow As Long ' (First, Last)*(Col, Row)
    Set ws = Criterion_Target.Worksheet
    Utillity.GetRangeBoundary Criterion_Target, _
                                    FirstRow, LastRow, _
                                    FirstCol, LastCol
    LastCol = FirstCol + 6 ' 6개 열 이동
    Set SetRangeForDraw = ws.Range(ws.Cells(FirstRow, LastCol), ws.Cells(LastRow, LastCol + 3))
    'Debug.Print "SetRangeForDraw : " & SetRangeForDraw.Address
End Function

Private Sub DecodeDate(ByRef Target As Range)
    On Error GoTo Endproc
    Dim ws As Worksheet: Set ws = Target.Worksheet
    Dim vDD As Long, LastRow As Long
    Dim WK As String, DT As Date
    
    LastRow = ws.Cells(ws.Rows.Count, Target.Column).End(xlUp).Row ' 날짜 셀의 마지막 행 추적
    DT = Int(CDate(ws.Cells(LastRow, 1).Value)) ' 마지막 행의 첫 열이 날짜 데이터가 있는 셀임
    vDD = CInt(Target.Value) ' 일단 고유 날짜값 저장
    If Day(DT) <> vDD Then DT = DateSerial(Year(DT), Month(DT), vDD) ' 실제날짜값(DT)과 명시된날짜값(vDD)를 비교후 명시된 날짜값을 추종하도록 설계
    WK = WeekdayKorean(DT) ' 요일 변환
    Target.Value = DT ' 최종값 전달
    Target.NumberFormat = "d aaa" ' 날짜와 요일이 기재되도록 포맷
    With Target
        .WrapText = False ' 줄바꿈 False
        .ShrinkToFit = True ' 셀에 맞춤 True
        .HorizontalAlignment = xlCenter ' 가로 중앙정렬
        .VerticalAlignment = xlCenter ' 세로 중앙정렬
        Select Case WK
            Case Is = "토"
                .Interior.Color = RGB(150, 200, 255)
            Case Is = "일"
                .Interior.Color = RGB(255, 150, 200)
            Case Else
                .Interior.Color = RGB(150, 255, 200)
        End Select
    End With
Endproc:
End Sub

