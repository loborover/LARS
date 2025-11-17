VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
END
Attribute VB_Name = "InventoryCart"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = False
Option Explicit

Private Makers As New Collection, parts As New Collection, Duplicated As New Collection
Private TargetWB As Workbook, TargetWS As Worksheet, Container As Range, Ship As Range

'초기화 이벤트 메서드
Private Sub Class_Initialize()

End Sub
'소멸 이벤트 메서드
Private Sub Class_Terminate()
    
End Sub
