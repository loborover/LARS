''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
BA_BOM_Viewer.bas Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
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
        If Not IsInCollection(DelCell.value, ColumnsForReport) Then ws.Columns(i).Delete
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
    
    Title = ws.Cells(findrow, FindCol).value
    StrIndex = InStr(Title, "@")
    If Not StrIndex = 0 Then
        Title = Left(Title, StrIndex - 1)
    End If
    ws.Cells(1, 1).value = Title
        
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
        If BOMitem.Checked Then Chkditem.Add BOMitem.Index 'SubItems(1)
    Next i
    
    If Chkditem.Count < 1 Then MsgBox "선택된 문서 없음": Exit Sub

    ListCount = Chkditem.Count
    For i = 1 To ListCount
AutoReportHandler.UpdateProgressBar AutoReportHandler.PB_BOM, ((i - 0.9) / ListCount * 100) ' 10프로
        Set BOMitem = BOMLV.ListItems.Item(Chkditem(i))
        Set BOMwb = Workbooks.open(BOMitem.SubItems(1)) ' Chkditem(i) / 주소값으로 호출함
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

    Dim Wb As Workbook: Set Wb = xlApp.Workbooks.open(BOMdirectory) ' 워크북 열기
    Dim ws As Worksheet: Set ws = Wb.Worksheets(1) ' 워크시트 선택

    ' 값을 읽어오기
    'Dim Title As String 모듈선언부의 Title 활용
    Dim Str As Long
    Title = ws.Cells(2, 3).value
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
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
BA_BOM_Viewer.bas End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Fillter.bas Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Sub AutoFilltering_BOM(ws As Worksheet, FillteringCol As Collection)
    
    Dim FirstCol As Long
    Dim LastCol As Long
    Dim FirstRow As Long
    Dim LastRow As Long
    Dim ChartTable As Range
           
    With ws
        FirstCol = .UsedRange.Find(What:=FillteringCol(1)).Column ' FillteringCol(1)="Lvl"
        LastCol = .UsedRange.Find(What:=FillteringCol(FillteringCol.Count)).Column
        FirstRow = .UsedRange.Find(What:=FillteringCol(1)).Row
        LastRow = .Cells(FirstRow, FirstCol).End(xlDown).Row
        Set ChartTable = .Range(.Cells(FirstRow, FirstCol), .Cells(LastRow, LastCol))
    End With
    
    With ChartTable
        .AutoFilter Field:=1, Criteria1:=BOM_Array(AutoReportHandler.itemLevel), Operator:=xlFilterValues
        '.AutoFilter Field:=1, Criteria1:=Array("0", ".1", "*S*"), Operator:=xlFilterValues
        .AutoFilter Field:=4, Criteria1:=">=1", Operator:=xlFilterValues
    End With

End Sub

Private Function BOM_Array(col As Collection) As Variant
    Dim arr() As Variant
    Dim i As Long
    
    ReDim arr(o To col.Count - 1)
    For i = 1 To col.Count
        arr(i - 1) = col(i)
    Next i
        
    BOM_Array = arr
End Function
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Fillter.bas End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Printer.bas Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Public Type PrintSetting
    PrintArea As String ' 주소
    Orientation As XlPageOrientation ' 인쇄방향
    LeftMargin As Single ' 좌측 여백
    RightMargin As Single ' 우측 여백
    TopMargin As Single ' 상단 여백
    BottomMargin As Single ' 하단 여백
    HeaderMargin As Single ' 헤더 여백
    FooterMargin As Single ' 푸터 여백
    PaperSize As XlPaperSize ' 종이 크기
    Zoom As Boolean
    FitToPagesWide As Variant ' 가로 페이지 개수
    FitToPagesTall As Variant ' 세로 페이지 개수
    CenterHorizontally As Boolean ' 프린트 내용을 가로 중앙정렬
    CenterVertically As Boolean ' 프린트 내용을 세로 중앙정렬
    PrintTitleRows As String ' 반복행 주소값
    AlignMarginsHeaderFooter As Boolean
    RightHeader As String
    LeftHeader As String
End Type

Public Type PrtNowBoolean
    BOM As Boolean
    DailyPlan As Boolean
    PartList As Boolean
    DailyReport As Boolean
End Type

'프린트 셋업이 유지되는지 체크하는 불리언 변수
Public PrintNow As PrtNowBoolean
Public OriginalKiller As PrtNowBoolean

Public PrinterName As Collection

Public ToDeleteDir As String
Public DefaultPrinter As String

Public Print_setup As Boolean
Private IsPrinterNameSet As Boolean

Public Property Get Bool_IPNS() As Boolean
    Bool_IPNS = IsPrinterNameSet
End Property

Private Sub InitializingPrinters()
    Dim objWMIService As SWbemServices
    Dim colPrinters As SWbemObjectSet
    Dim objPrinter As SWbemObject
    Dim i As Long: i = 1
    
    Set objWMIService = GetObject("winmgmts:\\.\root\cimv2")
    Set colPrinters = objWMIService.ExecQuery("Select * from Win32_Printer")
    Set PrinterName = New Collection
    
    For Each objPrinter In colPrinters
        If objPrinter.Default = True Then DefaultPrinter = objPrinter.Name
        PrinterName.Add objPrinter.Name
    Next objPrinter
    
    IsPrinterNameSet = True
    
End Sub

Public Sub PrinterNameSet()
    If DefaultPrinter = "" Then InitializingPrinters
End Sub

Function PS_BOM(ByRef ws As Worksheet, PR As Range) As PrintSetting
    Dim BA_BOM_Viewer_PS As PrintSetting
    Dim TitleRow As Long: TitleRow = ws.UsedRange.Find("0").Row - 1
    
    With BA_BOM_Viewer_PS
        .PrintArea = PR.Address '주소
        .Orientation = xlPortrait '인쇄방향 세로
        .LeftMargin = 0 ' 좌측 여백
        .RightMargin = 0 ' 우측 여백
        .TopMargin = 0 ' 상단 여백
        .BottomMargin = 0.3 ' 하단 여백
        .HeaderMargin = 0.68 ' 헤더 여백
        .FooterMargin = 0 ' 푸터 여백
        .PaperSize = xlPaperA4 ' 표준 A4 크기
        .Zoom = False
        .FitToPagesWide = 1 ' 가로 페이지 개수
        .FitToPagesTall = False ' 세로 페이지 개수
        .CenterHorizontally = True
        .CenterVertically = False
        .PrintTitleRows = "$1:$" & TitleRow
        .AlignMarginsHeaderFooter = False
        .RightHeader = "&""LG스마트체 Light""&8 " & "연월일" & Format(Now(), "YYMMDD") & Chr(10) _
                            & "&""LG스마트체 Light""&8 " & "시분초" & Format(Now(), "HHMMSS") & Chr(10) _
                            & "&""LG스마트체 Bold""&22 &P / &N"
    End With
    
    PS_BOM = BA_BOM_Viewer_PS
End Function
Function PS_BOMforPDF(ByRef ws As Worksheet, PR As Range) As PrintSetting
    Dim BA_BOM_Viewer_PS As PrintSetting
    Dim TitleRow As Long: TitleRow = ws.UsedRange.Find("0").Row - 1
    
    With BA_BOM_Viewer_PS
        .PrintArea = PR.Address '주소
        .Orientation = xlPortrait '인쇄방향 세로
        .LeftMargin = 0 ' 좌측 여백
        .RightMargin = 0 ' 우측 여백
        .TopMargin = 0.5 ' 상단 여백
        .BottomMargin = 0.3 ' 하단 여백
        .HeaderMargin = 0.68 ' 헤더 여백
        .FooterMargin = 0 ' 푸터 여백
        .PaperSize = xlPaperA4 ' 표준 A4 크기
        .Zoom = False
        .FitToPagesWide = 1 ' 가로 페이지 개수
        .FitToPagesTall = False ' 세로 페이지 개수
        .CenterHorizontally = True
        .CenterVertically = False
        .PrintTitleRows = "$1:$" & TitleRow
        .AlignMarginsHeaderFooter = False
        .RightHeader = "&""LG스마트체 Light""&8 " & "연월일" & Format(Now(), "YYMMDD") & Chr(10) _
                            & "&""LG스마트체 Light""&8 " & "시분초" & Format(Now(), "HHMMSS") & Chr(10) _
                            & "&""LG스마트체 Bold""&22 &P / &N"
    End With
    
    PS_BOMforPDF = BA_BOM_Viewer_PS
End Function

Function PS_DailyPlan(PR As Range) As PrintSetting
    Dim BB_DailyPlan_Viewer_PS As PrintSetting
    
    With BB_DailyPlan_Viewer_PS
        .PrintArea = PR.Address '주소
        .Orientation = xlPortrait '인쇄방향 세로
        .LeftMargin = 0 ' 좌측 여백
        .RightMargin = 0 ' 우측 여백
        .TopMargin = 0.3 ' 상단 여백
        .BottomMargin = 0.3 ' 하단 여백
        .HeaderMargin = 0 ' 헤더 여백
        .FooterMargin = 0 ' 푸터 여백
        .PaperSize = xlPaperA4 ' 표준 A4 크기
        .Zoom = False
        .FitToPagesWide = 1 ' 가로 페이지 개수
        .FitToPagesTall = False ' 세로 페이지 개수
        .CenterHorizontally = True
        .CenterVertically = False
        .PrintTitleRows = "$1:$2"
        .AlignMarginsHeaderFooter = False
        .RightHeader = ""
    End With
    
    PS_DailyPlan = BB_DailyPlan_Viewer_PS
End Function

Function PS_DPforPDF(PR As Range) As PrintSetting
    Dim BB_DailyPlan_Viewer_PS As PrintSetting
    
    With BB_DailyPlan_Viewer_PS
        .PrintArea = PR.Address '주소
        .Orientation = xlPortrait '인쇄방향 세로
        .LeftMargin = 0 ' 좌측 여백
        .RightMargin = 0 ' 우측 여백
        .TopMargin = 0.3 ' 상단 여백
        .BottomMargin = 0.3 ' 하단 여백
        .HeaderMargin = 0 ' 헤더 여백
        .FooterMargin = 0 ' 푸터 여백
        .PaperSize = xlPaperA4 ' 표준 A4 크기
        .Zoom = False
        .FitToPagesWide = 1 ' 가로 페이지 개수
        .FitToPagesTall = False ' 세로 페이지 개수
        .CenterHorizontally = True
        .CenterVertically = False
        .PrintTitleRows = "$1:$2"
        .AlignMarginsHeaderFooter = False
        .RightHeader = ""
    End With
    
    PS_DPforPDF = BB_DailyPlan_Viewer_PS
End Function

Function PS_PartList(PR As Range) As PrintSetting
    Dim PartList_Viewer_PS As PrintSetting
    
    With PartList_Viewer_PS
        .PrintArea = PR.Address '주소
        .Orientation = xlLandscape '인쇄방향 가로
        .LeftMargin = 0 ' 좌측 여백
        .RightMargin = 0 ' 우측 여백
        .TopMargin = 0 ' 상단 여백
        .BottomMargin = 0 ' 하단 여백
        .HeaderMargin = 0 ' 헤더 여백
        .FooterMargin = 0 ' 푸터 여백
        .PaperSize = xlPaperA4 ' 표준 A4 크기
        .Zoom = False
        .FitToPagesWide = 1 ' 가로 페이지 개수
        .FitToPagesTall = False ' 세로 페이지 개수
        .CenterHorizontally = True
        .CenterVertically = False
        .PrintTitleRows = "$1:$1"
        .AlignMarginsHeaderFooter = False
        .RightHeader = ""
    End With
    
    PS_PartList = PartList_Viewer_PS
End Function

Function PS_PLEP(PR As Range) As PrintSetting ' PartList each Parts
    Dim PartList_Viewer_PS As PrintSetting
    
    With PartList_Viewer_PS
        .PrintArea = PR.Address '주소
        .Orientation = xlPortrait '인쇄방향 가로
        .LeftMargin = 0 ' 좌측 여백
        .RightMargin = 0 ' 우측 여백
        .TopMargin = 0 ' 상단 여백
        .BottomMargin = 0 ' 하단 여백
        .HeaderMargin = 0 ' 헤더 여백
        .FooterMargin = 0 ' 푸터 여백
        .PaperSize = xlPaperA4 ' 표준 A4 크기
        .Zoom = False
        .FitToPagesWide = 1 ' 가로 페이지 개수
        .FitToPagesTall = False ' 세로 페이지 개수
        .CenterHorizontally = True
        .CenterVertically = False
        .PrintTitleRows = "$1:$1"
        .AlignMarginsHeaderFooter = False
        .RightHeader = ""
    End With
    
    PS_PLEP = PartList_Viewer_PS
End Function

Sub AutoPageSetup(ByRef ws As Worksheet, PrtStpVar As PrintSetting, _
                        Optional PreView As Boolean = False)
    '출력할 워크시트, 프린트셋업 변수, 프리뷰를 할지말지 결정하는 불리언

    With ws.PageSetup
        .PrintArea = PrtStpVar.PrintArea
        .Orientation = PrtStpVar.Orientation
        .LeftMargin = Application.CentimetersToPoints(PrtStpVar.LeftMargin) ' 좌측 여백
        .RightMargin = Application.CentimetersToPoints(PrtStpVar.RightMargin) ' 우측 여백
        .TopMargin = Application.CentimetersToPoints(PrtStpVar.TopMargin)  ' 상단 여백
        .BottomMargin = Application.CentimetersToPoints(PrtStpVar.BottomMargin)  ' 하단 여백
        .HeaderMargin = Application.CentimetersToPoints(PrtStpVar.HeaderMargin)  ' 헤더 여백
        .FooterMargin = Application.CentimetersToPoints(PrtStpVar.FooterMargin)  ' 푸터 여백
        .PaperSize = PrtStpVar.PaperSize  ' 표준 A4 크기
        .Zoom = PrtStpVar.Zoom
        .FitToPagesWide = PrtStpVar.FitToPagesWide ' 가로 페이지 개수
        .FitToPagesTall = PrtStpVar.FitToPagesTall ' 세로 페이지 개수
        .CenterHorizontally = PrtStpVar.CenterHorizontally
        .CenterVertically = PrtStpVar.CenterVertically
        .PrintTitleRows = PrtStpVar.PrintTitleRows
        .AlignMarginsHeaderFooter = PrtStpVar.AlignMarginsHeaderFooter
        .RightHeader = PrtStpVar.RightHeader
        .LeftHeader = PrtStpVar.LeftHeader
    End With
    
    If PreView Then
        ws.PrintPreview
    End If
    
    Print_setup = True
    
End Sub
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Printer.bas End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
BB_DailyPlan_Viewer.bas Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Option Explicit

Public MRB_DP As Boolean ' Manual_Reporting_Bool_DailyPlan

Private BoW As Single ' Black or White
Private DP_Processing_WB As New Workbook ' 모듈내 전역변수로 선언함
Private Target_WorkSheet As New Worksheet
Private PrintArea As Range ' 모듈내 프린트영역
Private Brush As New Painter
Private Title As String, wLine As String
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
            Dim vDate As String
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
        Set DP_Processing_WB = Workbooks.open(DPitem.SubItems(2))
AutoReportHandler.UpdateProgressBar AutoReportHandler.PB_BOM, (i - 0.91) / ListCount * 100
        wLine = DPitem.SubItems(1) ' Line 이름 인계
        Set Target_WorkSheet = DP_Processing_WB.Worksheets(1): Set ws = Target_WorkSheet: Set Brush.DrawingWorksheet = Target_WorkSheet ' 워크시트 타게팅
        DP_Processing_WB.Windows(1).WindowState = xlMinimized ' 최소화
        AutoReport_DailyPlan DP_Processing_WB '자동화 서식작성 코드
AutoReportHandler.UpdateProgressBar AutoReportHandler.PB_BOM, (i - 0.87) / ListCount * 100
        If PrintNow.DailyPlan Then
            Printer.PrinterNameSet  ' 기본프린터 이름 설정, 유지되는지 확인
            ws.PrintOut ActivePrinter:=DefaultPrinter, From:=1, to:=2, copies:=PaperCopies
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
        If MRB_DP Then Workbooks.open (SavedPath & ".xlsx") ' 메뉴얼 모드일 때 열기
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
    ws.Cells(1, 1).value = "투입" & vbLf & "시점"
    LastCol = ws.Cells(1, ws.Columns.Count).End(xlToLeft).Column ' 데이터가 존재하는 마지막 열

    For i = LastCol To 2 Step -1
        Set DelCell = ws.Cells(2, i)
        ' 현재 열이 vCFR에 없거나, 숫자(날짜)면서 생산량이 0일 경우 삭제
        If Not IsInCollection(DelCell.value, vCFR) Xor _
            (isNumeric(DelCell.value) And DelCell.Offset(1, 0).value > 0) Then ws.Columns(i).Delete ' 숨기려면 .Hidden = True
    Next i
    ' 새로운 서식 적용을 위한 열 추가 및 수정작업
    Set DelCell = ws.Rows(2).Find(What:="W/O 계획수량", lookAt:=xlWhole)
    If DelCell Is Nothing Then Stop ' 오류나면 정지
    DelCell.value = "계획" ' 원래의 열 제목이 너무 길어서 수정
    DelCell.Offset(0, 1).value = "IN" ' 원래의 열 제목이 너무 길어서 수정
    DelCell.Offset(0, 2).value = "OUT" ' 원래의 열 제목이 너무 길어서 수정
    startRow = DelCell.Offset(2, 0).Row ' StartRow 추출
    Set DelCell = DelCell.Offset(0, 3) ' 계획 셀에서 오른쪽으로 열이동 3번 하면 금일 날짜 나옴
    ws.Columns(DelCell.Column).Insert Shift:=xlShiftToRight, CopyOrigin:=xlFormatFromLeftOrAbove ' Connecter 2*2셀로 만듦
    ws.Columns(DelCell.Column).Insert Shift:=xlShiftToRight, CopyOrigin:=xlFormatFromLeftOrAbove
    DelCell.Offset(0, -1).value = "Connecter"
    ws.Range(DelCell.Offset(-1, -2), DelCell.Offset(0, -1)).Merge
    
    Do Until ws.Cells(1, DelCell.Offset(0, 3).Column + 1).value = ""
        ws.Columns(DelCell.Offset(0, 3).Column + 1).Delete ' D-day 기준, +3일까지 살리고 싸그리 삭제
    Loop
    LastCol = ws.Cells(1, ws.Columns.Count).End(xlToLeft).Column ' 데이터가 존재하는 마지막 열
    ws.Cells(2, LastCol + 1).value = wLine & "-Line" ' 라인 데이터 기입
    ws.Range(ws.Cells(1, LastCol + 1), ws.Cells(2, LastCol + 2)).Merge
    
    LastCol = ws.Cells(1, ws.Columns.Count).End(xlToLeft).Column ' 마지막날짜 열 찾기
    LastRow = ws.Cells(ws.Rows.Count, LastCol - 1).End(xlUp).Row ' 마지막 날짜의 마지막 행 찾기
    Title = DelCell.Column ' D-Day 셀의 열 값을 Title변수로 옮김.
    
    With ws.Range(ws.Cells(1, 1), ws.Cells(2, LastCol)) ' 제목부분 interior
        .WrapText = True
        .Interior.Color = RGB(199, 253, 240)
        .Font.Bold = True
    End With
    
    Do Until ws.Cells(LastRow + 1, 2).value = "" ' 마지막 밑으로 값이 있으면 삭제
        ws.Rows(LastRow + 1).Delete
    Loop
    
    Set DelCell = ws.Rows(2).Find(What:="계획", lookAt:=xlWhole)
    DelCell.Offset(1, 0).value = Application.WorksheetFunction.Sum(ws.Range(DelCell.Offset(2, 0), ws.Cells(LastRow, DelCell.Column)))
    If DelCell.Offset(1, 0).value > 9999 Then DelCell.Offset(1, 0).value = Format(DelCell.Offset(1, 0).value / 1000, "0.0") & "k"
    
    For i = startRow To LastRow
        ws.Cells(i, 20).value = Time_Filtering(ws.Cells(i, 1).value, ws.Cells(i + 1, 1).value)
        ws.Cells(i, 21).value = ws.Cells(i, 20).value / ws.Cells(i, 4).value
    Next i
    ws.Cells(1, 16).value = "Meta_Data"
    Dim arr As Variant: arr = Array("3001", "2101", "2102", "3304", "TPL", "UPPH")
    For i = LBound(arr) To UBound(arr)
        ws.Cells(startRow - 1, 16 + i).value = CStr(arr(i))
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
    Dim i As Long
    
    Set PR = ws.Cells(1, 1).CurrentRegion
    
    SetEdge(1) = xlEdgeLeft
    SetEdge(2) = xlEdgeRight
    SetEdge(3) = xlEdgeTop
    SetEdge(4) = xlEdgeBottom
    SetEdge(5) = xlInsideHorizontal
    SetEdge(6) = xlInsideVertical
    
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
        If xCell.value = "" Then xCell.Interior.Color = RGB(BoW, BoW, BoW) ' Brightness
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
    Dim Wb As Workbook: Set Wb = xlApp.Workbooks.open(DailyPlanDirectiory) ' 워크북 열기
    Dim ws As Worksheet: Set ws = Wb.Worksheets(1) ' 워크시트 선택

    ' 값을 읽어오기
    Dim col(1 To 2) As Long, smallestValue As Long: smallestValue = 31
    Dim cell As Range, Finder As Range
        
    For Each Finder In ws.Rows(2).Cells ' DP에서 날짜를 찾는 줄
        If Finder.value Like "*월" And Finder.Offset(2, 0).value > 0 Then Set cell = Finder: Exit For
        col(1) = col(1) + 1: If col(1) > 70 Then Exit For
    Next Finder
    If cell Is Nothing Then GetDailyPlanWhen = "It's Not a DailyPlan": GoTo NAD ' 열람한 문서가 DailyPlan이 아닐시 오류처리 단
    Title = cell.value ' 생산 월
    col(1) = cell.MergeArea.Cells(1, 1).Column: col(2) = cell.MergeArea.Cells(1, cell.MergeArea.Columns.Count).Column ' 생산 일 Range 지정을 위한 열 값 추적
    For Each cell In ws.Range(ws.Cells(3, col(1)), ws.Cells(3, col(2)))
        If isNumeric(cell.value) And cell.Offset(1, 0).value > 0 And cell.value < smallestValue Then smallestValue = cell.value
    Next cell
    Title = Title & "-" & smallestValue & "일" ' Title = *월-*일
    GetDailyPlanWhen = Title ' 날짜형 제목값 인계
    Title = smallestValue ' 날짜값
    Set cell = ws.Rows("2:3").Find(What:="생산 라인", lookAt:=xlWhole, LookIn:=xlValues)
    wLine = cell.Offset(2, 0).value
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
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
BB_DailyPlan_Viewer.bas End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
InventoryCart.cls Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Option Explicit

Private Makers As New Collection, parts As New Collection, Duplicated As New Collection
Private TargetWB As Workbook, TargetWS As Worksheet, Container As Range, Ship As Range

'초기화 이벤트 메서드
Private Sub Class_Initialize()

End Sub
'소멸 이벤트 메서드
Private Sub Class_Terminate()
    
End Sub
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
InventoryCart.cls End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
AA_Test.bas Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Private ws As Worksheet

Public Sub Painter_Test()
    Set ws = ThisWorkbook.Worksheets("test")
    Dim AAA As New Painter: Set AAA.DrawingWorksheet = ws
    Dim i As Long
    AAA.DeleteShapes ': Exit Sub
    Const startRow As Long = 3
    Const Gap As Long = 3
    
    Dim a As Long, b As Long, c As Long, d As Long
    Dim innerString As String, OuterString As String, BASD As Shape
    Dim SubTexts As New Collection: SubTexts.Add "ABCD"
    
    With ws
    AAA.OvalBridge .Cells(3, 16), .Cells(10, 16), 6, 10, dvDown, , , , dsRight, , d4UP, , "1234", SubTexts
    
    
    'For i = 1 To 22
    '    A = StartRow + (i - 1) * (Gap + 1): B = A + Gap: innerString = A - 2: OuterString = B - 2
    '    c = A + Gap * 1 / 2: d = c + Gap * 1 / 2
    '    ' Left
    '    Set BASD = AAA.OvalBridge(.Cells(c + 1, 11), .Cells(d, 11), Direction:=dsLeft, SplitRate_Vertical:=2 / 3 * 100, VerticalSplitDirection:=dvDown, MainText:=innerString)
    '    AAA.OvalBridge .Cells(A, 11), .Cells(c, 11), Direction:=dsLeft, SplitRate_Vertical:=2 / 3 * 100, VerticalSplitDirection:=dvUP, MainText:=innerString
    '    AAA.OvalBridge .Cells(A, 11), .Cells(B, 11), Direction:=dsLeft, SplitRate_Vertical:=1 / 3 * 100, VerticalSplitDirection:=dvBothSide, MainText:=OuterString, LineLength:=BASD.Width + 3
    '    ' Right
    '    Set BASD = AAA.OvalBridge(.Cells(c + 1, 13), .Cells(d, 13), Direction:=dsRight, SplitRate_Vertical:=2 / 3 * 100, VerticalSplitDirection:=dvDown, MainText:=innerString)
    '    AAA.OvalBridge .Cells(A, 13), .Cells(c, 13), Direction:=dsRight, SplitRate_Vertical:=2 / 3 * 100, VerticalSplitDirection:=dvUP, MainText:=innerString
    '    AAA.OvalBridge .Cells(A, 13), .Cells(B, 13), Direction:=dsRight, SplitRate_Vertical:=1 / 3 * 100, VerticalSplitDirection:=dvBothSide, MainText:=OuterString, LineLength:=BASD.Width + 3
    'Next i
    End With
End Sub
    

