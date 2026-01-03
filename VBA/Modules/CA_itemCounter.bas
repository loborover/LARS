Attribute VB_Name = "CA_itemCounter"
'==================================================================
' CA_itemCounter.bas
'==================================================================
Option Explicit
#Const Devmod = True

'==============================================================
' 모듈 전역(Workbook/Worksheet 바인딩 결과)
'==============================================================
Private xlApp As Excel.Application
Private tWB As Workbook, tWS As Worksheet
Private rWB As Workbook, rWS As Worksheet

'==============================================================
' [Public API]
'   - PL2IC : PartList -> itemCounter 생성 파이프라인
'==============================================================
Public Sub PL2IC(ByVal PartList_Directory As String)
    '----------------------------------------------------------
    ' 목적:
    '   1) PartList(Reference)를 열고 표를 스캔
    '   2) 셀 텍스트를 itemUnit으로 분해 (Re_Categorizing)
    '   3) ID_Hash 중복을 제거하고 날짜별 Count를 합산 (PL_Compressor)
    '   4) (추후) tWS(itemCounter)에 쓰기 (Writing_itemCounter_from_PL)
    '----------------------------------------------------------

    Dim rR As Range
    Dim rTitle As Long, rS As Long, rE As Long, cS As Long, cE As Long
    Dim itemsRaw As Collection
    Dim itemsMerged As Collection

    ' 1) Reference 바인딩
    Get_Reference PartList_Directory
    Set rR = rWS.Rows(1).Find("-Line", lookAt:=xlPart, LookIn:=xlValues)
    If rR Is Nothing Then Exit Sub

    ' 2) 스캔 범위 계산
    rTitle = rR.Row
    rS = rTitle + 1
    rE = rWS.Cells(rWS.Rows.Count, 1).End(xlUp).Row
    cS = rR.Column + 2
    cE = rWS.Cells(1, rWS.Columns.Count).End(xlToLeft).Column

    ' 3) 읽기(셀->itemUnit 분해 결과)
    Set itemsRaw = PL_iU_Reader(rS, cS, rE, cE)

    ' 4) 압축/병합(ID_Hash 기준 + 날짜별 Count 합산)
    Set itemsMerged = PL_Compressor(itemsRaw)

#If Not Devmod Then
    ' 5) (실사용) 타겟 바인딩 + 쓰기
    Set_Target ThisWorkbook.FullName, "itemCounter"
    Writing_itemCounter_from_PL itemsMerged
#Else
    ' 개발 테스트: test 시트에 덤프
    Writing_itemCounter_from_PL itemsMerged
#End If

    ' 6) 정리
    TempKiller itemsRaw
    TempKiller itemsMerged
End Sub

'==============================================================
' [Reader]
'   - PartList 표를 스캔하여 "셀별 분해결과(Collection(itemUnit))"를 모음
'
' 반환 구조:
'   Result(i) = Collection(itemUnit)   ' 셀 하나의 분해 결과
'==============================================================
Private Function PL_iU_Reader(ByVal rS As Long, ByVal cS As Long, ByVal rE As Long, ByVal cE As Long) As Collection
    Dim Result As New Collection

    Dim cDates As Long, cCounts As Long, rNickNames As Long
    Dim sNickName As String, sTarget As String
    Dim r As Long, c As Long

    rNickNames = 1
    cDates = rWS.Rows(rNickNames).Find("투입" & vbLf & "시점", lookAt:=xlPart, LookIn:=xlValues).Column
    cCounts = rWS.Rows(rNickNames).Find("수량", lookAt:=xlPart, LookIn:=xlValues).Column

    For c = cS To cE
        sNickName = CStr(rWS.Cells(rNickNames, c).Value)

        For r = rS To rE
            sTarget = CStr(rWS.Cells(r, c).Value)

            If LenB(sTarget) <> 0 Then
                ' 셀 하나 -> Collection(itemUnit)
                Result.Add Re_Categorizing( _
                            sTarget, _
                            sNickName, _
                            rWS.Cells(r, cDates).Value, _
                            rWS.Cells(r, cCounts).Value)
            End If
        Next r
    Next c

    Set PL_iU_Reader = Result
End Function

