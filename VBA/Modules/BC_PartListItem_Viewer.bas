Attribute VB_Name = "BC_PartListItem_Viewer"
Option Explicit

Private Enum CollectorTypes
    Unique = -3
    Duplicate = -6
End Enum

Public MRB_PL As Boolean ' Manual_Reporting_Bool_PartList

Private BoW As Single ' Black or White
Private PL_Processing_WB As New Workbook ' 모듈내 전역변수로 선언함
Private Target_WorkSheet As New Worksheet
Private PrintArea As Range, CopiedArea As Range  ' 모듈내 프린트영역
Private Brush As New Painter
Private Title As String, wLine As String
Private DayCount As Long, HeaderCol As Long
Private vCFR As Collection  ' Columns For Report
Private vVenderVisible As Boolean

Public Sub Read_PartList(Optional Handle As Boolean, Optional ByRef Target_Listview As ListView)
    Dim i As Long
    Dim PartList As New Collection
        
    If Target_Listview Is Nothing Then Set Target_Listview = UI.ListView_PartList_items: Target_Listview.ListItems.Clear
    Set PartList = FindFilesWithTextInName(Z_Directory.Source, "Excel_Export_")
    If PartList.Count = 0 Then: If Handle Then MsgBox "연결된 주소에 PartList 파일이 없음": Exit Sub
    
    With Target_Listview
        For i = 1 To PartList.Count
UI.UpdateProgressBar UI.PB_BOM, (i - i / 3) / PartList.Count * 100
            Dim vDate As String
            Dim PLCount As Long
            vDate = GetPartListWhen(PartList(i), DayCount)
            If vDate = "It's Not a PartList" Then GoTo SkipLoop
            PLCount = PLCount + 1
            With .ListItems.Add(, , vDate)
                .SubItems(1) = wLine
                .SubItems(2) = PartList(i)
                .SubItems(3) = "Ready" 'Print
                .SubItems(4) = CheckFileAlreadyWritten_PDF(vDate, dc_PartList) 'PDF
            End With
        .ListItems(PLCount).Checked = True ' 체크박스 체크
UI.UpdateProgressBar UI.PB_BOM, i / PartList.Count * 100

    With UI.ListView_PLfF_item.ListItems ' User interface ListVeiw input
        .Clear
        Dim r As Long
        For r = 1 To vCFR.Count
            .Add r, vCFR(r), vCFR(r)
        Next r
    End With
SkipLoop:
        Next i
    End With
    
    If Handle Then MsgBox "PartList " & PLCount & "장 연결완료"
End Sub
' 문서 자동화, 출력까지 한번에 실행하는 Sub
Public Sub Print_PartList(Optional Handle As Boolean)
    Dim PLLV As ListView, PLitem As listItem
    Dim Chkditem As New Collection
    Dim PaperCopies As Long, ListCount As Long, i As Long
    Dim SavedPath As String
    Dim ws As Worksheet
    
    BoW = UI.Brightness
    If UI.CB_PL_Ddays.value Then DayCount = UI.PL_Ddays_TB.text Else DayCount = 4
    Set Brush = New Painter
    
    PaperCopies = CInt(UI.PL_PN_Copies_TB.text)
    Set PLLV = UI.ListView_PartList_items
    ListCount = PLLV.ListItems.Count: If ListCount = 0 Then MsgBox "연결된 데이터 없음": Exit Sub

    For i = 1 To ListCount ' 체크박스 활성화된 아이템 선별
        Set PLitem = PLLV.ListItems.Item(i)
        If PLitem.Checked Then Chkditem.Add PLitem.Index 'SubItems(1)
    Next i
    
    If Chkditem.Count < 1 Then MsgBox "선택된 문서 없음": Exit Sub ' 선택된 문서가 없을 시 즉시 종료
    
    ListCount = Chkditem.Count
    For i = 1 To ListCount
UI.UpdateProgressBar UI.PB_BOM, (i - 0.99) / ListCount * 100
        Set PLitem = PLLV.ListItems.Item(Chkditem(i))
        Set PL_Processing_WB = Workbooks.open(PLitem.SubItems(2))
