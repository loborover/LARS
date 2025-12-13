VERSION 5.00
Begin {C62A69F0-16DC-11CE-9E98-00AA00574A4F} BCCUF 
   Caption         =   "Back Color Controller"
   ClientHeight    =   4845
   ClientLeft      =   45
   ClientTop       =   390
   ClientWidth     =   6645
   OleObjectBlob   =   "BCCUF.frx":0000
   StartUpPosition =   1  '소유자 가운데
End
Attribute VB_Name = "BCCUF"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
Private Const Color_Inversion_Criterion As Long = 204
Private pvRGB(1 To 2) As New ObjPivotAxis
Private Sub Userform_Initialize()
    
    BCR_Slidebar.Value = 210
    BCG_Slidebar.Value = 210
    BCB_Slidebar.Value = 210
End Sub
Public Property Get Documents_BackColor() As ObjPivotAxis
    Set Documents_BackColor = pvRGB(2)
End Property
Private Sub Userform_Terminate()
    AutoReportHandler.Doc_BackColor = pvRGB(1)
    With pvRGB(1)
        AutoReportHandler.BackColor_TB.BackColor = RGB(.X, .Y, .Z)
    End With
End Sub
Private Sub Bright_TB_KeyDown(ByVal KeyCode As MSForms.ReturnInteger, ByVal Shift As Integer)
    If KeyCode = vbKeyReturn Then ' 엔터 키 입력 시
        Dim inputStr As String
        Dim numericVal As Long
        Dim scaledVal As Long

        inputStr = Replace(Bright_TB.text, "%", "")
        If Trim(inputStr) = "" Then Exit Sub
        If Not IsNumeric(inputStr) Then Exit Sub

        numericVal = CDbl(inputStr)
        If numericVal < 0 Then numericVal = 0
        If numericVal > 100 Then numericVal = 100

        scaledVal = Int(numericVal / 100 * 255)
        If scaledVal < Bright_Slidebar.Min Then scaledVal = Bright_Slidebar.Min
        If scaledVal > Bright_Slidebar.Max Then scaledVal = Bright_Slidebar.Max

        Application.EnableEvents = False
        Bright_TB.text = Format(numericVal, "0.0") & "%"
        Application.EnableEvents = True

        Bright_Slidebar.Value = scaledVal
        Call Bright_Slidebar_Change

        KeyCode = 0 ' 삑 소리 방지
    End If
End Sub
Private Sub BCR_TB_KeyDown(ByVal KeyCode As MSForms.ReturnInteger, ByVal Shift As Integer)
    If KeyCode = vbKeyReturn Then ' 엔터 키 입력 시
        Dim inputStr As String
        Dim numericVal As Long
        Dim scaledVal As Long

        inputStr = Replace(BCR_TB.text, "%", "")
        If Trim(inputStr) = "" Then Exit Sub
        If Not IsNumeric(inputStr) Then Exit Sub

        numericVal = CDbl(inputStr)
        If numericVal < 0 Then numericVal = 0
        If numericVal > 100 Then numericVal = 100

        scaledVal = Int(numericVal / 100 * 255)
        If scaledVal < BCR_Slidebar.Min Then scaledVal = BCR_Slidebar.Min
        If scaledVal > BCR_Slidebar.Max Then scaledVal = BCR_Slidebar.Max

        Application.EnableEvents = False
        BCR_TB.text = Format(numericVal, "0.0") & "%"
        Application.EnableEvents = True

        BCR_Slidebar.Value = scaledVal
        Call BCR_Slidebar_Change

        KeyCode = 0 ' 삑 소리 방지
    End If
End Sub
Private Sub BCG_TB_KeyDown(ByVal KeyCode As MSForms.ReturnInteger, ByVal Shift As Integer)
    If KeyCode = vbKeyReturn Then ' 엔터 키 입력 시
        Dim inputStr As String
        Dim numericVal As Long
        Dim scaledVal As Long

        inputStr = Replace(BCG_TB.text, "%", "")
        If Trim(inputStr) = "" Then Exit Sub
        If Not IsNumeric(inputStr) Then Exit Sub

        numericVal = CDbl(inputStr)
        If numericVal < 0 Then numericVal = 0
        If numericVal > 100 Then numericVal = 100

        scaledVal = Int(numericVal / 100 * 255)
        If scaledVal < BCG_Slidebar.Min Then scaledVal = BCG_Slidebar.Min
        If scaledVal > BCG_Slidebar.Max Then scaledVal = BCG_Slidebar.Max

        Application.EnableEvents = False
        BCG_TB.text = Format(numericVal, "0.0") & "%"
        Application.EnableEvents = True

        BCG_Slidebar.Value = scaledVal
        Call BCG_Slidebar_Change

        KeyCode = 0 ' 삑 소리 방지
    End If