Public Sub Borders_Test()
    Set ws = ThisWorkbook.Worksheets("test")
    
    Debug.Print ws.Rows.Count
    Debug.Print ws.Columns.Count
    
    With ws.Cells(2, 33)
        '.Borders.Weight = xlThick
        '.Borders.LineStyle = xlContinuous: .value = "xlContinuous"
    '    .Borders.LineStyle = xlDash: .Borders.Weight = xlMedium: .value = "xlDash"
        '.Borders.LineStyle = xlDashDot: .value = "xlDashDot"
        '.Borders.LineStyle = xlDashDotDot: .value = "xlDashDotDot"
        '.Borders.LineStyle = xlDot: .value = "xlDot"
        '.Borders.LineStyle = xlDouble: .value = "xlDouble"
        '.Borders.LineStyle = xlLineStyleNone: .value = "xlLineStyleNone"
        '.Borders.LineStyle = xlSlantDashDot: .value = "xlSlantDashDot"
    End With
End Sub


Public Sub TTESTSET()
    Dim FFF As New Collection
    FFF.Add "Controller"
    FFF.Add "Balance Weight"
    FFF.Add "Knob"
    FFF.Add "Burner"
    
    SortColumnByFeeder FFF
End Sub

Sub t()
Debug.Print Z_Directory.Backup
Debug.Print Z_Directory.BOM

'Debug.Print GetWebText("https://naver.com")
'Debug.Print GetWebText("https://github.com/loborover/AutoReport/tree/main")
End Sub
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
AA_Test.bas End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
LinkToDB.bas Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
LinkToDB.bas End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
AutoReportHandler.frm Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Private Type ChkBx
    Manual_Reporting(1 To 2) As Boolean
    Print_Now(1 To 3) As Boolean
    Kill_Original(1 To 3) As Boolean
End Type

Private Const Color_Inversion_Criterion = 204
Private ii As Long ' 반복문용
Private ws As Worksheet
Private vCB As ChkBx ' CheckBox 관리용
Private BOMLevelCheckBox(1 To 4) As Boolean
Private BOMdir As New Collection, BOM_Level As New Collection
Private Brght As Single 'Brightness
Private pvRGB(1 To 2) As New ObjPivotAxis

Private Sub BackColor_TB_Click()
    BCCUF.Show vbModeless
End Sub
Public Property Get Doc_BackColor() As ObjPivotAxis
    Set Doc_BackColor = pvRGB(1)
End Property
Public Property Let Doc_BackColor(Target As ObjPivotAxis)
    Set pvRGB(1) = Target
End Property

Private Sub CbBx_Feeder_Change()
    D_ListView_Feeder_item_Updater CbBx_Feeder.value
End Sub

Private Sub CbBx_Target_Printer_Change()
    Printer.DefaultPrinter = CbBx_Target_Printer.value
End Sub

Private Sub Userform_Initialize() '전처리
    
    If Not isDirSetUp Then SetUpDirectories
    Dim i As Long, wLine As Long, wDate As Long ' 반복문용 변수
    Set ws = ThisWorkbook.Worksheets("Setting"): Set UI = Me
    
    Me.Version_Label.Caption = "V." & ws.Cells.Find("Version", lookAt:=xlWhole, MatchCase:=True).Offset(0, 1).value
    
    If Not Printer.Bool_IPNS Then Printer.PrinterNameSet ' 프린터 세팅되어 있는지 확인 후 프린터 목록 초기화
    For i = 1 To Printer.PrinterName.Count
        With Me.CbBx_Target_Printer
            .Additem Printer.PrinterName(i)
            If (Printer.DefaultPrinter = Printer.PrinterName(i)) Then .value = Printer.PrinterName(i)
        End With
    Next i
    
    DP_BCBR_Slidebar.value = 218 ' Brightness 초기화
    wLine = 35: wDate = 65
    
    ' CheckBox initializing
    Me.CB_PNBOM.value = True
    Me.CB_KO_ALL.value = True
    Me.CB_MRDP.value = True
    Me.CB_MRPL.value = True
    CB_PNDP_Click
    Me.CB_PNDP.value = False
    CB_PNPL_Click
    Me.CB_PNPL.value = False
    Me.CB_Lvl1_BOM.value = True
    Me.CB_LvlS_BOM.value = True
    CB_PL_Ddays_Click

    With Me.ListView_BOM
        .Left = 168: .Top = 12: .Width = 378: .Height = 192
        .ColumnHeaders.Add text:="Model Number", Width:=120
        .ColumnHeaders.Add text:="Directory", Width:=165
        .ColumnHeaders.Add text:="Print", Width:=45
        .ColumnHeaders.Add text:="PDF", Width:=45
    End With
    
    With Me.ListView_DailyPlan
        .Left = 168: .Top = 12: .Width = 378: .Height = 192
        .ColumnHeaders.Add text:="Date", Width:=wDate
        .ColumnHeaders.Add text:="Line", Width:=wLine
        .ColumnHeaders.Add text:="Directory", Width:=183
        .ColumnHeaders.Add text:="Print", Width:=45
        .ColumnHeaders.Add text:="PDF", Width:=45
        .ColumnHeaders.Add text:="acc", Width:=1
    End With
    
    With Me.ListView_PartList_items
        .Left = 168: .Top = 12: .Width = 378: .Height = 78
        .ColumnHeaders.Add text:="Date", Width:=wDate
        .ColumnHeaders.Add text:="Line", Width:=wLine
        .ColumnHeaders.Add text:="Directory", Width:=183
        .ColumnHeaders.Add text:="Print", Width:=45
        .ColumnHeaders.Add text:="PDF", Width:=45
    End With
    
    With Me.ListView_PLfF_item
        .ColumnHeaders.Add text:="PartList items", Width:=.Width - 3, Alignment:=lvwitemleft
    End With
    
    With Me.ListView_Feeder_item
        .ColumnHeaders.Add text:="Feeder's items", Width:=.Width - 3, Alignment:=lvwitemleft
    End With
    
    With Me.ListView_Feeders
        .ColumnHeaders.Add text:="Feeder's Name", Width:=.Width / 2, Alignment:=lvwitemleft
        .ColumnHeaders.Add text:="item Count", Width:=.Width / 2 - 3
    End With
    
    With Me.ListView_MD_Own
        .Left = 168: .Top = 12: .Width = 180: .Height = 186
'        .ColumnHeaders.Add text:="Date", Width:=wDate
'        .ColumnHeaders.Add text:="Line", Width:=wLine
'        .ColumnHeaders.Add text:="DailyPlan", Width:=50
'        .ColumnHeaders.Add text:="PartList", Width:=50
    End With
        
    With Me.DP_PN_Counter: .Min = 1: .Max = 999: .value = 1: Me.DP_PN_Copies_TB.text = .value: End With
    With Me.PL_PN_Counter: .Min = 1: .Max = 999: .value = 1: Me.DP_PN_Copies_TB.text = .value: End With
    With Me.PL_Ddays_Counter: .Min = 1: .Max = 999: .value = 2: Me.PL_Ddays_TB.text = .value: End With
    
    With Me.MultiPage_FeederChecker
        For i = 0 To .Pages.Count - 1
            .value = i
            DoEvents
        Next i
        .value = 1
    End With
    
    With Me.MultiPage1
        For i = 0 To .Pages.Count - 1
            .value = i
            DoEvents
        Next i
        .value = 1
    End With
    
    SetUp_FeederTrackers ' Feeder.bas 연결
        
End Sub
Private Sub Userform_Terminate() '후처리
    Set BOMdir = Nothing: Set BOM_Level = Nothing: Set UI = Nothing
End Sub
Private Sub DP_BCBR_TB_KeyDown(ByVal KeyCode As MSForms.ReturnInteger, ByVal Shift As Integer)
    If KeyCode = vbKeyReturn Then ' 엔터 키 입력 시
        Dim inputStr As String
        Dim numericVal As Long
        Dim scaledVal As Long

        inputStr = Replace(DP_BCBR_TB.text, "%", "")
        If Trim(inputStr) = "" Then Exit Sub
        If Not isNumeric(inputStr) Then Exit Sub

        numericVal = CDbl(inputStr)
        If numericVal < 0 Then numericVal = 0
        If numericVal > 100 Then numericVal = 100

        scaledVal = Int(numericVal / 100 * 255)
        If scaledVal < DP_BCBR_Slidebar.Min Then scaledVal = DP_BCBR_Slidebar.Min
        If scaledVal > DP_BCBR_Slidebar.Max Then scaledVal = DP_BCBR_Slidebar.Max

        Application.EnableEvents = False
        DP_BCBR_TB.text = Format(numericVal, "0.0") & "%"
        Application.EnableEvents = True

        DP_BCBR_Slidebar.value = scaledVal
        Call DP_BCBR_Slidebar_Change

        KeyCode = 0 ' 삑 소리 방지
    End If
End Sub

Private Sub DP_BCBR_Slidebar_Change()
    Me.DP_BCBR_TB.text = Format((DP_BCBR_Slidebar.value / 255 * 100), "0.0") & "%"
    DP_BCBR_Slidebar.SelLength = DP_BCBR_Slidebar.value
    Brght = DP_BCBR_Slidebar.value
    DP_BCBR_TB.BackColor = RGB(Brght, Brght, Brght)
    Brght = 255 + (Brght * -1)
    DP_BCBR_TB.ForeColor = RGB(Brght, Brght, Brght)
End Sub

Public Property Get Brightness() As Single
    Brightness = DP_BCBR_Slidebar.value
End Property
Private Sub CB_MR_All_Click()
    If CB_MR_All.value Then
        vCB.Manual_Reporting(1) = CB_MRDP.value
        vCB.Manual_Reporting(2) = CB_MRPL.value
        CB_MRDP.value = CB_MR_All.value
        CB_MRPL.value = CB_MR_All.value
        CB_MRDP.Enabled = Not CB_MR_All.value
        CB_MRPL.Enabled = Not CB_MR_All.value
    Else
        CB_MRDP.value = vCB.Manual_Reporting(1)
        CB_MRPL.value = vCB.Manual_Reporting(2)
        CB_MRDP.Enabled = Not CB_MR_All.value
        CB_MRPL.Enabled = Not CB_MR_All.value
    End If
End Sub
Private Sub CB_KO_ALL_Click()
    If CB_KO_ALL.value Then
        vCB.Kill_Original(1) = CB_KO_BOM.value
        vCB.Kill_Original(2) = CB_KO_DP.value
        vCB.Kill_Original(3) = CB_KO_PL.value
        CB_KO_BOM.value = CB_KO_ALL.value
        CB_KO_DP.value = CB_KO_ALL.value
        CB_KO_PL.value = CB_KO_ALL.value
        CB_KO_BOM.Enabled = Not CB_KO_ALL.value
        CB_KO_DP.Enabled = Not CB_KO_ALL.value
        CB_KO_PL.Enabled = Not CB_KO_ALL.value
    Else
        CB_KO_BOM.value = vCB.Kill_Original(1)
        CB_KO_DP.value = vCB.Kill_Original(2)
        CB_KO_PL.value = vCB.Kill_Original(3)
        CB_KO_BOM.Enabled = Not CB_KO_ALL.value
        CB_KO_DP.Enabled = Not CB_KO_ALL.value
        CB_KO_PL.Enabled = Not CB_KO_ALL.value
    End If
End Sub

Private Sub CB_PNALL_Click()
    If CB_PNALL.value Then
'전처리 PrintNow 값 살려두기
        vCB.Print_Now(1) = CB_PNBOM.value
        vCB.Print_Now(2) = CB_PNDP.value
        vCB.Print_Now(3) = CB_PNPL.value
' 전체 True 설정
        CB_PNBOM.value = CB_PNALL.value
        CB_PNDP.value = CB_PNALL.value
        CB_PNPL.value = CB_PNALL.value
' 잠금설정
        CB_PNBOM.Enabled = Not CB_PNALL.value
        CB_PNDP.Enabled = Not CB_PNALL.value
        CB_PNPL.Enabled = Not CB_PNALL.value
    Else
'PrintNow값 반환
        CB_PNBOM.value = vCB.Print_Now(1)
        CB_PNDP.value = vCB.Print_Now(2)
        CB_PNPL.value = vCB.Print_Now(3)
' 잠금설정
        CB_PNBOM.Enabled = Not CB_PNALL.value
        CB_PNDP.Enabled = Not CB_PNALL.value
        CB_PNPL.Enabled = Not CB_PNALL.value
    End If
End Sub

Private Sub CB_PNBOM_Click()
    PrintNow.BOM = CB_PNBOM.value
End Sub

Private Sub CB_PNDP_Click()
    PrintNow.DailyPlan = CB_PNDP.value
    
    Me.DP_PN_Copies_TB.Enabled = Me.CB_PNDP.value
    Me.DP_PN_Counter.Enabled = Me.CB_PNDP.value
End Sub
Private Sub CB_PNPL_Click()
    PrintNow.PartList = CB_PNPL.value
    
    Me.PL_PN_Copies_TB.Enabled = Me.CB_PNPL.value
    Me.PL_PN_Counter.Enabled = Me.CB_PNPL.value
End Sub
Private Sub CB_MRDP_Click()
    BB_DailyPlan_Viewer.MRB_DP = CB_MRDP.value
End Sub
Private Sub CB_MRPL_Click()
    BC_PartListItem_Viewer.MRB_PL = CB_MRPL.value
End Sub
Private Sub CB_PL_Ddays_Click()
    Me.PL_Ddays_Counter.Enabled = Me.CB_PL_Ddays.value
    Me.PL_Ddays_TB.Enabled = Me.CB_PL_Ddays.value
End Sub

Private Sub CB_KO_BOM_Click()
    OriginalKiller.BOM = CB_KO_BOM.value
End Sub

Private Sub CB_KO_DP_Click()
    OriginalKiller.DailyPlan = CB_KO_DP.value
End Sub
Private Sub CB_KO_PL_Click()
    OriginalKiller.PartList = CB_KO_PL.value
End Sub
Private Sub CB_Lvl1_BOM_Click()
    If CB_Lvl1_BOM.value Then
        BOM_Level.Add "0"
        BOM_Level.Add ".1"
    Else
        For ii = BOM_Level.Count To 1 Step -1
            If BOM_Level(ii) = "0" Or BOM_Level(ii) = ".1" Then BOM_Level.Remove ii
        Next
    End If
End Sub
Private Sub CB_Lvl2_BOM_Click()
    If CB_Lvl2_BOM.value Then
        BOM_Level.Add "..2"
    Else
        For ii = BOM_Level.Count To 1 Step -1
            If BOM_Level(ii) = "..2" Then BOM_Level.Remove ii
        Next
    End If
End Sub
Private Sub CB_Lvl3_BOM_Click()
    If CB_Lvl3_BOM.value Then
        BOM_Level.Add "...3"
    Else
        For ii = BOM_Level.Count To 1 Step -1
            If BOM_Level(ii) = "...3" Then BOM_Level.Remove ii
        Next
    End If
End Sub
Private Sub CB_LvlS_BOM_Click()
    If CB_LvlS_BOM.value Then
        BOM_Level.Add "*S*"
    Else
        For ii = BOM_Level.Count To 1 Step -1
            If BOM_Level(ii) = "*S*" Then BOM_Level.Remove ii
        Next
    End If
End Sub
Private Sub CB_LvlAll_BOM_Click()
    If CB_LvlAll_BOM.value Then
'전처리 PrintNow 값 살려두기
        BOMLevelCheckBox(1) = CB_Lvl1_BOM.value
        BOMLevelCheckBox(2) = CB_Lvl2_BOM.value
        BOMLevelCheckBox(3) = CB_Lvl3_BOM.value
        BOMLevelCheckBox(4) = CB_LvlS_BOM.value
' 전체 True 설정
        CB_Lvl1_BOM.value = CB_LvlAll_BOM.value
        CB_Lvl2_BOM.value = CB_LvlAll_BOM.value
        CB_Lvl3_BOM.value = CB_LvlAll_BOM.value
        CB_LvlS_BOM.value = CB_LvlAll_BOM.value
' 잠금설정
        CB_Lvl1_BOM.Enabled = Not CB_LvlAll_BOM.value
        CB_Lvl2_BOM.Enabled = Not CB_LvlAll_BOM.value
        CB_Lvl3_BOM.Enabled = Not CB_LvlAll_BOM.value
        CB_LvlS_BOM.Enabled = Not CB_LvlAll_BOM.value
' BOM_Level 컬렉션 초기화
        For ii = BOM_Level.Count To 1 Step -1
            BOM_Level.Remove ii
        Next ii
' BOM_Level 컬렉션 설정
        BOM_Level.Add "*Q*"
        BOM_Level.Add "*S*"
        BOM_Level.Add "0"
        BOM_Level.Add ".1"
        BOM_Level.Add "..2"
        BOM_Level.Add "...3"
        BOM_Level.Add "....4"
        BOM_Level.Add ".....5"
        BOM_Level.Add "......6"
    Else
'PrintNow값 반환
        CB_Lvl1_BOM.value = BOMLevelCheckBox(1)
        CB_Lvl2_BOM.value = BOMLevelCheckBox(2)
        CB_Lvl3_BOM.value = BOMLevelCheckBox(3)
        CB_LvlS_BOM.value = BOMLevelCheckBox(4)
' 잠금설정
        CB_Lvl1_BOM.Enabled = Not CB_LvlAll_BOM.value
        CB_Lvl2_BOM.Enabled = Not CB_LvlAll_BOM.value
        CB_Lvl3_BOM.Enabled = Not CB_LvlAll_BOM.value
        CB_LvlS_BOM.Enabled = Not CB_LvlAll_BOM.value
' BOM_Level 컬렉션 초기화
        For ii = BOM_Level.Count To 1 Step -1
            BOM_Level.Remove ii
        Next ii
' BOM_Level 컬렉션 재설정
        If CB_Lvl1_BOM.value Then
            BOM_Level.Add ".1"
            BOM_Level.Add "0"
        End If
        If CB_Lvl2_BOM.value Then BOM_Level.Add "..2"
        If CB_Lvl3_BOM.value Then BOM_Level.Add "...3"
        If CB_LvlS_BOM.value Then BOM_Level.Add "*S*"
    End If
End Sub
Private Sub AB_ReadBOM_Click()
    BA_BOM_Viewer.Read_BOM True
End Sub
Private Sub AB_PrintBOM_Click()
    BA_BOM_Viewer.Print_BOM True
End Sub

Private Sub AB_ReadDP_Click()
    BB_DailyPlan_Viewer.Read_DailyPlan True
End Sub
Private Sub AB_PrintDP_Click()
    BB_DailyPlan_Viewer.Print_DailyPlan True
End Sub

Private Sub AB_ReadPL_Click()
    BC_PartListItem_Viewer.Read_PartList True
End Sub
Private Sub AB_PrintPL_Click()
    BC_PartListItem_Viewer.Print_PartList True
End Sub

Private Sub AB_ReadDocs_Click()
    BD_MultiDocuments.Read_Documents True
End Sub

Private Sub AB_DeleteBOM_Click()
    If Not Delete_Each_Documents_For_Key(Me.ListView_BOM) Then BA_BOM_Viewer.Read_BOM False
End Sub
Private Sub AB_DeleteDP_Click()
    If Not Delete_Each_Documents_For_Key(Me.ListView_DailyPlan) Then BB_DailyPlan_Viewer.Read_DailyPlan False
End Sub
Private Sub AB_DeletePL_Click()
    If Not Delete_Each_Documents_For_Key(Me.ListView_PartList_items) Then BC_PartListItem_Viewer.Read_PartList False
End Sub

Private Sub AB_Cleaner_Click()
    Cleaner_Handler.Show vbModeless
End Sub
Private Sub AB_DeleteFeeder_Click()
    BCA_PLIV_Feeder.A_Delete_Feeder
End Sub
Private Sub AB_NewFeeder_Click()
    BCA_PLIV_Feeder.A_New_Feeder
End Sub
Private Sub AB_SaveFeeder_Click()
    BCA_PLIV_Feeder.A_Save_Feeder
End Sub
Private Sub AB_itemAdder_Click()
    BCA_PLIV_Feeder.C_Additem_List
End Sub

Private Sub AB_itemKiller_Click()
    BCA_PLIV_Feeder.C_Removeitem_List
End Sub
Private Function Delete_Each_Documents_For_Key(ByRef TargetListView As ListView) As Boolean
    Dim Target_item As ListItems: Set Target_item = TargetListView.ListItems
    Dim DeleteList As Long
    Dim i As Long, Ai As Long, Dir As Long
    
    For i = 1 To TargetListView.ColumnHeaders.Count
        If TargetListView.ColumnHeaders.Item(i) = "Directory" Then Dir = i - 1: Exit For
    Next i
    
    For i = 1 To Target_item.Count
        If Target_item(i).Checked = True Then DeleteList = DeleteList + 1
    Next i
    
    If DeleteList < 1 Then MsgBox "삭제할 항목 없음": Exit Function
    
    On Error Resume Next
    
    For i = 1 To Target_item.Count
        If Target_item(i).Checked = True Then
            Kill Target_item(i).SubItems(Dir)
            For Ai = 0 To 2
                Target_item(i).SubItems(Dir + Ai) = "Deleted"
            Next Ai
            Target_item(i).Checked = False
        End If
    Next i
    
    On Error GoTo 0
    Delete_Each_Documents_For_Key = True
End Function

Private Sub AB_Export2Zip_Click()
    Dim Temp As String
    Temp = ThisWorkbook.Path & "\ExcelExportedCodes"
    Cleaner.FolderKiller Temp
    Call AA_Updater.ExportAllVbaComponents
    Call AA_Updater.ExportAllModulesDirectlyToTextAndMarkdown
End Sub
Private Sub DP_PN_Copies_TB_KeyPress(ByVal KeyAscii As MSForms.ReturnInteger)
    ' 0~9와 백스페이스(8)만 허용
    If Not (KeyAscii >= 48 And KeyAscii <= 57 Or KeyAscii = 8) Then
        KeyAscii = 0
    End If
End Sub
Private Sub DP_PN_Copies_TB_Change()
    If Me.DP_PN_Copies_TB.text = "" Or Val(Me.DP_PN_Copies_TB.text) = 0 Then
        Me.DP_PN_Copies_TB.text = 1
        Me.DP_PN_Counter.value = 1
    Else
        Me.DP_PN_Counter.value = Val(Me.DP_PN_Copies_TB.text)
    End If
End Sub
Private Sub DP_PN_Counter_Change()
    Me.DP_PN_Copies_TB.text = Me.DP_PN_Counter.value
End Sub

Private Sub PL_PN_Copies_TB_KeyPress(ByVal KeyAscii As MSForms.ReturnInteger)
    ' 0~9와 백스페이스(8)만 허용
    If Not (KeyAscii >= 48 And KeyAscii <= 57 Or KeyAscii = 8) Then
        KeyAscii = 0
    End If
End Sub
Private Sub PL_PN_Copies_TB_Change()
    If Me.PL_PN_Copies_TB.text = "" Or Val(Me.PL_PN_Copies_TB.text) = 0 Then
        Me.PL_PN_Copies_TB.text = 1
        Me.PL_PN_Counter.value = 1
    Else
        Me.PL_PN_Counter.value = Val(Me.PL_PN_Copies_TB.text)
    End If
End Sub
Private Sub PL_PN_Counter_Change()
    Me.PL_PN_Copies_TB.text = Me.PL_PN_Counter.value
End Sub

Private Sub PL_Ddays_TB_KeyPress(ByVal KeyAscii As MSForms.ReturnInteger)
    ' 0~9와 백스페이스(8)만 허용
    If Not (KeyAscii >= 48 And KeyAscii <= 57 Or KeyAscii = 8) Then
        KeyAscii = 0
    End If
End Sub
Private Sub PL_Ddays_TB_Change()
    If Me.PL_Ddays_TB.text = "" Or Val(Me.PL_Ddays_TB.text) = 0 Then
        Me.PL_Ddays_TB.text = 1
        Me.PL_Ddays_Counter.value = 1
    ElseIf Me.PL_Ddays_TB.text > Me.PL_Ddays_Counter.Max Then
        Me.PL_Ddays_TB.text = Me.PL_Ddays_Counter.Max
    Else
        Me.PL_Ddays_Counter.value = Val(Me.PL_Ddays_TB.text)
    End If
End Sub
Private Sub PL_Ddays_Counter_Change()
    Me.PL_Ddays_TB.text = Me.PL_Ddays_Counter.value
End Sub
Public Sub UpdateProgressBar(ProgressBar As MSComctlLib.ProgressBar, _
                                        ByRef Index As Single, _
                                        Optional vMin As Single = 0, _
                                        Optional vMax As Single = 100)
    With ProgressBar
        .Min = vMin
        .Max = vMax
        .value = Index
    End With
End Sub

Public Property Get itemLevel() As Collection
    Set itemLevel = BOM_Level
End Property

Private Sub MultiPage_FeederChecker_Change()
    If MultiPage_FeederChecker.value = 0 Then D_ListView_Feeder_Updater
End Sub
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
AutoReportHandler.frm End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
StickerLabel.cls Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
'스티커라벨의 그림 드로잉 기능을 위한 클래스
Private DrawingWS As Worksheet
'초기화 이벤트 메서드
'Private Sub Class_Initialize()
'
'End Sub
'소멸 이벤트 메서드
'Private Sub Class_Terminate()
'
'End Sub
Friend Property Set Worksheet(ByRef value As Worksheet)
    Set DrawingWS = value
End Property

Public Function Left(ByRef Xaxis As Single, ByRef Yaxis As Single, _
                                Optional ByRef BorderWeight As Single = 0.5, _
                                Optional ByRef MainText As String = "None", _
                                Optional SubTexts As Collection, _
                                Optional ByVal LabelBeginType As arLabelShape = Round, _
                                Optional ByVal LabelEndType As arLabelShape = Box_Hexagon, _
                                Optional BridgeLineWeight As Single, _
                                Optional ByVal HoleLineDirection As ObjDirection4Way = d4RIGHT) As Shape
                                
    Set Left = StickerLabelSide(Xaxis, Yaxis, dsLeft, _
                    BorderWeight, MainText, SubTexts, LabelBeginType, LabelEndType, BridgeLineWeight, HoleLineDirection)