UI.UpdateProgressBar UI.PB_BOM, (i - 0.91) / ListCount * 100
        wLine = PLitem.SubItems(1) ' Line 이름 인계
        Set Target_WorkSheet = PL_Processing_WB.Worksheets(1): Set ws = Target_WorkSheet: Set Brush.DrawingWorksheet = Target_WorkSheet ' 워크시트 타게팅
        PL_Processing_WB.Windows(1).WindowState = xlMinimized ' 최소화
        AutoReport_PartList PL_Processing_WB '자동화 서식작성 코드
UI.UpdateProgressBar UI.PB_BOM, (i - 0.87) / ListCount * 100
        If PrintNow.PartList Then
            Printer.PrinterNameSet  ' 기본프린터 이름 설정, 유지되는지 확인
            ws.PrintOut ActivePrinter:=DefaultPrinter, From:=1, to:=2, copies:=PaperCopies
            PLitem.SubItems(3) = "Done" 'Print
        Else
            PLitem.SubItems(3) = "Pass" 'Print
        End If
UI.UpdateProgressBar UI.PB_BOM, (i - 0.73) / ListCount * 100
'저장을 위해 타이틀 수정
        Title = "PartList " & PLLV.ListItems.Item(Chkditem(i)).text & "_" & wLine
UI.UpdateProgressBar UI.PB_BOM, (i - 0.65) / ListCount * 100
'저장여부 결정
        SavedPath = SaveFilesWithCustomDirectory("PartList", PL_Processing_WB, Printer.PS_PartList(PrintArea), Title, True, False, OriginalKiller.PartList)
UI.UpdateProgressBar UI.PB_BOM, (i - 0.45) / ListCount * 100
        PLitem.SubItems(4) = "Done" 'PDF
UI.UpdateProgressBar UI.PB_BOM, (i - 0.35) / ListCount * 100
        If MRB_PL Then
            Dim tWB As Workbook, Target As Range
            Set tWB = Workbooks.open(SavedPath & ".xlsx")  ' 메뉴얼 모드일 때 열기
            Set Target = tWB.Worksheets(1).Rows(1).Find("-Line", lookAt:=xlPart, LookIn:=xlValues).Offset(1, 1)
            tWB.Worksheets(1).Activate
            Target.Select
            ActiveWindow.FreezePanes = True
        End If
'Progress Update
UI.UpdateProgressBar UI.PB_BOM, i / ListCount * 100
    Next i
        
    If Handle Then MsgBox ListCount & "장의 PartList 출력 완료"
    
End Sub
' 문서 서식 자동화
Private Sub AutoReport_PartList(ByRef Wb As Workbook)
    ' 초기화 변수
    Set Target_WorkSheet = Wb.Worksheets(1)
    Set vCFR = New Collection
    
    Dim i As Long, DrawingMap As D_Maps  ', LastRow As Long ' DailyPlan 데이터가 있는 마지막 행
    
    SetUsingColumns vCFR ' 사용하는 열 선정
    AR_1_EssentialDataExtraction ' 필수데이터 추출
    Interior_Set_PartList ' Range 서식 설정
    AutoPageSetup Target_WorkSheet, Printer.PS_PartList(PrintArea)   ' PrintPageSetup
    Set DrawingMap = AR_2_ModelGrouping4(2, , Target_WorkSheet, SubG)
'    MarkingUP_items DrawingMap
    MarkingUP_PL DrawingMap
    
    Set vCFR = Nothing
End Sub

Private Sub SetUsingColumns(ByRef UsingCol As Collection) ' 살릴 열 선정
    UsingCol.Add "투입" & vbLf & "시점"
    UsingCol.Add "W/O"
    UsingCol.Add "모델"
    UsingCol.Add "Suffix"
    UsingCol.Add "계획 수량"
    UsingCol.Add "Tool"
End Sub

