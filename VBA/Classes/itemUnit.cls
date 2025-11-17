VERSION 1.0 CLASS
BEGIN
  MultiUse = -1  'True
END
Attribute VB_Name = "itemUnit"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = False
Attribute VB_Exposed = False
Option Explicit
'==========================================================
' Class: itemUnit (간소화 버전)
' - 아이템 메타정보(Nick/Vender/PartNumber/QTY)
' - 월/일 기준 카운터: CountPerDay(Key As Variant)
'   * Key = Date  -> 해당 날짜(MM-DD)에 대응하는 슬롯
'   * Key = Long  -> 내부 인덱스(1~372 = 12*31) 직접 접근
'==========================================================

'----------------------------------------
' [기본 필드: 메타정보]
'----------------------------------------
Private vID As String
Private vNick As String
Private vVender As String
Private vPartNumber As String
Private vQTY As Long

'----------------------------------------
' [월일 카운터]
' - 12개월 * 31일 = 372칸 고정
' - Index = (Month(d) - 1) * 31 + Day(d)
'----------------------------------------
Private mCounts(1 To 372) As Long

'==========================================================
' 1) 메타정보: ID 생성 및 프로퍼티
'==========================================================
Public Property Get ID_Hash() As String
    ID_Hash = vID          ' 읽기 전용
End Property

Public Property Get NickName() As String
    NickName = vNick
End Property

Public Property Let NickName(ByVal Target As String)
    vNick = Target
    MakeID
End Property

Public Property Get Vender() As String
    Vender = vVender
End Property

Public Property Let Vender(ByVal Target As String)
    vVender = Target
    MakeID
End Property

Public Property Get PartNumber() As String
    PartNumber = vPartNumber
End Property

Public Property Let PartNumber(ByVal Target As String)
    vPartNumber = Target
    MakeID
End Property

Public Property Get QTY() As Long
    QTY = vQTY
End Property

Public Property Let QTY(ByVal Target As Long)
    vQTY = Target
End Property

' ID = Nick_Vender_PartNumber (세 필드가 모두 있을 때만)
Private Sub MakeID()
    If Len(vNick) > 0 And Len(vVender) > 0 And Len(vPartNumber) > 0 Then
        vID = vVender & "_" & vNick & "_" & vPartNumber
    End If
End Sub

'==========================================================
' 2) 수명/상태 관리
'==========================================================
Public Sub Clear()
    Dim i As Long
    For i = LBound(mCounts) To UBound(mCounts)
        mCounts(i) = 0
    Next i
End Sub

' 필요하면 형식 유지용
Public Sub Init()
    Clear
End Sub

'==========================================================
' 3) 내부 유틸리티 (인덱스 변환/검사)
'==========================================================
' Date -> 월일 인덱스(1~372) 변환
Private Function MDIndexFromDate(ByVal d As Date) As Long
    Dim m As Long, dy As Long, idx As Long

    m = Month(d)
    dy = Day(d)

    If m < 1 Or m > 12 Or dy < 1 Or dy > 31 Then
        Err.Raise 13, TypeName(Me) & ".MDIndexFromDate", "유효하지 않은 날짜입니다."
    End If

    idx = (m - 1) * 31 + dy          ' 1 ~ 372
    MDIndexFromDate = idx
End Function

Private Sub CheckIndex(ByVal idx As Long)
    If idx < LBound(mCounts) Or idx > UBound(mCounts) Then
        Err.Raise 9, TypeName(Me) & ".CheckIndex", "인덱스 범위를 벗어났습니다.(1~372)"
    End If
End Sub

'==========================================================
' 4) 핵심 인덱서: CountPerDay
'    - Key As Variant: Date 또는 Long(1~372)
'==========================================================
Public Property Get CountPerDay(ByVal Key As Variant) As Long
    Dim idx As Long

    If VarType(Key) = vbDate Then
        idx = MDIndexFromDate(CDate(Key))
    ElseIf isNumeric(Key) Then
        idx = CLng(Key)
    Else
        Err.Raise 13, TypeName(Me) & ".CountPerDay.Get", _
                    "Key는 Date 또는 Long(1~372) 이어야 합니다."
    End If

    CheckIndex idx
    CountPerDay = mCounts(idx)
End Property

Public Property Let CountPerDay(ByVal Key As Variant, ByVal value As Long)
    Dim idx As Long

    If VarType(Key) = vbDate Then
        idx = MDIndexFromDate(CDate(Key))
    ElseIf isNumeric(Key) Then
        idx = CLng(Key)
    Else
        Err.Raise 13, TypeName(Me) & ".CountPerDay.Let", _
                    "Key는 Date 또는 Long(1~372) 이어야 합니다."
    End If

    CheckIndex idx
    mCounts(idx) = value
End Property

Public Property Get SumCount() As Long
    Dim i As Long, Result As Long
    For i = LBound(mCounts) To UBound(mCounts)
        Result = Result + mCounts(i)
    Next i
    SumCount = Result
End Property

Public Function Copy() As itemUnit
    Dim Copied As New itemUnit, i As Long
    With Copied
        .NickName = vNick
        .PartNumber = vPartNumber
        .Vender = vVender
        For i = 1 To 372
            .CountPerDay(i) = mCounts(i)
        Next i
    End With
    Set Copy = Copied
End Function