End Sub
Private Sub BCB_TB_KeyDown(ByVal KeyCode As MSForms.ReturnInteger, ByVal Shift As Integer)
    If KeyCode = vbKeyReturn Then ' 엔터 키 입력 시
        Dim inputStr As String
        Dim numericVal As Long
        Dim scaledVal As Long

        inputStr = Replace(BCB_TB.text, "%", "")
        If Trim(inputStr) = "" Then Exit Sub
        If Not IsNumeric(inputStr) Then Exit Sub

        numericVal = CDbl(inputStr)
        If numericVal < 0 Then numericVal = 0
        If numericVal > 100 Then numericVal = 100

        scaledVal = Int(numericVal / 100 * 255)
        If scaledVal < BCB_Slidebar.Min Then scaledVal = BCB_Slidebar.Min
        If scaledVal > BCB_Slidebar.Max Then scaledVal = BCB_Slidebar.Max

        Application.EnableEvents = False
        BCB_TB.text = Format(numericVal, "0.0") & "%"
        Application.EnableEvents = True

        BCB_Slidebar.Value = scaledVal
        Call BCB_Slidebar_Change

        KeyCode = 0 ' 삑 소리 방지
    End If
End Sub

Private Sub Bright_Slidebar_Change()
    Me.Bright_TB.text = Format((Bright_Slidebar.Value / 255 * 100), "0.0") & "%"
    Bright_Slidebar.SelLength = Bright_Slidebar.Value
    Brght = Bright_Slidebar.Value
    Bright_TB.BackColor = RGB(Brght, Brght, Brght)
    Brght = 255 + (Brght * -1)
    Bright_TB.ForeColor = RGB(Brght, Brght, Brght)
    Update_Colors
End Sub
Private Sub BCR_Slidebar_Change()
    pvRGB(1).X = BCR_Slidebar.Value
    BCR_TB.text = Format((pvRGB(1).X / 255 * 100), "0.0") & "%"
    BCR_TB.BackColor = RGB(pvRGB(1).X, 0, 0)
    BCR_Slidebar.SelLength = pvRGB(1).X
    If pvRGB(1).X < Color_Inversion_Criterion Then
        BCR_TB.ForeColor = RGB(255, 255, 255)
    Else
        BCR_TB.ForeColor = RGB(0, 0, 0)
    End If
    Update_Colors
End Sub
Private Sub BCG_Slidebar_Change()
    pvRGB(1).Y = BCG_Slidebar.Value
    BCG_TB.text = Format((pvRGB(1).Y / 255 * 100), "0.0") & "%"
    BCG_TB.BackColor = RGB(0, pvRGB(1).Y, 0)
    BCG_Slidebar.SelLength = pvRGB(1).Y
    If pvRGB(1).Y < Color_Inversion_Criterion Then
        BCG_TB.ForeColor = RGB(255, 255, 255)
    Else
        BCG_TB.ForeColor = RGB(0, 0, 0)
    End If
    Update_Colors
End Sub
Private Sub BCB_Slidebar_Change()
    pvRGB(1).Z = BCB_Slidebar.Value
    BCB_TB.text = Format((pvRGB(1).Z / 255 * 100), "0.0") & "%"
    BCB_TB.BackColor = RGB(0, 0, pvRGB(1).Z)
    BCB_Slidebar.SelLength = pvRGB(1).Z
    If pvRGB(1).Z < Color_Inversion_Criterion Then
        BCB_TB.ForeColor = RGB(255, 255, 255)
    Else
        BCB_TB.ForeColor = RGB(0, 0, 0)
    End If
    Update_Colors
End Sub
Private Sub Update_Colors()
    With pvRGB(1)
        Test_TB.BackColor = RGB(.X, .Y, .Z)
    End With
    Set pvRGB(2) = pvRGB(1).Copy
End Sub
