VERSION 5.00
Begin {C62A69F0-16DC-11CE-9E98-00AA00574A4F} AutoReportHandler 
   Caption         =   "Controller"
   ClientHeight    =   9450.001
   ClientLeft      =   45
   ClientTop       =   390
   ClientWidth     =   11535
   OleObjectBlob   =   "AutoReportHandler.frx":0000
   StartUpPosition =   1  '소유자 가운데
End
Attribute VB_Name = "AutoReportHandler"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
Private Type ChkBx
    Manual_Reporting(1 To 2) As Boolean
    Print_Now(1 To 3) As Boolean
    Kill_Original(1 To 3) As Boolean
End Type

Private Const Color_Inversion_Criterion = 204
Private ii As Long ' 반복문용
Private ws As Worksheet
Private vCB As ChkBx ' CheckBox 관리용
Private BOMLevelCheckBox(1 To 4) As Boolean
Private BOMdir As New Collection, BOM_Level As New Collection
Private Brght As Single 'Brightness
Private pvRGB(1 To 2) As New ObjPivotAxis

Private Sub BackColor_TB_Click()
    BCCUF.Show vbModeless
End Sub
Public Property Get Doc_BackColor() As ObjPivotAxis
    Set Doc_BackColor = pvRGB(1)
End Property
Public Property Let Doc_BackColor(Target As ObjPivotAxis)
    Set pvRGB(1) = Target
End Property

Private Sub CB_Yoil_DP_Click()
    BB_DailyPlan_Viewer.Yoil_DP = CB_Yoil_DP.value
End Sub

Private Sub CbBx_Feeder_Change()
    D_ListView_Feeder_item_Updater CbBx_Feeder.value
End Sub

Private Sub CbBx_Target_Printer_Change()
    Printer.DefaultPrinter = CbBx_Target_Printer.value
End Sub

Private Sub Userform_Initialize() '전처리
    
    If Not isDirSetUp Then SetUpDirectories
    Dim i As Long, wLine As Long, wDate As Long ' 반복문용 변수
    Set ws = ThisWorkbook.Worksheets("Setting"): Set UI = Me
    
    Me.Version_Label.Caption = "V." & ws.Cells.Find("Version", lookAt:=xlWhole, MatchCase:=True).Offset(0, 1).value
    
    If Not Printer.Bool_IPNS Then Printer.PrinterNameSet ' 프린터 세팅되어 있는지 확인 후 프린터 목록 초기화
    For i = 1 To Printer.PrinterName.Count
        With Me.CbBx_Target_Printer
            .Additem Printer.PrinterName(i)
            If (Printer.DefaultPrinter = Printer.PrinterName(i)) Then .value = Printer.PrinterName(i)
        End With
    Next i
    
    DP_BCBR_Slidebar.value = 218 ' Brightness 초기화
    wLine = 35: wDate = 65
    
    ' CheckBox initializing
    Me.CB_PNBOM.value = True
    Me.CB_KO_ALL.value = True
    Me.CB_MRDP.value = True
    Me.CB_MRPL.value = True
    CB_PNDP_Click
    Me.CB_PNDP.value = False
    CB_PNPL_Click
    Me.CB_PNPL.value = False
    Me.CB_Lvl1_BOM.value = True
    Me.CB_LvlS_BOM.value = True
    CB_PL_Ddays_Click

    With Me.ListView_BOM
        .Left = 168: .Top = 12: .Width = 378: .Height = 192
        .ColumnHeaders.Add text:="Model Number", Width:=120
        .ColumnHeaders.Add text:="Directory", Width:=165
        .ColumnHeaders.Add text:="Print", Width:=45
        .ColumnHeaders.Add text:="PDF", Width:=45
    End With
    
    With Me.ListView_DailyPlan
        .Left = 168: .Top = 12: .Width = 378: .Height = 192
        .ColumnHeaders.Add text:="Date", Width:=wDate
        .ColumnHeaders.Add text:="Line", Width:=wLine
        .ColumnHeaders.Add text:="Directory", Width:=183
        .ColumnHeaders.Add text:="Print", Width:=45
        .ColumnHeaders.Add text:="PDF", Width:=45
        .ColumnHeaders.Add text:="acc", Width:=1
    End With
    
    With Me.ListView_PartList_items
        .Left = 168: .Top = 12: .Width = 378: .Height = 78
        .ColumnHeaders.Add text:="Date", Width:=wDate
        .ColumnHeaders.Add text:="Line", Width:=wLine
        .ColumnHeaders.Add text:="Directory", Width:=183
        .ColumnHeaders.Add text:="Print", Width:=45
        .ColumnHeaders.Add text:="PDF", Width:=45
    End With
    
    With Me.ListView_PLfF_item
        .ColumnHeaders.Add text:="PartList items", Width:=.Width - 3, Alignment:=lvwitemleft
    End With
    
    With Me.ListView_Feeder_item
        .ColumnHeaders.Add text:="Feeder's items", Width:=.Width - 3, Alignment:=lvwitemleft
    End With
    
    With Me.ListView_Feeders
        .ColumnHeaders.Add text:="Feeder's Name", Width:=.Width / 2, Alignment:=lvwitemleft
        .ColumnHeaders.Add text:="item Count", Width:=.Width / 2 - 3
    End With
    
    With Me.ListView_MD_Own
        .Left = 168: .Top = 12: .Width = 180: .Height = 186
