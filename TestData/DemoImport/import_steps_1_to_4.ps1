# Script tự động import và merge Bước 1 đến Bước 4 vào cơ sở dữ liệu
param (
    [string]$BaseUrl = "http://localhost:5023",
    [string]$Token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIwMDAwMDAwMC0wMDAwLTAwMDAtMDAwMC0wMDAwMDAwMDAwMDEiLCJyb2xlIjoiVGF4QWRtaW4iLCJodHRwOi8vc2NoZW1hcy5taWNyb3NvZnQuY29tL3dzLzIwMDgvMDYvaWRlbnRpdHkvY2xhaW1zL3JvbGUiOiJUYXhBZG1pbiIsImlzcyI6IlRheEtlZXBWTiIsImF1ZCI6IlRheEtlZXBWTiIsImV4cCI6MTg5MzQ1NjAwMH0.zArePIVKJr5fGvEHHWVLo7MiMQMjtFyzyEKe1ATtd_Q"
)

$headers = @{
    "Authorization" = "Bearer $Token"
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

$steps = @(
    @{ Name = "Bước 1 (VBHN 103)"; File = "buoc1_vbhn103.json"; Reason = "Bước 1: Luật TNCN hợp nhất" },
    @{ Name = "Bước 2 (NQ 954)";   File = "buoc2_nq954.json";   Reason = "Bước 2: Nâng mức giảm trừ năm 2020" },
    @{ Name = "Bước 3 (TT 111)";   File = "buoc3_tt111.json";   Reason = "Bước 3: Hướng dẫn Thông tư 111" },
    @{ Name = "Bước 4 (VBHN 112)"; File = "buoc4_vbhn112.json"; Reason = "Bước 4: Luật 109 sửa biểu thuế 5 bậc" }
)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "BAT DAU IMPORT VA MERGE BUOC 1 -> BUOC 4 VAO DATABASE" -ForegroundColor Cyan
Write-Host "BaseUrl: $BaseUrl" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

foreach ($step in $steps) {
    $filePath = Join-Path $scriptDir $step.File
    if (-not (Test-Path $filePath)) {
        Write-Error "Khong tim thay file: $filePath"
        exit 1
    }

    Write-Host "`nĐang xử lý $($step.Name)..." -ForegroundColor Yellow

    # 1. Import changeset
    $importUrl = "$BaseUrl/api/v1/admin/law/changesets/import"
    
    $fileBytes = [System.IO.File]::ReadAllBytes($filePath)
    $fileName = [System.IO.Path]::GetFileName($filePath)
    
    $boundary = [System.Guid]::NewGuid().ToString()
    $LF = "`r`n"
    
    $bodyLines = @(
        "--$boundary",
        "Content-Disposition: form-data; name=`"reason`"$LF",
        $step.Reason,
        "--$boundary",
        "Content-Disposition: form-data; name=`"file`"; filename=`"$fileName`"",
        "Content-Type: application/json$LF",
        [System.Text.Encoding]::UTF8.GetString($fileBytes),
        "--$boundary--$LF"
    ) -join "$LF"

    $contentType = "multipart/form-data; boundary=$boundary"

    try {
        $importResp = Invoke-RestMethod -Uri $importUrl -Method Post -Headers $headers -ContentType $contentType -Body $bodyLines
        $changesetId = $importResp.data.id
        Write-Host "  -> Đã tạo ChangesetId: $changesetId" -ForegroundColor Green
    } catch {
        Write-Error "Lỗi khi import $($step.Name): $($_.Exception.Message)"
        if ($_.Exception.Response) {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            Write-Host "Chi tiết: " $reader.ReadToEnd() -ForegroundColor Red
        }
        exit 1
    }

    # 2. Accept-all
    $acceptUrl = "$BaseUrl/api/v1/admin/law/changesets/$changesetId/accept-all"
    try {
        $null = Invoke-RestMethod -Uri $acceptUrl -Method Post -Headers $headers
        Write-Host "  -> Đã duyệt toàn bộ dòng (Accept-all)" -ForegroundColor Green
    } catch {
        Write-Error "Lỗi khi accept-all: $($_.Exception.Message)"
        exit 1
    }

    # 3. Merge
    $mergeUrl = "$BaseUrl/api/v1/admin/law/changesets/$changesetId/merge"
    try {
        $mergeResp = Invoke-RestMethod -Uri $mergeUrl -Method Post -Headers $headers
        $revNo = $mergeResp.data.revisionNo
        Write-Host "  -> MERGE THÀNH CÔNG! Đã tạo Revision: $revNo" -ForegroundColor Cyan
    } catch {
        Write-Error "Lỗi khi merge: $($_.Exception.Message)"
        if ($_.Exception.Response) {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            Write-Host "Chi tiết: " $reader.ReadToEnd() -ForegroundColor Red
        }
        exit 1
    }
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "HOAN TAT! DATABASE DA CO DU LIEU TU REVISION 1 DEN 4." -ForegroundColor Green
Write-Host "Bay gio ban co the upload PDF ND 253 qua Swagger de test AI!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
