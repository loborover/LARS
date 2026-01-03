VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
END
Attribute VB_Name = "itemGroup"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = False
Option Explicit

Private vEachCells As Collection, vUnits As Collection, vDates As Collection
Private vHighestDay As Date, vLowestDay As Date
Private ReGroupCounter As Long

Private Sub Class_Initialize()
    Set vEachCells = New Collection
    Set vUnits = New Collection
    Set vDates = New Collection
End Sub
Public Property Get Cells(ByVal index As Variant) As Collection
    If vEachCells.Count = 0 Then
        Set Cells = Nothing
        Debug.Print "itemGroup.Cell(index) - 잘못된 참조"
        Exit Property
    End If
    Set Cells = vEachCells(index)
End Property
Public Property Get CellsCount() As Long
    CellsCount = vEachCells.Count
End Property
Public Property Get Unit(ByVal index As Variant) As itemUnit
    If vUnits.Count = 0 Then
        Set Unit = Nothing
        Debug.Print "itemGroup.Unit(index) - 잘못된 참조"
        Exit Property
    End If
    Set Unit = vUnits(index)
End Property
Public Property Get UnitCount() As Long
    UnitCount = vUnits.Count
End Property
Public Property Get DaysCount() As Long
    DaysCount = vDates.Count
End Property
Public Property Get EachDay(ByVal index As Long) As Date
    If vDates.Count = 0 Then Exit Property
    EachDay = vDates(index)
End Property
Public Property Get HighestDay() As Date
    HighestDay = vHighestDay
End Property
Public Property Get LowestDay() As Date
    LowestDay = vLowestDay
End Property
Public Sub AddUnit(Target As itemUnit)
    vUnits.Add Target
    AddDates Target
    ReGroupCounter = ReGroupCounter + 1
    If ReGroupCounter > 999 Then ReGroup
End Sub
Public Sub ReGroup()
    Set vUnits = CompressorLV2(vEachCells)
    Set vUnits = CompressorLV1(vUnits)
    ReGroupCounter = 0
End Sub

Private Function CompressorLV1(ByRef Cells As Collection) As Collection
    Dim Result As New Collection
    Dim iuRef As itemUnit
    Dim iuDst As itemUnit
    Dim i As Long, r As Long
    Dim found As Boolean

    ' vUnits는 itemUnit들의 단순 Collection
    For i = 1 To Cells.Count
        Set iuRef = Cells(i)

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
        If Not found Then Result.Add iuRef
    Next i

    Set CompressorLV1 = Result
End Function
Private Function CompressorLV2(ByRef Cells As Collection) As Collection
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

    Set CompressorLV2 = Result
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

Private Sub AddDates(ByRef Target As itemUnit)
    Dim i As Long, d As Long
    Dim dateKey As Date
    Dim exists As Boolean
   
    For i = 1 To Target.DateKeyCount
        dateKey = Target.dateKey(i)
        exists = False
       
        ' 중복 체크
        For d = 1 To vDates.Count
            If vDates(d) = dateKey Then
                exists = True
                Exit For
            End If
        Next d
       
        ' 중복되지 않으면 추가
        If Not exists Then
            vDates.Add dateKey
            If vLowestDay > dateKey Then vLowestDay = dateKey
            If vHighestDay < dateKey Then vHighestDay = dateKey
        End If
    Next i
End Sub