'        .ColumnHeaders.Add text:="Date", Width:=wDate
'        .ColumnHeaders.Add text:="Line", Width:=wLine
'        .ColumnHeaders.Add text:="DailyPlan", Width:=50
'        .ColumnHeaders.Add text:="PartList", Width:=50
    End With
        
    With Me.DP_PN_Counter: .Min = 1: .Max = 999: .value = 1: Me.DP_PN_Copies_TB.text = .value: End With
    With Me.PL_PN_Counter: .Min = 1: .Max = 999: .value = 1: Me.DP_PN_Copies_TB.text = .value: End With
    With Me.PL_Ddays_Counter: .Min = 1: .Max = 999: .value = 2: Me.PL_Ddays_TB.text = .value: End With
    
    With Me.MultiPage_FeederChecker
        For i = 0 To .Pages.Count - 1
            .value = i
            DoEvents
        Next i
        .value = 1
    End With
    
    With Me.MultiPage1
        For i = 0 To .Pages.Count - 1
            .value = i
            DoEvents
        Next i
        .value = 1
    End With
    
    SetUp_FeederTrackers ' Feeder.bas 연결
        
End Sub
Private Sub Userform_Terminate() '후처리
    Set BOMdir = Nothing: Set BOM_Level = Nothing: Set UI = Nothing
End Sub
Private Sub DP_BCBR_TB_KeyDown(ByVal KeyCode As MSForms.ReturnInteger, ByVal Shift As Integer)
    If KeyCode = vbKeyReturn Then ' 엔터 키 입력 시
        Dim inputStr As String
        Dim numericVal As Long
        Dim scaledVal As Long

        inputStr = Replace(DP_BCBR_TB.text, "%", "")
        If Trim(inputStr) = "" Then Exit Sub
        If Not isNumeric(inputStr) Then Exit Sub

        numericVal = CDbl(inputStr)
        If numericVal < 0 Then numericVal = 0
        If numericVal > 100 Then numericVal = 100

        scaledVal = Int(numericVal / 100 * 255)
        If scaledVal < DP_BCBR_Slidebar.Min Then scaledVal = DP_BCBR_Slidebar.Min
        If scaledVal > DP_BCBR_Slidebar.Max Then scaledVal = DP_BCBR_Slidebar.Max

        Application.EnableEvents = False
        DP_BCBR_TB.text = Format(numericVal, "0.0") & "%"
        Application.EnableEvents = True

        DP_BCBR_Slidebar.value = scaledVal
        Call DP_BCBR_Slidebar_Change

        KeyCode = 0 ' 삑 소리 방지
    End If
End Sub

