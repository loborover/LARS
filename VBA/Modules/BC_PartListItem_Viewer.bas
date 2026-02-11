Attribute VB_Name = "BC_PartListItem_Viewer"
Option Explicit

Private Enum CollectorTypes
    Unique = -3
    Duplicate = -6
End Enum

Public ¿¹¿ÜÃ³¸® As Boolean
Public MRB_PL As Boolean ' Manual_Reporting_Bool_PartList

Private BoW As Single ' Black or White
Private PL_Processing_WB As New Workbook ' ¸ðµâ³» Àü¿ªº¯¼ö·Î ¼±¾ðÇÔ
Private Target_WorkSheet As New Worksheet
Private PrintArea As Range, CopiedArea As Range  ' ¸ðµâ³» ÇÁ¸°Æ®¿µ¿ª
Private Brush As New Painter
Private Title As String, wLine As String
Private DayCount As Long, HeaderCol As Long
Private vCFR As Collection  ' Columns For Report
Private vVendorVisible As Boolean

Public Sub Read_PartList(Optional Handle As Boolean, Optional ByRef Target_Listview As ListView)
    Dim i As Long
    Dim PartList As New Collection
        
    If Target_Listview Is Nothing Then Set Target_Listview = ARH.ListView_PartList_items: Target_Listview.ListItems.Clear
    Set PartList = FindFilesWithTextInName(Z_Directory.Source, "Excel_Export_")
    If PartList.Count = 0 Then: If Handle Then MsgBox "¿¬°áµÈ ÁÖ¼Ò¿¡ PartList ÆÄÀÏÀÌ ¾øÀ½": Exit Sub
    
    With Target_Listview
        For i = 1 To PartList.Count
ARH.UpdateProgressBar ARH.PB_BOM, (i - i / 3) / PartList.Count * 100
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
        .ListItems(PLCount).Checked = True ' Ã¼Å©¹Ú½º Ã¼Å©
ARH.UpdateProgressBar ARH.PB_BOM, i / PartList.Count * 100

    With ARH.ListView_PLfF_item.ListItems ' User interface ListVeiw input
        .Clear
        Dim r As Long
        For r = 1 To vCFR.Count
            .Add r, vCFR(r), vCFR(r)
        Next r
    End With
SkipLoop:
        Next i
    End With
    
    If Handle Then MsgBox "PartList " & PLCount & "Àå ¿¬°á¿Ï·á"
End Sub
' ¹®¼­ ÀÚµ¿È­, Ãâ·Â±îÁö ÇÑ¹ø¿¡ ½ÇÇàÇÏ´Â Sub
Public Sub Print_PartList(Optional Handle As Boolean)
    Dim PLLV As ListView, PLitem As listItem
    Dim Chkditem As New Collection
    Dim PaperCopies As Long, ListCount As Long, i As Long
    Dim SavedPath As String
    Dim ws As Worksheet
    
    BoW = ARH.Brightness
    If ARH.CB_PL_Ddays.Value Then DayCount = ARH.PL_Ddays_TB.text Else DayCount = 4
    Set Brush = New Painter
    
    PaperCopies = CInt(ARH.PL_PN_Copies_TB.text)
    Set PLLV = ARH.ListView_PartList_items
    ListCount = PLLV.ListItems.Count: If ListCount = 0 Then MsgBox "¿¬°áµÈ µ¥ÀÌÅÍ ¾øÀ½": Exit Sub

    For i = 1 To ListCount ' Ã¼Å©¹Ú½º È°¼ºÈ­µÈ ¾ÆÀÌÅÛ ¼±º°
        Set PLitem = PLLV.ListItems.Item(i)
        If PLitem.Checked Then Chkditem.Add PLitem.index 'SubItems(1)
    Next i
    
    If Chkditem.Count < 1 Then MsgBox "¼±ÅÃµÈ ¹®¼­ ¾øÀ½": Exit Sub ' ¼±ÅÃµÈ ¹®¼­°¡ ¾øÀ» ½Ã Áï½Ã Á¾·á
    
    ListCount = Chkditem.Count
    For i = 1 To ListCount
