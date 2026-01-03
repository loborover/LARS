Attribute VB_Name = "TimeKeeper"
Option Explicit

' 메인: (1) 날짜열+시간열 병합 또는 (2) 단일열(혼합포맷) → Target 열(Date) 저장, 표시형식은 "hh:mm"
' - ws         : 작업 시트
' - dateCol    : 날짜(또는 혼합) 열 번호 (예: D=4)
' - targetCol  : 타겟 열 번호 (예: F=6)
' - timeCol    : 시간 열 번호 (기본=0 → 단일열 파싱 모드)
Public Sub MergeDateTime_Flexible(ByRef ws As Worksheet, _
                                  ByVal dateCol As Long, ByVal targetCol As Long, _
                                  Optional ByVal timeCol As Long = 0, _
                                  Optional ByVal startRow As Long = 2, _
                                  Optional ByVal TargetHeader As String = "Input Time", _
                                  Optional ByVal Formatting As String = "hh:mm")

    Dim LastRow As Long, r As Long
    Dim vD As Variant, vT As Variant
    Dim DT As Date

    If ws Is Nothing Then Exit Sub

    Application.ScreenUpdating = False
    Application.EnableEvents = False

    ' 기준 열(날짜 혹은 혼합) 마지막 행
    LastRow = ws.Cells(ws.Rows.Count, dateCol).End(xlUp).Row
    If LastRow < startRow Then GoTo CleanExit ' 데이터 없음

    For r = startRow To LastRow
        vD = ws.Cells(r, dateCol).value2
        If timeCol > 0 Then
            vT = ws.Cells(r, timeCol).value2
        Else
            vT = Empty
        End If

        If TryParseDateTimeFlex(vD, vT, DT) Then
            ws.Cells(r, targetCol).Value = DT ' 값은 Date 직렬값
        Else
            ws.Cells(r, targetCol).ClearContents
        End If
    Next r

    ' 표시 형식(값은 Date 그대로 유지)
    ws.Range(ws.Cells(startRow, targetCol), ws.Cells(LastRow, targetCol)).NumberFormat = Formatting
    ws.Cells(startRow - 1, targetCol).Value = TargetHeader

CleanExit:
    Application.EnableEvents = True
    Application.ScreenUpdating = True
End Sub

' vDate, vTime을 다양한 포맷으로 받아 Date으로 변환
' - vTime이 제공되면: YYYYMMDD/날짜값 + 시간값/텍스트 등을 병합
' - vTime이 없으면: vDate(혼합열)에서 날짜+시간을 모두 파싱
Private Function TryParseDateTimeFlex(ByVal vDate As Variant, ByVal vTime As Variant, ByRef outDT As Date) As Boolean
    Dim baseDate As Date
    Dim tfrac As Double

    TryParseDateTimeFlex = False
    outDT = 0

    ' 두 열 병합 모드: vTime이 의미있게 들어온 경우
    If Not IsEmpty(vTime) And Not IsError(vTime) Then
        ' 날짜 해석
        If Not TryParse_DateOnly(vDate, baseDate) Then Exit Function
        ' 시간 해석
        If Not TryGetTimeFraction_Flex(vTime, tfrac) Then tfrac = 0#
        outDT = baseDate + tfrac
        TryParseDateTimeFlex = True
        Exit Function
    End If

    ' 단일 열(혼합 포맷) 모드: vDate 안에 날짜+시간(또는 둘 중 하나)
    If IsEmpty(vDate) Or IsError(vDate) Then Exit Function

    ' 1) 이미 날짜/시간 값(직렬)인 경우
    If IsDate(vDate) Then
        outDT = CDate(vDate)
        TryParseDateTimeFlex = True
        Exit Function
    End If

    ' 2) 텍스트인 경우 처리 (오전/오후, AM/PM, YYYYMMDD, hhmmss 등)
    Dim s As String, sNorm As String, Y As Long, m As Long, d As Long
    s = Trim$(CStr(vDate))
    If Len(s) = 0 Then Exit Function

    ' a) "YYYYMMDD" 단독
    If Len(s) = 8 And IsNumeric(s) Then
        If TryParseYmd8_ToDate(s, baseDate) Then
            outDT = baseDate ' 시간 00:00
            TryParseDateTimeFlex = True
            Exit Function
        End If
    End If

    ' b) "YYYYMMDD HH:MM[:SS]" 또는 "YYYYMMDD 오전 HH:MM[:SS]" 등
    If TryParse_Ymd8_And_TimeText(s, outDT) Then
        TryParseDateTimeFlex = True
        Exit Function
    End If

    ' c) 일반 텍스트 날짜/시간 (오전/오후 → AM/PM 치환 후 CDate 시도)
    sNorm = NormalizeKoreanAmPm(s)
    On Error Resume Next
    outDT = CDate(sNorm)
    If Err.Number = 0 Then
        TryParseDateTimeFlex = True
    End If
    On Error GoTo 0