Private Sub DP_BCBR_Slidebar_Change()
    Me.DP_BCBR_TB.text = Format((DP_BCBR_Slidebar.value / 255 * 100), "0.0") & "%"
    DP_BCBR_Slidebar.SelLength = DP_BCBR_Slidebar.value
    Brght = DP_BCBR_Slidebar.value
    DP_BCBR_TB.BackColor = RGB(Brght, Brght, Brght)
    Brght = 255 + (Brght * -1)
    DP_BCBR_TB.ForeColor = RGB(Brght, Brght, Brght)
End Sub

Public Property Get Brightness() As Single
    Brightness = DP_BCBR_Slidebar.value
End Property
Private Sub CB_MR_All_Click()
    If CB_MR_All.value Then
        vCB.Manual_Reporting(1) = CB_MRDP.value
        vCB.Manual_Reporting(2) = CB_MRPL.value
        CB_MRDP.value = CB_MR_All.value
        CB_MRPL.value = CB_MR_All.value
        CB_MRDP.Enabled = Not CB_MR_All.value
        CB_MRPL.Enabled = Not CB_MR_All.value
    Else
        CB_MRDP.value = vCB.Manual_Reporting(1)
        CB_MRPL.value = vCB.Manual_Reporting(2)
        CB_MRDP.Enabled = Not CB_MR_All.value
        CB_MRPL.Enabled = Not CB_MR_All.value
    End If
End Sub
Private Sub CB_KO_ALL_Click()
    If CB_KO_ALL.value Then
        vCB.Kill_Original(1) = CB_KO_BOM.value
        vCB.Kill_Original(2) = CB_KO_DP.value
        vCB.Kill_Original(3) = CB_KO_PL.value
        CB_KO_BOM.value = CB_KO_ALL.value
        CB_KO_DP.value = CB_KO_ALL.value
        CB_KO_PL.value = CB_KO_ALL.value
        CB_KO_BOM.Enabled = Not CB_KO_ALL.value
        CB_KO_DP.Enabled = Not CB_KO_ALL.value
        CB_KO_PL.Enabled = Not CB_KO_ALL.value
    Else
        CB_KO_BOM.value = vCB.Kill_Original(1)
        CB_KO_DP.value = vCB.Kill_Original(2)
        CB_KO_PL.value = vCB.Kill_Original(3)
        CB_KO_BOM.Enabled = Not CB_KO_ALL.value
        CB_KO_DP.Enabled = Not CB_KO_ALL.value
        CB_KO_PL.Enabled = Not CB_KO_ALL.value
    End If
End Sub

Private Sub CB_PNALL_Click()
    If CB_PNALL.value Then
'전처리 PrintNow 값 살려두기
        vCB.Print_Now(1) = CB_PNBOM.value
        vCB.Print_Now(2) = CB_PNDP.value
        vCB.Print_Now(3) = CB_PNPL.value
' 전체 True 설정
        CB_PNBOM.value = CB_PNALL.value
        CB_PNDP.value = CB_PNALL.value
        CB_PNPL.value = CB_PNALL.value
' 잠금설정
        CB_PNBOM.Enabled = Not CB_PNALL.value
        CB_PNDP.Enabled = Not CB_PNALL.value
        CB_PNPL.Enabled = Not CB_PNALL.value
    Else
'PrintNow값 반환
        CB_PNBOM.value = vCB.Print_Now(1)
        CB_PNDP.value = vCB.Print_Now(2)
        CB_PNPL.value = vCB.Print_Now(3)
' 잠금설정
        CB_PNBOM.Enabled = Not CB_PNALL.value
        CB_PNDP.Enabled = Not CB_PNALL.value
        CB_PNPL.Enabled = Not CB_PNALL.value
    End If
End Sub

Private Sub CB_PNBOM_Click()
    PrintNow.BOM = CB_PNBOM.value
End Sub

Private Sub CB_PNDP_Click()
    PrintNow.DailyPlan = CB_PNDP.value
    
    Me.DP_PN_Copies_TB.Enabled = Me.CB_PNDP.value
    Me.DP_PN_Counter.Enabled = Me.CB_PNDP.value
End Sub
Private Sub CB_PNPL_Click()
    PrintNow.PartList = CB_PNPL.value
    
    Me.PL_PN_Copies_TB.Enabled = Me.CB_PNPL.value
    Me.PL_PN_Counter.Enabled = Me.CB_PNPL.value