Private Sub AR_1_EssentialDataExtraction() ' AutoReport 초반 설정 / 필수 데이터 영역만 추출함
    Dim i As Long, c As Long, vStart As Long, vEnd As Long, CritCol(1 To 2) As Long
    Dim vCell As Range
    Dim ws As Worksheet: Set ws = Target_WorkSheet
    
    Application.DisplayAlerts = False ' initializing
    
    vStart = 2: vEnd = ws.Cells(ws.Rows.Count, 1).End(xlUp).Row
    ' D-Day 포함 N일치 데이터만 처리하도록 강제함.
    CritCol(1) = ws.Rows(1).Find("YYYYMMDD", lookAt:=xlWhole, LookIn:=xlValues).Column
    CritCol(2) = ws.Rows(1).Find("Input Time", lookAt:=xlWhole, LookIn:=xlValues).Column
    MergeDateTime_Flexible ws, CritCol(1), 1, CritCol(2), , "투입" & vbLf & "시점", "d-hh:mm"
    CritCol(2) = 0
    For i = vStart To vEnd
        If CritCol(2) >= DayCount Or vEnd = i Then '<- N위치
            vStart = i: Exit For
        ElseIf ws.Cells(i, CritCol(1)).value <> ws.Cells(i + 1, CritCol(1)).value Then
            CritCol(2) = CritCol(2) + 1
        End If
    Next i
    ws.Rows(vStart & ":" & vEnd).Delete ' 불필요한 행 삭제처리
    vEnd = vStart - 1: vStart = 2
    
    CritCol(1) = ws.Rows(1).Find("잔량", lookAt:=xlWhole, LookIn:=xlValues).Column
    For i = CritCol(1) To 1 Step -1 ' 불필요한 열 삭제처리
        If Not IsInCollection(ws.Cells(1, i).value, vCFR) Then ws.Columns(i).Delete
    Next i
    CritCol(1) = ws.Rows(1).Find("모델", lookAt:=xlWhole, LookIn:=xlValues).Column
    
    For i = vStart To vEnd ' 제목 외 데이터 행 시작부터 끝까지 데이터 처리
        Set vCell = ws.Cells(i, CritCol(1))
        vCell.value = vCell.value & "." & vCell.Offset(0, 1).value ' 모델, Suffix 열 병합
    Next i
    ws.Columns(vCell.Offset(0, 1).Column).Delete ' 불필요한 열 삭제처리
    CritCol(1) = ws.Rows(1).Find("계획 수량", lookAt:=xlWhole, LookIn:=xlValues).Offset(0, 1).Column
    CritCol(2) = ws.Cells(1, ws.Columns.Count).End(xlToLeft).Column
    PartCombine ws.Range(ws.Cells(1, CritCol(1)), ws.Cells(1, CritCol(2))), vStart, vEnd ' 단일 파트명으로 병합처리
    DeleteDuplicateRowsInColumn (CritCol(1) - 4), vStart, vEnd, ws ' WorkOrder 중복 행 제거
    For Each vCell In ws.Range(ws.Cells(1, CritCol(1)), ws.Cells(1, CritCol(2))) ' 제목 열 수정
        vCell.value = Replace(ExtractBracketValue(vCell.value), "_", vbLf)
    Next vCell
    Replacing_Parts ws.Range(ws.Cells(vStart, CritCol(1)), ws.Cells(vEnd, CritCol(2)))
    ws.Cells(1, 5).value = "수량" ' 제목열 정리
    ws.Columns(6).Insert: ws.Columns(6).Insert
    ws.Cells(1, 6).value = wLine & "-Line": ws.Cells(1, 6).Resize(1, 2).Merge
    ws.Name = "PartList_Total"
    'Set CopiedArea = ws.Range(ws.Cells(1, 1), ws.Cells(vEnd, 7)) ' 카피드에리어 설정단, 동적 추적으로 바꿀 것.
    If PL_Processing_WB.Worksheets.Count > 1 Then PL_Processing_WB.Worksheets(2).Delete
    
    ' If (Something...) Then EPLU vStart, vEnd ' Each Part ListUp

    'FPLU ' Feeder's Part ListUp
    Application.DisplayAlerts = True ' Terminate
End Sub
Private Sub EPLU(ByRef Collected_item As Collection) ' Each Part ListUp
    
End Sub
Private Sub FPLU(ByRef Collected_item As Collection) ' Feeder's Part ListUp
    
