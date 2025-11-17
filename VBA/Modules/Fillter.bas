Attribute VB_Name = "Fillter"
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