End Sub
Private Sub CB_MRDP_Click()
    BB_DailyPlan_Viewer.MRB_DP = CB_MRDP.value
End Sub
Private Sub CB_MRPL_Click()
    BC_PartListItem_Viewer.MRB_PL = CB_MRPL.value
End Sub
Private Sub CB_PL_Ddays_Click()
    Me.PL_Ddays_Counter.Enabled = Me.CB_PL_Ddays.value
    Me.PL_Ddays_TB.Enabled = Me.CB_PL_Ddays.value
End Sub

Private Sub CB_KO_BOM_Click()
    OriginalKiller.BOM = CB_KO_BOM.value
End Sub

Private Sub CB_KO_DP_Click()
    OriginalKiller.DailyPlan = CB_KO_DP.value
End Sub
Private Sub CB_KO_PL_Click()
    OriginalKiller.PartList = CB_KO_PL.value
End Sub
Private Sub CB_Lvl1_BOM_Click()
    If CB_Lvl1_BOM.value Then
        BOM_Level.Add "0"
        BOM_Level.Add ".1"
    Else
        For ii = BOM_Level.Count To 1 Step -1
            If BOM_Level(ii) = "0" Or BOM_Level(ii) = ".1" Then BOM_Level.Remove ii
        Next
    End If
End Sub
Private Sub CB_Lvl2_BOM_Click()
    If CB_Lvl2_BOM.value Then
        BOM_Level.Add "..2"
    Else
        For ii = BOM_Level.Count To 1 Step -1
            If BOM_Level(ii) = "..2" Then BOM_Level.Remove ii
        Next
    End If
End Sub
Private Sub CB_Lvl3_BOM_Click()
    If CB_Lvl3_BOM.value Then
        BOM_Level.Add "...3"
    Else
        For ii = BOM_Level.Count To 1 Step -1
            If BOM_Level(ii) = "...3" Then BOM_Level.Remove ii
        Next
    End If
End Sub
Private Sub CB_LvlS_BOM_Click()
    If CB_LvlS_BOM.value Then
        BOM_Level.Add "*S*"
    Else
        For ii = BOM_Level.Count To 1 Step -1
            If BOM_Level(ii) = "*S*" Then BOM_Level.Remove ii
        Next
    End If
End Sub
Private Sub CB_LvlAll_BOM_Click()
    If CB_LvlAll_BOM.value Then
'전처리 PrintNow 값 살려두기
        BOMLevelCheckBox(1) = CB_Lvl1_BOM.value
        BOMLevelCheckBox(2) = CB_Lvl2_BOM.value
        BOMLevelCheckBox(3) = CB_Lvl3_BOM.value
        BOMLevelCheckBox(4) = CB_LvlS_BOM.value
' 전체 True 설정
        CB_Lvl1_BOM.value = CB_LvlAll_BOM.value
        CB_Lvl2_BOM.value = CB_LvlAll_BOM.value
        CB_Lvl3_BOM.value = CB_LvlAll_BOM.value
        CB_LvlS_BOM.value = CB_LvlAll_BOM.value
' 잠금설정
        CB_Lvl1_BOM.Enabled = Not CB_LvlAll_BOM.value
        CB_Lvl2_BOM.Enabled = Not CB_LvlAll_BOM.value
        CB_Lvl3_BOM.Enabled = Not CB_LvlAll_BOM.value
        CB_LvlS_BOM.Enabled = Not CB_LvlAll_BOM.value
' BOM_Level 컬렉션 초기화
        For ii = BOM_Level.Count To 1 Step -1
            BOM_Level.Remove ii
        Next ii
' BOM_Level 컬렉션 설정
        BOM_Level.Add "*Q*"
        BOM_Level.Add "*S*"
        BOM_Level.Add "0"
        BOM_Level.Add ".1"
        BOM_Level.Add "..2"
        BOM_Level.Add "...3"
        BOM_Level.Add "....4"
        BOM_Level.Add ".....5"
        BOM_Level.Add "......6"
    Else