End Function

' YYYYMMDD(숫자/텍스트) → Date (시간 00:00)
Private Function TryParseYmd8_ToDate(ByVal v As Variant, ByRef outDate As Date) As Boolean
    Dim n As Long, Y As Long, m As Long, d As Long
    Dim s As String

    TryParseYmd8_ToDate = False
    If IsEmpty(v) Or IsError(v) Then Exit Function

    If IsNumeric(v) Then
        n = CLng(v)
        If n <= 0 Then Exit Function
        Y = n \ 10000
        m = (n \ 100) Mod 100
        d = n Mod 100
    Else
        s = Trim$(CStr(v))
        If Len(s) <> 8 Then Exit Function
        If Not IsNumeric(s) Then Exit Function
        Y = CLng(Left$(s, 4))
        m = CLng(mid$(s, 5, 2))
        d = CLng(Right$(s, 2))
    End If

    If Y < 1900 Or m < 1 Or m > 12 Or d < 1 Or d > 31 Then Exit Function

    outDate = DateSerial(Y, m, d)
    TryParseYmd8_ToDate = True
End Function

' 혼합 텍스트에서 "YYYYMMDD [오전/오후|AM/PM] hh:mm[:ss]" 패턴 처리
' 예: "20250831 오전 08:00:00", "20250831 8:00", "20250831 PM 8:00"
Private Function TryParse_Ymd8_And_TimeText(ByVal s As String, ByRef outDT As Date) As Boolean
    Dim sTrim As String, Y As Long, m As Long, d As Long
    Dim DatePart As String, timePart As String, posSp As Long
    Dim baseDate As Date, tfrac As Double

    TryParse_Ymd8_And_TimeText = False
    sTrim = Trim$(s)
    If Len(sTrim) < 8 Then Exit Function

    ' 앞 8자리가 YYYYMMDD인가?
    If Not IsNumeric(Left$(sTrim, 8)) Then Exit Function
    DatePart = Left$(sTrim, 8)
    If Not TryParseYmd8_ToDate(DatePart, baseDate) Then Exit Function

    ' 뒤쪽에서 시간부분 추출(있을 수도, 없을 수도)
    timePart = mid$(sTrim, 9) ' 9번째 이후
    timePart = Trim$(timePart)

    If Len(timePart) = 0 Then
        outDT = baseDate
        TryParse_Ymd8_And_TimeText = True
        Exit Function
    End If

    ' 시간 텍스트를 분수일로 변환
    If TryGetTimeFraction_Flex(timePart, tfrac) Then
        outDT = baseDate + tfrac
        TryParse_Ymd8_And_TimeText = True
    End If
End Function