ARH.UpdateProgressBar ARH.PB_BOM, (i - 0.99) / ListCount * 100
        Set PLitem = PLLV.ListItems.Item(Chkditem(i))
        Set PL_Processing_WB = Workbooks.Open(PLitem.SubItems(2))
ARH.UpdateProgressBar ARH.PB_BOM, (i - 0.91) / ListCount * 100
        wLine = PLitem.SubItems(1) ' Line ÀÌ¸§ ÀÎ°è
        Set Target_WorkSheet = PL_Processing_WB.Worksheets(1): Set ws = Target_WorkSheet: Set Brush.DrawingWorksheet = Target_WorkSheet ' ¿öÅ©½ÃÆ® Å¸°ÔÆÃ
        PL_Processing_WB.Windows(1).WindowState = xlMinimized ' ÃÖ¼ÒÈ­
        AutoReport_PartList PL_Processing_WB 'ÀÚµ¿È­ ¼­½ÄÀÛ¼º ÄÚµå
ARH.UpdateProgressBar ARH.PB_BOM, (i - 0.87) / ListCount * 100
        If PrintNow.PartList Then
            Printer.PrinterNameSet  ' ±âº»ÇÁ¸°ÅÍ ÀÌ¸§ ¼³Á¤, À¯ÁöµÇ´ÂÁö È®ÀÎ
            ws.PrintOut ActivePrinter:=DefaultPrinter, From:=1, To:=2, copies:=PaperCopies
            PLitem.SubItems(3) = "Done" 'Print
        Else
            PLitem.SubItems(3) = "Pass" 'Print
        End If
ARH.UpdateProgressBar ARH.PB_BOM, (i - 0.73) / ListCount * 100
'ÀúÀåÀ» À§ÇØ Å¸ÀÌÆ² ¼öÁ¤
        Title = "PartList " & PLLV.ListItems.Item(Chkditem(i)).text & "_" & wLine
ARH.UpdateProgressBar ARH.PB_BOM, (i - 0.65) / ListCount * 100
'ÀúÀå¿©ºÎ °áÁ¤
        SavedPath = SaveFilesWithCustomDirectory("PartList", PL_Processing_WB, Printer.PS_PartList(PrintArea), Title, True, False, OriginalKiller.PartList)
ARH.UpdateProgressBar ARH.PB_BOM, (i - 0.45) / ListCount * 100
        PLitem.SubItems(4) = "Done" 'PDF
ARH.UpdateProgressBar ARH.PB_BOM, (i - 0.35) / ListCount * 100
        If MRB_PL Then
            Dim tWB As Workbook, Target As Range
            Set tWB = Workbooks.Open(SavedPath & ".xlsx")  ' ¸Þ´º¾ó ¸ðµåÀÏ ¶§ ¿­±â
            Set Target = tWB.Worksheets(1).Rows(1).Find("-Line", LookAt:=xlPart, LookIn:=xlValues).Offset(1, 1)
            tWB.Worksheets(1).Activate
            Target.Select
            ActiveWindow.FreezePanes = True
        End If
'Progress Update
ARH.UpdateProgressBar ARH.PB_BOM, i / ListCount * 100
    Next i
        
    If Handle Then MsgBox ListCount & "ÀåÀÇ PartList Ãâ·Â ¿Ï·á"
    
End Sub
' ¹®¼­ ¼­½Ä ÀÚµ¿È­
Private Sub AutoReport_PartList(ByRef Wb As Workbook)
    ' ÃÊ±âÈ­ º¯¼ö
    Set Target_WorkSheet = Wb.Worksheets(1)
    Set vCFR = New Collection
    
    Dim i As Long, DrawingMap As D_Maps  ', LastRow As Long ' DailyPlan µ¥ÀÌÅÍ°¡ ÀÖ´Â ¸¶Áö¸· Çà
    
    SetUsingColumns vCFR ' »ç¿ëÇÏ´Â ¿­ ¼±Á¤
    AR_1_EssentialDataExtraction ' ÇÊ¼öµ¥ÀÌÅÍ ÃßÃâ
    Interior_Set_PartList ' Range ¼­½Ä ¼³Á¤
    AutoPageSetup Target_WorkSheet, Printer.PS_PartList(PrintArea)   ' PrintPageSetup
    Set DrawingMap = AR_2_ModelGrouping(2, , Target_WorkSheet, SubG)
