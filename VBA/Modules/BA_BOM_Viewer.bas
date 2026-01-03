Attribute VB_Name = "BA_BOM_Viewer"
Option Explicit
'한번 만든 Title의 값을 이 모듈안에서 다른 서브루틴, 함수에서 참조하기 위해 모듈부에서 선언
Private Title As String
Private PrintRange As Range
Private ColumnsForReport As New Collection
' 문서 서식 자동화
Private Sub AutoReport_BOM(ByRef Wb As Workbook)
    Dim ws As Worksheet
    Set ws = Wb.ActiveSheet
    Dim LastCol As Long
    Dim i As Long '반복문용 변수
    Dim DelCell As Range '반복문용 변수
    Dim TitleRange As Range
    Dim TableRange As Range
    
    SetUsingColumns ColumnsForReport
    
    LastCol = ws.Cells(1, ws.Columns.Count).End(xlToLeft).Column
    ' 각 열이 ColumnsForReport에 포함되어 있는지 확인하고, 포함되지 않는 경우 열 삭제
    For i = LastCol To 1 Step -1 'Step 연산자는 i 변수의 순서마다 어떤 연산을 할지 결정
        Set DelCell = ws.Cells(1, i)
        ' 현재 열이 ColumnsForReport에 없으면 삭제
        If Not IsInCollection(DelCell.Value, ColumnsForReport) Then ws.Columns(i).Delete
    Next i
    
    Dim InsertRow As Long '추가행 갯수
    InsertRow = 3
    '제목 입력을 위한 공백 행 InsertRow의 값 만큼 추가
    For i = 1 To InsertRow
        ws.Rows(1).Insert Shift:=xlShiftDown, CopyOrigin:=xlFormatFromLeftOrAbove
    Next i
'마지막 열을 다시 구해서 셀병합 영역지정
    LastCol = ws.Cells(InsertRow + 1, ws.Columns.Count).End(xlToLeft).Column
    Set TitleRange = ws.Range(ws.Cells(1, 1), ws.Cells(InsertRow, LastCol))
    TitleRange.Merge
'제목 입력
    Call AutoTitle(ws, ColumnsForReport)
'AutoFilltering
    Call AutoFilltering_BOM(ws, ColumnsForReport)
'CurrentRegion 메소드로 TableRange 영역지정
    Set TableRange = TitleRange.CurrentRegion
'Interior Borders, Columns Width
    Call Interior_Set_BOM(ws, TableRange)
End Sub

Private Sub SetUsingColumns(ByRef UsingCol As Collection)
    
    UsingCol.Add "Lvl"
    UsingCol.Add "Part No"
    UsingCol.Add "Description"
    UsingCol.Add "Qty"
    UsingCol.Add "UOM"
    UsingCol.Add "Maker"
    UsingCol.Add "Supply Type"
    
End Sub

Private Function FncSetPR(ws As Worksheet, C_Collection As Collection) As Range
        
    Dim FirstCol As Long
    Dim FirstRow As Long
    Dim LastCol As Long
    Dim LastRow As Long
    Dim ToPrintRange As Range
        
    With ws
        FirstCol = .UsedRange.Find(C_Collection(1)).Column
        FirstRow = .UsedRange.Find(C_Collection(1)).Row + 1
        LastCol = .UsedRange.Find(C_Collection(C_Collection.Count)).Column
        LastRow = .Cells(.Rows.Count, FirstCol).End(xlUp).Row
        Set FncSetPR = .Range(.Cells(FirstRow, FirstCol), .Cells(LastRow, LastCol))
    End With
    
End Function
Private Sub AutoTitle(ws As Worksheet, ColumnList As Collection)
    Dim findrow As Long
    Dim FindCol As Long
    Dim StrIndex As Long
    '모듈선언부의 Title변수 초기화
    Title = ""
    
    FindCol = ws.UsedRange.Find(What:=ColumnList(1)).Column
    findrow = ws.Columns(FindCol).Find(What:=0).Row
    FindCol = ws.UsedRange.Find(What:=ColumnList(2)).Column
    
    Title = ws.Cells(findrow, FindCol).Value
    StrIndex = InStr(Title, "@")
    If Not StrIndex = 0 Then
        Title = Left(Title, StrIndex - 1)
    End If
    ws.Cells(1, 1).Value = Title
        
    With ws.Cells(1, 1)
        .Font.Name = "LG스마트체 Bold"
        .Font.Size = 25
        .Font.Bold = True
        .HorizontalAlignment = xlCenter
        .VerticalAlignment = xlCenter
    End With

End Sub

Private Sub Interior_Set_BOM(ws As Worksheet, rng As Range)
    Dim SetEdge(1 To 6) As XlBordersIndex
    Dim colWidth(1 To 7) As Single
    Dim i As Long
    
    SetEdge(1) = xlEdgeLeft
    SetEdge(2) = xlEdgeRight
    SetEdge(3) = xlEdgeTop
    SetEdge(4) = xlEdgeBottom
    SetEdge(5) = xlInsideHorizontal
    SetEdge(6) = xlInsideVertical
    
    For i = LBound(SetEdge) To UBound(SetEdge)
        With rng.Borders(SetEdge(i))
            .LineStyle = xlContinuous
            .Color = RGB(0, 0, 0)
            .Weight = xlThin
        End With
    Next i
    
    colWidth(1) = 2.7
    colWidth(2) = 20
    colWidth(3) = 30
    colWidth(4) = 3
    colWidth(5) = 2.5
    colWidth(6) = 16
    colWidth(7) = 13
    
    For i = LBound(colWidth) To UBound(colWidth)
    ws.Columns(i).ColumnWidth = colWidth(i)
    Next i
End Sub

