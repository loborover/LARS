Attribute VB_Name = "CA_itemCounter"
Option Explicit

'==== 모듈 전역 변수 (기존 유지) ===============================================
Private xlApp As Excel.Application, xlAppSub As Excel.Application
Private cNickname As Long, cVender As Long, cPartNumber As Long, cSC As Long, cDD As Long
Private cLC As Long, cLS As Long, cLE As Long
Private cDC As Long, cDS As Long, cDE As Long
Private cTC As Long, cTS As Long, cTE As Long
Private cFC As Long, cFS As Long, cFE As Long
Private rStart As Long, rEnd As Long, rTitle As Long
Private tWB As Workbook, tWS As Worksheet, rWB As Workbook, rWS As Worksheet
Public Sub Test_CAIC()
    Dim Temp As New Collection, i As Long
    Set Temp = ReclassifingVNQ("[ABCD] 1234/5678(2)/9012(4)/3456(3) [EFGH] 9876(3)/5431", "Controller")
    For i = 1 To Temp.Count
        Dim iUTemp As New itemUnit
        Set iUTemp = Temp(i)
        Debug.Print "ID_Hash : " & iUTemp.ID_Hash
        Debug.Print "QTY : " & iUTemp.QTY
    Next i
End Sub
Public Sub testA()
    PL2IC "D:\Downloads\공급문서\AutoReport\PartList\PartList 10월-31일_C11.xlsx"
    
    Debug.Print "Target WorkBook : " & tWB.Name
    Debug.Print "Target WorkSheet : " & tWS.Name
    Debug.Print "Reference WorkBook : " & rWB.Name
    Debug.Print "Reference WorkSheet : " & rWS.Name
    'PL2DP
End Sub
Public Sub testPLiReader()
    Get_Reference "D:\Downloads\공급문서\AutoReport\PartList\PartList 11월-14일_C11.xlsx"
    Dim Temp As New Collection, Target As New itemUnit, i As Long
    Set Temp = PLitemReader(8, 10, 2, 52)
    For i = 1 To Temp.Count
        Set Target = Temp(i)
        Debug.Print Target.ID_Hash & " : " & Target.QTY & " = " & Target.CountPerDay(CDate("2025-11-14"))
    Next i
End Sub
Public Sub ReActive()
    Dim asdf As Workbook
    Set asdf = GetObject(ThisWorkbook.FullName)
    asdf.Application.Visible = True
End Sub

'==== 퍼블릭 API ================================================================
Public Sub PL2DP(ByVal DailyPlan_Directory As String, ByVal PartList_Directory As String)
    Set_Target DailyPlan_Directory
    Get_Reference PartList_Directory
    ' TODO: 구현 예정
End Sub