'==============================================================
' [Categorizer]
'   - 셀 문자열을 해석하여 itemUnit 여러 개로 분해
'==============================================================
Private Function Re_Categorizing( _
    ByVal Sample As String, _
    Optional ByVal NickName As String = "Unknown", _
    Optional ByVal InputDate As Date, _
    Optional ByVal LotCounts As Long = 1) As Collection

    On Error GoTo LogicSkip

    Dim Result As New Collection
    Dim Target As itemUnit
    Dim sVendor As String, sPartNumber As String, sQTY As String

    Dim Vendors As Variant, PartNumbers As Variant
    Dim i As Long, p As Long
    Dim pos As Long

    Sample = Trim$(CStr(Sample))
    Sample = Replace(Sample, " [", "$[")
    Vendors = Split(Sample, "$")

    For i = LBound(Vendors) To UBound(Vendors)
        sVendor = ExtractBracketValue(Vendors(i))
        PartNumbers = Split(Trim$(Replace(Vendors(i), "[" & sVendor & "]", "")), "/")

        For p = LBound(PartNumbers) To UBound(PartNumbers)
            pos = InStr(PartNumbers(p), "(")

            If pos = 0 Then
                sPartNumber = PartNumbers(p)
                sQTY = "1"
            Else
                sPartNumber = Left$(PartNumbers(p), pos - 1)
                sQTY = CStr(ExtractSmallBracketValue(PartNumbers(p)))
            End If

            Set Target = New itemUnit
            Target.NickName = RemoveLineBreaks(NickName)
            Target.Vendor = RemoveLineBreaks(sVendor)
            Target.PartNumber = RemoveLineBreaks(sPartNumber)
            Target.QTY = CLng(sQTY)

            ' 셀의 LotCounts(수량) * 개별 파트 QTY 를 해당 날짜에 기록
            Target.Count(InputDate) = LotCounts * CLng(sQTY)

            Result.Add Target
        Next p

        Erase PartNumbers
    Next i

    Erase Vendors
    Set Re_Categorizing = Result
    Exit Function

LogicSkip:
    ' 파싱 실패 셀은 스킵(필요하면 Debug.Print Sample 등 로깅)
End Function

'==============================================================
' [Compressor / Merger]  ★ 핵심 완성본 ★
'
' 입력:
'   Cells(i) = Collection(itemUnit)  ' 셀 단위 분해 결과
'
' 출력:
'   Result(j) = itemUnit
'   - ID_Hash가 같으면 Result의 대표 itemUnit에 날짜별 Count를 Merge
'   - ID_Hash가 다르면 신규 추가
'==============================================================
Private Function PL_Compressor(ByRef Cells As Collection) As Collection
    Dim Result As New Collection

    Dim cellPack As Collection
    Dim iuRef As itemUnit
    Dim iuDst As itemUnit

    Dim i As Long, n As Long, r As Long
    Dim found As Boolean

    ' 셀 단위 Collection을 순회
    For i = 1 To Cells.Count
        Set cellPack = Cells(i)                 ' Collection(itemUnit)

        ' 셀 안의 itemUnit들을 순회
        For n = 1 To cellPack.Count
            Set iuRef = cellPack(n)

            ' Result에서 동일 ID_Hash 찾기
            found = False
            For r = 1 To Result.Count
                Set iuDst = Result(r)

                If iuDst.ID_Hash = iuRef.ID_Hash Then
                    ' 동일 Hash -> 날짜별 Count 합산
                    iuDst.MergeCountsFrom iuRef
                    found = True
                    Exit For
                End If
            Next r

            ' 없으면 신규 추가
            If Not found Then
                Result.Add iuRef
            End If
        Next n
    Next i

    Set PL_Compressor = Result
End Function

'==============================================================
' [Writer]
'   - 최종(중복 제거+Merge 완료) itemUnit 리스트를 워크시트에 나열
'   - 현재는 Devmod일 때 test 시트에 덤프
'
' 주의:
'   - 실제 itemCounter 시트의 레이아웃이 확정되면
'     (열 위치/정렬/날짜 컬럼 구성) 기준으로 이 함수만 완성하면 됨.
'==============================================================
Private Sub Writing_itemCounter_from_PL(ByRef Target As Collection)
    Dim iu As itemUnit
    Dim r As Long, d As Long
    Dim ws As Worksheet: Set ws = ThisWorkbook.Worksheets("test")
    Dim baseCol As Long: baseCol = 36
#If Devmod Then
    
    Dim i As Long
    Dim Ref(0 To 3) As Date
    Ref(0) = "2025-12-19"
    Ref(1) = "2025-12-22"
    Ref(2) = "2025-12-23"
    Ref(3) = "2025-12-24"
    With ws
        For i = 0 To 9
            .Columns(baseCol).Delete
        Next i
        
        For i = 1 To Target.Count
            r = i + 1
            Set iu = Target(i)
            .Cells(1, baseCol).Value = "No"
            .Cells(1, baseCol + 1).Value = "NickName"
            .Cells(1, baseCol + 2).Value = "Vendor"
            .Cells(1, baseCol + 3).Value = "PartNumber"
            .Cells(1, baseCol + 4).Value = Ref(0)
            .Cells(1, baseCol + 5).Value = Ref(1)
            .Cells(1, baseCol + 6).Value = Ref(2)
            .Cells(1, baseCol + 7).Value = Ref(3)
            .Cells(1, baseCol + 8).Value = "Total"
            .Cells(1, baseCol + 9).Value = "Cycle Stock" ' 활성재고(Cycle Stock), 투입재고(Ready Stock) 안전재고(Safety Stock), 가용재고(Available Inventory), 할당 재고(Allocated Inventory)  현물재고(On-Hand Inventory)
            
            .Cells(r, baseCol).Value = i
            .Cells(r, baseCol + 1).Value = iu.NickName
            .Cells(r, baseCol + 2).Value = iu.Vendor
            .Cells(r, baseCol + 3).Value = iu.PartNumber
            .Cells(r, baseCol + 4).Value = iu.Count(Ref(0))
            .Cells(r, baseCol + 5).Value = iu.Count(Ref(1))
            .Cells(r, baseCol + 6).Value = iu.Count(Ref(2))
            .Cells(r, baseCol + 7).Value = iu.Count(Ref(3))
            .Cells(r, baseCol + 8).Value = iu.Count
            .Cells(r, baseCol + 9).Value = (iu.Count(Ref(0)) > 0) Or (iu.Count(Ref(1)) > 0)
            
        Next i
        
        For i = 0 To 9
            .Columns(baseCol + i).AutoFit
        Next i
    End With