End Sub
Private Sub Interior_Set_PartList(Optional ws As Worksheet)
    
    If ws Is Nothing Then Set ws = Target_WorkSheet
    Dim SetEdge(1 To 6) As XlBordersIndex
    Dim colWidth As New Collection
    Dim i As Long, FirstRow As Long, LastRow As Long: FirstRow = ws.Rows(1).Find("모델", lookAt:=xlWhole, LookIn:=xlValues).Row + 1: LastRow = ws.Cells(ws.Rows.Count, 1).End(xlUp).Row
    Dim xCell As Range, Target As Range: Set PrintArea = ws.Cells(1, 1).CurrentRegion: Set Target = PrintArea
    
    SetEdge(1) = xlEdgeLeft
    SetEdge(2) = xlEdgeRight
    SetEdge(3) = xlEdgeTop
    SetEdge(4) = xlEdgeBottom
    SetEdge(5) = xlInsideVertical
    SetEdge(6) = xlInsideHorizontal
    
    For Each xCell In Target
        With xCell
            If .value = "" Then .Interior.Color = RGB(BoW, BoW, BoW) ' Brightness
            .HorizontalAlignment = xlLeft
            .VerticalAlignment = xlCenter
        End With
    Next xCell
    
    ws.Columns(1).HorizontalAlignment = xlRight ' 1번째 열 우측정렬
    ws.Columns("B:D").HorizontalAlignment = xlLeft ' 2,3,4번째 열 좌측정렬
    ws.Columns(5).HorizontalAlignment = xlRight ' 5번째 열 우측정렬
    ws.Rows(1).HorizontalAlignment = xlCenter  ' 1번째 행 중앙정렬
    
    With Target ' PrintArea(index) = Target 인쇄영역의 인테리어 세팅
        .Rows(1).Interior.Color = RGB(133, 233, 233)
        .Rows(1).Font.Bold = True
        .Font.Name = "LG스마트체2.0 Regular"
        .Font.Size = 12
        
        For i = 1 To 5
            With .Borders(SetEdge(i))
                .LineStyle = xlContinuous
                .Color = RGB(0, 0, 0)
                .Weight = xlThin
            End With
        Next i
        
        With .Borders(xlInsideHorizontal)
            .LineStyle = xlDot
            .Weight = xlHairline
        End With
        
        .ColumnWidth = 150
        .WrapText = True
        .EntireColumn.AutoFit
        .EntireRow.AutoFit
    End With
    
    With ws
        .Columns(6).ColumnWidth = 6: .Columns(7).ColumnWidth = 7
        .Columns(6).Borders(xlEdgeRight).LineStyle = xlNone
        .Columns(.Rows(1).Find("Tool", lookAt:=xlWhole, LookIn:=xlValues).Column).Hidden = True ' Tool 열은 숨김처리
        For i = FirstRow To LastRow
            If isDayDiff(ws.Cells(i, 1), ws.Cells(i - 1, 1)) Then .Rows(i).Borders(xlEdgeTop).LineStyle = xlDash: .Rows(i).Borders(xlEdgeTop).Weight = xlMedium
        Next i
    End With
    
End Sub

Private Function GetPartListWhen(PartListDirectiory As String, Optional ByRef DDC As Long) As String
    ' Excel 애플리케이션을 새로운 인스턴스로 생성
    Dim xlApp As Excel.Application: Set xlApp = New Excel.Application: xlApp.Visible = False
    Dim Wb As Workbook: Set Wb = xlApp.Workbooks.open(PartListDirectiory) ' 워크북 열기
    Dim ws As Worksheet: Set ws = Wb.Worksheets(1) ' 워크시트 선택
    Dim cell As Range, SC As Long, EC As Long, i As Long
        
    Set cell = ws.Rows(1).Find(What:="YYYYMMDD", lookAt:=xlWhole, LookIn:=xlValues) ' PL에서 날짜를 찾는 줄
    If cell Is Nothing Then GetPartListWhen = "It's Not a PartList": GoTo NAP ' 열람한 문서가 PartList가 아닐시 오류처리 단
    DDC = -1 ' 마지막 값 선보정
    SC = cell.Row + 1: EC = ws.Cells(ws.Rows.Count, cell.Column).End(xlUp).Row
    For i = SC To EC
        If ws.Cells(i, cell.Column).value <> ws.Cells(i + 1, cell.Column).value Then DDC = DDC + 1
    Next i
    UI.PL_Ddays_Counter.Max = DDC
    Title = cell.Offset(1, 0).value ' YYYYMMDD 포맷된 날짜값 인계
    Title = mid(Title, 5, 2) & "월-" & mid(Title, 7, 2) & "일"
    GetPartListWhen = Title ' 날짜형 제목값 인계
    wLine = ws.Rows(1).Find(What:="Line", lookAt:=xlWhole, LookIn:=xlValues).Offset(1, 0).value ' 라인 값 추출
    SC = ws.Rows(1).Find(What:="잔량", lookAt:=xlWhole, LookIn:=xlValues).Offset(0, 1).Column + 1
    EC = ws.Cells(1, ws.Columns.Count).End(xlToLeft).Column
    Set cell = ws.Range(ws.Cells(1, SC), ws.Cells(1, EC))
    Set vCFR = New Collection: Set vCFR = PartCollector(cell, CollectionType:=Unique)
    
