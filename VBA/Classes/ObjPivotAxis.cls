VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
END
Attribute VB_Name = "ObjPivotAxis"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = False
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
