Attribute VB_Name = "AA_Updater"
#Const isDev = True
Private Const ThisModuleName As String = "AA_Updater"

' 폴더구조로 선별 후 출력
Sub ExportAllVbaComponents()
    Dim vbComp As Object
    Dim fso As Object
    Dim basePath As String
    Dim folderModules As String, folderClasses As String, folderForms As String
    Dim fileName As String

    ' 기본 경로 설정
    basePath = ThisWorkbook.Path & "\ExcelExportedCodes\"
    folderModules = basePath & "Modules\"
    folderClasses = basePath & "Classes\"
    folderForms = basePath & "Forms\"

    ' 폴더 생성
    Set fso = CreateObject("Scripting.FileSystemObject")
    If Not fso.FolderExists(basePath) Then fso.CreateFolder basePath
    If Not fso.FolderExists(folderModules) Then fso.CreateFolder folderModules
    If Not fso.FolderExists(folderClasses) Then fso.CreateFolder folderClasses
    If Not fso.FolderExists(folderForms) Then fso.CreateFolder folderForms

    ' 구성 요소 반복하며 내보내기
    For Each vbComp In ThisWorkbook.VBProject.VBComponents
        Select Case vbComp.Type
            Case 1: fileName = folderModules & vbComp.Name & ".bas"   ' 표준 모듈
            Case 2: fileName = folderClasses & vbComp.Name & ".cls"   ' 클래스 모듈
            Case 3: fileName = folderForms & vbComp.Name & ".frm"     ' 사용자 폼
            Case Else: fileName = vbNullString
        End Select

        If fileName <> vbNullString Then
            vbComp.Export fileName
        End If
    Next vbComp

    MsgBox "구성 요소가 폴더 구조로 내보내졌습니다." & vbLf & basePath, vbInformation
End Sub
' .Txt .Md 출력
Sub ExportAllModulesDirectlyToTextAndMarkdown()
    Dim vbComp As Object, fso As Object, txtStream As Object, mdStream As Object
    Dim exportPath As String, ext As String, fileName As String
    Dim codeLine As Variant
    Dim codeLines() As String, baseName As String, timeStamp As String, TxtFile As String, mdFile As String
    Dim totalLines As Long

    ' 파일명 구성
    baseName = Left(ThisWorkbook.Name, InStrRev(ThisWorkbook.Name, ".") - 1)
    timeStamp = Format(Now, "yymmddhhmm")
    exportPath = ThisWorkbook.Path & "\ExcelExportedCodes\"
    TxtFile = exportPath & baseName & "_SourceCode_" & timeStamp & ".txt"
    mdFile = exportPath & baseName & "_SourceCode_" & timeStamp & ".md"

    ' 폴더 생성
    Set fso = CreateObject("Scripting.FileSystemObject")
    If Not fso.FolderExists(exportPath) Then fso.CreateFolder exportPath

    ' 스트림 생성 (UTF-8)
    Set txtStream = CreateObject("ADODB.Stream")
    With txtStream
        .Charset = "utf-8"
        .Type = 2
        .Open
    End With

    Set mdStream = CreateObject("ADODB.Stream")
    With mdStream
        .Charset = "utf-8"
        .Type = 2
        .Open
    End With

    ' 구성 요소 반복
    For Each vbComp In ThisWorkbook.VBProject.VBComponents
        Select Case vbComp.Type
            Case 1: ext = ".bas"
            Case 2: ext = ".cls"
            Case 3: ext = ".frm"
            Case Else: ext = ""
        End Select

        If ext <> "" Then
            fileName = vbComp.Name & ext
            totalLines = vbComp.CodeModule.CountOfLines

            ' 코드 읽기
            If totalLines > 0 Then
                codeLines = Split(vbComp.CodeModule.Lines(1, totalLines), vbLf)
            Else
                codeLines = Split("", vbLf)
            End If

            ' TXT 파일 작성
            txtStream.WriteText String(60, "'") & vbLf
            txtStream.WriteText fileName & " Start" & vbLf
            txtStream.WriteText String(60, "'") & vbLf

            ' MD 파일 작성
            mdStream.WriteText "### " & fileName & vbLf
            mdStream.WriteText "````vba" & vbLf

            For Each codeLine In codeLines
                txtStream.WriteText codeLine & vbLf
                mdStream.WriteText codeLine & vbLf
            Next codeLine

            txtStream.WriteText String(60, "'") & vbLf
            txtStream.WriteText fileName & " End" & vbLf
            txtStream.WriteText String(60, "'") & vbLf & vbLf

            mdStream.WriteText "````" & vbLf & vbLf
        End If
    Next vbComp

    ' 저장 및 닫기
    txtStream.SaveToFile TxtFile, 2
    txtStream.Close
    mdStream.SaveToFile mdFile, 2
    mdStream.Close

    MsgBox "모든 코드가 병합되어 저장되었습니다!" & vbLf & _
           TxtFile & vbLf & mdFile, vbInformation
End Sub