'    MarkingUP_items DrawingMap
    MarkingUP_PL DrawingMap
    
    Set vCFR = Nothing
End Sub

Private Sub SetUsingColumns(ByRef UsingCol As Collection) ' »ì¸± ¿­ ¼±Á¤
    UsingCol.Add "ÅõÀÔ" & vbLf & "½ÃÁ¡"
    UsingCol.Add "W/O"
    UsingCol.Add "¸ðµ¨"
    UsingCol.Add "Suffix"
    UsingCol.Add "°èÈ¹ ¼ö·®"
    UsingCol.Add "Tool"
End Sub

Private Sub AR_1_EssentialDataExtraction() ' AutoReport ÃÊ¹Ý ¼³Á¤ / ÇÊ¼ö µ¥ÀÌÅÍ ¿µ¿ª¸¸ ÃßÃâÇÔ
    Dim i As Long, c As Long, vStart As Long, vEnd As Long, CritCol(1 To 2) As Long
    Dim vCell As Range
    Dim ws As Worksheet: Set ws = Target_WorkSheet
    
    Application.DisplayAlerts = False ' initializing
    
    vStart = 2: vEnd = ws.Cells(ws.Rows.Count, 1).End(xlUp).Row
    ' D-Day Æ÷ÇÔ NÀÏÄ¡ µ¥ÀÌÅÍ¸¸ Ã³¸®ÇÏµµ·Ï °­Á¦ÇÔ.
    CritCol(1) = ws.Rows(1).Find("YYYYMMDD", LookAt:=xlWhole, LookIn:=xlValues).Column
    CritCol(2) = ws.Rows(1).Find("Input Time", LookAt:=xlWhole, LookIn:=xlValues).Column
    MergeDateTime_Flexible ws, CritCol(1), 1, CritCol(2), , "ÅõÀÔ" & vbLf & "½ÃÁ¡", "hh:mm"
    CritCol(2) = 0
    For i = vStart To vEnd
        If CritCol(2) >= DayCount Or vEnd = i Then '<- NÀ§Ä¡
            vStart = i: Exit For
        ElseIf ws.Cells(i, CritCol(1)).Value <> ws.Cells(i + 1, CritCol(1)).Value Then
            CritCol(2) = CritCol(2) + 1
        End If
    Next i
    ws.Rows(vStart & ":" & vEnd).Delete ' ºÒÇÊ¿äÇÑ Çà »èÁ¦Ã³¸®
    vEnd = vStart - 1: vStart = 2
    CritCol(1) = ws.Rows(1).Find("ÀÜ·®", LookAt:=xlWhole, LookIn:=xlValues).Column
    For i = CritCol(1) To 1 Step -1 ' ºÒÇÊ¿äÇÑ ¿­ »èÁ¦Ã³¸®
        If Not IsInCollection(ws.Cells(1, i).Value, vCFR) Then ws.Columns(i).Delete
    Next i
    CritCol(1) = ws.Rows(1).Find("¸ðµ¨", LookAt:=xlWhole, LookIn:=xlValues).Column
    
    For i = vStart To vEnd ' Á¦¸ñ ¿Ü µ¥ÀÌÅÍ Çà ½ÃÀÛºÎÅÍ ³¡±îÁö µ¥ÀÌÅÍ Ã³¸®
        Set vCell = ws.Cells(i, CritCol(1))
        vCell.Value = vCell.Value & "." & vCell.Offset(0, 1).Value ' ¸ðµ¨, Suffix ¿­ º´ÇÕ
    Next i
    ws.Columns(vCell.Offset(0, 1).Column).Delete ' ºÒÇÊ¿äÇÑ ¿­ »èÁ¦Ã³¸®
    CritCol(1) = ws.Rows(1).Find("°èÈ¹ ¼ö·®", LookAt:=xlWhole, LookIn:=xlValues).Column
    CritCol(2) = ws.Cells(1, ws.Columns.Count).End(xlToLeft).Column
    PartCombine ws.Range(ws.Cells(1, CritCol(1) + 1), ws.Cells(1, CritCol(2))), vStart, vEnd ' ´ÜÀÏ ÆÄÆ®¸íÀ¸·Î º´ÇÕÃ³¸®
    DeleteDuplicateRowsInColumn (CritCol(1) - 3), vStart, vEnd, ws, CritCol(1) ' WorkOrder Áßº¹ Çà Á¦°Å
    For Each vCell In ws.Range(ws.Cells(1, CritCol(1) + 1), ws.Cells(1, CritCol(2))) ' Á¦¸ñ ¿­ ¼öÁ¤
        vCell.Value = Replace(ExtractBracketValue(vCell.Value), "_", vbLf)
    Next vCell
    Replacing_Parts ws.Range(ws.Cells(vStart, CritCol(1) + 1), ws.Cells(vEnd, CritCol(2)))
    ws.Cells(1, CritCol(1)).Value = "¼ö·®" ' Á¦¸ñ¿­ Á¤¸®f
    ws.Columns(6).Insert: ws.Columns(6).Insert
    ws.Cells(1, 6).Value = wLine & "-Line": ws.Cells(1, 6).Resize(1, 2).Merge
    ws.Name = "PartList_Total"
    'Set CopiedArea = ws.Range(ws.Cells(1, 1), ws.Cells(vEnd, 7)) ' Ä«ÇÇµå¿¡¸®¾î ¼³Á¤´Ü, µ¿Àû ÃßÀûÀ¸·Î ¹Ù²Ü °Í.
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
    Dim i As Long, FirstRow As Long, LastRow As Long: FirstRow = ws.Rows(1).Find("¸ðµ¨", LookAt:=xlWhole, LookIn:=xlValues).Row + 1: LastRow = ws.Cells(ws.Rows.Count, 1).End(xlUp).Row
    Dim xCell As Range, Target As Range: Set PrintArea = ws.Cells(1, 1).CurrentRegion: Set Target = PrintArea
    
    SetEdge(1) = xlEdgeLeft
    SetEdge(2) = xlEdgeRight
    SetEdge(3) = xlEdgeTop
    SetEdge(4) = xlEdgeBottom
    SetEdge(5) = xlInsideVertical
    SetEdge(6) = xlInsideHorizontal
    
    For Each xCell In Target
        With xCell
            If .Value = "" Then .Interior.Color = RGB(BoW, BoW, BoW) ' Brightness
            .HorizontalAlignment = xlLeft
            .VerticalAlignment = xlCenter
        End With
    Next xCell
    
    ws.Columns(1).HorizontalAlignment = xlRight ' 1¹øÂ° ¿­ ¿ìÃøÁ¤·Ä
    ws.Columns("B:D").HorizontalAlignment = xlLeft ' 2,3,4¹øÂ° ¿­ ÁÂÃøÁ¤·Ä
    ws.Columns(5).HorizontalAlignment = xlRight ' 5¹øÂ° ¿­ ¿ìÃøÁ¤·Ä
    ws.Rows(1).HorizontalAlignment = xlCenter  ' 1¹øÂ° Çà Áß¾ÓÁ¤·Ä
    
    With Target ' PrintArea(index) = Target ÀÎ¼â¿µ¿ªÀÇ ÀÎÅ×¸®¾î ¼¼ÆÃ
        .Rows(1).Interior.Color = RGB(133, 233, 233)
        .Rows(1).Font.Bold = True
        .Font.Name = "LG½º¸¶Æ®Ã¼2.0 Regular"
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
        .Columns(.Rows(1).Find("Tool", LookAt:=xlWhole, LookIn:=xlValues).Column).Hidden = True ' Tool ¿­Àº ¼û±èÃ³¸®
        On Error Resume Next
        For i = FirstRow To LastRow
            Set Target = .Cells(i, 1) ' ÅõÀÔ½ÃÁ¡ ¿­ ¼³Á¤ºÎºÐ
            If Day(CDate(Target.Value)) <> Day(CDate(Target.Offset(-1, 0).Value)) Then
                Target.NumberFormat = "d(aaa)"
                If Err.Number = 0 Then
                    .Rows(i).Borders(xlEdgeTop).LineStyle = xlDash
                    .Rows(i).Borders(xlEdgeTop).Weight = xlMedium
                End If
            End If
            Err.Clear
        Next i
        On Error GoTo 0
    End With
    
