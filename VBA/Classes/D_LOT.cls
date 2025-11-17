VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
END
Attribute VB_Name = "D_LOT"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = False
Option Explicit

Private vLot_info As New Collection
Private vStart As Range, vEnd As Range

'초기화 이벤트 메서드
'Private Sub Class_Initialize()
'
'End Sub
'소멸 이벤트 메서드
'Private Sub Class_Terminate()
'
'End Sub
Public Property Get ModelCount() As Long
    ModelCount = vLot_info.Count
End Property
Public Property Get Lot_Range() As Range
    If vStart Is Nothing Or vEnd Is Nothing Then
        Err.Raise 9998, , "Start, End 참조 오류"
        Exit Property
    ElseIf Not vStart.Worksheet Is vEnd.Worksheet Then
        Err.Raise 9999, , "Start와 End는 동일한 시트에 있어야 합니다."
        Exit Property
    End If
    Set Lot_Range = vStart.Worksheet.Range(vStart, vEnd)
End Property

Public Property Get Start_R() As Range
    Set Start_R = vStart
End Property
Public Property Set Start_R(Target_ModelNumber As Range)
    Set vStart = Target_ModelNumber
    If Not vEnd Is Nothing Then ParseModelinfo
End Property

Public Property Get End_R() As Range
    Set End_R = vEnd
End Property
Public Property Set End_R(Target As Range)
    Set vEnd = Target
    If Not vStart Is Nothing Then ParseModelinfo
End Property

Public Function Copy() As D_LOT
    Dim CopiedData As New D_LOT
    
    With CopiedData
        Set .Start_R = Me.Start_R
        Set .End_R = Me.End_R
    End With
    Set Copy = CopiedData
End Function
Public Function info(Optional Index As Long = 1) As ModelInfo
    If vLot_info(Index) Is Nothing Then Exit Function
    Set info = vLot_info(Index)
End Function

Private Sub ParseModelinfo()
    Dim vCell As Range, i As Long
    Dim TargetModelinfo As New ModelInfo
    Dim UniqueList As New Collection
    Dim ValueStr As String, Exists As Boolean
    
    On Error GoTo ErrorHandler
    
    For Each vCell In Me.Lot_Range.Cells
        ValueStr = Trim(CStr(vCell.value))
        If Len(ValueStr) > 0 Then
            Exists = False
            For i = 1 To UniqueList.Count
                If UniqueList(i) = ValueStr Then
                    Exists = True
                    Exit For
                End If
            Next i
            If Not Exists Then UniqueList.Add ValueStr
        End If
    Next
        
    For i = 1 To UniqueList.Count
        TargetModelinfo.FullName = UniqueList(i)
        vLot_info.Add TargetModelinfo.Copy
    Next i
    
    Exit Sub
ErrorHandler:
    Debug.Print "Err.Modelinfo"
    Exit Sub
End Sub