NAP:
    Wb.Close SaveChanges:=False: Set Wb = Nothing ' 워크북 닫기
    xlApp.Quit: Set xlApp = Nothing ' Excel 애플리케이션 종료
End Function
Private Function PartCollector(ByRef PartNamesArea As Range, _
    Optional ByRef UniqueParts As Collection, Optional ByRef Duplicated As Collection, _
    Optional CollectionType As CollectorTypes = 0) As Collection
    
    Dim ws As Worksheet: Set ws = PartNamesArea.Worksheet
    Dim cell As Range
    Dim BracketVal As String, i As Long
    
     If UniqueParts Is Nothing Or Duplicated Is Nothing Then Set UniqueParts = New Collection: Set Duplicated = New Collection
    
    ' 부품명 수집 반복문
    On Error Resume Next
    For Each cell In PartNamesArea
        BracketVal = ExtractBracketValue(cell.value)
            For i = 1 To UniqueParts.Count
                If BracketVal = UniqueParts(i) Then Duplicated.Add BracketVal, BracketVal ' 중복되는 부품명 수집
            Next i
        If BracketVal <> "" Then UniqueParts.Add BracketVal, BracketVal ' 고유 부품명 수집
    Next cell
    On Error GoTo 0
    
    ' 매개변수가 존재할 때만 Function으로서 기능함
    Select Case CollectionType
        Case 0: Set PartCollector = Nothing
        Case -3: Set PartCollector = UniqueParts
        Case -6: Set PartCollector = Duplicated
    End Select
End Function

