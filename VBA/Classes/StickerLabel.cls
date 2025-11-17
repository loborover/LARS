VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
END
Attribute VB_Name = "StickerLabel"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = False
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