End Sub

Private Function GetPartListWhen(PartListDirectiory As String, Optional ByRef DDC As Long) As String
    ' Excel ¾ÖÇÃ¸®ÄÉÀÌ¼ÇÀ» »õ·Î¿î ÀÎ½ºÅÏ½º·Î »ý¼º
    Dim xlApp As Excel.Application: Set xlApp = New Excel.Application: xlApp.Visible = False
    Dim Wb As Workbook: Set Wb = xlApp.Workbooks.Open(PartListDirectiory) ' ¿öÅ©ºÏ ¿­±â
    Dim ws As Worksheet: Set ws = Wb.Worksheets(1) ' ¿öÅ©½ÃÆ® ¼±ÅÃ
    Dim Cell As Range, SC As Long, EC As Long, i As Long
        
    Set Cell = ws.Rows(1).Find(What:="YYYYMMDD", LookAt:=xlWhole, LookIn:=xlValues) ' PL¿¡¼­ ³¯Â¥¸¦ Ã£´Â ÁÙ
    If Cell Is Nothing Then GetPartListWhen = "It's Not a PartList": GoTo NAP ' ¿­¶÷ÇÑ ¹®¼­°¡ PartList°¡ ¾Æ´Ò½Ã ¿À·ùÃ³¸® ´Ü
    DDC = -1 ' ¸¶Áö¸· °ª ¼±º¸Á¤
    SC = Cell.Row + 1: EC = ws.Cells(ws.Rows.Count, Cell.Column).End(xlUp).Row
    For i = SC To EC
        If ws.Cells(i, Cell.Column).Value <> ws.Cells(i + 1, Cell.Column).Value Then DDC = DDC + 1
    Next i
    ARH.PL_Ddays_Counter.Max = DDC
    Title = Cell.Offset(1, 0).Value ' YYYYMMDD Æ÷¸ËµÈ ³¯Â¥°ª ÀÎ°è
    Title = mid(Title, 5, 2) & "¿ù-" & mid(Title, 7, 2) & "ÀÏ"
    GetPartListWhen = Title ' ³¯Â¥Çü Á¦¸ñ°ª ÀÎ°è
    wLine = ws.Rows(1).Find(What:="Line", LookAt:=xlWhole, LookIn:=xlValues).Offset(1, 0).Value ' ¶óÀÎ °ª ÃßÃâ
    SC = ws.Rows(1).Find(What:="ÀÜ·®", LookAt:=xlWhole, LookIn:=xlValues).Offset(0, 1).Column + 1
    EC = ws.Cells(1, ws.Columns.Count).End(xlToLeft).Column
    Set Cell = ws.Range(ws.Cells(1, SC), ws.Cells(1, EC))
    Set vCFR = New Collection: Set vCFR = PartCollector(Cell, CollectionType:=Unique)
    