'PrintNow값 반환
        CB_Lvl1_BOM.value = BOMLevelCheckBox(1)
        CB_Lvl2_BOM.value = BOMLevelCheckBox(2)
        CB_Lvl3_BOM.value = BOMLevelCheckBox(3)
        CB_LvlS_BOM.value = BOMLevelCheckBox(4)
' 잠금설정
        CB_Lvl1_BOM.Enabled = Not CB_LvlAll_BOM.value
        CB_Lvl2_BOM.Enabled = Not CB_LvlAll_BOM.value
        CB_Lvl3_BOM.Enabled = Not CB_LvlAll_BOM.value
        CB_LvlS_BOM.Enabled = Not CB_LvlAll_BOM.value
' BOM_Level 컬렉션 초기화
        For ii = BOM_Level.Count To 1 Step -1
            BOM_Level.Remove ii
        Next ii
' BOM_Level 컬렉션 재설정
        If CB_Lvl1_BOM.value Then
            BOM_Level.Add ".1"
            BOM_Level.Add "0"
        End If
        If CB_Lvl2_BOM.value Then BOM_Level.Add "..2"
        If CB_Lvl3_BOM.value Then BOM_Level.Add "...3"
        If CB_LvlS_BOM.value Then BOM_Level.Add "*S*"
    End If
End Sub
Private Sub AB_ReadBOM_Click()
    BA_BOM_Viewer.Read_BOM True
End Sub
Private Sub AB_PrintBOM_Click()
    BA_BOM_Viewer.Print_BOM True
End Sub

Private Sub AB_ReadDP_Click()
    BB_DailyPlan_Viewer.Read_DailyPlan True
End Sub
Private Sub AB_PrintDP_Click()
    BB_DailyPlan_Viewer.Print_DailyPlan True
End Sub

Private Sub AB_ReadPL_Click()
    BC_PartListItem_Viewer.Read_PartList True
End Sub
Private Sub AB_PrintPL_Click()
    BC_PartListItem_Viewer.Print_PartList True
End Sub

Private Sub AB_ReadDocs_Click()
    BD_MultiDocuments.Read_Documents True
End Sub

Private Sub AB_DeleteBOM_Click()
    If Not Delete_Each_Documents_For_Key(Me.ListView_BOM) Then BA_BOM_Viewer.Read_BOM False
End Sub
Private Sub AB_DeleteDP_Click()
    If Not Delete_Each_Documents_For_Key(Me.ListView_DailyPlan) Then BB_DailyPlan_Viewer.Read_DailyPlan False
End Sub
Private Sub AB_DeletePL_Click()
    If Not Delete_Each_Documents_For_Key(Me.ListView_PartList_items) Then BC_PartListItem_Viewer.Read_PartList False
End Sub

Private Sub AB_Cleaner_Click()
    Cleaner_Handler.Show vbModeless
End Sub
Private Sub AB_DeleteFeeder_Click()
    BCA_PLIV_Feeder.A_Delete_Feeder
End Sub
Private Sub AB_NewFeeder_Click()
    BCA_PLIV_Feeder.A_New_Feeder
End Sub
Private Sub AB_SaveFeeder_Click()
    BCA_PLIV_Feeder.A_Save_Feeder
End Sub
Private Sub AB_itemAdder_Click()
    BCA_PLIV_Feeder.C_Additem_List
End Sub

Private Sub AB_itemKiller_Click()
    BCA_PLIV_Feeder.C_Removeitem_List
End Sub
Private Function Delete_Each_Documents_For_Key(ByRef TargetListView As ListView) As Boolean
    Dim Target_item As ListItems: Set Target_item = TargetListView.ListItems
    Dim DeleteList As Long
    Dim i As Long, Ai As Long, Dir As Long
    
    For i = 1 To TargetListView.ColumnHeaders.Count
        If TargetListView.ColumnHeaders.Item(i) = "Directory" Then Dir = i - 1: Exit For
    Next i
    
    For i = 1 To Target_item.Count
        If Target_item(i).Checked = True Then DeleteList = DeleteList + 1
    Next i
    
    If DeleteList < 1 Then MsgBox "삭제할 항목 없음": Exit Function
    
    On Error Resume Next
    
    For i = 1 To Target_item.Count
        If Target_item(i).Checked = True Then
            Kill Target_item(i).SubItems(Dir)
            For Ai = 0 To 2
                Target_item(i).SubItems(Dir + Ai) = "Deleted"
            Next Ai
            Target_item(i).Checked = False
        End If
    Next i
    
    On Error GoTo 0
    Delete_Each_Documents_For_Key = True
