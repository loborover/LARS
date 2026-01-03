VERSION 5.00
Begin {C62A69F0-16DC-11CE-9E98-00AA00574A4F} Tool_PL2DP 
   Caption         =   "Convert PL to DP"
   ClientHeight    =   2295
   ClientLeft      =   120
   ClientTop       =   465
   ClientWidth     =   6375
   OleObjectBlob   =   "Tool_PL2DP.frx":0000
   StartUpPosition =   1  '소유자 가운데
End
Attribute VB_Name = "Tool_PL2DP"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
' UserForm 원본 모듈임. 최대한 간소하게. interior, 호출 메서드만 나열
Option Explicit
Private isinit As Boolean
Private Sub initialize()
    If Not isinit Then isinit = initialize_Controller_PL2DP
End Sub

Private Sub AB_Run_Click()
    initialize
    PL2DP_Run
End Sub

Private Sub AB_SR_Click()
    initialize
    PL2DP_Set_Reference
End Sub

Private Sub AR_SD_Click()
    initialize
    PL2DP_Set_Detail
End Sub

Private Sub AR_ST_Click()
    initialize
    PL2DP_Set_Target
End Sub