NAP:
    Wb.Close SaveChanges:=False: Set Wb = Nothing ' ¿öÅ©ºÏ ´Ý±â
    xlApp.Quit: Set xlApp = Nothing ' Excel ¾ÖÇÃ¸®ÄÉÀÌ¼Ç Á¾·á
End Function
Private Function PartCollector(ByRef PartNamesArea As Range, _
    Optional ByRef UniqueParts As Collection, Optional ByRef Duplicated As Collection, _
    Optional CollectionType As CollectorTypes = 0) As Collection
    
    Dim ws As Worksheet: Set ws = PartNamesArea.Worksheet
    Dim Cell As Range
    Dim BracketVal As String, i As Long
    
     If UniqueParts Is Nothing Or Duplicated Is Nothing Then Set UniqueParts = New Collection: Set Duplicated = New Collection
    
    ' ºÎÇ°¸í ¼öÁý ¹Ýº¹¹®
    On Error Resume Next
    For Each Cell In PartNamesArea
        BracketVal = ExtractBracketValue(Cell.Value)
            For i = 1 To UniqueParts.Count
                If BracketVal = UniqueParts(i) Then Duplicated.Add BracketVal, BracketVal ' Áßº¹µÇ´Â ºÎÇ°¸í ¼öÁý
            Next i
        If BracketVal <> "" Then UniqueParts.Add BracketVal, BracketVal ' °íÀ¯ ºÎÇ°¸í ¼öÁý
    Next Cell
    On Error GoTo 0
    
    ' ¸Å°³º¯¼ö°¡ Á¸ÀçÇÒ ¶§¸¸ FunctionÀ¸·Î¼­ ±â´ÉÇÔ
    Select Case CollectionType
        Case 0: Set PartCollector = Nothing
        Case -3: Set PartCollector = UniqueParts
        Case -6: Set PartCollector = Duplicated
    End Select
