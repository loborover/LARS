VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
END
Attribute VB_Name = "D_Maps"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = False
Option Explicit

Private Main_Groups As New Collection
Private Sub_Groups As New Collection
Private T_Lot As New D_LOT

'초기화 이벤트 메서드
Private Sub Class_Initialize()
    Set Main_Groups = New Collection
    Set Sub_Groups = New Collection
    Set T_Lot = New D_LOT
End Sub
'소멸 이벤트 메서드
Private Sub Class_Terminate()
    Set Main_Groups = Nothing
    Set Sub_Groups = Nothing
    Set T_Lot = Nothing
End Sub
Public Function Main_Lot(Optional Index As Variant = 1) As D_LOT
    Set Main_Lot = Main_Groups.Item(Index)
End Function
Public Function Sub_Lot(Optional Index As Variant = 1) As D_LOT
    Set Sub_Lot = Sub_Groups.Item(Index)
End Function

Public Sub Set_Lot(ByRef Start_Range As Range, ByRef End_Range As Range, _
                        Optional GroupType As MorS = MainG)
'    If ws Is Nothing Then Set ws = Start_Range.Parent
    
    Set T_Lot = New D_LOT
    With T_Lot
        Set .Start_R = Start_Range
        Set .End_R = End_Range
    End With
    
    Select Case GroupType
    Case MainG
        Main_Groups.Add T_Lot '.Copy
    Case SubG
        Sub_Groups.Add T_Lot '.Copy
    End Select
End Sub
Public Sub Remove(Index As Variant, Optional Target As MorS = MainG)
    Select Case Target
    Case MainG
        Main_Groups.Remove Index
    Case SubG
        Sub_Groups.Remove Index
    End Select
End Sub
Public Sub RemoveAll(Optional Target As MorS = MainG)
    Dim i As Long
    Select Case Target
    Case MainG
        For i = Main_Groups.Count To 1 Step -1
            Main_Groups.Remove (i)
        Next i
    Case SubG
        For i = Sub_Groups.Count To 1 Step -1
            Sub_Groups.Remove (i)
        Next i
    End Select
End Sub
Public Function Count(Target As MorS) As Long
    Select Case Target
    Case MainG
        Count = Main_Groups.Count
    Case SubG
        Count = Sub_Groups.Count
    End Select
End Function
Public Function Recent_Lot(Optional Index As Long = 0, Optional Target As MorS = MainG) As D_LOT
    Dim Final As Long
    Select Case Target
    Case MainG
        Final = Main_Groups.Count + Index
        Set Recent_Lot = Main_Groups(Final)
    Case SubG
        Final = Sub_Groups.Count + Index
        Set Recent_Lot = Sub_Groups(Final)
    End Select
End Function

