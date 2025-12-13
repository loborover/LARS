VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
END
Attribute VB_Name = "ProductModel2"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = False
Option Explicit

Private vPrevious As ModelInfo
Private vCurrent As ModelInfo
Private vNext As ModelInfo
Private vStandardForGroup As String
Private vChanged As Boolean
Private Grouping As Boolean
Private vChCount As Long
Private vLOT As Long


'초기화 이벤트 메서드
Private Sub Class_Initialize()
    Set vPrevious = New ModelInfo
    Set vCurrent = New ModelInfo
    Set vNext = New ModelInfo
    vChanged = False
    vChCount = 0
    vLOT = 1
End Sub
'소멸 이벤트 메서드
Private Sub Class_Terminate()
    Set vPrevious = Nothing
    Set vCurrent = Nothing
    Set vNext = Nothing
End Sub
Public Property Get Another() As ModelInfo
    Set Another = vPrevious
End Property
Public Function Set_Another_Model(ByRef Target As Range) As ModelInfo
    vPrevious.FullName = Target.Value
    vPrevious.Set_Pivot Target
    Set Set_Another_Model = vPrevious
End Function

Public Property Get Crr() As ModelInfo
    Set Crr = vCurrent
End Property
Public Property Get Prv() As ModelInfo
    Set Prv = vPrevious
End Property
Public Property Get Nxt() As ModelInfo
    Set Nxt = vNext
End Property
Public Property Get Changed() As Boolean
    Changed = vChanged
End Property
Public Property Get Count() As Long
    Count = vChCount
End Property
Public Property Get Lot() As Long
    Lot = vLOT
End Property

Public Sub NextModel(ByRef NextTarget As Range)
    Set vPrevious = vCurrent.Copy  ' 현재 > 이전
    Set vCurrent = vNext.Copy  ' 다음 > 현재
    Set vNext = New ModelInfo
    vNext.WorkOrder = NextTarget.Offset(0, -1).Value
    vNext.FullName = NextTarget.Value
    vNext.Set_Pivot NextTarget
    
    vLOT = vLOT + 1 ' 모델이 누적될 때마다 전부 셈
    
End Sub
Public Sub SetModel(ByRef Current_Target As Range, ByRef Next_Target As Range)
    vCurrent.FullName = Current_Target.Value
    vCurrent.Set_Pivot Current_Target
    vNext.FullName = Next_Target.Value
    vNext.Set_Pivot Next_Target
End Sub

Public Function Compare2Models(ByRef Target1 As ModelInfo, ByRef Target2 As ModelInfo, Field As ModelinfoFeild) As Boolean
    Select Case Field
        Case 901: Compare2Models = (Target1.WorkOrder = Target2.WorkOrder)
        Case 902: Compare2Models = (Target1.FullName = Target2.FullName)
        Case 903: Compare2Models = (Target1.Number = Target2.Number)
        Case 904: Compare2Models = (Target1.SpecNumber = Target2.SpecNumber)
        Case 905: Compare2Models = (Target1.Spec = Target2.Spec)
        Case 906: Compare2Models = (Target1.TheType = Target2.TheType)
        Case 907: Compare2Models = (Target1.Species = Target2.Species)
        Case 908: Compare2Models = (Target1.TySpec = Target2.TySpec)
        Case 909: Compare2Models = (Target1.Color = Target2.Color)
        Case 910: Compare2Models = (Target1.Suffix = Target2.Suffix)
    End Select
End Function

' 디버깅용 디테일
Public Sub Detail(ByRef PrevM As ModelInfo, _
                        ByRef CurrM As ModelInfo, _
                        ByRef NextM As ModelInfo, _
                        Optional Controller As Long = 0)
    
    Dim Dealer As Long
    
    Select Case Controller
        Case 0
            Debug.Print "" ' 이전 입력과 구분을 위한 공백
            Debug.Print "이전 모델 : " & PrevM.FullName
            Debug.Print "현재 모델 : " & CurrM.FullName
            Debug.Print "다음 모델 : " & NextM.FullName
            Debug.Print CurrM.FullName
            Debug.Print CurrM.Number
            Debug.Print CurrM.Spec
            Debug.Print CurrM.TheType
            Debug.Print CurrM.Species
            Debug.Print CurrM.Suffix
            Debug.Print CurrM.Color
            Debug.Print "Model Count : " & Me.Count
            Debug.Print "is Changed? : " & Me.Changed
        Case 1
            For Dealer = 1 To 6
            Next Dealer
    End Select
End Sub

Public Sub test()
    Me.Compare2Models Me.Crr, Me.Nxt, mif_Spec
End Sub