End Function

Public Function Right(ByRef Xaxis As Single, ByRef Yaxis As Single, _
                                Optional ByRef BorderWeight As Single = 0.5, _
                                Optional ByRef MainText As String = "None", _
                                Optional SubTexts As Collection, _
                                Optional ByVal LabelBeginType As arLabelShape = Round, _
                                Optional ByVal LabelEndType As arLabelShape = Box_Hexagon, _
                                Optional BridgeLineWeight As Single, _
                                Optional ByVal HoleLineDirection As ObjDirection4Way = d4LEFT) As Shape
                                
    Set Right = StickerLabelSide(Xaxis, Yaxis, dsRight, _
                    BorderWeight, MainText, SubTexts, LabelBeginType, LabelEndType, BridgeLineWeight, HoleLineDirection)
End Function

Public Function Up(ByRef Xaxis As Single, ByRef Yaxis As Single, _
                                Optional ByRef BorderWeight As Single = 0.5, _
                                Optional ByRef MainText As String = "None", _
                                Optional SubTexts As Collection, _
                                Optional ByRef LabelBeginType As arLabelShape = Vrtcl_Rounded, _
                                Optional ByRef LabelEndType As arLabelShape = Vrtcl_Connecter, _
                                Optional ByRef BridgeLineWeight As Single, _
                                Optional ByVal HoleLineDirection As ObjDirection4Way = d4DOWN) As Shape
                                    
    Set Up = StickerLabelVertical(Xaxis, Yaxis, dvUP, _
                    BorderWeight, MainText, SubTexts, LabelBeginType, LabelEndType, BridgeLineWeight, HoleLineDirection)
                                
End Function

Public Function Down(ByRef Xaxis As Single, ByRef Yaxis As Single, _
                                Optional ByRef BorderWeight As Single = 0.5, _
                                Optional ByRef MainText As String = "None", _
                                Optional SubTexts As Collection, _
                                Optional ByRef LabelBeginType As arLabelShape = Vrtcl_Rounded, _
                                Optional ByRef LabelEndType As arLabelShape = Vrtcl_Connecter, _
                                Optional ByRef BridgeLineWeight As Single, _
                                Optional ByVal HoleLineDirection As ObjDirection4Way = d4UP) As Shape
                                
    Set Down = StickerLabelVertical(Xaxis, Yaxis, dvDown, _
                    BorderWeight, MainText, SubTexts, LabelBeginType, LabelEndType, BridgeLineWeight, HoleLineDirection)

End Function

Public Function AutoDirection() As Shape

End Function
' StickerLabel Single
Public Function SingleLabel(ByRef Xaxis As Single, ByRef Yaxis As Single, _
                                            Optional ByRef Length As Single = 10, _
                                            Optional ByRef LineWeight As Single = 1, _
                                            Optional ByRef Direction As ObjDirection4Way = d4UP, _
                                            Optional ByRef MainText As String = "None") As Shape
' Label Processing
    Dim LineEndPivot As New ObjPivotAxis ' 개체의 연산지점
    Dim shp(1 To 2) As Shape
    
    Set shp(2) = DrawingWS.Shapes.AddLabel(msoTextOrientationHorizontal, 0, 0, 0, 0)
    ApplyMainTextBoxSettings shp(2), MainText, 1.5
    
    With shp(2) ' 너비와 높이 추출 후 Length에 반영
        If Direction = d4LEFT Or Direction = d4RIGHT Then Length = Length + .Width / 2
        If Direction = d4UP Or Direction = d4DOWN Then Length = Length + .Height / 2
    End With
    
    If Direction = d4UP Or Direction = d4LEFT Then Length = Length * -1 ' UP과 LEFT는 반전
    
    Select Case Direction ' 방향타
    Case d4LEFT, d4RIGHT
        LineEndPivot.X = Xaxis + Length
        LineEndPivot.Y = Yaxis
    Case d4UP, d4DOWN
        LineEndPivot.X = Xaxis
        LineEndPivot.Y = Yaxis + Length
    End Select

    Set shp(1) = DrawingWS.Shapes.AddLine(Xaxis, Yaxis, LineEndPivot.X, LineEndPivot.Y)
    
    With shp(1) ' 메인 라인
        .line.ForeColor.RGB = RGB(0, 0, 0)
        .line.Weight = LineWeight + 1
        .ZOrder msoSendToBack
    End With
    
    With shp(2) ' 라벨 위치선정 및 외향 설정
        .Visible = msoTrue
        .Fill.ForeColor.RGB = RGB(255, 255, 255)
        .Left = LineEndPivot.X - .Width / 2
        .Top = LineEndPivot.Y - .Height / 2
        .line.Visible = msoTrue
        .line.Weight = LineWeight
        .line.ForeColor.RGB = RGB(0, 0, 0)
        .TextFrame.MarginLeft = .TextFrame.MarginLeft + 1.5
        .TextFrame.MarginRight = .TextFrame.MarginRight + 1.5
    End With
    
    ' Grouping 코드
    Dim shpNames() As String
    ReDim shpNames(1 To UBound(shp))

    For i = 1 To UBound(shp)
        shpNames(i) = shp(i).Name
    Next i

    Set SingleLabel = DrawingWS.Shapes.Range(shpNames).Group

End Function

' StickerLabel Vertical
Private Function StickerLabelVertical(ByRef Xaxis As Single, ByRef Yaxis As Single, _
                                Optional ByRef Direction As ObjDirectionVertical = dvUP, _
                                Optional ByRef BorderWeight As Single = 0.5, _
                                Optional ByRef MainText As String = "None", _
                                Optional SubTexts As Collection, _
                                Optional ByRef LabelBeginType As arLabelShape = Vrtcl_Rounded, _
                                Optional ByRef LabelEndType As arLabelShape = Vrtcl_Connecter, _
                                Optional ByRef BridgeLineWeight As Single, _
                                Optional ByVal HoleLineDirection As ObjDirection4Way = d4LEFT) As Shape
    
    Dim LabelPivot As New ObjPivotAxis ' 개체의 생성지점
    Dim Position As New ObjPivotAxis ' 개체의 연산지점
    
    Const NormalHeight As Single = 20 ' Begin, End ShapeType의 높이
    Const Gap As Single = 3 ' 세로간격 상수
    Const Spacing As Single = 2 ' 가로간격 상수
    Dim MainTextHeight As Single
    Dim SubTextHeight As Single
    Dim LabelHeight As Single ' 중간 Rectangle Shape의 높이
    Dim LabelWidth As Single ' 중간 Rectangle Shape의 너비
    Dim LabelLineWeight(1 To 3) As Single
    Dim LabelFloor As Long ' SubTexts 갯수를 뜻함. 예) 메인텍스트(1단) = 0, 타이틀/메인텍스트(2단) = 1, 타이틀/설명//메인텍스트(3단) = 2
    Dim Transfer As New ObjPivotAxis ' 입력된 텍스트로 부터 값을 추출해 높이, 너비 자동계산을 위한 전달용 변수
    
    Dim shp(1 To 10) As Shape
    Dim HoleLine As Shape
    
    If Not SubTexts Is Nothing Then
        LabelFloor = SubTexts.Count
    End If
    '변수초기화
    MainTextHeight = 18
    SubTextHeight = 15
    
    Set Transfer = GetStringMaxWidthNHeight(MainText, SubTexts) ' 입력된 텍스트의 최대 길이를 찾아 너비와 메인, 서브텍스트의 높이를 결정함.
    SubTextHeight = Transfer.Z
    MainTextHeight = Transfer.Y
    LabelWidth = Transfer.X + Spacing * 2 ' 중간 Rectangle 의 너비 값
    LabelHeight = MainTextHeight + Gap * 2 + (LabelFloor * (SubTextHeight + Gap)) ' 중간 Rectangle 의 높이 값
    
    LabelPivot.X = Xaxis
    LabelPivot.Y = Yaxis
    Position.X = LabelPivot.X - LabelWidth / 2
    Position.Y = LabelPivot.Y - LabelHeight - NormalHeight / 2
    
    If Position.X <= 0 Or Position.Y <= 0 Then
        MsgBox "Position Error"
        Exit Function
    End If
    
    LabelLineWeight(2) = BorderWeight * 3
    LabelLineWeight(1) = LabelLineWeight(2) + 2
' Outter White Line(1,2,3), Outter Black Line(4,5,6), Inner Shape(7,8,9)
    For i = 1 To 3
        Dim Part As Long
        Part = (i - 1) * 3
        Set shp(Part + 1) = DrawingWS.Shapes.AddShape(LabelBeginType, Position.X, Position.Y, LabelWidth, NormalHeight)
        Set shp(Part + 2) = DrawingWS.Shapes.AddShape(LabelEndType, Position.X, Position.Y + LabelHeight, LabelWidth, NormalHeight)
        Set shp(Part + 3) = DrawingWS.Shapes.AddShape(msoShapeRectangle, Position.X, Position.Y + NormalHeight / 2, LabelWidth, LabelHeight)
    Next i
            
    For i = 1 To 3
        With shp(i)
            .Fill.Visible = msoFalse
            .line.Visible = msoTrue
            .line.ForeColor.RGB = RGB(255, 255, 255)
            .line.Weight = LabelLineWeight(1)
        End With
    Next i
    
    For i = 4 To 6
        With shp(i)
            .Fill.Visible = msoFalse
            .line.Visible = msoTrue
            .line.ForeColor.RGB = RGB(0, 0, 0)
            .line.Weight = LabelLineWeight(2)
        End With
    Next i
    
    For i = 7 To 9
        With shp(i)
            .Fill.Visible = msoTrue
            .line.Visible = msoFalse
            .Fill.ForeColor.RGB = RGB(255, 255, 255)
        End With
    Next i
' LabelPivot Hole
    Dim HolePivot As New ObjPivotAxis
    Dim HoleSize As Single
    HoleSize = 8
    LabelLineWeight(3) = (LabelLineWeight(1) + LabelLineWeight(2)) / 4
    HolePivot.X = LabelPivot.X - HoleSize / 2
    HolePivot.Y = LabelPivot.Y - HoleSize / 2
    Set shp(10) = DrawingWS.Shapes.AddShape(msoShapeOval, HolePivot.X, HolePivot.Y, HoleSize, HoleSize)
    With shp(10)
        .Fill.ForeColor.RGB = RGB(0, 0, 0)
        .line.Visible = msoFalse
    End With
' Set semi Group
    Dim ShpName() As String
    ReDim ShpName(LBound(shp) To UBound(shp))
    For i = LBound(ShpName) To UBound(ShpName)
        ShpName(i) = shp(i).Name
    Next i
    Dim SemiGroup As Shape
    Set SemiGroup = DrawingWS.Shapes.Range(ShpName).Group
' Text Label
    Dim TxtBox() As Shape
    ReDim TxtBox(0 To LabelFloor) As Shape ' 0=MainText, 1~99 = SubText
    Set TxtBox(0) = DrawingWS.Shapes.AddLabel(msoTextOrientationHorizontal, 0, 0, 0, 0)
    Call ApplyMainTextBoxSettings(TxtBox(0), MainText)
    
    
        With TxtBox(0)
            .Visible = msoTrue
            .Width = LabelWidth
            .Left = Position.X
            .Top = Position.Y + NormalHeight / 2 + Gap + (LabelFloor * (SubTextHeight + Gap))
                If Direction = dvDown Then .Top = .Top + LabelHeight
        End With
    
    If Not SubTexts Is Nothing Then
        
            For i = 1 To LabelFloor
                Set TxtBox(i) = DrawingWS.Shapes.AddLabel(msoTextOrientationHorizontal, 0, 0, 0, 0)
            Call ApplySubTextBoxSettings(TxtBox(i), SubTexts.Item(i))
                With TxtBox(i)
                    .Visible = msoTrue
                    .Width = LabelWidth - Spacing
                    .Left = Position.X + Spacing
                    .Top = Position.Y + NormalHeight / 2 + Gap + ((LabelFloor - (LabelFloor - i + 1)) * (SubTextHeight + Gap))
                        If Direction = dvDown Then .Top = .Top + LabelHeight
                    
                End With
            Next i
        
        ' Gap Line
            Dim GLine() As Shape
            ReDim GLine(1 To LabelFloor) As Shape
            
            For i = 1 To UBound(GLine)
            
                Dim LinePoint As New ObjPivotAxis ' 라인 지정 로직의 가독성을 위한 변수 선언
                LinePoint.X = Position.X + Spacing
                LinePoint.Z = Position.X + LabelWidth - Spacing
                LinePoint.Y = Position.Y + (NormalHeight + Gap) / 2 + (((LabelFloor + 1) - i) * (SubTextHeight + Gap))
                If Direction = dvDown Then LinePoint.Y = LinePoint.Y + LabelHeight
            
                Set GLine(i) = DrawingWS.Shapes.AddLine(LinePoint.X, LinePoint.Y, LinePoint.Z, LinePoint.Y)
                With GLine(i)
                    '.Visible = msoFalse ' 디버깅 중엔 라인 지우기
                    .line.ForeColor.RGB = RGB(0, 0, 0)
                    .line.Weight = 1
                End With
            Next i
    End If
' Set Txt Group
    If Not SubTexts Is Nothing Then
        Dim Collecter As New Collection
        ReDim ShpName(LBound(TxtBox) To UBound(TxtBox) + UBound(GLine)) ' Textframe
        For i = LBound(TxtBox) To UBound(TxtBox)
            Collecter.Add TxtBox(i).Name
        Next i
        For i = LBound(GLine) To UBound(GLine)
            Collecter.Add GLine(i).Name
        Next i
        For i = LBound(ShpName) To UBound(ShpName)
            ShpName(i) = Collecter(i + 1)
        Next i
        Dim TxtGroup As Shape
        Set TxtGroup = DrawingWS.Shapes.Range(ShpName).Group
    End If
' Rotation Setting
    If Direction = dvUP Then
        SemiGroup.Rotation = 0
    ElseIf Direction = dvDown Then
        With SemiGroup
            .Rotation = 180
            .Top = .Top + LabelHeight
        End With
    End If
' Set Group
    If Not SubTexts Is Nothing Then
        Set SemiGroup = DrawingWS.Shapes.Range(Array(SemiGroup.Name, TxtGroup.Name)).Group
    Else
        Set SemiGroup = DrawingWS.Shapes.Range(Array(SemiGroup.Name, TxtBox(0).Name)).Group
    End If
' Hole Through Line
    Set HoleLine = PivotHoleNLineDirection(LabelPivot, HoleSize, SemiGroup, BridgeLineWeight, LabelLineWeight(1), HoleLineDirection)
    
    Set StickerLabelVertical = DrawingWS.Shapes.Range(Array(SemiGroup.Name, HoleLine.Name)).Group

'Debuging
'Call SpotChecker(Position.X, Position.Y)
'Call SpotChecker(LabelPivot.X, LabelPivot.Y)
End Function
' StickerLabel Side
Private Function StickerLabelSide(ByRef Xaxis As Single, ByRef Yaxis As Single, _
                                Optional ByRef Direction As ObjDirectionSide = dsLeft, _
                                Optional ByRef BorderWeight As Single = 0.5, _
                                Optional ByRef MainText As String = "None", _
                                Optional SubTexts As Collection, _
                                Optional ByRef LabelBeginType As arLabelShape = Round, _
                                Optional ByRef LabelEndType As arLabelShape = Box_Hexagon, _
                                Optional ByRef BridgeLineWeight As Single, _
                                Optional ByVal HoleLineDirection As ObjDirection4Way = d4DOWN) As Shape
    
    Dim LabelPivot As New ObjPivotAxis ' 개체의 생성지점
    Dim Position As New ObjPivotAxis ' 개체의 연산지점
    
    Const NormalWidth As Single = 20 ' Begin, End ShapeType의 너비
    Const Gap As Single = 0 ' 세로간격 상수
    Const Spacing As Single = 10 ' 가로간격 상수
    Dim MainTextHeight As Single
    Dim SubTextHeight As Single
    Dim LabelHeight As Single ' 중간 Rectangle Shape의 높이
    Dim LabelWidth As Single ' 중간 Rectangle Shape의 너비
    Dim LabelLineWeight(1 To 3) As Single
    Dim LabelFloor As Long ' SubTexts 갯수를 뜻함. 예) 메인텍스트(1단) = 0, 타이틀/메인텍스트(2단) = 1, 타이틀/설명//메인텍스트(3단) = 2
    Dim Transfer As New ObjPivotAxis ' 입력된 텍스트로 부터 값을 추출해 높이, 너비 자동계산을 위한 전달용 변수
    
    Dim shp(1 To 10) As Shape
    Dim HoleLine As Shape
    
    If Not SubTexts Is Nothing Then
        LabelFloor = SubTexts.Count
    End If
    '변수초기화
    MainTextHeight = 18
    SubTextHeight = 15
    
    Set Transfer = GetStringMaxWidthNHeight(MainText, SubTexts) ' 입력된 텍스트의 최대 길이를 찾아 너비와 메인, 서브텍스트의 높이를 결정함.
    SubTextHeight = Transfer.Z
    MainTextHeight = Transfer.Y
    LabelWidth = Transfer.X + Spacing ' 중간 Rectangle 의 너비 값
    LabelHeight = MainTextHeight + Gap * 2 + (LabelFloor * (SubTextHeight + Gap)) ' 중간 Rectangle 의 높이 값
    
    LabelPivot.X = Xaxis
    LabelPivot.Y = Yaxis
    Position.X = LabelPivot.X - NormalWidth / 2 - LabelWidth
    Position.Y = LabelPivot.Y - LabelHeight / 2
    
    If Position.X <= 0 Or Position.Y <= 0 Then
        MsgBox "Position Error"
        Exit Function
    End If
    
    LabelLineWeight(2) = BorderWeight * 3
    LabelLineWeight(1) = LabelLineWeight(2) + 2
' Outter White Line(1,2,3), Outter Black Line(4,5,6), Inner Shape(7,8,9)
    For i = 1 To 3
        Dim Part As Long
        Part = (i - 1) * 3
        Set shp(Part + 1) = DrawingWS.Shapes.AddShape(LabelBeginType, Position.X, Position.Y, NormalWidth, LabelHeight)
        Set shp(Part + 2) = DrawingWS.Shapes.AddShape(LabelEndType, Position.X + LabelWidth, Position.Y, NormalWidth, LabelHeight)
        Set shp(Part + 3) = DrawingWS.Shapes.AddShape(msoShapeRectangle, Position.X + (NormalWidth / 2), Position.Y, LabelWidth, LabelHeight)
    Next i
    
    For i = 1 To 3
        With shp(i)
            .Fill.Visible = msoFalse
            .line.Visible = msoTrue
            .line.ForeColor.RGB = RGB(255, 255, 255)
            .line.Weight = LabelLineWeight(1)
        End With
    Next i
    
    For i = 4 To 6
        With shp(i)
            .Fill.Visible = msoFalse
            .line.Visible = msoTrue
            .line.ForeColor.RGB = RGB(0, 0, 0)
            .line.Weight = LabelLineWeight(2)
        End With
    Next i
    
    For i = 7 To 9
        With shp(i)
            .Fill.Visible = msoTrue
            .line.Visible = msoFalse
            .Fill.ForeColor.RGB = RGB(255, 255, 255)
        End With
    Next i
' LabelPivot Hole
    Dim HolePivot As New ObjPivotAxis
    Dim HoleSize As Single
    HoleSize = 8
    LabelLineWeight(3) = (LabelLineWeight(1) + LabelLineWeight(2)) / 4
    HolePivot.X = LabelPivot.X - HoleSize / 2
    HolePivot.Y = LabelPivot.Y - HoleSize / 2
    Set shp(10) = DrawingWS.Shapes.AddShape(msoShapeOval, HolePivot.X, HolePivot.Y, HoleSize, HoleSize)
    With shp(10)
        .Fill.ForeColor.RGB = RGB(0, 0, 0)
        .line.Visible = msoFalse
    End With
' Set semi Group
    Dim ShpName() As String
    ReDim ShpName(LBound(shp) To UBound(shp))
    For i = LBound(ShpName) To UBound(ShpName)
        ShpName(i) = shp(i).Name
    Next i
    Dim SemiGroup As Shape
    Set SemiGroup = DrawingWS.Shapes.Range(ShpName).Group
' Text Label
    Dim TxtBox() As Shape
    ReDim TxtBox(0 To LabelFloor) As Shape ' 0=MainText, 1~99 = SubText
    Set TxtBox(0) = DrawingWS.Shapes.AddLabel(msoTextOrientationHorizontal, 0, 0, 0, 0)
    Call ApplyMainTextBoxSettings(TxtBox(0), MainText)
    
        With TxtBox(0)
            .Visible = msoTrue
            .Width = LabelWidth
            If Direction = dsLeft Then
                .Left = Position.X + NormalWidth / 2 - Spacing
            ElseIf Direction = dsRight Then
                .Left = LabelPivot.X + NormalWidth / 2
            End If
            .Top = Position.Y + Gap * 2 + (LabelFloor * (SubTextHeight + Gap))
        End With
    
    If Not SubTexts Is Nothing Then
        
            For i = 1 To LabelFloor
                Set TxtBox(i) = DrawingWS.Shapes.AddLabel(msoTextOrientationHorizontal, 0, 0, 0, 0)
            Call ApplySubTextBoxSettings(TxtBox(i), SubTexts.Item(i))
                With TxtBox(i)
                    .Visible = msoTrue
                    .Width = LabelWidth
                    If Direction = dsLeft Then
                        .Left = Position.X + NormalWidth / 2
                    ElseIf Direction = dsRight Then
                        .Left = LabelPivot.X + NormalWidth - Spacing
                    End If
                    .Top = Position.Y + Gap * 2 + ((LabelFloor - (LabelFloor - i + 1)) * (SubTextHeight + Gap))
                End With
            Next i
        
        ' Gap Line
            Dim GLine() As Shape
            ReDim GLine(1 To LabelFloor) As Shape
            For i = 1 To UBound(GLine)
            If Direction = dsLeft Then
                Set GLine(i) = DrawingWS.Shapes.AddLine(Position.X + NormalWidth / 2, Position.Y + Gap / 2 + (((LabelFloor + 1) - i) * (SubTextHeight + Gap)), _
                                                LabelPivot.X - Spacing, Position.Y + Gap / 2 + (((LabelFloor + 1) - i) * (SubTextHeight + Gap)))
            ElseIf Direction = dsRight Then
                Set GLine(i) = DrawingWS.Shapes.AddLine(LabelPivot.X + NormalWidth / 2, Position.Y + Gap / 2 + (((LabelFloor + 1) - i) * (SubTextHeight + Gap)), _
                                                LabelPivot.X + LabelWidth, Position.Y + Gap / 2 + (((LabelFloor + 1) - i) * (SubTextHeight + Gap)))
            End If
                With GLine(i)
                    .line.ForeColor.RGB = RGB(0, 0, 0)
                    .line.Weight = 1
                End With
            Next i
    End If
' Set Txt Group
    If Not SubTexts Is Nothing Then
        Dim Collecter As New Collection
        ReDim ShpName(LBound(TxtBox) To UBound(TxtBox) + UBound(GLine)) ' Textframe
        For i = LBound(TxtBox) To UBound(TxtBox)
            Collecter.Add TxtBox(i).Name
        Next i
        For i = LBound(GLine) To UBound(GLine)
            Collecter.Add GLine(i).Name
        Next i
        For i = LBound(ShpName) To UBound(ShpName)
            ShpName(i) = Collecter(i + 1)
        Next i
        Dim TxtGroup As Shape
        Set TxtGroup = DrawingWS.Shapes.Range(ShpName).Group
    End If
' Rotation Setting
    If Direction = dsLeft Then
        SemiGroup.Rotation = 0
    ElseIf Direction = dsRight Then
        With SemiGroup
            .Rotation = 180
            .Left = .Left + LabelWidth
        End With
    End If
' Set Group
    If Not SubTexts Is Nothing Then
        Set SemiGroup = DrawingWS.Shapes.Range(Array(SemiGroup.Name, TxtGroup.Name)).Group
    Else
        Set SemiGroup = DrawingWS.Shapes.Range(Array(SemiGroup.Name, TxtBox(0).Name)).Group
    End If
' Hole Through Line
    Set HoleLine = PivotHoleNLineDirection(LabelPivot, HoleSize, SemiGroup, BridgeLineWeight, LabelLineWeight(1), HoleLineDirection)
    
    Set StickerLabelSide = DrawingWS.Shapes.Range(Array(SemiGroup.Name, HoleLine.Name)).Group

'Debuging
'Call SpotChecker(Position.X, Position.Y, Gap:=40)
'Call SpotChecker(LabelPivot.X, LabelPivot.Y)
End Function
Private Function GetStringMaxWidthNHeight(MainText As String, SubTexts As Collection) As ObjPivotAxis
    Dim longestText As String
    Dim TextBox As Shape
    Dim Result As New ObjPivotAxis
    
    ' 가장 긴 텍스트 찾기
    longestText = MainText ' 우선 MainText를 가장 긴 텍스트로 가정
    MainOrSub = True ' MainText가 기본적으로 가장 길다고 가정
    
    If Not SubTexts Is Nothing Then
        For Each Item In SubTexts
            If Len(Item) > Len(longestText) Then longestText = Item
        Next Item
    End If

    ' MainText의 TextBox 설정 및 너비, 높이 측정
    Set TextBox = DrawingWS.Shapes.AddLabel(msoTextOrientationHorizontal, 0, 0, 0, 0)
    Call ApplyMainTextBoxSettings(TextBox, MainText)
    Result.Y = TextBox.Height ' MainText의 높이
    Result.X = TextBox.Width ' MainText가 가장 길다고 가정하고 지정
    TextBox.Delete

    ' SubTexts의 TextBox 설정 및 너비, 높이 측정 (MainText가 가장 길지 않은 경우)
    If Not SubTexts Is Nothing Then
        Set TextBox = DrawingWS.Shapes.AddLabel(msoTextOrientationHorizontal, 0, 0, 0, 0)
        Call ApplySubTextBoxSettings(TextBox, longestText)
        If TextBox.Width > Result.X Then Result.X = TextBox.Width        ' SubText 중 가장 긴 텍스트의 너비
        Result.Z = TextBox.Height ' SubText 중 가장 긴 텍스트의 높이
        TextBox.Delete
    End If

    Set GetStringMaxWidthNHeight = Result