Public Sub PL2IC(ByVal PartList_Directory As String)
    Set_Target ThisWorkbook.FullName, "itemCounter"
    Get_Reference PartList_Directory
    
    Dim cNickname As Long, cVender As Long, cPartNumber As Long, cSC As Long, cDD(0 To 4) As Long
    Dim cLC As Long, cLS As Long, cLE As Long ' Columns Line Cart, Line Set, Line Each
    Dim cDC As Long, cDS As Long, cDE As Long ' Columns Depot Cart, Depot Set, Depot Each
    Dim cTC As Long, cTS As Long, cTE As Long ' Columns Total Cart, Total Set, Total Each
    Dim cFC As Long, cFS As Long, cFE As Long ' Columns Fire Cart, Fire Set, Fire Each
    Dim rStart(0 To 1) As Long, rEnd(0 To 1) As Long, rTitle(0 To 1) As Long ' Rows Start, End, Title // 0:Reference, 1:Target
    Dim cStart(0 To 1) As Long, cEnd(0 To 1) As Long ' Columns Start, End // 0:Reference, 1:Target
    Dim rR As Range, tR As Range, i As Long
    Dim tiu As New itemUnit, Each_items As New Collection
    
    Set rR = rWS.Rows(1).Find("-Line", lookAt:=xlPart, LookIn:=xlValues)
    Set tR = tWS.Cells.Find("Setting", lookAt:=xlWhole, LookIn:=xlValues)
    ' Columns Number
    cNickname = tR.Column
    cVender = tR.Column + 1
    cPartNumber = tR.Column + 2
    cSC = tR.Column + 3
    For i = 0 To 4
        cDD(i) = cSC + i + 1
    Next i
    cLC = cDD(4) + 1
    cLS = cDD(4) + 2
    cLE = cDD(4) + 3
    cDC = cDD(4) + 4
    cDS = cDD(4) + 5
    cDE = cDD(4) + 6
    cTC = cDD(4) + 7
    cTS = cDD(4) + 8
    cTE = cDD(4) + 9
    cFC = cDD(4) + 10
    cFS = cDD(4) + 11
    cFE = cDD(4) + 12
    ' Rows Number
    rStart(1) = tR.Row + 3
    rEnd(1) = tWS.Cells(tWS.Rows.Count, cNickname).End(xlUp).Row
    cStart(1) = tR.Column
    cEnd(1) = tWS.Cells(rStart(1), tWS.Columns.Count).End(xlToLeft).Column
    rTitle(0) = rR.Row
    rStart(0) = rTitle(0) + 1
    rEnd(0) = rWS.Cells(rWS.Rows.Count, 1).End(xlUp).Row
    cStart(0) = rR.Column + 2
    cEnd(0) = rWS.Cells(1, rWS.Columns.Count).End(xlToLeft).Column
    
    Set Each_items = PLitemReader(cStart(0), cEnd(0), rStart(0), rEnd(0)) ' 읽기
    ' 쓰기
    TempKiller Each_items ' 날리기
    
End Sub
Private Function PLitemReader(cS As Long, cE As Long, rS As Long, rE As Long) As Collection
    Dim Result As New Collection, Temp As Collection
    Dim Tpitem As itemUnit, Rpitem As itemUnit
    Dim i As Long, n As Long, r As Long, c As Long
    Dim CountCol As Long, DayCol As Long, RowCount As Long
    Dim RawValue As String, PartsNickName As String
    Dim dKey As Date
    Dim Duplicated As Boolean

    ' 날짜/수량 열 찾기
    DayCol = rWS.Rows(1).Find("투입" & vbLf & "시점", lookAt:=xlPart, LookIn:=xlValues).Column
    CountCol = rWS.Rows(1).Find("수량", lookAt:=xlPart, LookIn:=xlValues).Column

    For c = cS To cE          ' 열 순회
        PartsNickName = CStr(rWS.Cells(1, c).value) ' 파트 닉네임

        For r = rS To rE      ' 행 순회
            RawValue = Trim$(CStr(rWS.Cells(r, c).value))
            If Len(RawValue) = 0 Then GoTo ContinueRow   ' 빈 셀은 스킵

            ' 날짜/수량 읽기
            If IsDate(rWS.Cells(r, DayCol).value) Then
                dKey = CDate(rWS.Cells(r, DayCol).value)
            Else
                GoTo ContinueRow   ' 날짜 없으면 스킵(원하시면 에러 처리로 변경 가능)
            End If

            If isNumeric(rWS.Cells(r, CountCol).value) Then
                RowCount = CLng(rWS.Cells(r, CountCol).value)
            Else
                RowCount = 0
            End If
            If RowCount = 0 Then GoTo ContinueRow

            ' 셀값 → itemUnit 컬렉션
            Set Temp = ReclassifingVNQ(RawValue, PartsNickName)

            For i = 1 To Temp.Count
                Set Tpitem = Temp(i)

                '-----------------------------
                ' 1) Result에서 같은 ID 찾기
                '-----------------------------
                Set Rpitem = Nothing
                Duplicated = False

                For n = 1 To Result.Count
                    Set Rpitem = Result(n)
                    If Rpitem.ID_Hash = Tpitem.ID_Hash Then
                        Duplicated = True
                        Exit For
                    End If
                Next n

                '-----------------------------
                ' 2) 없으면 새로 추가
                '-----------------------------
                If Not Duplicated Then
                    ' 그대로 참조해도 되고, 안전하게 복사본을 쓰고 싶으면 Tpitem.Copy
                    Set Rpitem = Tpitem.Copy
                    Result.Add Rpitem
                End If

                '-----------------------------
                ' 3) 월일 기준 카운트 누적
                '   CountPerDay(dKey)는 내부에서 월/일만 보고 저장
                '   RowCount(행의 수량) * Tpitem.QTY(파트당 필요 수량)
                '-----------------------------
                Rpitem.CountPerDay(dKey) = Rpitem.CountPerDay(dKey) + (RowCount * Tpitem.QTY)
            Next i