End Function

Private Sub Replacing_Parts(ByRef RangeTarget As Range) ' RpP
    ' ¹üÀ§ ³» °¢ ¼¿¿¡¼­ Vendor¿Í Parts Á¤º¸¸¦ ÃßÃâ ¹× Á¤¸®ÇÏ¿© ¼¿ °ªÀ» Àç±¸¼ºÇÕ´Ï´Ù.
   
    Dim vCell As Range, ws As Worksheet: Set ws = RangeTarget.Worksheet
    Dim i As Long, Start_P As Long, End_P As Long, Searching As Long
    Dim vVendor As Collection, vParts As Collection
    Dim sVendor As String, sParts As String, Target As String, Duplicated As Boolean
    Dim removeList As Variant
    removeList = Array("(ÁÖ)", "¢ß", "EKHQ_", " Co., Ltd.", " LTD", " CO,", " CO.,LTD")
   
    ' ´ë»ó ¼¿À» ÇÏ³ª¾¿ ¼øÈ¸
    For Each vCell In RangeTarget
        If Len(vCell.Value) > 0 Then ' ºó ¼¿ ¹«½Ã
            Target = vCell.Value ' ¿øº» ÅØ½ºÆ® ÃßÃâ
           
            ' Á¦°Å ´ë»ó ¹®ÀÚ¿­ Ä¡È¯
            For i = LBound(removeList) To UBound(removeList)
                Target = Replace(Target, removeList(i), "")
            Next i
            Target = Replace(Target, vbLf & "[", " [") ' ÁÙ¹Ù²Þ µÚ °ýÈ£¸¦ °ø¹éÀ¸·Î Á¤¸®ÇÏ¿© Æ÷¸Ë À¯Áö
            If Right$(Target, 1) = vbLf Then Target = Left$(Target, Len(Target) - 1) ' ¸¶Áö¸·¿¡ ÁÙ¹Ù²Þ ¹®ÀÚ ÀÖÀ» °æ¿ì Á¦°Å
           
            ' Collection ÃÊ±âÈ­
            Set vVendor = New Collection: Set vParts = New Collection
            Start_P = 0: End_P = 0: Searching = 0
           
            ' º¥´õ¿Í ÆÄÃ÷ ÃßÃâ ¹Ýº¹
            Do
                Searching = End_P + 1: Duplicated = False ' Å½»ö ½ÃÀÛ À§Ä¡ ÃÊ±âÈ­
                sVendor = ExtractBracketValue(Target, Searching) ' °ýÈ£ ¾ÈÀÇ º¥´õ ÃßÃâ
               
                 ' ¼¿°ªÀÇ Char°¡ 1ÀÚ ¹Ì¸¸ Or ¼¿°ªÀÇ Char°¡ 5ÀÚ ÀÌ»óÀÌ¸é¼­ µ¿½Ã¿¡ ¿µ¹® º¥´õÀÏ °æ¿ì = "µµÀÔÇ°"À¸·Î ´ëÃ¼
                 If Len(sVendor) < 1 Or (Len(sVendor) >= 5 And Not sVendor Like "*[°¡-ÆR]*") Then sVendor = "µµÀÔÇ°"
                
                 ' ÀÌ¹Ì µî·ÏµÈ º¥´õÀÎÁö Áßº¹ °Ë»ç
                 For i = 1 To vVendor.Count
                     If vVendor(i) = sVendor Then
                         Duplicated = True
                         Exit For ' Áßº¹ È®ÀÎµÇ¸é ·çÇÁ Á¾·á
                     End If
                 Next i
                
                 If Not Duplicated Then vVendor.Add sVendor, sVendor ' Áßº¹ ¾Æ´Ï¸é º¥´õ µî·Ï
                 Start_P = Searching + 2: End_P = InStr(Start_P, Target, " [") - 1 ' ´ÙÀ½ ÆÄÃ÷ ¹üÀ§ ¼³Á¤
                 If End_P < Start_P Then End_P = Len(Target) ' ¸¶Áö¸· ÆÄÃ÷ Ã³¸®
                 sParts = mid$(Target, Start_P, End_P - Start_P + 1) ' ÆÄÃ÷ ¹®ÀÚ¿­ ÃßÃâ
                
                 ' ÆÄÃ÷ Ãß°¡ ¶Ç´Â º´ÇÕ Ã³¸®
                 On Error Resume Next ' Å° Áßº¹ ¿À·ù ¹æÁö
                     If Not Duplicated Then
                         vParts.Add sParts, sVendor
                     Else
                         sParts = vParts(sVendor) & "/" & sParts ' º´ÇÕ
                         vParts.Remove sVendor
                         vParts.Add sParts, sVendor
                     End If
                 On Error GoTo 0 ' ¿À·ù Ã³¸® º¹±¸
            Loop Until End_P >= Len(Target) ' ÀüÃ¼ ¹®ÀÚ¿­ ³¡±îÁö ¹Ýº¹
            
            ' ¿¹¿ÜÃ³¸®
            'If ¿¹¿ÜÃ³¸® Then
                If ws.Cells(1, vCell.Column).Value = "Burner" Then
                    Select Case Target
                    Case Is = "[±â¹Ì] 4102/4202/4402/4502" ' Signature
                        Target = "[ÇÇÅ·] Oval/Best"
                    Case Is = "[±â¹Ì] 4102/4202/4402/4502 [SABAF S.P.A.] 6904/7302" ' Best
                        Target = "[ÇÇÅ·] Oval/Best/Sabaf"
                    Case Is = "[±â¹Ì] 4102/4202/4402/4502(2)" ' Better
                        Target = "[ÇÇÅ·] Oval/Better"
                    Case Is = "[±â¹Ì] 7906/8506/8606/8706" ' Old Gas Model
                        Target = "[ÇÇÅ·] FZ¡ÖFH/Better"
                    Case Else ' ¿¡·¯Ã³¸®
                        Target = "Matching Error"
                    End Select
                Else
                    ' ÀÏ¹ÝÃ³¸® / ÃÖÁ¾ ¹®ÀÚ¿­ Àç±¸¼º
                    Target = ""
                    For i = 1 To vVendor.Count
                        Target = Target & " [" & vVendor(i) & "] " & vParts(vVendor(i))
                    Next i
                End If
            'End If
            vCell.Value = Trim(Target) ' °á°ú¸¦ ¼¿¿¡ ±â·Ï
        End If
    Next vCell