Private Sub Replacing_Parts(ByRef RangeTarget As Range) ' RpP
    ' 범위 내 각 셀에서 Vender와 Parts 정보를 추출 및 정리하여 셀 값을 재구성합니다.
   
    Dim vCell As Range, ws As Worksheet: Set ws = RangeTarget.Worksheet
    Dim i As Long, Start_P As Long, End_P As Long, Searching As Long
    Dim vVender As Collection, vParts As Collection
    Dim sVender As String, sParts As String, Target As String, Duplicated As Boolean
    Dim removeList As Variant
    removeList = Array("(주)", "㈜", "EKHQ_", " Co., Ltd.", " LTD", " CO,", " CO.,LTD")
   
    ' 대상 셀을 하나씩 순회
    For Each vCell In RangeTarget
        If Len(vCell.value) > 0 Then ' 빈 셀 무시
            Target = vCell.value ' 원본 텍스트 추출
           
            ' 제거 대상 문자열 치환
            For i = LBound(removeList) To UBound(removeList)
                Target = Replace(Target, removeList(i), "")
            Next i
            Target = Replace(Target, vbLf & "[", " [") ' 줄바꿈 뒤 괄호를 공백으로 정리하여 포맷 유지
            If Right$(Target, 1) = vbLf Then Target = Left$(Target, Len(Target) - 1) ' 마지막에 줄바꿈 문자 있을 경우 제거
           
            ' Collection 초기화
            Set vVender = New Collection: Set vParts = New Collection
            Start_P = 0: End_P = 0: Searching = 0
           
            ' 벤더와 파츠 추출 반복
            Do
                Searching = End_P + 1: Duplicated = False ' 탐색 시작 위치 초기화
                sVender = ExtractBracketValue(Target, Searching) ' 괄호 안의 벤더 추출
               
                 ' 셀값의 Char가 1자 미만 Or 셀값의 Char가 5자 이상이면서 동시에 영문 벤더일 경우 = "도입품"으로 대체
                 If Len(sVender) < 1 Or (Len(sVender) >= 5 And Not sVender Like "*[가-힣]*") Then sVender = "도입품"
                
                 ' 이미 등록된 벤더인지 중복 검사
                 For i = 1 To vVender.Count
                     If vVender(i) = sVender Then
                         Duplicated = True
                         Exit For ' 중복 확인되면 루프 종료
                     End If
                 Next i
                
                 If Not Duplicated Then vVender.Add sVender, sVender ' 중복 아니면 벤더 등록
                 Start_P = Searching + 2: End_P = InStr(Start_P, Target, " [") - 1 ' 다음 파츠 범위 설정
                 If End_P < Start_P Then End_P = Len(Target) ' 마지막 파츠 처리
                 sParts = mid$(Target, Start_P, End_P - Start_P + 1) ' 파츠 문자열 추출
                
                 ' 파츠 추가 또는 병합 처리
                 On Error Resume Next ' 키 중복 오류 방지
                     If Not Duplicated Then
                         vParts.Add sParts, sVender
                     Else
                         sParts = vParts(sVender) & "/" & sParts ' 병합
                         vParts.Remove sVender
                         vParts.Add sParts, sVender
                     End If
                 On Error GoTo 0 ' 오류 처리 복구
            Loop Until End_P >= Len(Target) ' 전체 문자열 끝까지 반복
            
            ' 예외처리
            If ws.Cells(1, vCell.Column).value = "Burner" Then
                Select Case Target
                Case Is = "[기미] 4102/4202/4402/4502" ' Signature
                    Target = "Oval, Best"
                Case Is = "[기미] 4102/4202/4402/4502 [SABAF S.P.A.] 6904/7302" ' Best
                    Target = "Oval, Best, Sabaf"
                Case Is = "[기미] 4102/4202/4402/4502(2)" ' Better
                    Target = "Oval, Better"
                Case Else ' 에러처리
                    Target = "Matching Error Plz Chk Sub RpP"
                End Select
            Else
                ' 일반처리 / 최종 문자열 재구성
                Target = ""
                For i = 1 To vVender.Count
                    Target = Target & " [" & vVender(i) & "] " & vParts(vVender(i))
                Next i
            End If

            vCell.value = Trim(Target) ' 결과를 셀에 기록
        End If
    Next vCell
End Sub

Private Sub PartCombine(ByRef PartNamesArea As Range, ByVal rStart As Long, ByVal rEnd As Long)
    Dim ws As Worksheet: Set ws = PartNamesArea.Worksheet
    Dim UniqueParts As New Collection, Duplicated As New Collection
    Dim cell As Range
    Dim BracketVal As String
    Dim i As Long, CBTr As Long

    PartCollector PartNamesArea, UniqueParts, Duplicated
    
    ' 전역변수로 컬렉션 깊은 복사 / 전역변수로 보내기 위한 복사
    Set vCFR = New Collection
    For i = 1 To UniqueParts.Count
        vCFR.Add Replace(UniqueParts(i), "_", vbLf)
    Next i

    ' 2개 이상의 부품열 정보가 있는 부품만 병합 실행
    For i = 1 To Duplicated.Count
        Set UniqueParts = New Collection
        For Each cell In PartNamesArea
            BracketVal = ExtractBracketValue(cell.value)
            If BracketVal = Duplicated(i) Then UniqueParts.Add cell ' 중복된 부품 셀 선별
        Next cell
        For CBTr = rStart To rEnd ' 파트별 병합
            CCBC UniqueParts, CBTr ' Combine Target Row
        Next CBTr
        For CBTr = 2 To UniqueParts.Count ' 정리 완료 후 잉여 열 삭제
            UniqueParts(CBTr).EntireColumn.Delete ' 1번째 셀에 취합했으므로 나머지 셀(열) 삭제
        Next CBTr
    Next i
