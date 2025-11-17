Attribute VB_Name = "AA_Test"
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