ContinueRow:
        Next r
    Next c

    Set PLitemReader = Result
End Function
Private Function ReclassifingVNQ(ByVal Sample As String, Optional ByVal NickName As String = "Unknown") As Collection ' Reclassifing Vender, partNumber, QTY by Cell Value
    Dim Result As New Collection, Target As itemUnit
    Dim sVender As String, sPartNumber As String, sQTY As String
    Dim Venders As Variant, PartNumbers As Variant
    Dim i As Long, p As Long
    
    Sample = Trim(CStr(Sample))
    Sample = Replace(Sample, " [", "$[") ' 공백 없애기
    Venders = Split(Sample, "$") ' Vender별로 분류
    For i = LBound(Venders) To UBound(Venders)
        sVender = ExtractBracketValue(Venders(i))
        PartNumbers = Split(Trim(Replace(Venders(i), "[" & sVender & "]", "")), "/") ' 1개의 Vender 내의 부품넘버별로 분류
        For p = LBound(PartNumbers) To UBound(PartNumbers)
            sPartNumber = InStr(PartNumbers(p), "(")
            If CLng(sPartNumber) = 0 Then
                sPartNumber = PartNumbers(p)
                sQTY = 1
            Else
                sPartNumber = Left$(PartNumbers(p), CLng(sPartNumber) - 1)
                sQTY = ExtractSmallBracketValue(PartNumbers(p))
            End If
            Set Target = New itemUnit
            Target.NickName = NickName
            Target.Vender = sVender
            Target.PartNumber = sPartNumber
            Target.QTY = CLng(sQTY)
            Result.Add Target
        Next p
    Erase PartNumbers
    Next i
    Erase Venders
    
    Set ReclassifingVNQ = Result
End Function
'==== 타겟(자기 자신) 바인드 ====================================================
Private Sub Set_Target(ByVal TargetDir As String, Optional ByVal Target_Worksheet_index As Variant = 1)
    If LenB(TargetDir) = 0 Then Exit Sub

    If Not BindWorkbook( _
            TargetDir:=TargetDir, _
            WantVisible:=False, _
            AppOut:=xlApp, _
            WbOut:=tWB) Then
        Debug.Print "Failed to bind target workbook: " & TargetDir
        Exit Sub
    End If

    If Not BindWorksheet(Wb:=tWB, WSRef:=Target_Worksheet_index, WsOut:=tWS) Then
        Debug.Print "Failed to bind target worksheet: " & CStr(Target_Worksheet_index)
        Exit Sub
    End If
End Sub

'==== 레퍼런스(상대 파일) 바인드 ================================================
Private Sub Get_Reference(ByVal TargetDir As String, Optional ByVal Target_Worksheet_index As Variant = 1)
    If LenB(TargetDir) = 0 Then Exit Sub

    If Not BindWorkbook( _
            TargetDir:=TargetDir, _
            WantVisible:=False, _
            AppOut:=xlApp, _
            WbOut:=rWB) Then
        Debug.Print "Failed to bind reference workbook: " & TargetDir
        Exit Sub
    End If

    If Not BindWorksheet(Wb:=rWB, WSRef:=Target_Worksheet_index, WsOut:=rWS) Then
        Debug.Print "Failed to bind reference worksheet: " & CStr(Target_Worksheet_index)
        Exit Sub
    End If
End Sub

'==== 유틸 (아이템 편집/표시 등은 추후 구현) ====================================
Private Sub itemAdder()
End Sub

Private Sub itemKiller()
End Sub

Private Sub itemEditor()
End Sub

Private Sub DayFollower()
End Sub

Private Sub RowSaver()
End Sub

Private Sub ViewerRefresh()
    
End Sub