End Function

Private Sub ApplyMainTextBoxSettings(ByRef TextBox As Shape, ByRef text As String, _
                                                Optional ByRef Margin As Single = 0)
    With TextBox
        .Visible = msoFalse
        .TextFrame.AutoSize = True
        .TextFrame2.WordWrap = msoFalse
        .TextFrame.Characters.text = text
        .TextFrame.Characters.Font.Name = "LG스마트체2.0 Bold"
        .TextFrame.Characters.Font.Size = 17
        .TextFrame.Characters.Font.Bold = msoTrue
        .TextFrame.HorizontalAlignment = xlHAlignCenter
        .TextFrame.VerticalAlignment = xlVAlignCenter
        .TextFrame.MarginBottom = Margin
        .TextFrame.MarginTop = Margin
        .TextFrame.MarginLeft = Margin
        .TextFrame.MarginRight = Margin
    End With
End Sub
Private Sub ApplySubTextBoxSettings(ByRef TextBox As Shape, ByRef text As String, _
                                                Optional ByRef Margin As Single = 0)
    With TextBox
        .Visible = msoFalse
        .TextFrame.AutoSize = True
        .TextFrame2.WordWrap = msoFalse
        .TextFrame.Characters.text = text
        .TextFrame.Characters.Font.Name = "LG스마트체2.0 SemiBold"
        .TextFrame.Characters.Font.Size = 15
        .TextFrame.Characters.Font.Bold = msoFalse
        .TextFrame.HorizontalAlignment = xlHAlignLeft
        .TextFrame.VerticalAlignment = xlVAlignCenter
        .TextFrame.MarginBottom = Margin
        .TextFrame.MarginTop = Margin
        .TextFrame.MarginLeft = Margin
        .TextFrame.MarginRight = Margin
    End With
End Sub
Private Function PivotHoleNLineDirection(ByRef HolePivot As ObjPivotAxis, ByRef HoleSize As Single, _
                                                    ByRef TargetShape As Shape, _
                                                    Optional ByRef BorderWeight As Single = 1, _
                                                    Optional ByRef LineWeight As Single = 1, _
                                                    Optional LineDirection As ObjDirection4Way = d4DOWN) As Shape
    Dim shp(1 To 3) As Shape
    Dim Pivot(1 To 3) As New ObjPivotAxis
    Dim Size(1 To 2) As Single
    Size(1) = HoleSize * 1 / 2
    Size(2) = HoleSize * 1 / 4
    
    For i = 1 To 2
        Pivot(i).X = HolePivot.X - Size(i) / 2
        Pivot(i).Y = HolePivot.Y - Size(i) / 2
    Next i
    
    With TargetShape
        
        Select Case LineDirection
        Case d4UP
            Pivot(3).X = HolePivot.X
            Pivot(3).Y = .Top - LineWeight / 2
        Case d4DOWN
            Pivot(3).X = HolePivot.X
            Pivot(3).Y = .Top + .Height + LineWeight / 2
        Case d4LEFT
            Pivot(3).X = .Left - LineWeight / 2
            Pivot(3).Y = HolePivot.Y
        Case d4RIGHT
            Pivot(3).X = .Left + .Width + LineWeight / 2
            Pivot(3).Y = HolePivot.Y
        End Select
        
    End With
    
    Set shp(1) = DrawingWS.Shapes.AddShape(msoShapeOval, Pivot(1).X, Pivot(1).Y, Size(1), Size(1))
    With shp(1)
        .Fill.ForeColor.RGB = RGB(255, 255, 255)
        .line.Visible = msoFalse
    End With
    
    Set shp(2) = DrawingWS.Shapes.AddShape(msoShapeOval, Pivot(2).X, Pivot(2).Y, Size(2), Size(2))
    With shp(2)
        .Fill.ForeColor.RGB = RGB(0, 0, 0)
        .line.Visible = msoFalse
    End With
    
    Set shp(3) = DrawingWS.Shapes.AddLine(HolePivot.X, HolePivot.Y, Pivot(3).X, Pivot(3).Y)
    With shp(3)
        .line.ForeColor.RGB = RGB(0, 0, 0)
        .line.Weight = BorderWeight
    End With
    
    Dim ShpName(1 To 3) As String
    For i = 1 To 3
        ShpName(i) = shp(i).Name
    Next i
    
    Set PivotHoleNLineDirection = DrawingWS.Shapes.Range(ShpName).Group
    
'Debuging
'Call SpotChecker(Pivot(3).X, Pivot(3).Y)
End Function

' For Debug
Private Sub SpotChecker(Xaxis As Single, Yaxis As Single, _
                                Optional Size As Single = 3, _
                                Optional Gap As Single = 15, _
                                Optional LineWeight As Single = 0)
        
    Dim shp(1 To 3) As Shape
    
    Set shp(1) = DrawingWS.Shapes.AddShape(msoShapeOval, Xaxis - Size / 2, Yaxis - Size / 2, Size, Size)
    With shp(1)
        .line.Visible = msoFalse
        .Fill.ForeColor.RGB = RGB(255, 0, 0)
    End With
    
    Set shp(2) = DrawingWS.Shapes.AddLine(Xaxis, Yaxis - Gap, Xaxis, Yaxis + Gap)
    With shp(2)
        .line.Weight = LineWeight
        .line.ForeColor.RGB = RGB(255, 0, 0)
    End With
    
    Set shp(3) = DrawingWS.Shapes.AddLine(Xaxis - Gap, Yaxis, Xaxis + Gap, Yaxis)
    With shp(3)
        .line.Weight = LineWeight
        .line.ForeColor.RGB = RGB(255, 0, 0)
    End With
    
    ' Set Group
    Dim ShpName() As String
    ReDim ShpName(LBound(shp) To UBound(shp))
    For i = LBound(ShpName) To UBound(ShpName)
        ShpName(i) = shp(i).Name
    Next i
    DrawingWS.Shapes.Range(ShpName).Group
End Sub
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
StickerLabel.cls End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Painter.cls Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
'워크시트에 그림 정보를 사용해야 할 때, 최종출력용 그림 기능을 모아둔 클래스

Private DrawingWS As Worksheet
Private clsStickerLabel As StickerLabel
Private vLineWeight As Single
' Utillity 모듈에 Enum 선언함

'초기화 이벤트 메서드
Private Sub Class_Initialize()
    Set clsStickerLabel = New StickerLabel
End Sub
'소멸 이벤트 메서드
Private Sub Class_Terminate()
    Set clsStickerLabel = Nothing
End Sub
Public Property Set DrawingWorksheet(ByRef value As Worksheet)
    Set DrawingWS = value
    Set Me.StickerLabel.Worksheet = DrawingWS
End Property
Public Property Get DrawingWorksheet() As Worksheet
    Set DrawingWorksheet = DrawingWS
End Property
Public Property Get StickerLabel() As StickerLabel
    Set StickerLabel = clsStickerLabel
End Property

Public Sub DrawShape(ShapeType As MsoAutoShapeType, _
                    TargetCell As Range, _
                    Optional ByVal Size As Single = 2)
    Dim Pivot As New ObjPivotAxis
    
    With TargetCell
        Pivot.X = (.Left + (.Width * 4 / 5)) - Size / 2
        Pivot.Y = (.Top + (.Height / 2)) - Size / 2
    End With
    
    With DrawingWS.Shapes.AddShape(ShapeType, Pivot.X, Pivot.Y, Size, Size)
        .Fill.ForeColor.RGB = RGB(0, 0, 0)
        .line.ForeColor.RGB = RGB(255, 255, 255)
        .line.Weight = 1
    End With
End Sub
' 오발 브릿지 스티커라벨
Public Function OvalBridge(StartCell As Range, EndCell As Range, _
                    Optional ByVal SplitRate_Horizon As Long = 7, _
                    Optional ByVal SplitRate_Vertical As Long = 50, _
                    Optional ByVal VerticalSplitDirection As ObjDirectionVertical = dvMid, _
                    Optional ByVal OvalSize As Single = 5, _
                    Optional ByVal LineLength As Single = 10, _
                    Optional ByVal LineWeight As Single = 2, _
                    Optional ByVal Direction As ObjDirectionSide = dsLeft, _
                    Optional ByRef SLabelMod As Boolean = False, _
                    Optional ByVal HoleLineDirection As ObjDirection4Way = d4DOWN, _
                    Optional ByRef BorderWeight As Single = 0.5, _
                    Optional ByRef MainText As String = "None", _
                    Optional ByRef SubTexts As Collection, _
                    Optional ByVal LabelBeginType As arLabelShape = Round, _
                    Optional ByVal LabelEndType As arLabelShape = Box_Hexagon) As Shape ', _
'                    Optional ByVal InnerMod As Boolean = False
    Dim StartPivot As New ObjPivotAxis, EndPivot As New ObjPivotAxis, LabelPivot As New ObjPivotAxis
    Dim ColDiff As Long, RowDiff As Long, SpLev_Horizon As Long, vtclsplt_Start As Long, vtclsplt_End As Long, vIM As Long
    Dim ForGroup(1 To 2) As String
    
    If SplitRate_Vertical < 1 Or SplitRate_Vertical > 100 Then Debug.Print "Err.Out of 1-100": Exit Function ' Error 선별
    Select Case VerticalSplitDirection
    Case Is = -48 ' Both Side
        vtclsplt_Start = SplitRate_Vertical: vtclsplt_End = SplitRate_Vertical
    Case Is = 48 ' vMid
        vtclsplt_Start = 50: vtclsplt_End = 50
    Case Is = -88 ' Up
        vtclsplt_Start = SplitRate_Vertical: vtclsplt_End = 50
    Case Is = 88 ' Down
        vtclsplt_Start = 50: vtclsplt_End = SplitRate_Vertical
    End Select
    SpLev_Horizon = SplitRate_Horizon - 1
    ColDiff = EndCell.Column - StartCell.Column: RowDiff = EndCell.Row - StartCell.Row
    If MainText = "" Or MainText = "0" Then Exit Function ' MainText의 내용이 아예 비어 있으면 출력 안함

    With StartCell
        If Direction = dsLeft Then
            StartPivot.X = (.Left + (.Width * SpLev_Horizon / SplitRate_Horizon)) - (OvalSize / 2)
        ElseIf Direction = dsRight Then
            StartPivot.X = (.Left + (.Width * 1 / SplitRate_Horizon)) - (OvalSize / 2)
        End If
        StartPivot.Y = (.Top + (.Height * vtclsplt_Start / 100)) - (OvalSize / 2)
    End With
    
    With EndCell
        If Direction = dsLeft Then
            EndPivot.X = (.Left + (.Width * SpLev_Horizon / SplitRate_Horizon)) - (OvalSize / 2)
        ElseIf Direction = dsRight Then
            EndPivot.X = (.Left + (.Width * 1 / SplitRate_Horizon)) - (OvalSize / 2)
        End If
        EndPivot.Y = (.Top + (.Height * (100 - vtclsplt_End) / 100)) - (OvalSize / 2)
    End With
    
    If ColDiff < 0 Then ' 우향타일 경우 적용
        ForGroup(1) = Bridge(DrawOval(EndPivot.X, EndPivot.Y, OvalSize), DrawOval(StartPivot.X, StartPivot.Y, OvalSize), _
            LineLength, LineWeight, Direction).Name
    Else ' 좌향타일 경우 적용
        ForGroup(1) = Bridge(DrawOval(StartPivot.X, StartPivot.Y, OvalSize), DrawOval(EndPivot.X, EndPivot.Y, OvalSize), _
            LineLength, LineWeight, Direction).Name
    End If
    
' 라벨 Start - End 행 차이가 1이하일 경우, 싱글라벨
' 라벨 Start - End 행 차이가 1초과일 경우 And Start End 열 일치, 일반라벨
If SLabelMod Then RowDiff = 0 ' 싱글라벨 모드일 때 무조건 0으로 만들기
Const LRGM As Long = 2 ' Label Row Gap Minimum 최소 행 차이 수
'vIM = IIf(InnerMod, -1, 1)
Select Case RowDiff
Case Is <= LRGM ' 행 차이가 1이하 = 2셀 이하 / 싱글라벨
    If ColDiff < 0 Then
        If Direction = dsLeft Then LabelPivot.X = EndPivot.X - LineLength + OvalSize / 2 ' 좌향시 생성지점 조정
        If Direction = dsRight Then LabelPivot.X = StartPivot.X - LineLength + OvalSize / 2 ' 우향시 생성지점 조정
    Else
        If Direction = dsLeft Then LabelPivot.X = StartPivot.X - LineLength + OvalSize / 2 ' 좌향시 생성지점 조정
        If Direction = dsRight Then LabelPivot.X = EndPivot.X - LineLength + OvalSize / 2 ' 우향시 생성지점 조정
    End If
    LabelPivot.Y = EndPivot.Y - (EndPivot.Y - StartPivot.Y) / 2 + OvalSize / 2
    ForGroup(2) = Me.StickerLabel.SingleLabel(LabelPivot.X, LabelPivot.Y, MainText:=MainText, Direction:=Direction).Name
Case Is > LRGM ' 행 차이가 1초과 = 2셀 이상
    Select Case Direction
    Case dsLeft
        If ColDiff < 0 Then
            LabelPivot.X = EndPivot.X - LineLength + OvalSize / 2
            LabelPivot.Y = EndPivot.Y - (EndPivot.Y - StartPivot.Y) / 2 + OvalSize / 2
        Else
            LabelPivot.X = StartPivot.X - LineLength + OvalSize / 2
            LabelPivot.Y = EndPivot.Y - (EndPivot.Y - StartPivot.Y) / 2 + OvalSize / 2
        End If
    
        ForGroup(2) = Me.StickerLabel.Left(LabelPivot.X, LabelPivot.Y, HoleLineDirection:=HoleLineDirection, BridgeLineWeight:=LineWeight, _
                                BorderWeight:=BorderWeight, MainText:=MainText, SubTexts:=SubTexts, LabelBeginType:=LabelBeginType, LabelEndType:=LabelEndType).Name
    Case dsRight
        If ColDiff < 0 Then
            LabelPivot.X = StartPivot.X - LineLength + OvalSize / 2
            LabelPivot.Y = EndPivot.Y - (EndPivot.Y - StartPivot.Y) / 2 + OvalSize / 2
        Else
            LabelPivot.X = EndPivot.X - LineLength + OvalSize / 2
            LabelPivot.Y = EndPivot.Y - (EndPivot.Y - StartPivot.Y) / 2 + OvalSize / 2
        End If
    
        ForGroup(2) = Me.StickerLabel.Right(LabelPivot.X, LabelPivot.Y, HoleLineDirection:=HoleLineDirection, BridgeLineWeight:=LineWeight, _
                                BorderWeight:=BorderWeight, MainText:=MainText, SubTexts:=SubTexts, LabelBeginType:=LabelBeginType, LabelEndType:=LabelEndType).Name
    End Select
End Select
    
    Set OvalBridge = DrawingWS.Shapes.Range(ForGroup).Group 'Grouping 코드
    
End Function
Public Function DotMacker(ByRef Target As Range, Optional Size As Single = 2, _
                                    Optional DivisionRate As Long = 8, Optional Direction As ObjDirectionSide = dsLeft) As Shape
    Dim Xaxis As Single, Yaxis As Single
    
    With Target
        Select Case Direction
        Case dsRight
            Xaxis = .Left + (.Width * (DivisionRate - 1) / DivisionRate) - Size / 2
        Case dsLeft
            Xaxis = .Left + (.Width * 1 / DivisionRate) - Size / 2
        End Select
        Yaxis = .Top + .Height / 2 - Size / 2
    End With
    
    Set DotMacker = DrawOval(Xaxis, Yaxis, Size)
End Function

Private Function DrawOval(ByVal Xaxis As Single, ByVal Yaxis As Single, _
                Optional ByRef Size As Single = 2) As Shape
    Dim Result As Shape
    Set Result = DrawingWS.Shapes.AddShape(msoShapeOval, Xaxis, Yaxis, Size, Size)
    
    With Result
        .Fill.ForeColor.RGB = RGB(0, 0, 0)
        .line.ForeColor.RGB = RGB(255, 255, 255)
        .line.Weight = 1
    End With
    
    Set DrawOval = Result
End Function

Private Function Bridge(StartShp As Shape, EndShp As Shape, _
                        Optional ByRef Length As Single = 10, _
                        Optional ByRef LineWeight As Single = 2, _
                        Optional ByRef Direction As ObjDirectionSide = dsLeft) As Shape
    Dim StartShape As New ObjPivotAxis, EndShape As New ObjPivotAxis
    Dim Liner(1 To 3) As Shape
    Dim ForGroup(1 To 5) As String
    
    With StartShp
        Select Case Direction
        Case dsLeft
            StartShape.X = .Width / 2 + .Left 'X
            StartShape.Y = .Height / 2 + .Top 'Y
        Case dsRight
            EndShape.X = .Width / 2 + .Left 'X
            EndShape.Y = .Height / 2 + .Top 'Y
        End Select
    End With
    
    With EndShp
        Select Case Direction
        Case dsLeft
            EndShape.X = .Width / 2 + .Left 'X
            EndShape.Y = .Height / 2 + .Top 'Y
        Case dsRight
            StartShape.X = .Width / 2 + .Left 'X
            StartShape.Y = .Height / 2 + .Top 'Y
        End Select
    End With
    
    If Direction = dsRight Then Length = Length * -1
    Set Liner(1) = DrawingWS.Shapes.AddLine(StartShape.X, StartShape.Y, StartShape.X - Length, StartShape.Y)
    Set Liner(2) = DrawingWS.Shapes.AddLine(StartShape.X - Length, StartShape.Y, StartShape.X - Length, EndShape.Y)
    Set Liner(3) = DrawingWS.Shapes.AddLine(StartShape.X - Length, EndShape.Y, EndShape.X, EndShape.Y)
    
    For i = 1 To 3
        With Liner(i).line
            .ForeColor.RGB = RGB(0, 0, 0)
            .Weight = LineWeight
        End With
        ForGroup(i) = Liner(i).Name
    Next i
    
    ForGroup(4) = StartShp.Name
    ForGroup(5) = EndShp.Name
    
    Set Bridge = DrawingWS.Shapes.Range(ForGroup).Group
    
End Function
Private Function TitleCollecting(ByRef T1 As String, ByRef T2 As String, ByRef T3 As String) As Collection
    If (Len(T1) = 0) And (Len(T2) = 0) And (Len(T3) = 0) Then
        Set TitleCollecting = Nothing
        Exit Function
    End If
    
    Dim Prcss As New Collection
        If Not T1 = "" Then Prcss.Add T1
        If Not T2 = "" Then Prcss.Add T2
        If Not T3 = "" Then Prcss.Add T3
    Set TitleCollecting = Prcss
End Function
' 미리 설정된 데이터 구조에 따라 OvalBridge 시작지점, 끝지점 좌표를 찍어주는 Function
Private Function Stamp_it(ByRef StartR As Range, ByRef EndR As Range, _
                    Optional ByVal Label_Side As ObjDirectionSide = dsLeft, _
                    Optional ByVal SingleLabelMod As Boolean = False, _
                    Optional ByVal MainT As String = "", _
                    Optional ByVal Text1 As String = "", Optional ByVal Text2 As String = "", Optional ByVal Text3 As String = "") As Shape
    'Dim MainT As String ' Main Text
    Dim SubT As New Collection ' Sub Text Collection
    Dim RowDiff As Long, ColDiff As Long, ColOff As Long ' Row Difference, Column Difference, Column Offset
    Dim SRRC As Long, ERRC As Long ' Start/End Row Cell Count
    Dim F_StartR As Range, F_EndR As Range ' Final Start and End Range
    Dim StartRowCount As Long, EndRowCount As Long

    ' 기본 데이터 설정
    RowDiff = EndR.Row - StartR.Row
    ColDiff = EndR.Column - StartR.Column
    If MainT = "" Then MainT = Application.WorksheetFunction.Sum(Range(StartR, EndR))
    ColOff = IIf(Label_Side = dsRight, 1, -1) ' 라벨 방향 설정

    ' 단일 셀 처리 (점 찍기)
    If RowDiff = 0 And ColDiff = 0 Then
        Me.DotMacker StartR.Offset(0, 1), 5
        Exit Function
    End If

    StartRowCount = Utillity.fCCNEC(Range(StartR, StartR.Offset(0, ColDiff)))
    EndRowCount = Utillity.fCCNEC(Range(EndR, EndR.Offset(0, -ColDiff)))

    ' SRRC, ERRC 최적화 (불필요한 중첩 제거)
    If StartRowCount > 1 Then
        SRRC = IIf((ColDiff > 0 And Label_Side = dsRight) Or (ColDiff < 0 And Label_Side = dsLeft), StartRowCount * ColOff, ColOff)
        ERRC = IIf((ColDiff > 0 And Label_Side = dsLeft) Or (ColDiff < 0 And Label_Side = dsRight), EndRowCount * ColOff, ColOff)
    ElseIf EndRowCount > 1 Then
        ERRC = IIf((ColDiff > 0 And Label_Side = dsLeft) Or (ColDiff < 0 And Label_Side = dsRight), EndRowCount * ColOff, ColOff)
        SRRC = IIf((ColDiff > 0 And Label_Side = dsLeft) Or (ColDiff < 0 And Label_Side = dsRight), StartRowCount * ColOff, ColOff)
    Else
        SRRC = StartRowCount * ColOff
        ERRC = EndRowCount * ColOff
    End If

    ' 최종 위치 설정
    Set F_StartR = StartR.Offset(0, SRRC)
    Set F_EndR = EndR.Offset(0, ERRC)

    ' 최종 OvalBridge 호출F
    Set Stamp_it = Me.OvalBridge(F_StartR, F_EndR, Direction:=Label_Side, _
                  MainText:=MainT, SLabelMod:=SingleLabelMod, SubTexts:=TitleCollecting(Text1, Text2, Text3))

End Function

Public Function Stamp_it_Auto(ByRef Criterion_Range As Range, _
                                    Optional ByRef Stpt_Direction As ObjDirectionSide = dsLeft, Optional ByRef Stpt_SingleLabel As Boolean = False, _
                                    Optional ByVal Text1 As String, Optional ByVal Text2 As String, Optional ByVal Text3 As String, _
                                    Optional ByRef CollectionForUndo As Collection)
    Dim FirstCol As Long, LastCol As Long, FirstRow As Long, LastRow As Long ' (First, Last)*(Col, Row)
    Dim Check As Range, StartR As Range, EndR As Range ' check, Start, End Range

    For Each Check In Criterion_Range ' 영역 지정용 반복루틴
        If Not IsEmpty(Check.value) Then
            If FirstRow <= 0 Then FirstRow = Check.Row
            If LastRow < Check.Row Then LastRow = Check.Row: LastCol = Check.Column
            Select Case Stpt_Direction
            Case -44 'dsLeft
                If Check.Row = FirstRow And FirstCol <= 0 Then FirstCol = Check.Column
                If LastCol >= Check.Column Then LastCol = Check.Column
            Case 44 ' dsRight
                If Check.Row = FirstRow And FirstCol < Check.Column Then FirstCol = Check.Column
                If LastCol <= Check.Column Then LastCol = Check.Column
            End Select
        End If
    Next Check
   
    If FirstCol < 0 Or FirstRow < 0 Or LastCol < 0 Or LastRow < 0 Then Debug.Print "잘못된 영역 참조": Exit Function ' 오류발생시 종료
    Set StartR = DrawingWS.Cells(FirstRow, FirstCol): Set EndR = DrawingWS.Cells(LastRow, LastCol)
    Set Stamp_it_Auto = Stamp_it(StartR, EndR, Stpt_Direction, Stpt_SingleLabel, Application.WorksheetFunction.Sum(Criterion_Range), Text1, Text2, Text3)
    If Not CollectionForUndo Is Nothing Then CollectionForUndo.Add Stamp_it_Auto ' 컬렉션에 적재
End Function
Public Function MultiOvalBridge() As Shape

End Function
' For Debug
Private Sub SpotChecker(Xaxis As Single, Yaxis As Single, _
                                Optional Size As Single = 3, _
                                Optional Gap As Single = 15, _
                                Optional LineWeight As Single = 0)
        
    Dim shp(1 To 3) As Shape
    
    Set shp(1) = DrawingWS.Shapes.AddShape(msoShapeOval, Xaxis - Size / 2, Yaxis - Size / 2, Size, Size)
    With shp(1)
        .line.Visible = msoFalse
        .Fill.ForeColor.RGB = RGB(255, 0, 0)
    End With
    
    Set shp(2) = DrawingWS.Shapes.AddLine(Xaxis, Yaxis - Gap, Xaxis, Yaxis + Gap)
    With shp(2)
        .line.Weight = LineWeight
        .line.ForeColor.RGB = RGB(255, 0, 0)
    End With
    
    Set shp(3) = DrawingWS.Shapes.AddLine(Xaxis - Gap, Yaxis, Xaxis + Gap, Yaxis)
    With shp(3)
        .line.Weight = LineWeight
        .line.ForeColor.RGB = RGB(255, 0, 0)
    End With
    
    ' Set Group
    Dim ShpName() As String
    ReDim ShpName(LBound(shp) To UBound(shp))
    For i = LBound(ShpName) To UBound(ShpName)
        ShpName(i) = shp(i).Name
    Next i
    DrawingWS.Shapes.Range(ShpName).Group
