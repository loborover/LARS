VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
END
Attribute VB_Name = "Painter"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = False
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
Public Property Set DrawingWorksheet(ByRef Value As Worksheet)
    Set DrawingWS = Value
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
        If Not IsEmpty(Check.Value) Then
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
