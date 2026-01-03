Attribute VB_Name = "AB_C_PL2DP"
' 핵심 로직의 분리된 모듈임.
Option Explicit

Private UI As Tool_PL2DP

'=======================================
' 외부로부터 호출용 메서드
'=======================================
Public Function initialize_Controller_PL2DP() As Boolean
        
        initialize_Controller_PL2DP
End Function

Public Sub PL2DP_Set_Reference()
    Set_Reference
End Sub
Public Sub PL2DP_Set_Target()
    Set_Target
End Sub
Public Sub PL2DP_Set_Detail()

End Sub
Public Sub PL2DP_Run()
    Run
End Sub
'=======================================
' 내부 로직
'=======================================
Private Sub Run()
    Dim Ref As String, Trg As String
    Ref = UI.TB_Reference.text
    Trg = UI.TB_Target.text
    
End Sub

Private Sub Set_Reference()
    
End Sub
Private Sub Set_Target()
    
End Sub
Private Sub Set_Detail()

End Sub