End Sub

Private Sub PartCombine(ByRef PartNamesArea As Range, ByVal rStart As Long, ByVal rEnd As Long)
    Dim ws As Worksheet: Set ws = PartNamesArea.Worksheet
    Dim UniqueParts As New Collection, Duplicated As New Collection
    Dim Cell As Range
    Dim BracketVal As String
    Dim i As Long, CBTr As Long

    PartCollector PartNamesArea, UniqueParts, Duplicated
    
    ' Àü¿ªº¯¼ö·Î ÄÃ·º¼Ç ±íÀº º¹»ç / Àü¿ªº¯¼ö·Î º¸³»±â À§ÇÑ º¹»ç
    Set vCFR = New Collection
    For i = 1 To UniqueParts.Count
        vCFR.Add Replace(UniqueParts(i), "_", vbLf)
    Next i

    ' 2°³ ÀÌ»óÀÇ ºÎÇ°¿­ Á¤º¸°¡ ÀÖ´Â ºÎÇ°¸¸ º´ÇÕ ½ÇÇà
    For i = 1 To Duplicated.Count
        Set UniqueParts = New Collection
        For Each Cell In PartNamesArea
            BracketVal = ExtractBracketValue(Cell.Value)
            If BracketVal = Duplicated(i) Then UniqueParts.Add Cell ' Áßº¹µÈ ºÎÇ° ¼¿ ¼±º°
        Next Cell
        For CBTr = rStart To rEnd ' ÆÄÆ®º° º´ÇÕ
            CCBC UniqueParts, CBTr ' Combine Target Row
        Next CBTr
        For CBTr = 2 To UniqueParts.Count ' Á¤¸® ¿Ï·á ÈÄ À×¿© ¿­ »èÁ¦
            UniqueParts(CBTr).EntireColumn.Delete ' 1¹øÂ° ¼¿¿¡ ÃëÇÕÇßÀ¸¹Ç·Î ³ª¸ÓÁö ¼¿(¿­) »èÁ¦
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
    For i = 1 To CER.Count ' ÇÊ¿ä Á¤º¸ ÃëÇÕ
        If Trim(CER(i).Value) <> "" Then ValueList = ValueList & CER(i).Value & vbLf
        If i > 1 Then CER(i).Value = "" ' ÃëÇÕ ÈÄ À×¿©¼¿ÀÇ °ª »èÁ¦
    Next i
    If Right$(ValueList, 1) = vbLf Then ValueList = Left$(ValueList, Len(ValueList) - 1)
    
    ' Áß¾ÓÁ¤·Ä ¹× ÅØ½ºÆ® »ðÀÔ
    With CER(1)
        .Value = ValueList
        .HorizontalAlignment = xlCenter
        .VerticalAlignment = xlCenter
    End With