Sub ForceUpdateMacro()
    Dim latestVersion As String
    Dim localVersion As String
    Dim versionUrl As String
    Dim fileUrl As String
    Dim savePath As String
    Dim ws As Worksheet
    Dim VersionCell As Range
    
    ' Setting 확인
    Set ws = ThisWorkbook.Worksheets("Setting")
    If ("Dev" = ws.Cells.Find(What:="Develop", lookAt:=xlWhole, MatchCase:=True).Offset(0, 1).value) Then
        #If Not isDev Then
            MsgBox "개발 모드이므로 업데이트 진행 제한", vbInformation, "개발여부 확인"
        #End If
        Exit Sub
    End If

    Set VersionCell = ws.Cells.Find(What:="Version", lookAt:=xlWhole, MatchCase:=True)
    'Debug.Print VersionCell.Address
    
    ' GitLab Raw URL 설정
    versionUrl = "http://mod.lge.com/hub/seongsu1.lee/excelmacroupdater/-/raw/main/Version.txt"
    fileUrl = "http://mod.lge.com/hub/seongsu1.lee/excelmacroupdater/-/raw/main/AutoReport.xlsb"
    
    ' 현재 사용 중인 버전 (Setting Worksheet의 Version 행을 찾아 값 열의 값을 참조함)
    localVersion = VersionCell.Offset(0, 1).value
    
    ' 최신 버전 확인
    latestVersion = GetWebText(versionUrl)
    
    ' 버전 비교 및 업데이트 수행
    If Trim(localVersion) < Trim(latestVersion) Then
        MsgBox "새 버전(" & latestVersion & ")이 감지되었습니다. 업데이트를 진행합니다.", vbInformation
        
        ' 다운로드 경로 설정
        savePath = Environ("TEMP") & "\NewMacro.xlsb"
        
        ' 최신 매크로 파일 다운로드
        If DownloadFile(fileUrl, savePath) Then
            ' 기존 파일 닫기 및 새 파일 실행
            ThisWorkbook.Close False
            Workbooks.Open savePath
        Else
            MsgBox "업데이트 다운로드에 실패했습니다.", vbExclamation
        End If
    Else
        MsgBox "현재 최신 버전을 사용 중입니다.", vbInformation
    End If
End Sub

Function GetWebText(url As String) As String
    Dim http As Object
    Set http = CreateObject("MSXML2.XMLHTTP")
    
    ' 요청 전송
    http.Open "GET", url, False
    http.Send
    
    ' 응답 확인
    If http.Status = 200 Then
        GetWebText = http.responseText
    Else
        GetWebText = "Error"
    End If
End Function

Function DownloadFile(url As String, savePath As String) As Boolean
    Dim http As Object
    Dim stream As Object
    
    On Error Resume Next
    Set http = CreateObject("MSXML2.XMLHTTP")
    
    ' 파일 다운로드 요청
    http.Open "GET", url, False
    http.Send
    
    ' 다운로드 확인
    If http.Status = 200 Then
        Set stream = CreateObject("ADODB.Stream")
        stream.Type = 1
        stream.Open
        stream.Write http.responseBody
        stream.SaveToFile savePath, 2
        stream.Close
        
        ' 다운로드 성공
        DownloadFile = True
    Else
        ' 다운로드 실패
        DownloadFile = False
    End If
End Function

' === Export 구조 기반 Import 유틸리티 ===
' - Export된 모듈을 폴더에서 불러와 ThisWorkbook에 적재
' - 중복된 모듈명은 자동 제거 후 Import

Public Sub ImportAllVbaComponents()
    Dim basePath As String
    basePath = ThisWorkbook.Path & "\ExcelExportedCodes\"
   
    ImportModulesFromFolder basePath & "Modules\"
    ImportModulesFromFolder basePath & "Classes\"
    ImportModulesFromFolder basePath & "Forms\"
   
    MsgBox "Import 완료!", vbInformation
End Sub

Private Sub ImportModulesFromFolder(ByVal folderPath As String)
    Dim fso As Object, file As Object, files As Object
    Dim vbCompName As String, vbProj As Object
    Set fso = CreateObject("Scripting.FileSystemObject")
    If Not fso.FolderExists(folderPath) Then Exit Sub

    Set vbProj = ThisWorkbook.VBProject
    Set files = fso.GetFolder(folderPath).files
   
    For Each file In files
        If IsVbaFile(file.Name) Then
            vbCompName = GetVBNameFromFile(file.Path)
            If LenB(vbCompName) > 0 Then
                ' 기존 모듈 삭제
                On Error Resume Next
                vbProj.VBComponents.Remove vbProj.VBComponents(vbCompName)
                On Error GoTo 0
            End If
            vbProj.VBComponents.Import file.Path
        End If
    Next
End Sub

Private Function IsVbaFile(ByVal fileName As String) As Boolean
    Dim ext As String
    ext = LCase$(mid(fileName, InStrRev(fileName, ".") + 1))
    IsVbaFile = (ext = "bas" Or ext = "cls" Or ext = "frm")
End Function

Private Function GetVBNameFromFile(ByVal filePath As String) As String
    Dim ff As Integer: ff = FreeFile
    Dim line As String, vbName As String
    Open filePath For Input As #ff
    Do While Not EOF(ff)
        Line Input #ff, line
        If LCase$(line) Like "*attribute vb_name*" Then
            vbName = Trim$(Split(line, "=")(1))
            vbName = Replace(vbName, """", "")
            Exit Do
        End If
    Loop
    Close #ff
    GetVBNameFromFile = vbName
End Function
