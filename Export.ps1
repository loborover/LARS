# 결과 파일명
$output = "Project_Code_Final.md"

# 포함할 확장자 목록 (필요하면 여기에 추가하세요)
$targetExt = @(".cs", ".cshtml", ".razor", ".js", ".ts", ".css", ".json", ".xml", ".sql", ".config")

# 1. 결과 파일 초기화 (기존 파일 있으면 덮어쓰기)
Set-Content -Path $output -Value "# Project Code Dump" -Encoding utf8

# 2. 파일 목록 가져오기 (bin, obj, .git, .vs 폴더 무조건 제외)
Write-Host "파일을 검색 중입니다..."
$files = Get-ChildItem -Path . -Recurse -File | Where-Object {
    $_.DirectoryName -notmatch "\\bin" -and
    $_.DirectoryName -notmatch "\\obj" -and
    $_.DirectoryName -notmatch "\\.git" -and
    $_.DirectoryName -notmatch "\\.vs" -and
    $_.DirectoryName -notmatch "\\node_modules"
}

# 3. 파일 순회하며 내용 쓰기
foreach ($f in $files) {
    # 확장자가 목록에 있는지 확인
    if ($targetExt -contains $f.Extension.ToLower()) {
       
        # 진행 상황 출력
        Write-Host "처리 중: $($f.Name)"

        # 구분선 및 파일명 기록
        Add-Content -Path $output -Value "`n----------------------------------------"
        Add-Content -Path $output -Value "## File: $($f.Name)"
        Add-Content -Path $output -Value "Path: $($f.FullName)"
       
        # 코드 블록 시작 (백틱 3개 직접 입력 대신 문자열로 처리)
        Add-Content -Path $output -Value "```"
       
        # 파일 내용 읽어서 추가 (오류 발생 시 건너뜀)
        try {
            $content = Get-Content $f.FullName -Raw -ErrorAction Stop
            Add-Content -Path $output -Value $content
        } catch {
            Add-Content -Path $output -Value "None"
        }
       
        # 코드 블록 끝
        Add-Content -Path $output -Value "```"
    }
}

Write-Host "`n완료되었습니다. '$output' 파일을 확인해주세요."