End Sub
Private Sub MarkingUP_items(ByRef Target As D_Maps)
    Dim ws As Worksheet: Set ws = Target_WorkSheet
    Dim i As Long, tCol As Long, sCol As Long, eCol As Long, tRow As Long, sRow As Long, eRow As Long, Total As Long
    Dim CrrR As Range, NxtR As Range
    sCol = ws.Rows(1).Find("-Line", LookAt:=xlPart, LookIn:=xlValues).Column + 2
    eCol = ws.Cells(1, ws.Columns.Count).End(xlToLeft).Column
'    LastRow = ws.Cells(ws.Rows.Count, 1).End(xlUp).Row
    
    With Target
        If .Count(MainG) > 0 Then .RemoveAll MainG
        For tCol = sCol To eCol
            For i = 1 To .Count(SubG)
                Set CrrR = .Sub_Lot(i).Start_R.Offset(0, tCol - sCol + 5)
                If Not .Count(SubG) = i Then
                    Set NxtR = .Sub_Lot(i + 1).Start_R.Offset(0, tCol - sCol + 5)
                    If CrrR.Value <> NxtR.Value Then eRow = CrrR.Row
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
    
    For i = 1 To Target.Count(SubG) ' SubGroups À­¶óÀÎ ¶óÀÌ´×
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