End Function

Private Sub AB_Export2Zip_Click()
    Dim Temp As String
    Temp = ThisWorkbook.Path & "\ExcelExportedCodes"
    Cleaner.FolderKiller Temp
    Call AA_Updater.ExportAllVbaComponents
    Call AA_Updater.ExportAllModulesDirectlyToTextAndMarkdown
End Sub
Private Sub DP_PN_Copies_TB_KeyPress(ByVal KeyAscii As MSForms.ReturnInteger)
    ' 0~9와 백스페이스(8)만 허용
    If Not (KeyAscii >= 48 And KeyAscii <= 57 Or KeyAscii = 8) Then
        KeyAscii = 0
    End If
End Sub
Private Sub DP_PN_Copies_TB_Change()
    If Me.DP_PN_Copies_TB.text = "" Or Val(Me.DP_PN_Copies_TB.text) = 0 Then
        Me.DP_PN_Copies_TB.text = 1
        Me.DP_PN_Counter.value = 1
    Else
        Me.DP_PN_Counter.value = Val(Me.DP_PN_Copies_TB.text)
    End If
End Sub
Private Sub DP_PN_Counter_Change()
    Me.DP_PN_Copies_TB.text = Me.DP_PN_Counter.value
End Sub

Private Sub PL_PN_Copies_TB_KeyPress(ByVal KeyAscii As MSForms.ReturnInteger)
    ' 0~9와 백스페이스(8)만 허용
    If Not (KeyAscii >= 48 And KeyAscii <= 57 Or KeyAscii = 8) Then
        KeyAscii = 0
    End If
End Sub
Private Sub PL_PN_Copies_TB_Change()
    If Me.PL_PN_Copies_TB.text = "" Or Val(Me.PL_PN_Copies_TB.text) = 0 Then
        Me.PL_PN_Copies_TB.text = 1
        Me.PL_PN_Counter.value = 1
    Else
        Me.PL_PN_Counter.value = Val(Me.PL_PN_Copies_TB.text)
    End If
End Sub
Private Sub PL_PN_Counter_Change()
    Me.PL_PN_Copies_TB.text = Me.PL_PN_Counter.value
End Sub

Private Sub PL_Ddays_TB_KeyPress(ByVal KeyAscii As MSForms.ReturnInteger)
    ' 0~9와 백스페이스(8)만 허용
    If Not (KeyAscii >= 48 And KeyAscii <= 57 Or KeyAscii = 8) Then
        KeyAscii = 0
    End If
End Sub
Private Sub PL_Ddays_TB_Change()
    If Me.PL_Ddays_TB.text = "" Or Val(Me.PL_Ddays_TB.text) = 0 Then
        Me.PL_Ddays_TB.text = 1
        Me.PL_Ddays_Counter.value = 1
    ElseIf Me.PL_Ddays_TB.text > Me.PL_Ddays_Counter.Max Then
        Me.PL_Ddays_TB.text = Me.PL_Ddays_Counter.Max
    Else
        Me.PL_Ddays_Counter.value = Val(Me.PL_Ddays_TB.text)
    End If
End Sub
Private Sub PL_Ddays_Counter_Change()
    Me.PL_Ddays_TB.text = Me.PL_Ddays_Counter.value
End Sub
Public Sub UpdateProgressBar(ProgressBar As MSComctlLib.ProgressBar, _
                                        ByRef Index As Single, _
                                        Optional vMin As Single = 0, _
                                        Optional vMax As Single = 100)
    With ProgressBar
        .Min = vMin
        .Max = vMax
        .value = Index
    End With
End Sub

Public Property Get itemLevel() As Collection
    Set itemLevel = BOM_Level
End Property

Private Sub MultiPage_FeederChecker_Change()
    If MultiPage_FeederChecker.value = 0 Then D_ListView_Feeder_Updater
End Sub
