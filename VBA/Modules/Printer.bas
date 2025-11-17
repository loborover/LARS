Attribute VB_Name = "Printer"
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