End Sub
' Test용 프로시저
Public Sub TestMethod()
    Dim Pivot As New ObjPivotAxis
    Dim Some As Range
    Set Some = DrawingWS.Cells(5, 5)
    With Some
        Pivot.X = .Left
        Pivot.Y = .Top
    End With
    
    SpotChecker Pivot.X, Pivot.Y
End Sub
Public Sub DeleteShapes()
    For i = DrawingWS.Shapes.Count To 1 Step -1
        DrawingWS.Shapes(i).Delete
    Next i
End Sub
Public Sub ShapesSelect()
    For i = DrawingWS.Shapes.Count To 1 Step -1
        DrawingWS.Shapes(i).Select
    Next i
End Sub
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Painter.cls End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
ObjPivotAxis.cls Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Private vXaxis As Single, vYaxis As Single, vZaxis As Single
Public Property Let X(value As Single): vXaxis = value: End Property
Public Property Get X() As Single: X = vXaxis: End Property
Public Property Let Y(value As Single): vYaxis = value: End Property
Public Property Get Y() As Single: Y = vYaxis: End Property
Public Property Let Z(value As Single): vZaxis = value: End Property
Public Property Get Z() As Single:    Z = vZaxis: End Property
Public Function Copy() As ObjPivotAxis
    Dim CopiedObj As New ObjPivotAxis
    With CopiedObj
        .X = Me.X
        .Y = Me.Y
        .Z = Me.Z
    End With
    Set Copy = CopiedObj
End Function
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
ObjPivotAxis.cls End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
ProductModel2.cls Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Option Explicit

Private vPrevious As ModelInfo
Private vCurrent As ModelInfo
Private vNext As ModelInfo
Private vStandardForGroup As String
Private vChanged As Boolean
Private Grouping As Boolean
Private vChCount As Long
Private vLOT As Long


'초기화 이벤트 메서드
Private Sub Class_Initialize()
    Set vPrevious = New ModelInfo
    Set vCurrent = New ModelInfo
    Set vNext = New ModelInfo
    vChanged = False
    vChCount = 0
    vLOT = 1
End Sub
'소멸 이벤트 메서드
Private Sub Class_Terminate()
    Set vPrevious = Nothing
    Set vCurrent = Nothing
    Set vNext = Nothing
End Sub
Public Property Get Another() As ModelInfo
    Set Another = vPrevious
End Property
Public Function Set_Another_Model(ByRef Target As Range) As ModelInfo
    vPrevious.FullName = Target.value
    vPrevious.Set_Pivot Target
    Set Set_Another_Model = vPrevious
End Function

Public Property Get Crr() As ModelInfo
    Set Crr = vCurrent
End Property
Public Property Get Prv() As ModelInfo
    Set Prv = vPrevious
End Property
Public Property Get Nxt() As ModelInfo
    Set Nxt = vNext
End Property
Public Property Get Changed() As Boolean
    Changed = vChanged
End Property
Public Property Get Count() As Long
    Count = vChCount
End Property
Public Property Get Lot() As Long
    Lot = vLOT
End Property

Public Sub NextModel(ByRef NextTarget As Range)
    Set vPrevious = vCurrent.Copy  ' 현재 > 이전
    Set vCurrent = vNext.Copy  ' 다음 > 현재
    Set vNext = New ModelInfo
    vNext.WorkOrder = NextTarget.Offset(0, -1).value
    vNext.FullName = NextTarget.value
    vNext.Set_Pivot NextTarget
    
    vLOT = vLOT + 1 ' 모델이 누적될 때마다 전부 셈
    
End Sub
Public Sub SetModel(ByRef Current_Target As Range, ByRef Next_Target As Range)
    vCurrent.FullName = Current_Target.value
    vCurrent.Set_Pivot Current_Target
    vNext.FullName = Next_Target.value
    vNext.Set_Pivot Next_Target
End Sub

Public Function Compare2Models(ByRef Target1 As ModelInfo, ByRef Target2 As ModelInfo, Field As ModelinfoFeild) As Boolean
    Select Case Field
        Case 901: Compare2Models = (Target1.WorkOrder = Target2.WorkOrder)
        Case 902: Compare2Models = (Target1.FullName = Target2.FullName)
        Case 903: Compare2Models = (Target1.Number = Target2.Number)
        Case 904: Compare2Models = (Target1.SpecNumber = Target2.SpecNumber)
        Case 905: Compare2Models = (Target1.Spec = Target2.Spec)
        Case 906: Compare2Models = (Target1.TheType = Target2.TheType)
        Case 907: Compare2Models = (Target1.Species = Target2.Species)
        Case 908: Compare2Models = (Target1.TySpec = Target2.TySpec)
        Case 909: Compare2Models = (Target1.Color = Target2.Color)
        Case 910: Compare2Models = (Target1.Suffix = Target2.Suffix)
    End Select
End Function

' 디버깅용 디테일
Public Sub Detail(ByRef PrevM As ModelInfo, _
                        ByRef CurrM As ModelInfo, _
                        ByRef NextM As ModelInfo, _
                        Optional Controller As Long = 0)
    
    Dim Dealer As Long
    
    Select Case Controller
        Case 0
            Debug.Print "" ' 이전 입력과 구분을 위한 공백
            Debug.Print "이전 모델 : " & PrevM.FullName
            Debug.Print "현재 모델 : " & CurrM.FullName
            Debug.Print "다음 모델 : " & NextM.FullName
            Debug.Print CurrM.FullName
            Debug.Print CurrM.Number
            Debug.Print CurrM.Spec
            Debug.Print CurrM.TheType
            Debug.Print CurrM.Species
            Debug.Print CurrM.Suffix
            Debug.Print CurrM.Color
            Debug.Print "Model Count : " & Me.Count
            Debug.Print "is Changed? : " & Me.Changed
        Case 1
            For Dealer = 1 To 6
            Next Dealer
    End Select
End Sub

Public Sub test()
    Me.Compare2Models Me.Crr, Me.Nxt, mif_Spec
End Sub
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
ProductModel2.cls End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
ModelInfo.cls Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Option Explicit

Public Enum ModelinfoFeild
    mif_WorkOrder = 901
    mif_FullName = 902
    mif_Number = 903
    mif_SpecNumber = 904
    mif_Spec = 905
    mif_Type = 906
    mif_Species = 907
    mif_TySpec = 908
    mif_Color = 909
    mif_Suffix = 910
End Enum

Private vWorkOrder As String
Private vFullName As String ' LSGL6335F.A
Private vNumber As String ' LSGL6335F
Private vSpecNumber As String ' LSGL6335
Private vSpec As String ' 6335
Private vType As String ' LSGL
Private vSpecies As String ' LS63
Private vTnS As String ' LS6335
Private vColor As String ' F
Private vSuffix As String ' A
Private vPivot(0 To 1) As Long

'초기화 이벤트 메서드
'Private Sub Class_Initialize()
'    ReDim Preserve vBooleans(1 To VarCount) As Boolean
'End Sub
'소멸 이벤트 메서드
'Private Sub Class_Terminate()
    
'End Sub

Public Property Get WorkOrder() As String
    WorkOrder = vWorkOrder
End Property
Public Property Let WorkOrder(value As String)
    vWorkOrder = value
End Property
Public Property Get FullName() As String
    FullName = vFullName
End Property
Public Property Let FullName(value As String)
    If vFullName <> value Then ParseModelinfo value
End Property
Public Property Get Suffix() As String
    Suffix = vSuffix
End Property
Public Property Get Number() As String
    Number = vNumber
End Property
Public Property Get Spec() As String
    Spec = vSpec
End Property
Public Property Get TheType() As String
    TheType = vType
End Property
Public Property Get Species() As String
    Species = vSpecies
End Property
Public Property Get SpecNumber() As String
    SpecNumber = vSpecNumber
End Property
Public Property Get TySpec() As String
    TySpec = vTnS
End Property
Public Property Get Color() As String
    Color = vColor
End Property
Public Property Get Row() As Long
    Row = vPivot(0)
End Property
Public Property Get col() As Long
    col = vPivot(1)
End Property
Friend Sub Set_Pivot(Optional ByRef Target As Range = Nothing, _
                             Optional ByVal Row As Long = -255, Optional ByVal Column As Long = -255)  ' Pivot 설정
    Select Case Target Is Nothing
    Case False
        vPivot(0) = Target.Row
        vPivot(1) = Target.Column
    Case True
        If Row > 0 Then vPivot(0) = Row
        If Column > 0 Then vPivot(1) = Column
    End Select
End Sub
Private Sub ParseModelinfo(ByRef WOFN As String) ' 모델명을 분리, 구분하는 서브루틴
    
    Dim Dot As Long
    
    vFullName = WOFN
    Dot = InStr(vFullName, ".")
    vNumber = Left(vFullName, Dot - 1)
    vSpec = mid(vNumber, 5, 4)
    vType = Left(vNumber, 4)
    vSpecies = Left(vType, 2) & Left(vSpec, 2)
    vSpecNumber = vType & vSpec
    vSuffix = mid(vFullName, Dot + 1)
    vColor = mid(vNumber, 9)
    vTnS = Left(vType, 2) & vSpec
    
End Sub
Public Function Copy() As ModelInfo ' Copy Function
    Dim cLS As New ModelInfo
    With cLS
        .FullName = Me.FullName
        .WorkOrder = Me.WorkOrder
        .Set_Pivot Row:=Me.Row, Column:=Me.col
    End With
    Set Copy = cLS
End Function
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
ModelInfo.cls End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
AA_Updater.bas Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
#Const isDev = True
Private Const ThisModuleName As String = "AA_Updater"

' 폴더구조로 선별 후 출력
Sub ExportAllVbaComponents()
    Dim vbComp As Object
    Dim fso As Object
    Dim basePath As String
    Dim folderModules As String, folderClasses As String, folderForms As String
    Dim fileName As String

    ' 기본 경로 설정
    basePath = ThisWorkbook.Path & "\ExcelExportedCodes\"
    folderModules = basePath & "Modules\"
    folderClasses = basePath & "Classes\"
    folderForms = basePath & "Forms\"

    ' 폴더 생성
    Set fso = CreateObject("Scripting.FileSystemObject")
    If Not fso.FolderExists(basePath) Then fso.CreateFolder basePath
    If Not fso.FolderExists(folderModules) Then fso.CreateFolder folderModules
    If Not fso.FolderExists(folderClasses) Then fso.CreateFolder folderClasses
    If Not fso.FolderExists(folderForms) Then fso.CreateFolder folderForms

    ' 구성 요소 반복하며 내보내기
    For Each vbComp In ThisWorkbook.VBProject.VBComponents
        Select Case vbComp.Type
            Case 1: fileName = folderModules & vbComp.Name & ".bas"   ' 표준 모듈
            Case 2: fileName = folderClasses & vbComp.Name & ".cls"   ' 클래스 모듈
            Case 3: fileName = folderForms & vbComp.Name & ".frm"     ' 사용자 폼
            Case Else: fileName = vbNullString
        End Select

        If fileName <> vbNullString Then
            vbComp.Export fileName
        End If
    Next vbComp

    MsgBox "구성 요소가 폴더 구조로 내보내졌습니다." & vbLf & basePath, vbInformation
End Sub
' .Txt .Md 출력
Sub ExportAllModulesDirectlyToTextAndMarkdown()
    Dim vbComp As Object, fso As Object, txtStream As Object, mdStream As Object
    Dim exportPath As String, ext As String, fileName As String
    Dim codeLine As Variant
    Dim codeLines() As String, baseName As String, timeStamp As String, TxtFile As String, mdFile As String
    Dim totalLines As Long

    ' 파일명 구성
    baseName = Left(ThisWorkbook.Name, InStrRev(ThisWorkbook.Name, ".") - 1)
    timeStamp = Format(Now, "yymmddhhmm")
    exportPath = ThisWorkbook.Path & "\ExcelExportedCodes\"
    TxtFile = exportPath & baseName & "_SourceCode_" & timeStamp & ".txt"
    mdFile = exportPath & baseName & "_SourceCode_" & timeStamp & ".md"

    ' 폴더 생성
    Set fso = CreateObject("Scripting.FileSystemObject")
    If Not fso.FolderExists(exportPath) Then fso.CreateFolder exportPath

    ' 스트림 생성 (UTF-8)
    Set txtStream = CreateObject("ADODB.Stream")
    With txtStream
        .Charset = "utf-8"
        .Type = 2
        .open
    End With

    Set mdStream = CreateObject("ADODB.Stream")
    With mdStream
        .Charset = "utf-8"
        .Type = 2
        .open
    End With

    ' 구성 요소 반복
    For Each vbComp In ThisWorkbook.VBProject.VBComponents
        Select Case vbComp.Type
            Case 1: ext = ".bas"
            Case 2: ext = ".cls"
            Case 3: ext = ".frm"
            Case Else: ext = ""
        End Select

        If ext <> "" Then
            fileName = vbComp.Name & ext
            totalLines = vbComp.CodeModule.CountOfLines

            ' 코드 읽기
            If totalLines > 0 Then
                codeLines = Split(vbComp.CodeModule.Lines(1, totalLines), vbLf)
            Else
                codeLines = Split("", vbLf)
            End If

            ' TXT 파일 작성
            txtStream.WriteText String(60, "'") & vbLf
            txtStream.WriteText fileName & " Start" & vbLf
            txtStream.WriteText String(60, "'") & vbLf

            ' MD 파일 작성
            mdStream.WriteText "### " & fileName & vbLf
            mdStream.WriteText "````vba" & vbLf

            For Each codeLine In codeLines
                txtStream.WriteText codeLine & vbLf
                mdStream.WriteText codeLine & vbLf
            Next codeLine

            txtStream.WriteText String(60, "'") & vbLf
            txtStream.WriteText fileName & " End" & vbLf
            txtStream.WriteText String(60, "'") & vbLf & vbLf

            mdStream.WriteText "````" & vbLf & vbLf
        End If
    Next vbComp

    ' 저장 및 닫기
    txtStream.SaveToFile TxtFile, 2
    txtStream.Close
    mdStream.SaveToFile mdFile, 2
    mdStream.Close

    MsgBox "모든 코드가 병합되어 저장되었습니다!" & vbLf & _
           TxtFile & vbLf & mdFile, vbInformation
End Sub

Sub ForceUpdateMacro()
    Dim latestVersion As String
    Dim localVersion As String
    Dim versionUrl As String
    Dim fileUrl As String
    Dim savePath As String
    Dim ws As Worksheet
    Dim VersionCell As Range
    
    ' Setting 확인
    Set ws = ThisWorkbook.Worksheets("Setting")
    If ("Dev" = ws.Cells.Find(What:="Develop", lookAt:=xlWhole, MatchCase:=True).Offset(0, 1).value) Then
        #If Not isDev Then
            MsgBox "개발 모드이므로 업데이트 진행 제한", vbInformation, "개발여부 확인"
        #End If
        Exit Sub
    End If

    Set VersionCell = ws.Cells.Find(What:="Version", lookAt:=xlWhole, MatchCase:=True)
    'Debug.Print VersionCell.Address
    
    ' GitLab Raw URL 설정
    versionUrl = "http://mod.lge.com/hub/seongsu1.lee/excelmacroupdater/-/raw/main/Version.txt"
    fileUrl = "http://mod.lge.com/hub/seongsu1.lee/excelmacroupdater/-/raw/main/AutoReport.xlsb"
    
    ' 현재 사용 중인 버전 (Setting Worksheet의 Version 행을 찾아 값 열의 값을 참조함)
    localVersion = VersionCell.Offset(0, 1).value
    
    ' 최신 버전 확인
    latestVersion = GetWebText(versionUrl)
    
    ' 버전 비교 및 업데이트 수행
    If Trim(localVersion) < Trim(latestVersion) Then
        MsgBox "새 버전(" & latestVersion & ")이 감지되었습니다. 업데이트를 진행합니다.", vbInformation
        
        ' 다운로드 경로 설정
        savePath = Environ("TEMP") & "\NewMacro.xlsb"
        
        ' 최신 매크로 파일 다운로드
        If DownloadFile(fileUrl, savePath) Then
            ' 기존 파일 닫기 및 새 파일 실행
            ThisWorkbook.Close False
            Workbooks.open savePath
        Else
            MsgBox "업데이트 다운로드에 실패했습니다.", vbExclamation
        End If
    Else
        MsgBox "현재 최신 버전을 사용 중입니다.", vbInformation
    End If
End Sub

Function GetWebText(url As String) As String
    Dim http As Object
    Set http = CreateObject("MSXML2.XMLHTTP")
    
    ' 요청 전송
    http.open "GET", url, False
    http.Send
    
    ' 응답 확인
    If http.Status = 200 Then
        GetWebText = http.responseText
    Else
        GetWebText = "Error"
    End If
End Function

Function DownloadFile(url As String, savePath As String) As Boolean
    Dim http As Object
    Dim stream As Object
    
    On Error Resume Next
    Set http = CreateObject("MSXML2.XMLHTTP")
    
    ' 파일 다운로드 요청
    http.open "GET", url, False
    http.Send
    
    ' 다운로드 확인
    If http.Status = 200 Then
        Set stream = CreateObject("ADODB.Stream")
        stream.Type = 1
        stream.open
        stream.Write http.responseBody
        stream.SaveToFile savePath, 2
        stream.Close
        
        ' 다운로드 성공
        DownloadFile = True
    Else
        ' 다운로드 실패
        DownloadFile = False
    End If
End Function

' === Export 구조 기반 Import 유틸리티 ===
' - Export된 모듈을 폴더에서 불러와 ThisWorkbook에 적재
' - 중복된 모듈명은 자동 제거 후 Import

Public Sub ImportAllVbaComponents()
    Dim basePath As String
    basePath = ThisWorkbook.Path & "\ExcelExportedCodes\"
   
    ImportModulesFromFolder basePath & "Modules\"
    ImportModulesFromFolder basePath & "Classes\"
    ImportModulesFromFolder basePath & "Forms\"
   
    MsgBox "Import 완료!", vbInformation
End Sub

Private Sub ImportModulesFromFolder(ByVal folderPath As String)
    Dim fso As Object, file As Object, files As Object
    Dim vbCompName As String, vbProj As Object
    Set fso = CreateObject("Scripting.FileSystemObject")
    If Not fso.FolderExists(folderPath) Then Exit Sub

    Set vbProj = ThisWorkbook.VBProject
    Set files = fso.GetFolder(folderPath).files
   
    For Each file In files
        If IsVbaFile(file.Name) Then
            vbCompName = GetVBNameFromFile(file.Path)
            If LenB(vbCompName) > 0 Then
                ' 기존 모듈 삭제
                On Error Resume Next
                vbProj.VBComponents.Remove vbProj.VBComponents(vbCompName)
                On Error GoTo 0
            End If
            vbProj.VBComponents.Import file.Path
        End If
    Next
End Sub

Private Function IsVbaFile(ByVal fileName As String) As Boolean
    Dim ext As String
    ext = LCase$(mid(fileName, InStrRev(fileName, ".") + 1))
    IsVbaFile = (ext = "bas" Or ext = "cls" Or ext = "frm")
End Function