End Sub
Private Sub CCBC(ByRef Target As Collection, Optional ByVal TargetRow As Long = -1) ' Cell Combine By Columns
    Dim i As Long
    Dim ValueList As String ', vMaker As String, vParts As String
    Dim CER As New Collection ' Chosen Each Range
    If TargetRow > 0 Then
        Dim ws As Worksheet: Set ws = Target(1).Worksheet
        For i = 1 To Target.Count
            CER.Add ws.Cells(TargetRow, Target(i).Column)
        Next i
    Else
        Set CER = Target
    End If
    
    If CER.Count < 1 Then Exit Sub
    For i = 1 To CER.Count ' 필요 정보 취합
        If Trim(CER(i).value) <> "" Then ValueList = ValueList & CER(i).value & vbLf
        If i > 1 Then CER(i).value = "" ' 취합 후 잉여셀의 값 삭제
    Next i
    If Right$(ValueList, 1) = vbLf Then ValueList = Left$(ValueList, Len(ValueList) - 1)
    
    ' 중앙정렬 및 텍스트 삽입
    With CER(1)
        .value = ValueList
        .HorizontalAlignment = xlCenter
        .VerticalAlignment = xlCenter
    End With
End Sub
Private Sub MarkingUP_items(ByRef Target As D_Maps)
    Dim ws As Worksheet: Set ws = Target_WorkSheet
    Dim i As Long, tCol As Long, sCol As Long, eCol As Long, tRow As Long, sRow As Long, eRow As Long, Total As Long
    Dim CrrR As Range, NxtR As Range
    sCol = ws.Rows(1).Find("-Line", lookAt:=xlPart, LookIn:=xlValues).Column + 2
    eCol = ws.Cells(1, ws.Columns.Count).End(xlToLeft).Column
'    LastRow = ws.Cells(ws.Rows.Count, 1).End(xlUp).Row
    
    With Target
        If .Count(MainG) > 0 Then .RemoveAll MainG
        For tCol = sCol To eCol
            For i = 1 To .Count(SubG)
                Set CrrR = .Sub_Lot(i).Start_R.Offset(0, tCol - sCol + 5)
                If Not .Count(SubG) = i Then
                    Set NxtR = .Sub_Lot(i + 1).Start_R.Offset(0, tCol - sCol + 5)
                    If CrrR.value <> NxtR.value Then eRow = CrrR.Row
                Else
                    eRow = CrrR.Row
                End If
                If sRow = 0 Then sRow = CrrR.Row
                If sRow > 0 And eRow > 0 Then .Set_Lot ws.Cells(sRow, tCol), ws.Cells(eRow, tCol): sRow = 0: eRow = 0
            Next i
            
            For i = .Count(MainG) To 1 Step -1
                With .Main_Lot(i)
                    Total = Application.WorksheetFunction.Sum(ws.Range(ws.Cells(.Start_R.Row, 5), ws.Cells(.End_R.Row, 5)))
                    'Brush.Stamp_it .Start_R, .End_R, dsLeft, True, Total, ws.Cells(1, tCol).value
                End With
                .Remove i
            Next i
        Next tCol
    
    End With

End Sub
Private Sub MarkingUP_PL(ByRef Target As D_Maps)
    Dim i As Long
    
    For i = 1 To Target.Count(SubG) ' SubGroups 윗라인 라이닝
        If Not Target.Sub_Lot(i).Start_R.Borders(xlEdgeTop).LineStyle = xlDash Then
            With ForLining(Target.Sub_Lot(i).Start_R, Row).Borders(xlEdgeTop)
                .LineStyle = xlContinuous
                .Weight = xlThin
            End With
        End If
    Next i
    
    With Target
        For i = .Count(SubG) To 1 Step -1 ' Sub Group Stamp it
            With .Sub_Lot(i)
                Brush.Stamp_it_Auto Target_WorkSheet.Range(.Start_R, .End_R).Offset(0, 2), dsRight, True
            End With
            .Remove i, SubG
        Next i
    End With
End Sub