' 시간값의 "소수일수" 추출 (0.0 ~ <1.0)
' 허용:
'  - 실제 날짜/시간 값 (직렬)
'  - "08:00:00"/"8:00"/"8:00 PM"/"오전 8:00" 등의 텍스트
'  - "080000" 등 6자리 시간 텍스트
Private Function TryGetTimeFraction_Flex(ByVal v As Variant, ByRef outFrac As Double) As Boolean
    Dim s As String, hh As Long, nn As Long, ss As Long
    Dim sNorm As String

    TryGetTimeFraction_Flex = False
    outFrac = 0#

    If IsEmpty(v) Or IsError(v) Then Exit Function

    ' 이미 날짜/시간 값(직렬)인 경우
    If IsDate(v) Then
        outFrac = CDbl(CDate(v)) - Fix(CDbl(CDate(v))) ' 정수부 제거: 시간 부분만
        TryGetTimeFraction_Flex = True
        Exit Function
    End If

    ' 텍스트 처리
    s = Trim$(CStr(v))
    If Len(s) = 0 Then Exit Function

    ' "오전/오후" → AM/PM 정규화
    sNorm = NormalizeKoreanAmPm(s)

    If InStr(sNorm, ":") > 0 Then
        ' "hh:mm[:ss]" (AM/PM 포함 가능)
        On Error Resume Next
        outFrac = TimeValue(sNorm)
        If Err.Number = 0 Then TryGetTimeFraction_Flex = True
        On Error GoTo 0
    ElseIf Len(sNorm) = 6 And IsNumeric(sNorm) Then
        ' "hhmmss"
        hh = CLng(Left$(sNorm, 2))
        nn = CLng(mid$(sNorm, 3, 2))
        ss = CLng(Right$(sNorm, 2))
        If hh >= 0 And hh <= 23 And nn >= 0 And nn <= 59 And ss >= 0 And ss <= 59 Then
            outFrac = TimeSerial(hh, nn, ss)
            TryGetTimeFraction_Flex = True
        End If
    End If
End Function

' 한국어 오전/오후를 AM/PM으로 치환하고, 불필요한 중복 공백 정리
Private Function NormalizeKoreanAmPm(ByVal s As String) As String
    Dim T As String
    T = s
    ' 변형 케이스 최소화: 앞뒤 공백에 둔감하게
    T = Replace(T, "오전", "AM")
    T = Replace(T, "오 후", "PM") ' 혹시 있을 느슨한 표기
    T = Replace(T, "오후", "PM")
    ' 다중 공백 축소(간단치환)
    Do While InStr(T, "  ") > 0
        T = Replace(T, "  ", " ")
    Loop
    NormalizeKoreanAmPm = Trim$(T)
End Function

' (선택) DateOnly 전용 파서: 숫자형 직렬/텍스트 YYYYMMDD/표준 날짜텍스트를 아우름
Private Function TryParse_DateOnly(ByVal v As Variant, ByRef outDate As Date) As Boolean
    Dim s As String
    TryParse_DateOnly = False
    outDate = 0

    If IsEmpty(v) Or IsError(v) Then Exit Function

    If IsDate(v) Then
        outDate = DateValue(CDate(v))
        TryParse_DateOnly = True
        Exit Function
    End If

    s = Trim$(CStr(v))
    If Len(s) = 8 And IsNumeric(s) Then
        If TryParseYmd8_ToDate(s, outDate) Then
            TryParse_DateOnly = True
            Exit Function
        End If
    End If

    ' 일반 텍스트 날짜(구분자 포함)도 허용
    On Error Resume Next
    outDate = DateValue(CDate(s))
    If Err.Number = 0 Then TryParse_DateOnly = True
    On Error GoTo 0
End Function

'========================
' 공개 API
'========================
' StartDT ~ EndDT 사이에서
' - 중간 날짜(시작일+1 ~ 종료일-1)는 무시
' - 시작일의 StartDT~자정, 종료일의 자정~EndDT만 계산
' - 각 날짜 구간에서 제외시간과 겹치는 부분만 차감
Public Function Time_Filtering(ByVal StartDT As Date, ByVal EndDT As Date) As Date
    Dim TC As New Collection
    Dim Total As Double
    Dim startDay As Date, endDay As Date
    Dim startDayEnd As Date, endDayStart As Date

    If EndDT <= StartDT Then
        Time_Filtering = 0
        Exit Function
    End If

    ' (예시) 제외시간 정의: "hh:mm:ss-hh:mm:ss"
    TC.Add "00:00:00-08:00:00"
    TC.Add "10:00:00-10:10:00"
    TC.Add "12:00:00-13:00:00"
    TC.Add "15:00:00-15:10:00"
    TC.Add "17:00:00-17:30:00"
    TC.Add "19:30:00-19:40:00"
    TC.Add "20:30:00-00:00:00"

    startDay = DateSerial(Year(StartDT), Month(StartDT), Day(StartDT))
    endDay = DateSerial(Year(EndDT), Month(EndDT), Day(EndDT))

    If startDay = endDay Then
        ' 같은 날짜 안에서 끝나는 경우: 단일 구간 계산
        Total = NetDurationSingleDay(StartDT, EndDT, TC)
    Else
        ' 서로 다른 날짜: 시작일 구간 + 종료일 구간만 계산
        startDayEnd = DateAdd("d", 1, startDay) ' 다음 날 00:00
        endDayStart = endDay                     ' 종료일 00:00

        ' 시작일: StartDT ~ 자정
        Total = Total + NetDurationSingleDay(StartDT, startDayEnd, TC)
        ' 종료일: 자정 ~ EndDT
        Total = Total + NetDurationSingleDay(endDayStart, EndDT, TC)
        ' 중간 날짜(startDay+1 ~ endDay-1)는 전부 제외 (의도적으로 아무 것도 더하지 않음)
    End If

    Time_Filtering = Total ' Double(일수) → Date 직렬 반환