Public Sub Read_BOM(Optional Handle As Boolean)
    Dim i As Long
    Dim BOM As New Collection
        
    AutoReportHandler.ListView_BOM.ListItems.Clear
    ' 지정한 주소에 지정한 String값을 가진 파일만 추출하는 구문, 체계도 없는것들 맨날 조직변경해서 이거 자주 추적 업데이트 해야댐 ㅡㅡ
    Set BOM = FindFilesWithTextInName(Z_Directory.Source, "@CVZ")
    If BOM.Count = 0 Then: If Handle Then MsgBox "연결된 주소에 BOM 파일이 없음": Exit Sub
    
    With AutoReportHandler.ListView_BOM
        
        For i = 1 To BOM.Count
        AutoReportHandler.UpdateProgressBar AutoReportHandler.PB_BOM, (i - i / 2) / BOM.Count * 100
            Dim vModelName As String
            vModelName = GetModelName(BOM(i))
            
            With .ListItems.Add(, , vModelName)
                .SubItems(1) = BOM(i)
                .SubItems(2) = "Ready" 'Print
                .SubItems(3) = CheckFileAlreadyWritten_PDF(vModelName, dc_BOM) 'PDF
            End With
            AutoReportHandler.ListView_BOM.ListItems(i).Checked = True ' 체크박스 체크
            
            AutoReportHandler.UpdateProgressBar AutoReportHandler.PB_BOM, i / BOM.Count * 100
        Next i
        
    End With
    
    If Handle Then
        MsgBox "BOM시트 " & BOM.Count & "종 연결완료"
    End If
    
End Sub

Public Sub Print_BOM(Optional Handle As Boolean)
    Dim BOMwb As Workbook
    Dim BOMLV As ListView
    Dim BOMitem As listItem
    Dim ListCount As Long
    Dim i As Long
    Dim Chkditem As New Collection
    
    Set BOMLV = AutoReportHandler.ListView_BOM
    ListCount = BOMLV.ListItems.Count
    
    If ListCount = 0 Then
        MsgBox "연결된 데이터 없음"
        Exit Sub
    End If
    
    For i = 1 To ListCount ' 체크박스 활성화된 아이템 선별
        Set BOMitem = BOMLV.ListItems.Item(i)
        If BOMitem.Checked Then Chkditem.Add BOMitem.index 'SubItems(1)
    Next i
    
    If Chkditem.Count < 1 Then MsgBox "선택된 문서 없음": Exit Sub

    ListCount = Chkditem.Count
    For i = 1 To ListCount
AutoReportHandler.UpdateProgressBar AutoReportHandler.PB_BOM, ((i - 0.9) / ListCount * 100) ' 10프로
        Set BOMitem = BOMLV.ListItems.Item(Chkditem(i))
        Set BOMwb = Workbooks.Open(BOMitem.SubItems(1)) ' Chkditem(i) / 주소값으로 호출함
        Dim ws As Worksheet
        Set ws = BOMwb.Worksheets(1)
        BOMwb.Windows(1).WindowState = xlMinimized
AutoReportHandler.UpdateProgressBar AutoReportHandler.PB_BOM, ((i - 0.7) / ListCount * 100) ' 30프로
'자동화 서식작성 코드
        AutoReport_BOM BOMwb
AutoReportHandler.UpdateProgressBar AutoReportHandler.PB_BOM, ((i - 0.5) / ListCount * 100)  ' 50프로
'프린트여부 결정
        If PrintNow.BOM Then
            
            Printer.PrinterNameSet ' 기본프린터 이름 설정, 유지되는지 확인
            AutoPageSetup ws, PS_BOM(ws, FncSetPR(ws, ColumnsForReport)) ' PageSetup
            ws.PrintOut ActivePrinter:=DefaultPrinter
            BOMitem.SubItems(2) = "Done"  'Print
            Application.Wait Now + TimeValue("0:00:01") '1초 딜레이
        Else
            BOMitem.SubItems(2) = "Pass" 'Print
        End If
AutoReportHandler.UpdateProgressBar AutoReportHandler.PB_BOM, ((i - 0.3) / ListCount * 100) ' 70프로
'저장을 위해 타이틀 수정, 윈도우에서 "." 이후의 String은 확장자로 인식하기 때문
        Title = Replace(Title, ".", "_")
'저장여부 결정
        SaveFilesWithCustomDirectory "BOM", BOMwb, PS_BOMforPDF(ws, FncSetPR(ws, ColumnsForReport)), Title, False, True, OriginalKiller.BOM
        BOMitem.SubItems(3) = "Done" 'PDF
'Progress Update
AutoReportHandler.UpdateProgressBar AutoReportHandler.PB_BOM, (i / ListCount * 100) ' 100프로
    Next i
    
    If Handle Then
        MsgBox ListCount & "종의 BOM 출력 완료"
    End If
    
End Sub

Private Function GetModelName(BOMdirectory As String) As String
    ' Excel 애플리케이션을 새로운 인스턴스로 생성
    Dim xlApp As Excel.Application
    Set xlApp = New Excel.Application
    xlApp.Visible = False

    Dim Wb As Workbook: Set Wb = xlApp.Workbooks.Open(BOMdirectory) ' 워크북 열기
    Dim ws As Worksheet: Set ws = Wb.Worksheets(1) ' 워크시트 선택

    ' 값을 읽어오기
    'Dim Title As String 모듈선언부의 Title 활용
    Dim Str As Long
    Title = ws.Cells(2, 3).Value
    Str = InStr(Title, "@")
    Title = Left(Title, Str - 1)
    GetModelName = Title

    ' 워크북 닫기
    Wb.Close SaveChanges:=False
    Set Wb = Nothing

    ' Excel 애플리케이션 종료
    xlApp.Quit
    Set xlApp = Nothing
End Function