Private Function GetVBNameFromFile(ByVal filePath As String) As String
    Dim ff As Integer: ff = FreeFile
    Dim line As String, vbName As String
    Open filePath For Input As #ff
    Do While Not EOF(ff)
        Line Input #ff, line
        If LCase$(line) Like "*attribute vb_name*" Then
            vbName = Trim$(Split(line, "=")(1))
            vbName = Replace(vbName, """", "")
            Exit Do
        End If
    Loop
    Close #ff
    GetVBNameFromFile = vbName
End Function
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
AA_Updater.bas End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Cleaner_Handler.frm Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Cleaner_Handler.frm End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
D_Maps.cls Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Option Explicit

Private Main_Groups As New Collection
Private Sub_Groups As New Collection
Private T_Lot As New D_LOT

'초기화 이벤트 메서드
Private Sub Class_Initialize()
    Set Main_Groups = New Collection
    Set Sub_Groups = New Collection
    Set T_Lot = New D_LOT
End Sub
'소멸 이벤트 메서드
Private Sub Class_Terminate()
    Set Main_Groups = Nothing
    Set Sub_Groups = Nothing
    Set T_Lot = Nothing
End Sub
Public Function Main_Lot(Optional Index As Variant = 1) As D_LOT
    Set Main_Lot = Main_Groups.Item(Index)
End Function
Public Function Sub_Lot(Optional Index As Variant = 1) As D_LOT
    Set Sub_Lot = Sub_Groups.Item(Index)
End Function

Public Sub Set_Lot(ByRef Start_Range As Range, ByRef End_Range As Range, _
                        Optional GroupType As MorS = MainG)
'    If ws Is Nothing Then Set ws = Start_Range.Parent
    
    Set T_Lot = New D_LOT
    With T_Lot
        Set .Start_R = Start_Range
        Set .End_R = End_Range
    End With
    
    Select Case GroupType
    Case MainG
        Main_Groups.Add T_Lot '.Copy
    Case SubG
        Sub_Groups.Add T_Lot '.Copy
    End Select
End Sub
Public Sub Remove(Index As Variant, Optional Target As MorS = MainG)
    Select Case Target
    Case MainG
        Main_Groups.Remove Index
    Case SubG
        Sub_Groups.Remove Index
    End Select
End Sub
Public Sub RemoveAll(Optional Target As MorS = MainG)
    Dim i As Long
    Select Case Target
    Case MainG
        For i = Main_Groups.Count To 1 Step -1
            Main_Groups.Remove (i)
        Next i
    Case SubG
        For i = Sub_Groups.Count To 1 Step -1
            Sub_Groups.Remove (i)
        Next i
    End Select
End Sub
Public Function Count(Target As MorS) As Long
    Select Case Target
    Case MainG
        Count = Main_Groups.Count
    Case SubG
        Count = Sub_Groups.Count
    End Select
End Function
Public Function Recent_Lot(Optional Index As Long = 0, Optional Target As MorS = MainG) As D_LOT
    Dim Final As Long
    Select Case Target
    Case MainG
        Final = Main_Groups.Count + Index
        Set Recent_Lot = Main_Groups(Final)
    Case SubG
        Final = Sub_Groups.Count + Index
        Set Recent_Lot = Sub_Groups(Final)
    End Select
End Function

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
D_Maps.cls End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
D_LOT.cls Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Option Explicit

Private vLot_info As New Collection
Private vStart As Range, vEnd As Range

'초기화 이벤트 메서드
'Private Sub Class_Initialize()
'
'End Sub
'소멸 이벤트 메서드
'Private Sub Class_Terminate()
'
'End Sub
Public Property Get ModelCount() As Long
    ModelCount = vLot_info.Count
End Property
Public Property Get Lot_Range() As Range
    If vStart Is Nothing Or vEnd Is Nothing Then
        Err.Raise 9998, , "Start, End 참조 오류"
        Exit Property
    ElseIf Not vStart.Worksheet Is vEnd.Worksheet Then
        Err.Raise 9999, , "Start와 End는 동일한 시트에 있어야 합니다."
        Exit Property
    End If
    Set Lot_Range = vStart.Worksheet.Range(vStart, vEnd)
End Property

Public Property Get Start_R() As Range
    Set Start_R = vStart
End Property
Public Property Set Start_R(Target_ModelNumber As Range)
    Set vStart = Target_ModelNumber
    If Not vEnd Is Nothing Then ParseModelinfo
End Property

Public Property Get End_R() As Range
    Set End_R = vEnd
End Property
Public Property Set End_R(Target As Range)
    Set vEnd = Target
    If Not vStart Is Nothing Then ParseModelinfo
End Property

Public Function Copy() As D_LOT
    Dim CopiedData As New D_LOT
    
    With CopiedData
        Set .Start_R = Me.Start_R
        Set .End_R = Me.End_R
    End With
    Set Copy = CopiedData
End Function
Public Function info(Optional Index As Long = 1) As ModelInfo
    If vLot_info(Index) Is Nothing Then Exit Function
    Set info = vLot_info(Index)
End Function

Private Sub ParseModelinfo()
    Dim vCell As Range, i As Long
    Dim TargetModelinfo As New ModelInfo
    Dim UniqueList As New Collection
    Dim ValueStr As String, Exists As Boolean
    
    On Error GoTo ErrorHandler
    
    For Each vCell In Me.Lot_Range.Cells
        ValueStr = Trim(CStr(vCell.value))
        If Len(ValueStr) > 0 Then
            Exists = False
            For i = 1 To UniqueList.Count
                If UniqueList(i) = ValueStr Then
                    Exists = True
                    Exit For
                End If
            Next i
            If Not Exists Then UniqueList.Add ValueStr
        End If
    Next
        
    For i = 1 To UniqueList.Count
        TargetModelinfo.FullName = UniqueList(i)
        vLot_info.Add TargetModelinfo.Copy
    Next i
    
    Exit Sub
ErrorHandler:
    Debug.Print "Err.Modelinfo"
    Exit Sub
End Sub
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
D_LOT.cls End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Cleaner.bas Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Sub FolderKiller(ByVal folderDirectory As String)
    Dim fso As Object
    Set fso = CreateObject("Scripting.FileSystemObject")
    
    If fso.FolderExists(folderDirectory) Then
        fso.DeleteFolder folderDirectory, True
    Else
        Exit Sub
    End If
End Sub
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Cleaner.bas End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
BC_PartListItem_Viewer.bas Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
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
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
BC_PartListItem_Viewer.bas End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
BCB_PLIV_Focus.bas Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Option Explicit



''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
BCB_PLIV_Focus.bas End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
BCCUF.frm Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Private Const Color_Inversion_Criterion As Long = 204
Private pvRGB(1 To 2) As New ObjPivotAxis
Private Sub Userform_Initialize()
    
    BCR_Slidebar.value = 210
    BCG_Slidebar.value = 210
    BCB_Slidebar.value = 210
End Sub
Public Property Get Documents_BackColor() As ObjPivotAxis
    Set Documents_BackColor = pvRGB(2)
End Property
Private Sub Userform_Terminate()
    AutoReportHandler.Doc_BackColor = pvRGB(1)
    With pvRGB(1)
        AutoReportHandler.BackColor_TB.BackColor = RGB(.X, .Y, .Z)
    End With
End Sub
Private Sub Bright_TB_KeyDown(ByVal KeyCode As MSForms.ReturnInteger, ByVal Shift As Integer)
    If KeyCode = vbKeyReturn Then ' 엔터 키 입력 시
        Dim inputStr As String
        Dim numericVal As Long
        Dim scaledVal As Long

        inputStr = Replace(Bright_TB.text, "%", "")
        If Trim(inputStr) = "" Then Exit Sub
        If Not isNumeric(inputStr) Then Exit Sub

        numericVal = CDbl(inputStr)
        If numericVal < 0 Then numericVal = 0
        If numericVal > 100 Then numericVal = 100

        scaledVal = Int(numericVal / 100 * 255)
        If scaledVal < Bright_Slidebar.Min Then scaledVal = Bright_Slidebar.Min
        If scaledVal > Bright_Slidebar.Max Then scaledVal = Bright_Slidebar.Max

        Application.EnableEvents = False
        Bright_TB.text = Format(numericVal, "0.0") & "%"
        Application.EnableEvents = True

        Bright_Slidebar.value = scaledVal
        Call Bright_Slidebar_Change

        KeyCode = 0 ' 삑 소리 방지
    End If
End Sub
Private Sub BCR_TB_KeyDown(ByVal KeyCode As MSForms.ReturnInteger, ByVal Shift As Integer)
    If KeyCode = vbKeyReturn Then ' 엔터 키 입력 시
        Dim inputStr As String
        Dim numericVal As Long
        Dim scaledVal As Long

        inputStr = Replace(BCR_TB.text, "%", "")
        If Trim(inputStr) = "" Then Exit Sub
        If Not isNumeric(inputStr) Then Exit Sub

        numericVal = CDbl(inputStr)
        If numericVal < 0 Then numericVal = 0
        If numericVal > 100 Then numericVal = 100

        scaledVal = Int(numericVal / 100 * 255)
        If scaledVal < BCR_Slidebar.Min Then scaledVal = BCR_Slidebar.Min
        If scaledVal > BCR_Slidebar.Max Then scaledVal = BCR_Slidebar.Max

        Application.EnableEvents = False
        BCR_TB.text = Format(numericVal, "0.0") & "%"
        Application.EnableEvents = True

        BCR_Slidebar.value = scaledVal
        Call BCR_Slidebar_Change

        KeyCode = 0 ' 삑 소리 방지
    End If
End Sub
Private Sub BCG_TB_KeyDown(ByVal KeyCode As MSForms.ReturnInteger, ByVal Shift As Integer)
    If KeyCode = vbKeyReturn Then ' 엔터 키 입력 시
        Dim inputStr As String
        Dim numericVal As Long
        Dim scaledVal As Long

        inputStr = Replace(BCG_TB.text, "%", "")
        If Trim(inputStr) = "" Then Exit Sub
        If Not isNumeric(inputStr) Then Exit Sub

        numericVal = CDbl(inputStr)
        If numericVal < 0 Then numericVal = 0
        If numericVal > 100 Then numericVal = 100

        scaledVal = Int(numericVal / 100 * 255)
        If scaledVal < BCG_Slidebar.Min Then scaledVal = BCG_Slidebar.Min
        If scaledVal > BCG_Slidebar.Max Then scaledVal = BCG_Slidebar.Max

        Application.EnableEvents = False
        BCG_TB.text = Format(numericVal, "0.0") & "%"
        Application.EnableEvents = True

        BCG_Slidebar.value = scaledVal
        Call BCG_Slidebar_Change

        KeyCode = 0 ' 삑 소리 방지
    End If
End Sub
Private Sub BCB_TB_KeyDown(ByVal KeyCode As MSForms.ReturnInteger, ByVal Shift As Integer)
    If KeyCode = vbKeyReturn Then ' 엔터 키 입력 시
        Dim inputStr As String
        Dim numericVal As Long
        Dim scaledVal As Long

        inputStr = Replace(BCB_TB.text, "%", "")
        If Trim(inputStr) = "" Then Exit Sub
        If Not isNumeric(inputStr) Then Exit Sub

        numericVal = CDbl(inputStr)
        If numericVal < 0 Then numericVal = 0
        If numericVal > 100 Then numericVal = 100

        scaledVal = Int(numericVal / 100 * 255)
        If scaledVal < BCB_Slidebar.Min Then scaledVal = BCB_Slidebar.Min
        If scaledVal > BCB_Slidebar.Max Then scaledVal = BCB_Slidebar.Max

        Application.EnableEvents = False
        BCB_TB.text = Format(numericVal, "0.0") & "%"
        Application.EnableEvents = True

        BCB_Slidebar.value = scaledVal
        Call BCB_Slidebar_Change

        KeyCode = 0 ' 삑 소리 방지
    End If
End Sub

Private Sub Bright_Slidebar_Change()
    Me.Bright_TB.text = Format((Bright_Slidebar.value / 255 * 100), "0.0") & "%"
    Bright_Slidebar.SelLength = Bright_Slidebar.value
    Brght = Bright_Slidebar.value
    Bright_TB.BackColor = RGB(Brght, Brght, Brght)
    Brght = 255 + (Brght * -1)
    Bright_TB.ForeColor = RGB(Brght, Brght, Brght)
    Update_Colors
End Sub
Private Sub BCR_Slidebar_Change()
    pvRGB(1).X = BCR_Slidebar.value
    BCR_TB.text = Format((pvRGB(1).X / 255 * 100), "0.0") & "%"
    BCR_TB.BackColor = RGB(pvRGB(1).X, 0, 0)
    BCR_Slidebar.SelLength = pvRGB(1).X
    If pvRGB(1).X < Color_Inversion_Criterion Then
        BCR_TB.ForeColor = RGB(255, 255, 255)
    Else
        BCR_TB.ForeColor = RGB(0, 0, 0)
    End If
    Update_Colors
End Sub
Private Sub BCG_Slidebar_Change()
    pvRGB(1).Y = BCG_Slidebar.value
    BCG_TB.text = Format((pvRGB(1).Y / 255 * 100), "0.0") & "%"
    BCG_TB.BackColor = RGB(0, pvRGB(1).Y, 0)
    BCG_Slidebar.SelLength = pvRGB(1).Y
    If pvRGB(1).Y < Color_Inversion_Criterion Then
        BCG_TB.ForeColor = RGB(255, 255, 255)
    Else
        BCG_TB.ForeColor = RGB(0, 0, 0)
    End If
    Update_Colors
End Sub
Private Sub BCB_Slidebar_Change()
    pvRGB(1).Z = BCB_Slidebar.value
    BCB_TB.text = Format((pvRGB(1).Z / 255 * 100), "0.0") & "%"
    BCB_TB.BackColor = RGB(0, 0, pvRGB(1).Z)
    BCB_Slidebar.SelLength = pvRGB(1).Z
    If pvRGB(1).Z < Color_Inversion_Criterion Then
        BCB_TB.ForeColor = RGB(255, 255, 255)
    Else
        BCB_TB.ForeColor = RGB(0, 0, 0)
    End If
    Update_Colors
End Sub
Private Sub Update_Colors()
    With pvRGB(1)
        Test_TB.BackColor = RGB(.X, .Y, .Z)
    End With
    Set pvRGB(2) = pvRGB(1).Copy
End Sub
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
BCCUF.frm End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
BCA_PLIV_Feeder.bas Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
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
        ws.Columns(Chk.Column).Hidden = Not IsInCollection(Chk.value, Feeder)
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
    If UI.CbBx_Feeder.ListCount = 0 Then UI.CbBx_Feeder.value = "": Exit Sub
    Dim i As Long
    Dim Target As String: Target = UI.CbBx_Feeder.value
    Feeders.Remove Target
    UI.CbBx_Feeder.value = ""
    For i = 0 To UI.CbBx_Feeder.ListCount - 1
        If UI.CbBx_Feeder.List(i) = Target Then UI.CbBx_Feeder.RemoveItem i: Exit Sub
    Next i
End Sub
Public Sub A_New_Feeder()
    ' 콤보박스 리스트와 중복되지 않게끔 피더 이름을 추가하고 피더유닛을 생성함
    If UI.CbBx_Feeder.value = "" Then Exit Sub
    Dim NewFeeder As New FeederUnit
    If Not FOTFC(UI.CbBx_Feeder.value, UI.CbBx_Feeder) Then
        UI.CbBx_Feeder.Additem UI.CbBx_Feeder.value
        NewFeeder.Name = UI.CbBx_Feeder.value
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
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
BCA_PLIV_Feeder.bas End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
FeederUnit.cls Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Option Explicit

Private pName As String
Private pItems As Collection

Private Sub Class_Initialize()
    Set pItems = New Collection
End Sub

Public Property Get Name() As String
    Name = pName
End Property

Public Property Let Name(ByVal value As String)
    pName = value
End Property

Public Property Get itemBox() As Collection
    Set itemBox = pItems
End Property

Public Function Copy() As FeederUnit
    Dim CopiedObj As FeederUnit: Set CopiedObj = New FeederUnit
    Dim i As Long
    With CopiedObj
        .Name = pName
        For i = 1 To pItems.Count
            .itemBox.Add pItems.Item(i)
        Next i
    End With
    Set Copy = CopiedObj
End Function
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
FeederUnit.cls End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
BD_MultiDocuments.bas Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
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
    
    FillListView_Intersection Clt_DP, Clt_PLi, LV_MD, 2025, "날짜", "라인", "DailyPlan", "PartList"

    DPCount = Clt_DP.Count: PLCount = Clt_PLi.Count: MDCount = LV_MD.ListItems.Count
    If Handle Then MsgBox "DailyPlan : " & DPCount & "장 연결됨" & vbLf & _
                                "PartList : " & PLCount & "장 연결됨" & vbLf & _
                                "Multi Documents : " & MDCount & "장 연결됨" & vbLf & _
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
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
BD_MultiDocuments.bas End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
TimeKeeper.bas Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Option Explicit

' 메인: (1) 날짜열+시간열 병합 또는 (2) 단일열(혼합포맷) → Target 열(Date) 저장, 표시형식은 "hh:mm"
' - ws         : 작업 시트
' - dateCol    : 날짜(또는 혼합) 열 번호 (예: D=4)
' - targetCol  : 타겟 열 번호 (예: F=6)
' - timeCol    : 시간 열 번호 (기본=0 → 단일열 파싱 모드)
Public Sub MergeDateTime_Flexible(ByRef ws As Worksheet, _
                                  ByVal dateCol As Long, ByVal targetCol As Long, _
                                  Optional ByVal timeCol As Long = 0, _
                                  Optional ByVal startRow As Long = 2, _
                                  Optional ByVal TargetHeader As String = "Input Time", _
                                  Optional ByVal Formatting As String = "hh:mm")

    Dim LastRow As Long, r As Long
    Dim vD As Variant, vT As Variant
    Dim dt As Date

    If ws Is Nothing Then Exit Sub

    Application.ScreenUpdating = False
    Application.EnableEvents = False

    ' 기준 열(날짜 혹은 혼합) 마지막 행
    LastRow = ws.Cells(ws.Rows.Count, dateCol).End(xlUp).Row
    If LastRow < startRow Then GoTo CleanExit ' 데이터 없음

    For r = startRow To LastRow
        vD = ws.Cells(r, dateCol).value2
        If timeCol > 0 Then
            vT = ws.Cells(r, timeCol).value2
        Else
            vT = Empty
        End If

        If TryParseDateTimeFlex(vD, vT, dt) Then
            ws.Cells(r, targetCol).value = dt ' 값은 Date 직렬값
        Else
            ws.Cells(r, targetCol).ClearContents
        End If
    Next r

    ' 표시 형식(값은 Date 그대로 유지)
    ws.Range(ws.Cells(startRow, targetCol), ws.Cells(LastRow, targetCol)).NumberFormat = Formatting
    ws.Cells(startRow - 1, targetCol).value = TargetHeader

CleanExit:
    Application.EnableEvents = True
    Application.ScreenUpdating = True
End Sub

' vDate, vTime을 다양한 포맷으로 받아 Date으로 변환
' - vTime이 제공되면: YYYYMMDD/날짜값 + 시간값/텍스트 등을 병합
' - vTime이 없으면: vDate(혼합열)에서 날짜+시간을 모두 파싱
Private Function TryParseDateTimeFlex(ByVal vDate As Variant, ByVal vTime As Variant, ByRef outDT As Date) As Boolean
    Dim baseDate As Date
    Dim tfrac As Double

    TryParseDateTimeFlex = False
    outDT = 0

    ' 두 열 병합 모드: vTime이 의미있게 들어온 경우
    If Not IsEmpty(vTime) And Not IsError(vTime) Then
        ' 날짜 해석
        If Not TryParse_DateOnly(vDate, baseDate) Then Exit Function
        ' 시간 해석
        If Not TryGetTimeFraction_Flex(vTime, tfrac) Then tfrac = 0#
        outDT = baseDate + tfrac
        TryParseDateTimeFlex = True
        Exit Function
    End If

    ' 단일 열(혼합 포맷) 모드: vDate 안에 날짜+시간(또는 둘 중 하나)
    If IsEmpty(vDate) Or IsError(vDate) Then Exit Function

    ' 1) 이미 날짜/시간 값(직렬)인 경우
    If IsDate(vDate) Then
        outDT = CDate(vDate)
        TryParseDateTimeFlex = True
        Exit Function
    End If

    ' 2) 텍스트인 경우 처리 (오전/오후, AM/PM, YYYYMMDD, hhmmss 등)
    Dim s As String, sNorm As String, Y As Long, m As Long, d As Long
    s = Trim$(CStr(vDate))
    If Len(s) = 0 Then Exit Function

    ' a) "YYYYMMDD" 단독
    If Len(s) = 8 And isNumeric(s) Then
        If TryParseYmd8_ToDate(s, baseDate) Then
            outDT = baseDate ' 시간 00:00
            TryParseDateTimeFlex = True
            Exit Function
        End If
    End If

    ' b) "YYYYMMDD HH:MM[:SS]" 또는 "YYYYMMDD 오전 HH:MM[:SS]" 등
    If TryParse_Ymd8_And_TimeText(s, outDT) Then
        TryParseDateTimeFlex = True
        Exit Function
    End If

    ' c) 일반 텍스트 날짜/시간 (오전/오후 → AM/PM 치환 후 CDate 시도)
    sNorm = NormalizeKoreanAmPm(s)
    On Error Resume Next
    outDT = CDate(sNorm)
    If Err.Number = 0 Then
        TryParseDateTimeFlex = True
    End If
    On Error GoTo 0
End Function

' YYYYMMDD(숫자/텍스트) → Date (시간 00:00)
Private Function TryParseYmd8_ToDate(ByVal v As Variant, ByRef outDate As Date) As Boolean
    Dim n As Long, Y As Long, m As Long, d As Long
    Dim s As String

    TryParseYmd8_ToDate = False
    If IsEmpty(v) Or IsError(v) Then Exit Function

    If isNumeric(v) Then
        n = CLng(v)
        If n <= 0 Then Exit Function
        Y = n \ 10000
        m = (n \ 100) Mod 100
        d = n Mod 100
    Else
        s = Trim$(CStr(v))
        If Len(s) <> 8 Then Exit Function
        If Not isNumeric(s) Then Exit Function
        Y = CLng(Left$(s, 4))
        m = CLng(mid$(s, 5, 2))
        d = CLng(Right$(s, 2))
    End If

    If Y < 1900 Or m < 1 Or m > 12 Or d < 1 Or d > 31 Then Exit Function

    outDate = DateSerial(Y, m, d)
    TryParseYmd8_ToDate = True
End Function

' 혼합 텍스트에서 "YYYYMMDD [오전/오후|AM/PM] hh:mm[:ss]" 패턴 처리
' 예: "20250831 오전 08:00:00", "20250831 8:00", "20250831 PM 8:00"
Private Function TryParse_Ymd8_And_TimeText(ByVal s As String, ByRef outDT As Date) As Boolean
    Dim sTrim As String, Y As Long, m As Long, d As Long
    Dim datePart As String, timePart As String, posSp As Long
    Dim baseDate As Date, tfrac As Double

    TryParse_Ymd8_And_TimeText = False
    sTrim = Trim$(s)
    If Len(sTrim) < 8 Then Exit Function

    ' 앞 8자리가 YYYYMMDD인가?
    If Not isNumeric(Left$(sTrim, 8)) Then Exit Function
    datePart = Left$(sTrim, 8)
    If Not TryParseYmd8_ToDate(datePart, baseDate) Then Exit Function

    ' 뒤쪽에서 시간부분 추출(있을 수도, 없을 수도)
    timePart = mid$(sTrim, 9) ' 9번째 이후
    timePart = Trim$(timePart)

    If Len(timePart) = 0 Then
        outDT = baseDate
        TryParse_Ymd8_And_TimeText = True
        Exit Function
    End If

    ' 시간 텍스트를 분수일로 변환
    If TryGetTimeFraction_Flex(timePart, tfrac) Then
        outDT = baseDate + tfrac
        TryParse_Ymd8_And_TimeText = True
    End If
End Function

' 시간값의 "소수일수" 추출 (0.0 ~ <1.0)
' 허용:
'  - 실제 날짜/시간 값 (직렬)
'  - "08:00:00"/"8:00"/"8:00 PM"/"오전 8:00" 등의 텍스트
'  - "080000" 등 6자리 시간 텍스트
Private Function TryGetTimeFraction_Flex(ByVal v As Variant, ByRef outFrac As Double) As Boolean
    Dim s As String, hh As Long, nn As Long, ss As Long
    Dim sNorm As String

    TryGetTimeFraction_Flex = False
    outFrac = 0#

    If IsEmpty(v) Or IsError(v) Then Exit Function

    ' 이미 날짜/시간 값(직렬)인 경우
    If IsDate(v) Then
        outFrac = CDbl(CDate(v)) - Fix(CDbl(CDate(v))) ' 정수부 제거: 시간 부분만
        TryGetTimeFraction_Flex = True
        Exit Function
    End If

    ' 텍스트 처리
    s = Trim$(CStr(v))
    If Len(s) = 0 Then Exit Function

    ' "오전/오후" → AM/PM 정규화
    sNorm = NormalizeKoreanAmPm(s)

    If InStr(sNorm, ":") > 0 Then
        ' "hh:mm[:ss]" (AM/PM 포함 가능)
        On Error Resume Next
        outFrac = TimeValue(sNorm)
        If Err.Number = 0 Then TryGetTimeFraction_Flex = True
        On Error GoTo 0
    ElseIf Len(sNorm) = 6 And isNumeric(sNorm) Then
        ' "hhmmss"
        hh = CLng(Left$(sNorm, 2))
        nn = CLng(mid$(sNorm, 3, 2))
        ss = CLng(Right$(sNorm, 2))
        If hh >= 0 And hh <= 23 And nn >= 0 And nn <= 59 And ss >= 0 And ss <= 59 Then
            outFrac = TimeSerial(hh, nn, ss)
            TryGetTimeFraction_Flex = True
        End If
    End If
End Function

' 한국어 오전/오후를 AM/PM으로 치환하고, 불필요한 중복 공백 정리
Private Function NormalizeKoreanAmPm(ByVal s As String) As String
    Dim t As String
    t = s
    ' 변형 케이스 최소화: 앞뒤 공백에 둔감하게
    t = Replace(t, "오전", "AM")
    t = Replace(t, "오 후", "PM") ' 혹시 있을 느슨한 표기
    t = Replace(t, "오후", "PM")
    ' 다중 공백 축소(간단치환)
    Do While InStr(t, "  ") > 0
        t = Replace(t, "  ", " ")
    Loop
    NormalizeKoreanAmPm = Trim$(t)
End Function

' (선택) DateOnly 전용 파서: 숫자형 직렬/텍스트 YYYYMMDD/표준 날짜텍스트를 아우름
Private Function TryParse_DateOnly(ByVal v As Variant, ByRef outDate As Date) As Boolean
    Dim s As String
    TryParse_DateOnly = False
    outDate = 0

    If IsEmpty(v) Or IsError(v) Then Exit Function

    If IsDate(v) Then
        outDate = DateValue(CDate(v))
        TryParse_DateOnly = True
        Exit Function
    End If

    s = Trim$(CStr(v))
    If Len(s) = 8 And isNumeric(s) Then
        If TryParseYmd8_ToDate(s, outDate) Then
            TryParse_DateOnly = True
            Exit Function
        End If
    End If

    ' 일반 텍스트 날짜(구분자 포함)도 허용
    On Error Resume Next
    outDate = DateValue(CDate(s))
    If Err.Number = 0 Then TryParse_DateOnly = True
    On Error GoTo 0
End Function

'========================
' 공개 API
'========================
' StartDT ~ EndDT 사이에서
' - 중간 날짜(시작일+1 ~ 종료일-1)는 무시
' - 시작일의 StartDT~자정, 종료일의 자정~EndDT만 계산
' - 각 날짜 구간에서 제외시간과 겹치는 부분만 차감
Public Function Time_Filtering(ByVal StartDT As Date, ByVal EndDT As Date) As Date
    Dim TC As New Collection
    Dim Total As Double
    Dim startDay As Date, endDay As Date
    Dim startDayEnd As Date, endDayStart As Date

    If EndDT <= StartDT Then
        Time_Filtering = 0
        Exit Function
    End If

    ' (예시) 제외시간 정의: "hh:mm:ss-hh:mm:ss"
    TC.Add "00:00:00-08:00:00"
    TC.Add "10:00:00-10:10:00"
    TC.Add "12:00:00-13:00:00"
    TC.Add "15:00:00-15:10:00"
    TC.Add "17:00:00-17:30:00"
    TC.Add "19:30:00-19:40:00"
    TC.Add "20:30:00-00:00:00"

    startDay = DateSerial(Year(StartDT), Month(StartDT), Day(StartDT))
    endDay = DateSerial(Year(EndDT), Month(EndDT), Day(EndDT))

    If startDay = endDay Then
        ' 같은 날짜 안에서 끝나는 경우: 단일 구간 계산
        Total = NetDurationSingleDay(StartDT, EndDT, TC)
    Else
        ' 서로 다른 날짜: 시작일 구간 + 종료일 구간만 계산
        startDayEnd = DateAdd("d", 1, startDay) ' 다음 날 00:00
        endDayStart = endDay                     ' 종료일 00:00

        ' 시작일: StartDT ~ 자정
        Total = Total + NetDurationSingleDay(StartDT, startDayEnd, TC)
        ' 종료일: 자정 ~ EndDT
        Total = Total + NetDurationSingleDay(endDayStart, EndDT, TC)
        ' 중간 날짜(startDay+1 ~ endDay-1)는 전부 제외 (의도적으로 아무 것도 더하지 않음)
    End If

    Time_Filtering = Total ' Double(일수) → Date 직렬 반환
End Function

'========================
' 헬퍼: 단일 날짜 구간만 다룸
'========================
' [segStart, segEnd) 가 반드시 같은 날짜(자정 미만) 범위라고 가정하고,
' 제외시간 컬렉션(TC)과의 겹침만큼 Duration에서 차감
Private Function NetDurationSingleDay(ByVal segStart As Date, ByVal segEnd As Date, ByVal TC As Collection) As Double
    Dim dur As Double
    Dim s As Variant, parts() As String
    Dim exStartT As Date, exEndT As Date
    Dim exStart As Date, exEnd As Date
    Dim ovS As Date, ovE As Date

    If segEnd <= segStart Then
        NetDurationSingleDay = 0#
        Exit Function
    End If

    dur = segEnd - segStart ' 기본 길이(일수)

    For Each s In TC
        parts = Split(CStr(s), "-")
        If UBound(parts) = 1 Then
            ' 시간 텍스트를 Time으로
            exStartT = TimeValue(parts(0))
            exEndT = TimeValue(parts(1))

            ' 같은 "날짜" 기준으로 제외구간 구성
            ' 1) 정상 순행(exEndT > exStartT): segStart의 날짜에 그대로 매핑
            If exEndT > exStartT Then
                exStart = DateSerial(Year(segStart), Month(segStart), Day(segStart)) + (exStartT - Fix(exStartT))
                exEnd = DateSerial(Year(segStart), Month(segStart), Day(segStart)) + (exEndT - Fix(exEndT))

                ovS = Application.WorksheetFunction.Max(segStart, exStart)
                ovE = Application.WorksheetFunction.Min(segEnd, exEnd)
                If ovE > ovS Then dur = dur - (ovE - ovS)

            ' 2) 자정 걸침(exEndT <= exStartT): 두 조각으로 분리
            '    a) 당일 exStartT ~ 24:00 (같은 날짜 조각)
            '    b) 익일 00:00 ~ exEndT   (다음날 조각) → 단일일 처리에서는 무시
            Else
                ' a) 같은 날짜의 후미 조각만 반영
                exStart = DateSerial(Year(segStart), Month(segStart), Day(segStart)) + (exStartT - Fix(exStartT))
                exEnd = DateAdd("d", 1, DateSerial(Year(segStart), Month(segStart), Day(segStart))) ' 자정(다음날 00:00)

                ovS = Application.WorksheetFunction.Max(segStart, exStart)
                ovE = Application.WorksheetFunction.Min(segEnd, exEnd)
                If ovE > ovS Then dur = dur - (ovE - ovS)

                ' b) 익일 조각은 이 함수의 책임 범위가 아님(해당 날짜에서 다시 계산됨)
            End If
        End If
    Next

    NetDurationSingleDay = IIf(dur > 0, dur, 0#)
End Function
Public Function isDayDiff(ByRef T1 As Range, ByRef T2 As Range, Optional ByVal MinDays As Long = 1) As Boolean
    If Not IsDate(T1.value) Or Not IsDate(T2.value) Then isDayDiff = False: Exit Function
    If Abs(DateValue(CDate(T2.value)) - DateValue(CDate(T1.value))) >= MinDays Then isDayDiff = True
End Function

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
TimeKeeper.bas End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Utillity.bas Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Option Explicit

#If Win64 Then
    Private Declare PtrSafe Function SendMessage Lib "user32" Alias "SendMessageA" ( _
        ByVal hWnd As LongPtr, ByVal wMsg As Long, _
        ByVal wParam As LongPtr, ByVal lParam As LongPtr) As LongPtr
#Else
    Private Declare Function SendMessage Lib "user32" Alias "SendMessageA" ( _
        ByVal hWnd As Long, ByVal wMsg As Long, _
        ByVal wParam As Long, ByVal lParam As Long) As Long
#End If

Public UI As AutoReportHandler

'---------------------------
' 1) 파싱 결과를 담는 UDT
'---------------------------
Public Type MDToken
    DocType As DocumentTypes   ' dc_DailyPlan / dc_PartList
    Month As Integer
    Day As Integer
    LineAddr As String         ' 예: "C11"
    fullPath As String         ' 원본 경로
    fileName As String         ' 파일명만
    DateValue As Date          ' BaseYear 적용된 실제 Date
    WeekdayVb As VbDayOfWeek   ' vbMonday 등
    WeekdayK As String         ' "월","화","수" ...
End Type

Public Enum DocumentTypes
    dc_BOM = -11
    dc_DailyPlan = -12
    dc_PartList = -13
End Enum
Public Enum MorS
    MainG = -100
    SubG = -200
End Enum
Public Enum RorC
    Row = -144
    Column = -211
End Enum
Public Enum arLabelShape
    Arrow = 58
    Arrow_Done = 36
    Box = 1
    Box_Dash = 2
    Box_Rounded = 5
    Box_Card = 75
    Box_Hexagon = 10
    Box_Octagon = 6
    Box_Pentagon = 51
    Box_Plque = 28
    Cross = 11
    Round = 69
    Vrtcl_Connecter = 74
    Vrtcl_ArrowCallout = 56
    Vrtcl_Document = 67
    Vrtcl_Wave = 103
    Vrtcl_Rounded = 86
End Enum
Public Enum ObjDirectionVertical
    dvBothSide = -48
    dvUP = -88
    dvMid = 48
    dvDown = 88
End Enum
Public Enum ObjDirectionSide
    dsLeft = -44
    dsRight = 44
End Enum
Public Enum ObjDirection4Way
    d4UP = -88
    d4DOWN = 88
    d4LEFT = -44
    d4RIGHT = 44
End Enum

Public Function SaveFilesWithCustomDirectory(directoryPath As String, _
                ByRef Wb As Workbook, _
                ByRef PDFpagesetup As PrintSetting, _
                Optional ByRef vTitle As String = "UndefinedFile", _
                Optional SaveToXlsx As Boolean = False, _
                Optional SaveToPDF As Boolean = True, _
                Optional OriginalKiller As Boolean = True) As String
    On Error Resume Next
    Dim ws As Worksheet: Set ws = Wb.Worksheets(1)
    Dim ExcelPath As String, savePath As String, ToDeleteDir As String
    ExcelPath = ThisWorkbook.Path: ToDeleteDir = Wb.FullName
'주소가 없으면 생성
    If Dir(ExcelPath & "\" & directoryPath, vbDirectory) = "" Then MkDir ExcelPath & "\" & directoryPath
'파일 저장용 주소 생성
    savePath = ExcelPath & "\" & directoryPath & "\" & vTitle
'이미 저장된 파일이 있다면 삭제
    If Dir(savePath & ".xlsx") <> "" Then Kill savePath & ".xlsx"
    If Dir(savePath & ".pdf") <> "" Then Kill savePath & ".pdf"
'PDF 셋업 후 PDF출력
    AutoPageSetup ws, PDFpagesetup
    If SaveToPDF Then ws.PrintOut ActivePrinter:="Microsoft Print to PDF", PrintToFile:=True, prtofilename:=savePath & ".pdf"
'엑셀로 저장할지 결정
    If SaveToXlsx Then Wb.Close SaveChanges:=True, fileName:=savePath Else Wb.Close SaveChanges:=False
    If OriginalKiller Then Kill ToDeleteDir
    SaveFilesWithCustomDirectory = savePath
    On Error GoTo 0
End Function

Function FindFilesWithTextInName(directoryPath As String, searchText As String, _
                                        Optional FileExtForSort As String) As Collection
    Dim fileName As String, filePath As String, FEFS As Long
    Dim resultPaths As New Collection
    
    fileName = Dir(directoryPath & "\*.*") ' 지정된 디렉토리에서 파일 목록 얻기
    ' 파일 목록을 확인하면서 조건에 맞는 파일 찾기
    Do While fileName <> ""
        ' 파일 이름에 특정 텍스트가 포함되어 있는지 확인
        FEFS = IIf(FileExtForSort = "", 1, InStr(1, fileName, FileExtForSort, vbBinaryCompare))
        If InStr(1, fileName, searchText, vbTextCompare) > 0 And FEFS > 0 Then
            ' 조건에 맞는 파일의 경로를 생성
            filePath = directoryPath & "\" & fileName
            ' 조건에 맞는 파일의 경로를 리스트에 추가
            resultPaths.Add filePath
        End If
        fileName = Dir ' 다음 파일 검색
    Loop
    
    ' 조건에 맞는 파일이 하나 이상인 경우 리스트 반환
    If resultPaths.Count > 0 Then
        Set FindFilesWithTextInName = resultPaths
    Else
        ' 조건에 맞는 파일을 찾지 못한 경우 빈 Collection 반환
        Set FindFilesWithTextInName = New Collection
    End If
End Function

Function IsInArray(valToBeFound As Variant, arr As Variant) As Boolean
    Dim element As Variant
    On Error Resume Next
    IsInArray = (UBound(Filter(arr, valToBeFound)) > -1)
    On Error GoTo 0
End Function

Public Function IsInCollection(valToBeFound As Variant, col As Collection) As Boolean
    Dim i As Long
    For i = 1 To col.Count
        If valToBeFound = col(i) Then
            IsInCollection = True
            Exit Function
        Else
            IsInCollection = False
        End If
    Next i
End Function

Function ColumnLetter(ColumnNumber As Long) As String
    Dim d As Long
    Dim m As Long
    Dim Name As String
    
    d = ColumnNumber
    Do
        m = (d - 1) Mod 26
        Name = Chr(65 + m) & Name
        d = (d - m) \ 26
    Loop While d > 0
    
    ColumnLetter = Name
End Function

Public Function GetRangeBoundary(rng As Range, _
                                         Optional ByRef First_Row As Long = -1, Optional ByRef Last_Row As Long = -1, _
                                        Optional ByRef First_Column As Long = -1, Optional ByRef Last_Column As Long = -1, _
                                        Optional isLeftToRight As Boolean = True) As Long
    Dim FOS As Boolean ' True = Function, False = Sub
    If First_Row = -1 Or _
        First_Column = -1 Or _
        Last_Row = -1 Or _
        Last_Column = -1 Then FOS = True
    
    First_Row = rng.Row
    Last_Row = rng.Rows(rng.Rows.Count).Row
    
    If isLeftToRight Then
        First_Column = rng.Column
        Last_Column = rng.Columns(rng.Columns.Count).Column
    Else
        First_Column = rng.Columns(rng.Columns.Count).Column
        Last_Column = rng.Column
    End If
    
    If Not FOS Then Exit Function
    
    GetRangeBoundary = First_Row
    
End Function

' CountCountinuousNonEmptyCells / 비어있지 않은 셀의 개수를 반환하는 함수 / CountNonEmptyCells
Public Function fCCNEC(ByVal TargetRange As Range) As Long
    Dim cell As Range
    Dim Count As Long
    Dim foundValue As Boolean

    Count = 0
    foundValue = False
    
    For Each cell In TargetRange
        If Not IsEmpty(cell.value) Then
            If Not foundValue Then
                foundValue = True ' 최초의 값 있는 셀을 찾음
            End If
            Count = Count + 1 ' 연속된 값 카운트
        ElseIf foundValue Then
            Exit For ' 첫 값 이후 공백을 만나면 종료
        End If
    Next cell
    
    fCCNEC = Count
End Function

' 셀 기준으로  줄 긋는 서브루틴
Public Sub CellLiner(ByRef Target As Range, _
                                Optional vEdge As XlBordersIndex = xlEdgeTop, _
                                Optional vLineStyle As XlLineStyle = xlContinuous, _
                                Optional vWeight As XlBorderWeight = xlThin)
    Dim ws As Worksheet: Set ws = Target.Worksheet
    Dim PrcssR As Range, vRorC As String
    
    If vEdge = xlEdgeTop Or xlEdgeBottom Then
        vRorC = CStr(Target.Row)
    ElseIf vEdge = xlEdgeLeft Or xlEdgeRight Then
        vRorC = CStr(Target.Column)
    Else: Exit Sub
    End If
    Set PrcssR = ws.Range(vRorC & ":" & vRorC)
    With PrcssR.Borders(vEdge)
        .LineStyle = vLineStyle
        .Weight = vWeight
        .Color = RGB(0, 0, 0)
    End With
End Sub

Public Function ForLining(ByRef Target As Range, Optional Division As RorC = Row) As Range
    Dim ws As Worksheet: Set ws = Target.Parent
    
    Select Case Division
    Case Row
        Set ForLining = ws.Range(Target.Row & ":" & Target.Row)
    Case Column
        Set ForLining = ws.Range(Target.Column & ":" & Target.Column)
    End Select
    
End Function

' Utillity CFAW_PDF
Public Function CheckFileAlreadyWritten_PDF(ByRef Document_Name As String, dt As DocumentTypes) As String
    Dim Document_Path As String, DTs As String
    
    Select Case dt
        Case -11 ' BOM
            DTs = "BOM"
            Document_Name = Replace(Document_Name, ".", "_") & ".pdf"
        Case -12 ' DailyPlan
            DTs = "DailyPlan"
            Document_Name = Document_Name & ".pdf"
        Case -13 ' PartList
            DTs = "PartList"
            Document_Name = Document_Name & ".pdf"
    End Select
    
    Document_Path = ThisWorkbook.Path & "\" & DTs
    If Not Dir(Document_Path & "\" & Document_Name, vbDirectory) <> "" Then
        CheckFileAlreadyWritten_PDF = "Ready"
        Exit Function
    Else
        CheckFileAlreadyWritten_PDF = "Written"
        Exit Function
    End If
End Function
Public Sub SelfMerge(ByRef MergeTarget As Range)
    Dim r As Long, c As Long
    Dim cell As Range
    Dim ValueList As String
    'Dim ws As Worksheet: Set ws = MergeTarget.Parent
    
    If MergeTarget Is Nothing Then Exit Sub
    For r = 1 To MergeTarget.Rows.Count
        For c = 1 To MergeTarget.Columns.Count
            Set cell = MergeTarget.Cells(r, c)
            If Trim(cell.value) <> "" Then ValueList = ValueList & cell.value & vbLf
        Next c
    Next r
    
    ' 병합 및 텍스트 삽입
    With MergeTarget
        .Merge
        .value = ValueList
        .HorizontalAlignment = xlCenter
        .VerticalAlignment = xlCenter
    End With
End Sub

Public Function ExtractBracketValue(ByVal Txt As String, Optional ByRef Searching As Long = 1) As String
    Txt = Trim(CStr(Txt)) ' 자동교정
    Dim sPos As Long, ePos As Long
    sPos = InStr(Searching, Txt, "["): ePos = InStr(Searching + 1, Txt, "]")
    
    If sPos > 0 And ePos > sPos Then
        ExtractBracketValue = mid(Txt, sPos + 1, ePos - sPos - 1)
    Else
        ExtractBracketValue = ""
    End If
    Searching = ePos
End Function

Public Function ExtractSmallBracketValue(ByVal Txt As String, Optional ByRef Searching As Long = 1) As String
    Txt = Trim(CStr(Txt)) ' 자동교정
    Dim sPos As Long, ePos As Long
    sPos = InStr(Searching, Txt, "("): ePos = InStr(Searching + 1, Txt, ")")
    
    If sPos > 0 And ePos > sPos Then
        ExtractSmallBracketValue = mid(Txt, sPos + 1, ePos - sPos - 1)
    Else
        ExtractSmallBracketValue = ""
    End If
    Searching = ePos
End Function

Public Sub DeleteDuplicateRowsInColumn(ByVal targetCol As Long, ByRef startRow As Long, ByRef EndRow As Long, _
        Optional ByRef tgtWs As Worksheet)

    Dim colValues As New Collection   ' 중복 체크용 컬렉션
    Dim i As Long, DeleteRowCount As Long
    Dim cellVal As String

    If tgtWs Is Nothing Then Set tgtWs = ActiveSheet ' 범용성 확보

    ' 아래에서 위로 순회하면서 중복 검사 및 삭제
    For i = EndRow To startRow Step -1
        ' 지정된 컬럼의 값을 가져와 공백 제거
        cellVal = Trim$(tgtWs.Cells(i, targetCol).value)

        ' 빈 문자열이 아닐 때만 검사
        If Len(cellVal) > 0 Then
            On Error Resume Next
            ' 키로 cellVal을 지정하여 컬렉션에 추가 시도
            colValues.Add Item:=cellVal, Key:=cellVal

            ' 오류 번호 457: 이미 동일한 Key가 존재함을 의미
            If Err.Number = 457 Then
                ' 중복으로 판단된 행을 삭제
                tgtWs.Rows(i).Delete
                DeleteRowCount = DeleteRowCount + 1
            End If

            ' 오류 상태 초기화
            Err.Clear
            On Error GoTo 0
        End If
    Next i
    
    EndRow = EndRow - DeleteRowCount
End Sub

'---------------------------
' 2) 정규식 헬퍼(Late Binding)
'---------------------------
Private Function RxFirst(ByVal pattern As String, ByVal text As String) As String
    Dim rx As Object, m As Object
    Set rx = CreateObject("VBScript.RegExp")
    rx.pattern = pattern
    rx.Global = False
    rx.IgnoreCase = True
    If rx.test(text) Then
        Set m = rx.Execute(text)(0)
        RxFirst = m.SubMatches(0) ' 반드시 () 캡처 1개짜리 패턴 전제
    Else
        RxFirst = vbNullString
    End If
End Function

'---------------------------
' 3) 한국어 요일 반환
'---------------------------
Private Function WeekdayKorean(d As Date) As String
    Select Case Weekday(d, vbSunday)
        Case vbSunday:    WeekdayKorean = "일"
        Case vbMonday:    WeekdayKorean = "월"
        Case vbTuesday:   WeekdayKorean = "화"
        Case vbWednesday: WeekdayKorean = "수"
        Case vbThursday:  WeekdayKorean = "목"
        Case vbFriday:    WeekdayKorean = "금"
        Case vbSaturday:  WeekdayKorean = "토"
    End Select
End Function

'---------------------------
' 4) 파일명 파서
'   예) "DailyPlan 5월-28일_C11.xlsx"
'---------------------------
Private Function ParseMDToken(ByVal fullPath As String, Optional ByVal BaseYear As Long = 0) As MDToken
    Dim t As MDToken, nm As String
    Dim ms As String, ds As String, ln As String, dt As Date, Y As Long
   
    nm = mid$(fullPath, InStrRev(fullPath, "\") + 1)
    nm = Replace$(nm, ".xlsx", "", , , vbTextCompare)
    t.fullPath = fullPath
    t.fileName = nm
   
    ' 문서타입
    If InStr(1, nm, "DailyPlan", vbTextCompare) > 0 Then
        t.DocType = dc_DailyPlan
    ElseIf InStr(1, nm, "PartList", vbTextCompare) > 0 Then
        t.DocType = dc_PartList
    Else
        t.DocType = 0 ' 알 수 없음
    End If
   
    ' 월/일   (예: "5월-28일" / "09월-05일")
    ms = RxFirst("([0-9]{1,2})(?=월)", nm)
    ds = RxFirst("([0-9]{1,2})(?=일)", nm)
   
    If Len(ms) > 0 Then t.Month = CInt(ms)
    If Len(ds) > 0 Then t.Day = CInt(ds)
   
    ' 라인   (예: "_C11" , "C11")
    ln = RxFirst("C([0-9]{1,3})", nm)
    If Len(ln) > 0 Then t.LineAddr = "C" & ln
   
    ' 연도
    If BaseYear = 0 Then
        Y = Year(Date) ' 기본 현재 연도
    Else
        Y = BaseYear
    End If
   
    If t.Month >= 1 And t.Day >= 1 Then
        On Error Resume Next
        dt = DateSerial(Y, t.Month, t.Day)
        On Error GoTo 0
        If dt > 0 Then
            t.DateValue = dt
            t.WeekdayVb = Weekday(dt, vbSunday)
            t.WeekdayK = WeekdayKorean(dt)
        End If
    End If
   
    ParseMDToken = t
End Function

'---------------------------------------------
' 5) ListView 선별 추가기 (요일/라인 필터)
'    wantDocType  : 0 이면 타입 무시
'    wantLine     : "" 이면 라인 무시 (예: "C11")
'    wantWeekday  : 0 이면 요일 무시 (vbMonday 등)
'---------------------------------------------
Public Sub FillListView_ByFilter(ByRef files As Collection, ByRef lv As ListView, _
        Optional ByVal wantDocType As DocumentTypes = 0, _
        Optional ByVal wantLine As String = "", _
        Optional ByVal wantWeekday As VbDayOfWeek = 0, _
        Optional ByVal BaseYear As Long = 0)
   
    Dim i As Long
    Dim t As MDToken
    Dim it As listItem
   
    With lv
        .ListItems.Clear
        ' 컬럼 헤더 구성 예시 (필요 시 한 번만 구성)
        If .ColumnHeaders.Count = 0 Then
            .ColumnHeaders.Add , , "날짜"
            .ColumnHeaders.Add , , "요일"
            .ColumnHeaders.Add , , "라인"
            .ColumnHeaders.Add , , "문서"
            .ColumnHeaders.Add , , "경로"
        End If
    End With
   
    For i = 1 To files.Count
        t = ParseMDToken(CStr(files(i)), BaseYear)
        If wantDocType <> 0 Then If t.DocType <> wantDocType Then GoTo CONTINUE_NEXT ' 타입 필터
        If Len(wantLine) > 0 Then If StrComp(t.LineAddr, wantLine, vbTextCompare) <> 0 Then GoTo CONTINUE_NEXT ' 라인 필터
        If wantWeekday <> 0 Then If t.WeekdayVb <> wantWeekday Then GoTo CONTINUE_NEXT ' 요일 필터
       
        ' ListView 입력
        If t.DateValue > 0 Then
            Set it = lv.ListItems.Add(, , Format$(t.DateValue, "m월-d일"))
        Else
            Set it = lv.ListItems.Add(, , "미상")
        End If
       
        it.SubItems(1) = t.WeekdayK
        it.SubItems(2) = IIf(Len(t.LineAddr) > 0, t.LineAddr, "-")
        it.SubItems(3) = IIf(t.DocType = dc_DailyPlan, "DailyPlan", IIf(t.DocType = dc_PartList, "PartList", "-"))
        it.SubItems(4) = t.fullPath
        it.Checked = True
       
CONTINUE_NEXT:
    Next i
End Sub

'---------------------------------------------
' 6) 사용 중인 GetFoundSentences 교체판
'    - 패턴 문자열 대신 용도 구분: "date" 또는 "line"
'    - 기존 코드 호환 목적: "*월-*일" -> "date", "*-Line" -> "line"
'---------------------------------------------
Public Function GetFoundSentences(ByVal Search As String, ByVal Target As String) As String
    Dim nm As String, ms As String, ds As String, ln As String
    nm = mid$(Target, InStrRev(Target, "\") + 1)
    nm = Replace$(nm, ".xlsx", "", , , vbTextCompare)
   
    If InStr(1, Search, "월", vbTextCompare) > 0 Then
        ms = RxFirst("([0-9]{1,2})(?=월)", nm)
        ds = RxFirst("([0-9]{1,2})(?=일)", nm)
        If Len(ms) > 0 And Len(ds) > 0 Then
            GetFoundSentences = CStr(CLng(ms)) & "월-" & CStr(CLng(ds)) & "일"
        Else
            GetFoundSentences = ""
        End If
        Exit Function
    End If
   
    If InStr(1, Search, "Line", vbTextCompare) > 0 Or InStr(1, Search, "C", vbTextCompare) > 0 Then
        ln = RxFirst("C([0-9]{1,3})", nm)
        If Len(ln) > 0 Then GetFoundSentences = "C" & ln Else GetFoundSentences = ""
        Exit Function
    End If
   
    ' 기타: 기본은 공백
    GetFoundSentences = ""
End Function
'--- 날짜/라인 키 빌드: 파일명 예) "DailyPlan 5월-28일_C11.xlsx"
Private Function BuildKeyFromPath(ByVal fullPath As String, Optional ByVal BaseYear As Long = 0) As String
    Dim nm As String, m As String, d As String, ln As String
    Dim Y As Long, dt As Date
   
    nm = mid$(fullPath, InStrRev(fullPath, "\") + 1)
    nm = Replace$(nm, ".xlsx", "", , , vbTextCompare)
   
    m = RxFirst("([0-9]{1,2})(?=월)", nm)
    d = RxFirst("([0-9]{1,2})(?=일)", nm)
    ln = RxFirst("C([0-9]{1,3})", nm)
   
    If Len(m) = 0 Or Len(d) = 0 Or Len(ln) = 0 Then
        BuildKeyFromPath = vbNullString
        Exit Function
    End If
   
    If BaseYear = 0 Then Y = Year(Date) Else Y = BaseYear
    On Error Resume Next
    dt = DateSerial(Y, CLng(m), CLng(d))
    On Error GoTo 0
    If dt = 0 Then
        BuildKeyFromPath = vbNullString
        Exit Function
    End If
   
    ' 키 정규화: yyyy-mm-dd|C##
    BuildKeyFromPath = Format$(dt, "yyyy-mm-dd") & "|" & "C" & CStr(CLng(ln))
End Function

'--- 교집합을 outLV에 채우기 (입력: 파일 경로 컬렉션 2개)
Public Sub FillListView_Intersection(ByRef filesA As Collection, ByRef filesB As Collection, ByRef outLV As ListView, _
                                            Optional ByVal BaseYear As Long = 0, _
                                            Optional ByVal A_Discription As String, Optional ByVal B_Discription As String, Optional ByVal C_Discription As String, Optional ByVal D_Discription As String)
    Dim i As Long
    Dim keyMap As New Collection         ' Key 전용 Map (Collection을 Map처럼 사용)
    Dim itemA As String, itemB As String, Key As String
    Dim it As listItem
    If A_Discription = "" Then A_Discription = "A경로": If B_Discription = "" Then B_Discription = "B경로"
    If C_Discription = "" Then C_Discription = "C경로": If D_Discription = "" Then D_Discription = "D경로"
    ' 컬럼 구성(최초 1회)
    With outLV
        .ListItems.Clear
        If .ColumnHeaders.Count = 0 Then
            .ColumnHeaders.Add , , A_Discription, LenA(A_Discription)
            .ColumnHeaders.Add , , B_Discription, LenA(B_Discription)
            .ColumnHeaders.Add , , C_Discription, LenA(C_Discription)
            .ColumnHeaders.Add , , D_Discription, LenA(D_Discription)
        End If
    End With
   
    ' 1) A집합 Key 적재 (Key 충돌은 무시)
    For i = 1 To filesA.Count
        itemA = CStr(filesA(i))
        Key = BuildKeyFromPath(itemA, BaseYear)
        If Len(Key) > 0 Then
            On Error Resume Next
                keyMap.Add itemA, Key     ' Item=원본경로, Key=정규화키
                ' 이미 존재하면 Err=457 -> 최초 한 개만 보관(존재성 체크가 목적)
                Err.Clear
            On Error GoTo 0
        End If
    Next i
   
    ' 2) B를 순회하며 교집합만 출력
    For i = 1 To filesB.Count
        itemB = CStr(filesB(i))
        Key = BuildKeyFromPath(itemB, BaseYear)
        If Len(Key) = 0 Then GoTo CONT_NEXT
       
        ' 존재성 검사: col.Item(key) → 에러 없으면 존재
        Dim aPath As String, dtText As String, lnText As String
        On Error Resume Next
            aPath = CStr(keyMap.Item(Key))   ' 없으면 에러
        If Err.Number = 0 Then
            ' 키에서 표시용 날짜/라인 분리
            dtText = Split(Key, "|")(0)      ' yyyy-mm-dd
            lnText = Split(Key, "|")(1)      ' C##
            With outLV
                Set it = .ListItems.Add(, , Format$(CDate(dtText), "m월-d일"))
                it.SubItems(1) = lnText
                it.SubItems(2) = aPath
                it.SubItems(3) = itemB
                it.Checked = True
            End With
        End If
        Err.Clear
        On Error GoTo 0
CONT_NEXT:
    Next i
    'LvAutoFit outLV
End Sub

' 문자열의 예상 폭을 pt로 근사 계산 (가볍고 빠른 추정치)
Public Function LenA(ByVal Expression As String, _
                     Optional ByVal Achr As Single = 14.9, _
                     Optional ByVal LatinScale As Single = 2 / 5) As Single
    Dim w As Single, i As Long, code As Long, n As Long: n = Len(Expression)
    If n = 0 Then LenA = 0: Exit Function
    For i = 1 To n
        code = AscW(mid$(Expression, i, 1)) ' Mid$ 사용: Variant 방지 + 약간 더 빠름
        If code >= &HAC00 And code <= &HD7A3 Then w = w + Achr Else w = w + Achr * LatinScale ' 가(AC00=44032) ~ 힣(D7A3=55203)
    Next i
    LenA = w  ' Single 그대로 반환 (소수점 유지)
End Function

Public Sub LvAutoFit(ByRef lvw As MSComctlLib.ListView, Optional ByVal UseHeader As Boolean = True)
    Const LVM_FIRST& = &H1000
    Const LVM_SETCOLUMNWIDTH& = (LVM_FIRST + 30)
    Const LVSCW_AUTOSIZE& = -1
    Const LVSCW_AUTOSIZE_USEHEADER& = -2
    Dim i As Long, mode As Long
    mode = IIf(UseHeader, LVSCW_AUTOSIZE_USEHEADER, LVSCW_AUTOSIZE)
    For i = 0 To lvw.ColumnHeaders.Count - 1
        Call SendMessage(lvw.hWnd, LVM_SETCOLUMNWIDTH, i, mode)
    Next
End Sub

Public Sub Diagnose_MSCOMCTL()
    Debug.Print String(60, "-")
    Debug.Print "[Excel/Office Bitness]"
#If Win64 Then
    Debug.Print "Office: 64-bit"
#Else
    Debug.Print "Office: 32-bit"
#End If

    Debug.Print String(60, "-")
    Debug.Print "[Common Controls OCX Files]"
    Debug.Print "SysWOW64\\MSCOMCTL.OCX : "; IIf(FileExists("C:\Windows\SysWOW64\MSCOMCTL.OCX"), "Yes", "No")
    Debug.Print "System32\\MSCOMCTL.OCX : "; IIf(FileExists("C:\Windows\System32\MSCOMCTL.OCX"), "Yes", "No")
    Debug.Print "Office VFS\\MSCOMCTL   : "; IIf(FileExists("C:\Program Files\Microsoft Office\root\VFS\System\MSCOMCTL.OCX"), "Yes", "No")

    Debug.Print String(60, "-")
    Debug.Print "[References Status]"
    On Error Resume Next
    Dim r As Reference
    For Each r In ThisWorkbook.VBProject.References
        Debug.Print IIf(r.IsBroken, "MISSING: ", "OK      : "); r.Description
    Next r
    On Error GoTo 0

    Debug.Print String(60, "-")
    Debug.Print "※ MISSING이면 Tools>References에서 Browse로 MSCOMCTL.OCX 재지정 후 체크."
End Sub

Private Function FileExists(ByVal f As String) As Boolean
    FileExists = (Len(Dir$(f, vbNormal)) > 0)
End Function
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Utillity.bas End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
AB_ContorlApps.bas Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''


''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
AB_ContorlApps.bas End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Z_Directory.bas Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Private Const DvBOM As String = "BOM"
Private Const DvDP As String = "DailyPlan"
Private Const DvPL As String = "PartList"
Private Const DvFD As String = "Feeder"
Private Const DvMD As String = "MultiDocuments"
Private Const DvBackup As String = "Backup"
Private Const DvDev As String = "A_Develop"
Private SourceFileFolder_Directory As String

Private ws As Worksheet
Public isDirSetUp As Boolean

Public Sub SetUpDirectories()
    Set ws = ThisWorkbook.Worksheets("Setting")
    SourceFileFolder_Directory = ws.Columns(1).Find(What:="Source", lookAt:=xlWhole).Offset(0, 1).value
    isDirSetUp = True
End Sub

Public Property Get BOM() As String
    BOM = ThisWorkbook.Path & DvBOM
End Property
Public Property Get DailyPlan() As String
    DailyPlan = ThisWorkbook.Path & DvDP
End Property
Public Property Get PartList() As String
    PartList = ThisWorkbook.Path & DvPL
End Property
Public Property Get Feeder() As String
    Feeder = ThisWorkbook.Path & DvFD
End Property
Public Property Get MultiDocuments() As String
    MultiDocuments = ThisWorkbook.Path & DvMD
End Property
Public Property Get Backup() As String
    Backup = ThisWorkbook.Path & DvBackup
End Property
Public Property Get Develop() As String
    Develop = ThisWorkbook.Path & DvDev
End Property
Public Property Get Source() As String
    Source = SourceFileFolder_Directory
End Property
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Z_Directory.bas End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Git_Con.frm Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Git_Con.frm End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Git_Kit.bas Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Option Explicit
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Git_Kit.bas End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
itemUnit.cls Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Option Explicit
'==========================================================
' Class: itemUnit (간소화 버전)
' - 아이템 메타정보(Nick/Vender/PartNumber/QTY)
' - 월/일 기준 카운터: CountPerDay(Key As Variant)
'   * Key = Date  -> 해당 날짜(MM-DD)에 대응하는 슬롯
'   * Key = Long  -> 내부 인덱스(1~372 = 12*31) 직접 접근
'==========================================================

'----------------------------------------
' [기본 필드: 메타정보]
'----------------------------------------
Private vID As String
Private vNick As String
Private vVender As String
Private vPartNumber As String
Private vQTY As Long

'----------------------------------------
' [월일 카운터]
' - 12개월 * 31일 = 372칸 고정
' - Index = (Month(d) - 1) * 31 + Day(d)
'----------------------------------------
Private mCounts(1 To 372) As Long

'==========================================================
' 1) 메타정보: ID 생성 및 프로퍼티
'==========================================================
Public Property Get ID_Hash() As String
    ID_Hash = vID          ' 읽기 전용
End Property

Public Property Get NickName() As String
    NickName = vNick
End Property

Public Property Let NickName(ByVal Target As String)
    vNick = Target
    MakeID
End Property

Public Property Get Vender() As String
    Vender = vVender
End Property

Public Property Let Vender(ByVal Target As String)
    vVender = Target
    MakeID
End Property

Public Property Get PartNumber() As String
    PartNumber = vPartNumber
End Property

Public Property Let PartNumber(ByVal Target As String)
    vPartNumber = Target
    MakeID
End Property

Public Property Get QTY() As Long
    QTY = vQTY
End Property

Public Property Let QTY(ByVal Target As Long)
    vQTY = Target
End Property

' ID = Nick_Vender_PartNumber (세 필드가 모두 있을 때만)
Private Sub MakeID()
    If Len(vNick) > 0 And Len(vVender) > 0 And Len(vPartNumber) > 0 Then
        vID = vVender & "_" & vNick & "_" & vPartNumber
    End If
End Sub

'==========================================================
' 2) 수명/상태 관리
'==========================================================
Public Sub Clear()
    Dim i As Long
    For i = LBound(mCounts) To UBound(mCounts)
        mCounts(i) = 0
    Next i
End Sub

' 필요하면 형식 유지용
Public Sub Init()
    Clear
End Sub

'==========================================================
' 3) 내부 유틸리티 (인덱스 변환/검사)
'==========================================================
' Date -> 월일 인덱스(1~372) 변환
Private Function MDIndexFromDate(ByVal d As Date) As Long
    Dim m As Long, dy As Long, idx As Long

    m = Month(d)
    dy = Day(d)

    If m < 1 Or m > 12 Or dy < 1 Or dy > 31 Then
        Err.Raise 13, TypeName(Me) & ".MDIndexFromDate", "유효하지 않은 날짜입니다."
    End If

    idx = (m - 1) * 31 + dy          ' 1 ~ 372
    MDIndexFromDate = idx
End Function

Private Sub CheckIndex(ByVal idx As Long)
    If idx < LBound(mCounts) Or idx > UBound(mCounts) Then
        Err.Raise 9, TypeName(Me) & ".CheckIndex", "인덱스 범위를 벗어났습니다.(1~372)"
    End If
End Sub

'==========================================================
' 4) 핵심 인덱서: CountPerDay
'    - Key As Variant: Date 또는 Long(1~372)
'==========================================================
Public Property Get CountPerDay(ByVal Key As Variant) As Long
    Dim idx As Long

    If VarType(Key) = vbDate Then
        idx = MDIndexFromDate(CDate(Key))
    ElseIf isNumeric(Key) Then
        idx = CLng(Key)
    Else
        Err.Raise 13, TypeName(Me) & ".CountPerDay.Get", _
                    "Key는 Date 또는 Long(1~372) 이어야 합니다."
    End If

    CheckIndex idx
    CountPerDay = mCounts(idx)
End Property

Public Property Let CountPerDay(ByVal Key As Variant, ByVal value As Long)
    Dim idx As Long

    If VarType(Key) = vbDate Then
        idx = MDIndexFromDate(CDate(Key))
    ElseIf isNumeric(Key) Then
        idx = CLng(Key)
    Else
        Err.Raise 13, TypeName(Me) & ".CountPerDay.Let", _
                    "Key는 Date 또는 Long(1~372) 이어야 합니다."
    End If

    CheckIndex idx
    mCounts(idx) = value
End Property

Public Property Get SumCount() As Long
    Dim i As Long, Result As Long
    For i = LBound(mCounts) To UBound(mCounts)
        Result = Result + mCounts(i)
    Next i
    SumCount = Result
End Property

Public Function Copy() As itemUnit
    Dim Copied As New itemUnit, i As Long
    With Copied
        .NickName = vNick
        .PartNumber = vPartNumber
        .Vender = vVender
        For i = 1 To 372
            .CountPerDay(i) = mCounts(i)
        Next i
    End With
    Set Copy = Copied
End Function
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
itemUnit.cls End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
CA_itemCounter.bas Start
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Option Explicit

'==== 모듈 전역 변수 (기존 유지) ===============================================
Private xlApp As Excel.Application, xlAppSub As Excel.Application
Private cNickname As Long, cVender As Long, cPartNumber As Long, cSC As Long, cDD As Long
Private cLC As Long, cLS As Long, cLE As Long
Private cDC As Long, cDS As Long, cDE As Long
Private cTC As Long, cTS As Long, cTE As Long
Private cFC As Long, cFS As Long, cFE As Long
Private rStart As Long, rEnd As Long, rTitle As Long
Private tWB As Workbook, tWS As Worksheet, rWB As Workbook, rWS As Worksheet
Public Sub Test_CAIC()
    Dim Temp As New Collection, i As Long
    Set Temp = ReclassifingVNQ("[ABCD] 1234/5678(2)/9012(4)/3456(3) [EFGH] 9876(3)/5431", "Controller")
    For i = 1 To Temp.Count
        Dim iUTemp As New itemUnit
        Set iUTemp = Temp(i)
        Debug.Print "ID_Hash : " & iUTemp.ID_Hash
        Debug.Print "QTY : " & iUTemp.QTY
    Next i
End Sub
Public Sub testA()
    PL2IC "D:\Downloads\공급문서\AutoReport\PartList\PartList 10월-31일_C11.xlsx"
    
    Debug.Print "Target WorkBook : " & tWB.Name
    Debug.Print "Target WorkSheet : " & tWS.Name
    Debug.Print "Reference WorkBook : " & rWB.Name
    Debug.Print "Reference WorkSheet : " & rWS.Name
    'PL2DP
End Sub
Public Sub testPLiReader()
    Get_Reference "D:\Downloads\공급문서\AutoReport\PartList\PartList 11월-14일_C11.xlsx"
    Dim Temp As New Collection, Target As New itemUnit, i As Long
    Set Temp = PLitemReader(8, 10, 2, 52)
    For i = 1 To Temp.Count
        Set Target = Temp(i)
        Debug.Print Target.ID_Hash & " : " & Target.QTY & " = " & Target.CountPerDay(CDate("2025-11-14"))
    Next i
End Sub
Public Sub ReActive()
    Dim asdf As Workbook
    Set asdf = GetObject(ThisWorkbook.FullName)
    asdf.Application.Visible = True
End Sub

'==== 퍼블릭 API ================================================================
Public Sub PL2DP(ByVal DailyPlan_Directory As String, ByVal PartList_Directory As String)
    Set_Target DailyPlan_Directory
    Get_Reference PartList_Directory
    ' TODO: 구현 예정
End Sub

Public Sub PL2IC(ByVal PartList_Directory As String)
    Set_Target ThisWorkbook.FullName, "itemCounter"
    Get_Reference PartList_Directory
    
    Dim cNickname As Long, cVender As Long, cPartNumber As Long, cSC As Long, cDD(0 To 4) As Long
    Dim cLC As Long, cLS As Long, cLE As Long ' Columns Line Cart, Line Set, Line Each
    Dim cDC As Long, cDS As Long, cDE As Long ' Columns Depot Cart, Depot Set, Depot Each
    Dim cTC As Long, cTS As Long, cTE As Long ' Columns Total Cart, Total Set, Total Each
    Dim cFC As Long, cFS As Long, cFE As Long ' Columns Fire Cart, Fire Set, Fire Each
    Dim rStart(0 To 1) As Long, rEnd(0 To 1) As Long, rTitle(0 To 1) As Long ' Rows Start, End, Title // 0:Reference, 1:Target
    Dim cStart(0 To 1) As Long, cEnd(0 To 1) As Long ' Columns Start, End // 0:Reference, 1:Target
    Dim rR As Range, tR As Range, i As Long
    Dim tiu As New itemUnit, Each_items As New Collection
    
    Set rR = rWS.Rows(1).Find("-Line", lookAt:=xlPart, LookIn:=xlValues)
    Set tR = tWS.Cells.Find("Setting", lookAt:=xlWhole, LookIn:=xlValues)
    ' Columns Number
    cNickname = tR.Column
    cVender = tR.Column + 1
    cPartNumber = tR.Column + 2
    cSC = tR.Column + 3
    For i = 0 To 4
        cDD(i) = cSC + i + 1
    Next i
    cLC = cDD(4) + 1
    cLS = cDD(4) + 2
    cLE = cDD(4) + 3
    cDC = cDD(4) + 4
    cDS = cDD(4) + 5
    cDE = cDD(4) + 6
    cTC = cDD(4) + 7
    cTS = cDD(4) + 8
    cTE = cDD(4) + 9
    cFC = cDD(4) + 10
    cFS = cDD(4) + 11
    cFE = cDD(4) + 12
    ' Rows Number
    rStart(1) = tR.Row + 3
    rEnd(1) = tWS.Cells(tWS.Rows.Count, cNickname).End(xlUp).Row
    cStart(1) = tR.Column
    cEnd(1) = tWS.Cells(rStart(1), tWS.Columns.Count).End(xlToLeft).Column
    rTitle(0) = rR.Row
    rStart(0) = rTitle(0) + 1
    rEnd(0) = rWS.Cells(rWS.Rows.Count, 1).End(xlUp).Row
    cStart(0) = rR.Column + 2
    cEnd(0) = rWS.Cells(1, rWS.Columns.Count).End(xlToLeft).Column
    
    Set Each_items = PLitemReader(cStart(0), cEnd(0), rStart(0), rEnd(0)) ' 읽기
    ' 쓰기
    TempKiller Each_items ' 날리기
    
End Sub
Private Function PLitemReader(cS As Long, cE As Long, rS As Long, rE As Long) As Collection
    Dim Result As New Collection, Temp As Collection
    Dim Tpitem As itemUnit, Rpitem As itemUnit
    Dim i As Long, n As Long, r As Long, c As Long
    Dim CountCol As Long, DayCol As Long, RowCount As Long
    Dim RawValue As String, PartsNickName As String
    Dim dKey As Date
    Dim Duplicated As Boolean

    ' 날짜/수량 열 찾기
    DayCol = rWS.Rows(1).Find("투입" & vbLf & "시점", lookAt:=xlPart, LookIn:=xlValues).Column
    CountCol = rWS.Rows(1).Find("수량", lookAt:=xlPart, LookIn:=xlValues).Column

    For c = cS To cE          ' 열 순회
        PartsNickName = CStr(rWS.Cells(1, c).value) ' 파트 닉네임

        For r = rS To rE      ' 행 순회
            RawValue = Trim$(CStr(rWS.Cells(r, c).value))
            If Len(RawValue) = 0 Then GoTo ContinueRow   ' 빈 셀은 스킵

            ' 날짜/수량 읽기
            If IsDate(rWS.Cells(r, DayCol).value) Then
                dKey = CDate(rWS.Cells(r, DayCol).value)
            Else
                GoTo ContinueRow   ' 날짜 없으면 스킵(원하시면 에러 처리로 변경 가능)
            End If

            If isNumeric(rWS.Cells(r, CountCol).value) Then
                RowCount = CLng(rWS.Cells(r, CountCol).value)
            Else
                RowCount = 0
            End If
            If RowCount = 0 Then GoTo ContinueRow

            ' 셀값 → itemUnit 컬렉션
            Set Temp = ReclassifingVNQ(RawValue, PartsNickName)

            For i = 1 To Temp.Count
                Set Tpitem = Temp(i)

                '-----------------------------
                ' 1) Result에서 같은 ID 찾기
                '-----------------------------
                Set Rpitem = Nothing
                Duplicated = False

                For n = 1 To Result.Count
                    Set Rpitem = Result(n)
                    If Rpitem.ID_Hash = Tpitem.ID_Hash Then
                        Duplicated = True
                        Exit For
                    End If
                Next n

                '-----------------------------
                ' 2) 없으면 새로 추가
                '-----------------------------
                If Not Duplicated Then
                    ' 그대로 참조해도 되고, 안전하게 복사본을 쓰고 싶으면 Tpitem.Copy
                    Set Rpitem = Tpitem.Copy
                    Result.Add Rpitem
                End If

                '-----------------------------
                ' 3) 월일 기준 카운트 누적
                '   CountPerDay(dKey)는 내부에서 월/일만 보고 저장
                '   RowCount(행의 수량) * Tpitem.QTY(파트당 필요 수량)
                '-----------------------------
                Rpitem.CountPerDay(dKey) = Rpitem.CountPerDay(dKey) + (RowCount * Tpitem.QTY)
            Next i

ContinueRow:
        Next r
    Next c

    Set PLitemReader = Result
End Function
Private Function ReclassifingVNQ(ByVal Sample As String, Optional ByVal NickName As String = "Unknown") As Collection ' Reclassifing Vender, partNumber, QTY by Cell Value
    Dim Result As New Collection, Target As itemUnit
    Dim sVender As String, sPartNumber As String, sQTY As String
    Dim Venders As Variant, PartNumbers As Variant
    Dim i As Long, p As Long
    
    Sample = Trim(CStr(Sample))
    Sample = Replace(Sample, " [", "$[") ' 공백 없애기
    Venders = Split(Sample, "$") ' Vender별로 분류
    For i = LBound(Venders) To UBound(Venders)
        sVender = ExtractBracketValue(Venders(i))
        PartNumbers = Split(Trim(Replace(Venders(i), "[" & sVender & "]", "")), "/") ' 1개의 Vender 내의 부품넘버별로 분류
        For p = LBound(PartNumbers) To UBound(PartNumbers)
            sPartNumber = InStr(PartNumbers(p), "(")
            If CLng(sPartNumber) = 0 Then
                sPartNumber = PartNumbers(p)
                sQTY = 1
            Else
                sPartNumber = Left$(PartNumbers(p), CLng(sPartNumber) - 1)
                sQTY = ExtractSmallBracketValue(PartNumbers(p))
            End If
            Set Target = New itemUnit
            Target.NickName = NickName
            Target.Vender = sVender
            Target.PartNumber = sPartNumber
            Target.QTY = CLng(sQTY)
            Result.Add Target
        Next p
    Erase PartNumbers
    Next i
    Erase Venders
    
    Set ReclassifingVNQ = Result
End Function
'==== 타겟(자기 자신) 바인드 ====================================================
Private Sub Set_Target(ByVal TargetDir As String, Optional ByVal Target_Worksheet_index As Variant = 1)
    If LenB(TargetDir) = 0 Then Exit Sub

    If Not BindWorkbook( _
            TargetDir:=TargetDir, _
            WantVisible:=False, _
            AppOut:=xlApp, _
            WbOut:=tWB) Then
        Debug.Print "Failed to bind target workbook: " & TargetDir
        Exit Sub
    End If

    If Not BindWorksheet(Wb:=tWB, WSRef:=Target_Worksheet_index, WsOut:=tWS) Then
        Debug.Print "Failed to bind target worksheet: " & CStr(Target_Worksheet_index)
        Exit Sub
    End If
End Sub

'==== 레퍼런스(상대 파일) 바인드 ================================================
Private Sub Get_Reference(ByVal TargetDir As String, Optional ByVal Target_Worksheet_index As Variant = 1)
    If LenB(TargetDir) = 0 Then Exit Sub

    If Not BindWorkbook( _
            TargetDir:=TargetDir, _
            WantVisible:=False, _
            AppOut:=xlApp, _
            WbOut:=rWB) Then
        Debug.Print "Failed to bind reference workbook: " & TargetDir
        Exit Sub
    End If

    If Not BindWorksheet(Wb:=rWB, WSRef:=Target_Worksheet_index, WsOut:=rWS) Then
        Debug.Print "Failed to bind reference worksheet: " & CStr(Target_Worksheet_index)
        Exit Sub
    End If
End Sub

'==== 유틸 (아이템 편집/표시 등은 추후 구현) ====================================
Private Sub itemAdder()
End Sub

Private Sub itemKiller()
End Sub

Private Sub itemEditor()
End Sub

Private Sub DayFollower()
End Sub

Private Sub RowSaver()
End Sub

Private Sub ViewerRefresh()
    
End Sub

'===============================================================================
'= 공용 헬퍼: Workbook/Worksheet 바인딩 (중복 제거의 핵심)
'===============================================================================
' [동작 순서]
' 1) GetObject(TargetDir) 시도: 이미 열려 있으면 해당 인스턴스의 Workbook 반환, 열려 있지 않으면 열기
' 2) 실패 시: 실행 중 Excel 인스턴스에 붙어서 FullName/Name 비교로 찾기
' 3) 그래도 실패 시: 새 인스턴스를 띄워서 Open (실패하면 Quit)
'
' 반환값: 성공 True / 실패 False
Private Function BindWorkbook( _
    ByVal TargetDir As String, _
    ByVal WantVisible As Boolean, _
    ByRef AppOut As Excel.Application, _
    ByRef WbOut As Workbook) As Boolean

    Dim fileName As String
    fileName = Dir$(TargetDir)
    If Len(fileName) = 0 Then
        Debug.Print "File not found: " & TargetDir
        BindWorkbook = False
        Exit Function
    End If

    On Error GoTo EH_GetObject
    ' 1) 최우선: 열려 있든 아니든 GetObject로 직접 바인딩(또는 개방)
    Set WbOut = GetObject(TargetDir)              ' Workbook
    Set AppOut = WbOut.Application                ' Excel.Application
    If StrComp(WbOut.FullName, ThisWorkbook.FullName, vbTextCompare) <> 0 Then AppOut.Visible = WantVisible
    BindWorkbook = True
    Exit Function

EH_GetObject:
    ' 2) 실행 중 Excel 인스턴스에서 검색
    Dim runningApp As Excel.Application
    Dim Wb As Workbook

    On Error Resume Next
    Set runningApp = GetObject(, "Excel.Application")
    On Error GoTo 0

    If Not runningApp Is Nothing Then
        For Each Wb In runningApp.Workbooks
            If StrComp(Wb.FullName, TargetDir, vbTextCompare) = 0 _
               Or StrComp(Wb.Name, fileName, vbTextCompare) = 0 Then
                Set WbOut = Wb
                Set AppOut = runningApp
                If StrComp(WbOut.FullName, ThisWorkbook.FullName, vbTextCompare) <> 0 Then AppOut.Visible = WantVisible
                BindWorkbook = True
                Exit Function
            End If
        Next
    End If

    ' 3) 새 인스턴스 생성 후 열기
    Dim newApp As Excel.Application
    Set newApp = New Excel.Application
    newApp.Visible = WantVisible

    On Error GoTo EH_OpenNew
    Set WbOut = newApp.Workbooks.open(fileName:=TargetDir, ReadOnly:=True, Notify:=False)
    Set AppOut = newApp
    BindWorkbook = True
    Exit Function

EH_OpenNew:
    ' 열기 실패하면 새 인스턴스 정리
    On Error Resume Next
    newApp.Quit
    Set newApp = Nothing
    On Error GoTo 0

    Debug.Print "Failed to open workbook: " & TargetDir
    BindWorkbook = False
End Function

' Worksheet 인덱스(숫자) / 이름(문자열) 모두 지원
Private Function BindWorksheet( _
    ByVal Wb As Workbook, _
    ByVal WSRef As Variant, _
    ByRef WsOut As Worksheet) As Boolean

    On Error GoTo EH
    If isNumeric(WSRef) Then
        Set WsOut = Wb.Worksheets(CLng(WSRef))
    Else
        Set WsOut = Wb.Worksheets(CStr(WSRef))
    End If

    BindWorksheet = True
    Exit Function

EH:
    BindWorksheet = False
End Function

Private Sub TempKiller(Optional ByRef Temp As Variant)
    If Not tWB Is Nothing Then
        If StrComp(tWB.FullName, ThisWorkbook.FullName, vbTextCompare) <> 0 Then tWB.Close False
        Set tWS = Nothing
        Set tWB = Nothing
    End If
    
    If Not rWB Is Nothing Then
        If StrComp(rWB.FullName, ThisWorkbook.FullName, vbTextCompare) <> 0 Then rWB.Close False
        Set rWS = Nothing
        Set rWB = Nothing
    End If
    
    Set Temp = Nothing
End Sub

''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
CA_itemCounter.bas End
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''