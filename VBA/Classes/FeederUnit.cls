VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
END
Attribute VB_Name = "FeederUnit"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = False
Option Explicit

Private pName As String
Private pItems As Collection

Private Sub Class_Initialize()
    Set pItems = New Collection
End Sub

Public Property Get Name() As String
    Name = pName
End Property

Public Property Let Name(ByVal Value As String)
    pName = Value
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
