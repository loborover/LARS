VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
END
Attribute VB_Name = "itemUnit"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = False
'==================================================================
' itemUnit.cls
'==================================================================
Option Explicit

'==========================================================
' Class: itemUnit
' 목적:
'   - 자재 메타정보(Nick/Vender/PartNumber/QTY)
'   - 날짜별 투입 수량 Count(인덱서 형태)
'   - ID_Hash 기준으로 병합(MergeCountsFrom)
'
' 설계 핵심:
'   - vDateKey(0) = -1 : placeholder (유효 데이터 아님)
'   - 유효 날짜 Key는 1..UBound(vDateKey)
'==========================================================

Private vID As String
Private vNick As String, vVender As String, vPartNumber As String
Private vQTY As Long

' 날짜별 카운터
Private vDateKey() As Long
Private vDateCounts() As Long
Private mDatesInitialized As Boolean

Private Sub Class_Initialize()
    mDatesInitialized = False
End Sub

'------------------------------
' 메타정보 프로퍼티
'------------------------------
Public Property Get ID_Hash() As String
    ID_Hash = vID
End Property

Public Property Get NickName() As String
    NickName = vNick
End Property
Public Property Let NickName(ByVal Target As String)
    vNick = Target: MakeID
End Property

Public Property Get Vender() As String
    Vender = vVender
End Property
Public Property Let Vender(ByVal Target As String)
    vVender = Target: MakeID
End Property

Public Property Get PartNumber() As String
    PartNumber = vPartNumber
End Property
Public Property Let PartNumber(ByVal Target As String)
    vPartNumber = Target: MakeID
End Property

Public Property Get QTY() As Long
    QTY = vQTY
End Property
Public Property Let QTY(ByVal Target As Long)
    vQTY = Target
End Property

Private Sub MakeID()
    If Len(vNick) > 0 And Len(vVender) > 0 And Len(vPartNumber) > 0 Then
        vID = vVender & "_" & vNick & "_" & vPartNumber
    End If
End Sub

'------------------------------
' Count 인덱서
'   - Count()              : 전체 합계 (읽기 전용)
'   - Count(Date)          : 해당 날짜 값 읽기
'   - Count(Date) = value  : 해당 날짜 값 쓰기/생성
'------------------------------
Public Property Get Count(Optional ByVal Month_Day As Variant = vbNullString) As Long
    Dim dKey As Long, idx As Long
    EnsureDateArraysInitialized

    If Month_Day = vbNullString Then
        Count = TotalCount
        Exit Property
    End If

    dKey = NormalizeDateKey(Month_Day)
    idx = IndexOfDateKey(dKey)

    If idx >= 0 Then
        Count = vDateCounts(idx)
    Else
        Count = 0
    End If
End Property

Public Property Let Count(ByVal Month_Day As Variant, ByVal Value As Long)
    Dim dKey As Long, idx As Long
    EnsureDateArraysInitialized

    dKey = NormalizeDateKey(Month_Day)
    idx = IndexOfDateKey(dKey)

    If idx >= 0 Then
        vDateCounts(idx) = Value
    Else
        idx = AddDateKey(dKey)
        vDateCounts(idx) = Value
    End If
End Property

'------------------------------
' Merge: 상대 itemUnit의 날짜별 Count를 내 Count에 합산
'------------------------------
Public Sub MergeCountsFrom(ByVal OtherItem As itemUnit)
    Dim i As Long
    Dim dDate As Date, dCount As Long

    For i = 1 To OtherItem.DateKeyCount
        dDate = OtherItem.DateKey(i)
        dCount = OtherItem.Count(dDate)
        If dCount <> 0 Then
            Me.Count(dDate) = Me.Count(dDate) + dCount
        End If
    Next i
End Sub

'------------------------------
' 날짜 Key 열거 (1..DateKeyCount)
'------------------------------
Public Property Get DateKeyCount() As Long
    If Not mDatesInitialized Then
        DateKeyCount = 0
    ElseIf UBound(vDateKey) = 0 Then
        DateKeyCount = 0
    Else
        DateKeyCount = UBound(vDateKey)   ' 유효 키는 1..UBound
    End If
End Property

Public Property Get DateKey(ByVal Index1 As Long) As Date
    If mDatesInitialized Then
        If Index1 >= 1 And Index1 <= UBound(vDateKey) Then
            DateKey = CDate(vDateKey(Index1))
        End If
    End If
End Property

'------------------------------
' 내부 헬퍼
'------------------------------
Private Function TotalCount() As Long
    Dim i As Long
    If Not mDatesInitialized Then Exit Function
    For i = 1 To UBound(vDateCounts)
        TotalCount = TotalCount + vDateCounts(i)
    Next i
End Function

Private Sub EnsureDateArraysInitialized()
    If Not mDatesInitialized Then
        ReDim vDateKey(0 To 0)
        ReDim vDateCounts(0 To 0)
        vDateKey(0) = -1
        vDateCounts(0) = 0
        mDatesInitialized = True
    End If
End Sub

Private Function NormalizeDateKey(ByVal Month_Day As Variant) As Long
    NormalizeDateKey = CLng(Int(CDate(Month_Day)))
End Function

Private Function IndexOfDateKey(ByVal dKey As Long) As Long
    Dim i As Long
    If Not mDatesInitialized Then
        IndexOfDateKey = -1
        Exit Function
    End If

    For i = 1 To UBound(vDateKey)
        If vDateKey(i) = dKey Then
            IndexOfDateKey = i
            Exit Function
        End If
    Next i

    IndexOfDateKey = -1
End Function

Private Function AddDateKey(ByVal dKey As Long) As Long
    Dim newIndex As Long
    If Not mDatesInitialized Then EnsureDateArraysInitialized

    newIndex = UBound(vDateKey) + 1
    ReDim Preserve vDateKey(0 To newIndex)
    ReDim Preserve vDateCounts(0 To newIndex)

    vDateKey(newIndex) = dKey
    vDateCounts(newIndex) = 0
    AddDateKey = newIndex
End Function