'===============================================================================
'= 공용 헬퍼: Workbook/Worksheet 바인딩 (중복 제거의 핵심)
'===============================================================================
' [동작 순서]
' 1) GetObject(TargetDir) 시도: 이미 열려 있으면 해당 인스턴스의 Workbook 반환, 열려 있지 않으면 열기
' 2) 실패 시: 실행 중 Excel 인스턴스에 붙어서 FullName/Name 비교로 찾기
' 3) 그래도 실패 시: 새 인스턴스를 띄워서 Open (실패하면 Quit)
'
' 반환값: 성공 True / 실패 False
Private Function BindWorkbook( _
    ByVal TargetDir As String, _
    ByVal WantVisible As Boolean, _
    ByRef AppOut As Excel.Application, _
    ByRef WbOut As Workbook) As Boolean

    Dim fileName As String
    fileName = Dir$(TargetDir)
    If Len(fileName) = 0 Then
        Debug.Print "File not found: " & TargetDir
        BindWorkbook = False
        Exit Function
    End If

    On Error GoTo EH_GetObject
    ' 1) 최우선: 열려 있든 아니든 GetObject로 직접 바인딩(또는 개방)
    Set WbOut = GetObject(TargetDir)              ' Workbook
    Set AppOut = WbOut.Application                ' Excel.Application
    If StrComp(WbOut.FullName, ThisWorkbook.FullName, vbTextCompare) <> 0 Then AppOut.Visible = WantVisible
    BindWorkbook = True
    Exit Function

EH_GetObject:
    ' 2) 실행 중 Excel 인스턴스에서 검색
    Dim runningApp As Excel.Application
    Dim Wb As Workbook

    On Error Resume Next
    Set runningApp = GetObject(, "Excel.Application")
    On Error GoTo 0

    If Not runningApp Is Nothing Then
        For Each Wb In runningApp.Workbooks
            If StrComp(Wb.FullName, TargetDir, vbTextCompare) = 0 _
               Or StrComp(Wb.Name, fileName, vbTextCompare) = 0 Then
                Set WbOut = Wb
                Set AppOut = runningApp
                If StrComp(WbOut.FullName, ThisWorkbook.FullName, vbTextCompare) <> 0 Then AppOut.Visible = WantVisible
                BindWorkbook = True
                Exit Function
            End If
        Next
    End If

    ' 3) 새 인스턴스 생성 후 열기
    Dim newApp As Excel.Application
    Set newApp = New Excel.Application
    newApp.Visible = WantVisible

    On Error GoTo EH_OpenNew
    Set WbOut = newApp.Workbooks.Open(fileName:=TargetDir, ReadOnly:=True, Notify:=False)
    Set AppOut = newApp
    BindWorkbook = True
    Exit Function

EH_OpenNew:
    ' 열기 실패하면 새 인스턴스 정리
    On Error Resume Next
    newApp.Quit
    Set newApp = Nothing
    On Error GoTo 0

    Debug.Print "Failed to open workbook: " & TargetDir
    BindWorkbook = False
End Function

' Worksheet 인덱스(숫자) / 이름(문자열) 모두 지원
Private Function BindWorksheet( _
    ByVal Wb As Workbook, _
    ByVal WSRef As Variant, _
    ByRef WsOut As Worksheet) As Boolean

    On Error GoTo EH
    If isNumeric(WSRef) Then
        Set WsOut = Wb.Worksheets(CLng(WSRef))
    Else
        Set WsOut = Wb.Worksheets(CStr(WSRef))
    End If

    BindWorksheet = True
    Exit Function

EH:
    BindWorksheet = False
End Function

Private Sub TempKiller(Optional ByRef Temp As Variant)
    If Not tWB Is Nothing Then
        If StrComp(tWB.FullName, ThisWorkbook.FullName, vbTextCompare) <> 0 Then tWB.Close False
        Set tWS = Nothing
        Set tWB = Nothing
    End If
    
    If Not rWB Is Nothing Then
        If StrComp(rWB.FullName, ThisWorkbook.FullName, vbTextCompare) <> 0 Then rWB.Close False
        Set rWS = Nothing
        Set rWB = Nothing
    End If
    
    Set Temp = Nothing
End Sub