#Else
    ' (선택) 기존 출력 영역 정리
    ws.Range(ws.Cells(1, baseCol), ws.Cells(ws.Rows.Count, baseCol + 50)).ClearContents

    ' 헤더
    ws.Cells(1, baseCol).Value = "No"
    ws.Cells(1, baseCol + 1).Value = "NickName"
    ws.Cells(1, baseCol + 2).Value = "Vendor"
    ws.Cells(1, baseCol + 3).Value = "PartNumber"
    ws.Cells(1, baseCol + 4).Value = "Total"

    ' 데이터
    For r = 1 To Target.Count
        Set iu = Target(r)

        ws.Cells(r + 1, baseCol).Value = r
        ws.Cells(r + 1, baseCol + 1).Value = iu.NickName
        ws.Cells(r + 1, baseCol + 2).Value = iu.Vendor
        ws.Cells(r + 1, baseCol + 3).Value = iu.PartNumber
        ws.Cells(r + 1, baseCol + 4).Value = iu.Count   ' 전체합

        ' 날짜별 Count (날짜 키 개수만큼 우측으로 출력)
        For d = 1 To iu.DateKeyCount
            ws.Cells(1, baseCol + 4 + d).Value = Format$(iu.dateKey(d), "mm-dd")
            ws.Cells(r + 1, baseCol + 4 + d).Value = iu.Count(iu.dateKey(d))
        Next d
    Next r
#End If
End Sub

'==============================================================
' [Binding Helpers]  (당신 코드 유지 / 위치만 하단 정리)
'==============================================================
Private Sub Set_Target(ByVal TargetDir As String, Optional ByVal Target_Worksheet_index As Variant = 1)
    If LenB(TargetDir) = 0 Then Exit Sub
    If Not BindWorkbook(TargetDir, False, xlApp, tWB) Then Exit Sub
    If Not BindWorksheet(tWB, Target_Worksheet_index, tWS) Then Exit Sub
End Sub

Private Sub Get_Reference(ByVal TargetDir As String, Optional ByVal Target_Worksheet_index As Variant = 1)
    If LenB(TargetDir) = 0 Then Exit Sub
    If Not BindWorkbook(TargetDir, False, xlApp, rWB) Then Exit Sub
    If Not BindWorksheet(rWB, Target_Worksheet_index, rWS) Then Exit Sub
End Sub

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
    Set WbOut = GetObject(TargetDir)
    Set AppOut = WbOut.Application
    If StrComp(WbOut.FullName, ThisWorkbook.FullName, vbTextCompare) <> 0 Then AppOut.Visible = WantVisible
    BindWorkbook = True
    Exit Function

EH_GetObject:
    Dim runningApp As Excel.Application
    Dim Wb As Workbook

    On Error Resume Next
    Set runningApp = GetObject(, "Excel.Application")
    On Error GoTo 0

    If Not runningApp Is Nothing Then
        For Each Wb In runningApp.Workbooks
            If StrComp(Wb.FullName, TargetDir, vbTextCompare) = 0 Or StrComp(Wb.Name, fileName, vbTextCompare) = 0 Then
                Set WbOut = Wb
                Set AppOut = runningApp
                If StrComp(WbOut.FullName, ThisWorkbook.FullName, vbTextCompare) <> 0 Then AppOut.Visible = WantVisible
                BindWorkbook = True
                Exit Function
            End If
        Next
    End If

    Dim newApp As Excel.Application
    Set newApp = New Excel.Application
    newApp.Visible = WantVisible

    On Error GoTo EH_OpenNew
    Set WbOut = newApp.Workbooks.Open(fileName:=TargetDir, ReadOnly:=True, Notify:=False)
    Set AppOut = newApp
    BindWorkbook = True
    Exit Function

EH_OpenNew:
    On Error Resume Next
    newApp.Quit
    Set newApp = Nothing
    On Error GoTo 0

    Debug.Print "Failed to open workbook: " & TargetDir
    BindWorkbook = False
End Function

Private Function BindWorksheet(ByVal Wb As Workbook, ByVal WSRef As Variant, ByRef WsOut As Worksheet) As Boolean
    On Error GoTo EH
    If IsNumeric(WSRef) Then
        Set WsOut = Wb.Worksheets(CLng(WSRef))
    Else
        Set WsOut = Wb.Worksheets(CStr(WSRef))
    End If
    BindWorksheet = True
    Exit Function
EH:
    BindWorksheet = False
End Function

Private Sub TempKiller(Optional ByRef temp As Variant)
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

    Set temp = Nothing
End Sub

