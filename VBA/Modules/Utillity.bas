Attribute VB_Name = "Utillity"
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
' 1) ÆÄ½Ì °á°ú¸¦ ´ã´Â UDT
'---------------------------
Public Type MDToken
    DocType As DocumentTypes   ' dc_DailyPlan / dc_PartList
    Month As Integer
    Day As Integer
    LineAddr As String         ' ¿¹: "C11"
    fullPath As String         ' ¿øº» °æ·Î
    fileName As String         ' ÆÄÀÏ¸í¸¸
    DateValue As Date          ' BaseYear Àû¿ëµÈ ½ÇÁ¦ Date
    WeekdayVb As VbDayOfWeek   ' vbMonday µî
    WeekdayK As String         ' "¿ù","È­","¼ö" ...
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
'ÁÖ¼Ò°¡ ¾øÀ¸¸é »ý¼º
    If Dir(ExcelPath & "\" & directoryPath, vbDirectory) = "" Then MkDir ExcelPath & "\" & directoryPath
'ÆÄÀÏ ÀúÀå¿ë ÁÖ¼Ò »ý¼º
    savePath = ExcelPath & "\" & directoryPath & "\" & vTitle
'ÀÌ¹Ì ÀúÀåµÈ ÆÄÀÏÀÌ ÀÖ´Ù¸é »èÁ¦
    If Dir(savePath & ".xlsx") <> "" Then Kill savePath & ".xlsx"
    If Dir(savePath & ".pdf") <> "" Then Kill savePath & ".pdf"
'PDF ¼Â¾÷ ÈÄ PDFÃâ·Â
    AutoPageSetup ws, PDFpagesetup
    If SaveToPDF Then ws.PrintOut ActivePrinter:="Microsoft Print to PDF", PrintToFile:=True, prtofilename:=savePath & ".pdf"
'¿¢¼¿·Î ÀúÀåÇÒÁö °áÁ¤
    If SaveToXlsx Then Wb.Close SaveChanges:=True, fileName:=savePath Else Wb.Close SaveChanges:=False
    If OriginalKiller Then Kill ToDeleteDir
    SaveFilesWithCustomDirectory = savePath
    On Error GoTo 0
End Function

Function FindFilesWithTextInName(directoryPath As String, searchText As String, _
                                        Optional FileExtForSort As String) As Collection
    Dim fileName As String, filePath As String, FEFS As Long
    Dim resultPaths As New Collection
    
    fileName = Dir(directoryPath & "\*.*") ' ÁöÁ¤µÈ µð·ºÅä¸®¿¡¼­ ÆÄÀÏ ¸ñ·Ï ¾ò±â
    ' ÆÄÀÏ ¸ñ·ÏÀ» È®ÀÎÇÏ¸é¼­ Á¶°Ç¿¡ ¸Â´Â ÆÄÀÏ Ã£±â
    Do While fileName <> ""
        ' ÆÄÀÏ ÀÌ¸§¿¡ Æ¯Á¤ ÅØ½ºÆ®°¡ Æ÷ÇÔµÇ¾î ÀÖ´ÂÁö È®ÀÎ
        FEFS = IIf(FileExtForSort = "", 1, InStr(1, fileName, FileExtForSort, vbBinaryCompare))
        If InStr(1, fileName, searchText, vbTextCompare) > 0 And FEFS > 0 Then
            ' Á¶°Ç¿¡ ¸Â´Â ÆÄÀÏÀÇ °æ·Î¸¦ »ý¼º
            filePath = directoryPath & "\" & fileName
            ' Á¶°Ç¿¡ ¸Â´Â ÆÄÀÏÀÇ °æ·Î¸¦ ¸®½ºÆ®¿¡ Ãß°¡
            resultPaths.Add filePath
        End If
        fileName = Dir ' ´ÙÀ½ ÆÄÀÏ °Ë»ö
    Loop
    
    ' Á¶°Ç¿¡ ¸Â´Â ÆÄÀÏÀÌ ÇÏ³ª ÀÌ»óÀÎ °æ¿ì ¸®½ºÆ® ¹ÝÈ¯
    If resultPaths.Count > 0 Then
        Set FindFilesWithTextInName = resultPaths
    Else
        ' Á¶°Ç¿¡ ¸Â´Â ÆÄÀÏÀ» Ã£Áö ¸øÇÑ °æ¿ì ºó Collection ¹ÝÈ¯
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

' CountCountinuousNonEmptyCells / ºñ¾îÀÖÁö ¾ÊÀº ¼¿ÀÇ °³¼ö¸¦ ¹ÝÈ¯ÇÏ´Â ÇÔ¼ö / CountNonEmptyCells
Public Function fCCNEC(ByVal TargetRange As Range) As Long
    Dim cell As Range
    Dim Count As Long
    Dim foundValue As Boolean

    Count = 0
    foundValue = False
    
    For Each cell In TargetRange
        If Not IsEmpty(cell.Value) Then
            If Not foundValue Then
                foundValue = True ' ÃÖÃÊÀÇ °ª ÀÖ´Â ¼¿À» Ã£À½
            End If
            Count = Count + 1 ' ¿¬¼ÓµÈ °ª Ä«¿îÆ®
        ElseIf foundValue Then
            Exit For ' Ã¹ °ª ÀÌÈÄ °ø¹éÀ» ¸¸³ª¸é Á¾·á
        End If
    Next cell
    
    fCCNEC = Count
End Function

' ¼¿ ±âÁØÀ¸·Î  ÁÙ ±ß´Â ¼­ºê·çÆ¾
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
Public Function CheckFileAlreadyWritten_PDF(ByRef Document_Name As String, DT As DocumentTypes) As String
    Dim Document_Path As String, DTs As String
    
    Select Case DT
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
            If Trim(cell.Value) <> "" Then ValueList = ValueList & cell.Value & vbLf
        Next c
    Next r
    
    ' º´ÇÕ ¹× ÅØ½ºÆ® »ðÀÔ
    With MergeTarget
        .Merge
        .Value = ValueList
        .HorizontalAlignment = xlCenter
        .VerticalAlignment = xlCenter
    End With
End Sub

Public Function ExtractBracketValue(ByVal Txt As String, Optional ByRef Searching As Long = 1) As String
    Txt = Trim(CStr(Txt)) ' ÀÚµ¿±³Á¤
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
    Txt = Trim(CStr(Txt)) ' ÀÚµ¿±³Á¤
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

    Dim colValues As New Collection   ' Áßº¹ Ã¼Å©¿ë ÄÃ·º¼Ç
    Dim i As Long, DeleteRowCount As Long
    Dim cellVal As String

    If tgtWs Is Nothing Then Set tgtWs = ActiveSheet ' ¹ü¿ë¼º È®º¸

    ' ¾Æ·¡¿¡¼­ À§·Î ¼øÈ¸ÇÏ¸é¼­ Áßº¹ °Ë»ç ¹× »èÁ¦
    For i = EndRow To startRow Step -1
        ' ÁöÁ¤µÈ ÄÃ·³ÀÇ °ªÀ» °¡Á®¿Í °ø¹é Á¦°Å
        cellVal = Trim$(tgtWs.Cells(i, targetCol).Value)

        ' ºó ¹®ÀÚ¿­ÀÌ ¾Æ´Ò ¶§¸¸ °Ë»ç
        If Len(cellVal) > 0 Then
            On Error Resume Next
            ' Å°·Î cellValÀ» ÁöÁ¤ÇÏ¿© ÄÃ·º¼Ç¿¡ Ãß°¡ ½Ãµµ
            colValues.Add Item:=cellVal, Key:=cellVal

            ' ¿À·ù ¹øÈ£ 457: ÀÌ¹Ì µ¿ÀÏÇÑ Key°¡ Á¸ÀçÇÔÀ» ÀÇ¹Ì
            If Err.Number = 457 Then
                ' Áßº¹À¸·Î ÆÇ´ÜµÈ ÇàÀ» »èÁ¦
                tgtWs.Rows(i).Delete
                DeleteRowCount = DeleteRowCount + 1
            End If

            ' ¿À·ù »óÅÂ ÃÊ±âÈ­
            Err.Clear
            On Error GoTo 0
        End If
    Next i
    
    EndRow = EndRow - DeleteRowCount
End Sub

'---------------------------
' 2) Á¤±Ô½Ä ÇïÆÛ(Late Binding)
'---------------------------
Private Function RxFirst(ByVal pattern As String, ByVal text As String) As String
    Dim rx As Object, m As Object
    Set rx = CreateObject("VBScript.RegExp")
    rx.pattern = pattern
    rx.Global = False
    rx.IgnoreCase = True
    If rx.test(text) Then
        Set m = rx.Execute(text)(0)
        RxFirst = m.SubMatches(0) ' ¹Ýµå½Ã () Ä¸Ã³ 1°³Â¥¸® ÆÐÅÏ ÀüÁ¦
    Else
        RxFirst = vbNullString
    End If
End Function

'---------------------------
' 3) ÇÑ±¹¾î ¿äÀÏ ¹ÝÈ¯
'---------------------------
Public Function WeekdayKorean(d As Date) As String
    Select Case Weekday(d, vbSunday)
        Case vbSunday:    WeekdayKorean = "ÀÏ"
        Case vbMonday:    WeekdayKorean = "¿ù"
        Case vbTuesday:   WeekdayKorean = "È­"
        Case vbWednesday: WeekdayKorean = "¼ö"
        Case vbThursday:  WeekdayKorean = "¸ñ"
        Case vbFriday:    WeekdayKorean = "±Ý"
        Case vbSaturday:  WeekdayKorean = "Åä"
    End Select
End Function

'---------------------------
' 4) ÆÄÀÏ¸í ÆÄ¼­
'   ¿¹) "DailyPlan 5¿ù-28ÀÏ_C11.xlsx"
'---------------------------
Private Function ParseMDToken(ByVal fullPath As String, Optional ByVal BaseYear As Long = 0) As MDToken
    Dim T As MDToken, nm As String
    Dim ms As String, ds As String, ln As String, DT As Date, Y As Long
   
    nm = mid$(fullPath, InStrRev(fullPath, "\") + 1)
    nm = Replace$(nm, ".xlsx", "", , , vbTextCompare)
    T.fullPath = fullPath
    T.fileName = nm
   
    ' ¹®¼­Å¸ÀÔ
    If InStr(1, nm, "DailyPlan", vbTextCompare) > 0 Then
        T.DocType = dc_DailyPlan
    ElseIf InStr(1, nm, "PartList", vbTextCompare) > 0 Then
        T.DocType = dc_PartList
    Else
        T.DocType = 0 ' ¾Ë ¼ö ¾øÀ½
    End If
   
    ' ¿ù/ÀÏ   (¿¹: "5¿ù-28ÀÏ" / "09¿ù-05ÀÏ")
    ms = RxFirst("([0-9]{1,2})(?=¿ù)", nm)
    ds = RxFirst("([0-9]{1,2})(?=ÀÏ)", nm)
   
    If Len(ms) > 0 Then T.Month = CInt(ms)
    If Len(ds) > 0 Then T.Day = CInt(ds)
   
    ' ¶óÀÎ   (¿¹: "_C11" , "C11")
    ln = RxFirst("C([0-9]{1,3})", nm)
    If Len(ln) > 0 Then T.LineAddr = "C" & ln
   
    ' ¿¬µµ
    If BaseYear = 0 Then
        Y = Year(Date) ' ±âº» ÇöÀç ¿¬µµ
    Else
        Y = BaseYear
    End If
   
    If T.Month >= 1 And T.Day >= 1 Then
        On Error Resume Next
        DT = DateSerial(Y, T.Month, T.Day)
        On Error GoTo 0
        If DT > 0 Then
            T.DateValue = DT
            T.WeekdayVb = Weekday(DT, vbSunday)
            T.WeekdayK = WeekdayKorean(DT)
        End If
    End If
   
    ParseMDToken = T
End Function

'---------------------------------------------
' 5) ListView ¼±º° Ãß°¡±â (¿äÀÏ/¶óÀÎ ÇÊÅÍ)
'    wantDocType  : 0 ÀÌ¸é Å¸ÀÔ ¹«½Ã
'    wantLine     : "" ÀÌ¸é ¶óÀÎ ¹«½Ã (¿¹: "C11")
'    wantWeekday  : 0 ÀÌ¸é ¿äÀÏ ¹«½Ã (vbMonday µî)
'---------------------------------------------
Public Sub FillListView_ByFilter(ByRef files As Collection, ByRef lv As ListView, _
        Optional ByVal wantDocType As DocumentTypes = 0, _
        Optional ByVal wantLine As String = "", _
        Optional ByVal wantWeekday As VbDayOfWeek = 0, _
        Optional ByVal BaseYear As Long = 0)
   
    Dim i As Long
    Dim T As MDToken
    Dim it As listItem
   
    With lv
        .ListItems.Clear
        ' ÄÃ·³ Çì´õ ±¸¼º ¿¹½Ã (ÇÊ¿ä ½Ã ÇÑ ¹ø¸¸ ±¸¼º)
        If .ColumnHeaders.Count = 0 Then
            .ColumnHeaders.Add , , "³¯Â¥"
            .ColumnHeaders.Add , , "¿äÀÏ"
            .ColumnHeaders.Add , , "¶óÀÎ"
            .ColumnHeaders.Add , , "¹®¼­"
            .ColumnHeaders.Add , , "°æ·Î"
        End If
    End With
   
    For i = 1 To files.Count
        T = ParseMDToken(CStr(files(i)), BaseYear)
        If wantDocType <> 0 Then If T.DocType <> wantDocType Then GoTo CONTINUE_NEXT ' Å¸ÀÔ ÇÊÅÍ
        If Len(wantLine) > 0 Then If StrComp(T.LineAddr, wantLine, vbTextCompare) <> 0 Then GoTo CONTINUE_NEXT ' ¶óÀÎ ÇÊÅÍ
        If wantWeekday <> 0 Then If T.WeekdayVb <> wantWeekday Then GoTo CONTINUE_NEXT ' ¿äÀÏ ÇÊÅÍ
       
        ' ListView ÀÔ·Â
        If T.DateValue > 0 Then
            Set it = lv.ListItems.Add(, , Format$(T.DateValue, "m¿ù-dÀÏ"))
        Else
            Set it = lv.ListItems.Add(, , "¹Ì»ó")
        End If
       
        it.SubItems(1) = T.WeekdayK
        it.SubItems(2) = IIf(Len(T.LineAddr) > 0, T.LineAddr, "-")
        it.SubItems(3) = IIf(T.DocType = dc_DailyPlan, "DailyPlan", IIf(T.DocType = dc_PartList, "PartList", "-"))
        it.SubItems(4) = T.fullPath
        it.Checked = True
       
CONTINUE_NEXT:
    Next i
End Sub

'---------------------------------------------
' 6) »ç¿ë ÁßÀÎ GetFoundSentences ±³Ã¼ÆÇ
'    - ÆÐÅÏ ¹®ÀÚ¿­ ´ë½Å ¿ëµµ ±¸ºÐ: "date" ¶Ç´Â "line"
'    - ±âÁ¸ ÄÚµå È£È¯ ¸ñÀû: "*¿ù-*ÀÏ" -> "date", "*-Line" -> "line"
'---------------------------------------------
Public Function GetFoundSentences(ByVal Search As String, ByVal Target As String) As String
    Dim nm As String, ms As String, ds As String, ln As String
    nm = mid$(Target, InStrRev(Target, "\") + 1)
    nm = Replace$(nm, ".xlsx", "", , , vbTextCompare)
   
    If InStr(1, Search, "¿ù", vbTextCompare) > 0 Then
        ms = RxFirst("([0-9]{1,2})(?=¿ù)", nm)
        ds = RxFirst("([0-9]{1,2})(?=ÀÏ)", nm)
        If Len(ms) > 0 And Len(ds) > 0 Then
            GetFoundSentences = CStr(CLng(ms)) & "¿ù-" & CStr(CLng(ds)) & "ÀÏ"
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
   
    ' ±âÅ¸: ±âº»Àº °ø¹é
    GetFoundSentences = ""
End Function
'--- ³¯Â¥/¶óÀÎ Å° ºôµå: ÆÄÀÏ¸í ¿¹) "DailyPlan 5¿ù-28ÀÏ_C11.xlsx"
Private Function BuildKeyFromPath(ByVal fullPath As String, Optional ByVal BaseYear As Long = 0) As String
    Dim nm As String, m As String, d As String, ln As String
    Dim Y As Long, DT As Date
   
    nm = mid$(fullPath, InStrRev(fullPath, "\") + 1)
    nm = Replace$(nm, ".xlsx", "", , , vbTextCompare)
   
    m = RxFirst("([0-9]{1,2})(?=¿ù)", nm)
    d = RxFirst("([0-9]{1,2})(?=ÀÏ)", nm)
    ln = RxFirst("C([0-9]{1,3})", nm)
   
    If Len(m) = 0 Or Len(d) = 0 Or Len(ln) = 0 Then
        BuildKeyFromPath = vbNullString
        Exit Function
    End If
   
    If BaseYear = 0 Then Y = Year(Date) Else Y = BaseYear
    On Error Resume Next
    DT = DateSerial(Y, CLng(m), CLng(d))
    On Error GoTo 0
    If DT = 0 Then
        BuildKeyFromPath = vbNullString
        Exit Function
    End If
   
    ' Å° Á¤±ÔÈ­: yyyy-mm-dd|C##
    BuildKeyFromPath = Format$(DT, "yyyy-mm-dd") & "|" & "C" & CStr(CLng(ln))
End Function

'--- ±³ÁýÇÕÀ» outLV¿¡ Ã¤¿ì±â (ÀÔ·Â: ÆÄÀÏ °æ·Î ÄÃ·º¼Ç 2°³)
Public Sub FillListView_Intersection(ByRef filesA As Collection, ByRef filesB As Collection, ByRef outLV As ListView, _
                                            Optional ByVal BaseYear As Long = 0, _
                                            Optional ByVal A_Discription As String, Optional ByVal B_Discription As String, Optional ByVal C_Discription As String, Optional ByVal D_Discription As String)
    Dim i As Long
    Dim keyMap As New Collection         ' Key Àü¿ë Map (CollectionÀ» MapÃ³·³ »ç¿ë)
    Dim itemA As String, itemB As String, Key As String
    Dim it As listItem
    If A_Discription = "" Then A_Discription = "A°æ·Î": If B_Discription = "" Then B_Discription = "B°æ·Î"
    If C_Discription = "" Then C_Discription = "C°æ·Î": If D_Discription = "" Then D_Discription = "D°æ·Î"
    ' ÄÃ·³ ±¸¼º(ÃÖÃÊ 1È¸)
    With outLV
        .ListItems.Clear
        If .ColumnHeaders.Count = 0 Then
            .ColumnHeaders.Add , , A_Discription, LenA(A_Discription)
            .ColumnHeaders.Add , , B_Discription, LenA(B_Discription)
            .ColumnHeaders.Add , , C_Discription, LenA(C_Discription)
            .ColumnHeaders.Add , , D_Discription, LenA(D_Discription)
        End If
    End With
   
    ' 1) AÁýÇÕ Key ÀûÀç (Key Ãæµ¹Àº ¹«½Ã)
    For i = 1 To filesA.Count
        itemA = CStr(filesA(i))
        Key = BuildKeyFromPath(itemA, BaseYear)
        If Len(Key) > 0 Then
            On Error Resume Next
                keyMap.Add itemA, Key     ' Item=¿øº»°æ·Î, Key=Á¤±ÔÈ­Å°
                ' ÀÌ¹Ì Á¸ÀçÇÏ¸é Err=457 -> ÃÖÃÊ ÇÑ °³¸¸ º¸°ü(Á¸Àç¼º Ã¼Å©°¡ ¸ñÀû)
                Err.Clear
            On Error GoTo 0
        End If
    Next i
   
    ' 2) B¸¦ ¼øÈ¸ÇÏ¸ç ±³ÁýÇÕ¸¸ Ãâ·Â
    For i = 1 To filesB.Count
        itemB = CStr(filesB(i))
        Key = BuildKeyFromPath(itemB, BaseYear)
        If Len(Key) = 0 Then GoTo CONT_NEXT
       
        ' Á¸Àç¼º °Ë»ç: col.Item(key) ¡æ ¿¡·¯ ¾øÀ¸¸é Á¸Àç
        Dim aPath As String, dtText As String, lnText As String
        On Error Resume Next
            aPath = CStr(keyMap.Item(Key))   ' ¾øÀ¸¸é ¿¡·¯
        If Err.Number = 0 Then
            ' Å°¿¡¼­ Ç¥½Ã¿ë ³¯Â¥/¶óÀÎ ºÐ¸®
            dtText = Split(Key, "|")(0)      ' yyyy-mm-dd
            lnText = Split(Key, "|")(1)      ' C##
            With outLV
                Set it = .ListItems.Add(, , Format$(CDate(dtText), "m¿ù-dÀÏ"))
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

' ¹®ÀÚ¿­ÀÇ ¿¹»ó ÆøÀ» pt·Î ±Ù»ç °è»ê (°¡º±°í ºü¸¥ ÃßÁ¤Ä¡)
Public Function LenA(ByVal Expression As String, _
                     Optional ByVal Achr As Single = 14.9, _
                     Optional ByVal LatinScale As Single = 2 / 5) As Single
    Dim w As Single, i As Long, code As Long, n As Long: n = Len(Expression)
    If n = 0 Then LenA = 0: Exit Function
    For i = 1 To n
        code = AscW(mid$(Expression, i, 1)) ' Mid$ »ç¿ë: Variant ¹æÁö + ¾à°£ ´õ ºü¸§
        If code >= &HAC00 And code <= &HD7A3 Then w = w + Achr Else w = w + Achr * LatinScale ' °¡(AC00=44032) ~ ÆR(D7A3=55203)
    Next i
    LenA = w  ' Single ±×´ë·Î ¹ÝÈ¯ (¼Ò¼öÁ¡ À¯Áö)
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
    Debug.Print "¡Ø MISSINGÀÌ¸é Tools>References¿¡¼­ Browse·Î MSCOMCTL.OCX ÀçÁöÁ¤ ÈÄ Ã¼Å©."
End Sub

Private Function FileExists(ByVal f As String) As Boolean
    FileExists = (Len(Dir$(f, vbNormal)) > 0)
End Function