End Function

'========================
' 헬퍼: 단일 날짜 구간만 다룸
'========================
' [segStart, segEnd) 가 반드시 같은 날짜(자정 미만) 범위라고 가정하고,
' 제외시간 컬렉션(TC)과의 겹침만큼 Duration에서 차감
Private Function NetDurationSingleDay(ByVal segStart As Date, ByVal segEnd As Date, ByVal TC As Collection) As Double
    Dim dur As Double
    Dim s As Variant, parts() As String
    Dim exStartT As Date, exEndT As Date
    Dim exStart As Date, exEnd As Date
    Dim ovS As Date, ovE As Date

    If segEnd <= segStart Then
        NetDurationSingleDay = 0#
        Exit Function
    End If

    dur = segEnd - segStart ' 기본 길이(일수)

    For Each s In TC
        parts = Split(CStr(s), "-")
        If UBound(parts) = 1 Then
            ' 시간 텍스트를 Time으로
            exStartT = TimeValue(parts(0))
            exEndT = TimeValue(parts(1))

            ' 같은 "날짜" 기준으로 제외구간 구성
            ' 1) 정상 순행(exEndT > exStartT): segStart의 날짜에 그대로 매핑
            If exEndT > exStartT Then
                exStart = DateSerial(Year(segStart), Month(segStart), Day(segStart)) + (exStartT - Fix(exStartT))
                exEnd = DateSerial(Year(segStart), Month(segStart), Day(segStart)) + (exEndT - Fix(exEndT))

                ovS = Application.WorksheetFunction.Max(segStart, exStart)
                ovE = Application.WorksheetFunction.Min(segEnd, exEnd)
                If ovE > ovS Then dur = dur - (ovE - ovS)

            ' 2) 자정 걸침(exEndT <= exStartT): 두 조각으로 분리
            '    a) 당일 exStartT ~ 24:00 (같은 날짜 조각)
            '    b) 익일 00:00 ~ exEndT   (다음날 조각) → 단일일 처리에서는 무시
            Else
                ' a) 같은 날짜의 후미 조각만 반영
                exStart = DateSerial(Year(segStart), Month(segStart), Day(segStart)) + (exStartT - Fix(exStartT))
                exEnd = DateAdd("d", 1, DateSerial(Year(segStart), Month(segStart), Day(segStart))) ' 자정(다음날 00:00)

                ovS = Application.WorksheetFunction.Max(segStart, exStart)
                ovE = Application.WorksheetFunction.Min(segEnd, exEnd)
                If ovE > ovS Then dur = dur - (ovE - ovS)

                ' b) 익일 조각은 이 함수의 책임 범위가 아님(해당 날짜에서 다시 계산됨)
            End If
        End If
    Next

    NetDurationSingleDay = IIf(dur > 0, dur, 0#)
End Function
Public Function isDayDiff(ByRef T1 As Range, ByRef T2 As Range, Optional ByVal MinDays As Long = 1) As Boolean
    If Not IsDate(T1.Value) Or Not IsDate(T2.Value) Then isDayDiff = False: Exit Function
    If Abs(DateValue(CDate(T2.Value)) - DateValue(CDate(T1.Value))) >= MinDays Then isDayDiff = True
End Function

