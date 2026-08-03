# ==========================================
# Unity Git Remote Backup Script
# 目的: 現在のブランチの変更をコミットし、リモートに送信してバックアップします。
# ==========================================

$today = (Get-Date -Format "yyyy/MM/dd")

Write-Host "=== リモートバックアップを開始します ($today) ===" -ForegroundColor Cyan

if (-not (Test-Path ".git")) {
    Write-Host "[エラー] このフォルダはGitリポジトリではありません。" -ForegroundColor Red
    exit
}

$currentBranch = git rev-parse --abbrev-ref HEAD
Write-Host "--- 現在のブランチ: $currentBranch"

# 1. 変更をステージング
Write-Host "--- 1. 変更をステージングしています..."
git add .

# 2. コミット（変更がある場合のみ）
$changes = git diff --staged --quiet
if ($LASTEXITCODE -eq 0) {
    Write-Host "--- 2. コミットする変更はありません。リモートとの同期に進みます。"
} else {
    $commitMessage = "Backup: $today"
    Write-Host "--- 2. 変更をコミットしています..."
    git commit -m $commitMessage
    Write-Host "--- コミット完了: `"$commitMessage`""
}

# 3. リモートの変更を取り込む
Write-Host "--- 3. リモートリポジトリの変更を取り込んでいます (pull)..."
$remoteBranchExists = git ls-remote --heads origin $currentBranch
if ($remoteBranchExists) {
    git pull --rebase origin $currentBranch
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[エラー] リモートの変更の取り込みに失敗しました。コンフリクトを解決してください。" -ForegroundColor Red
        exit
    }
} else {
    Write-Host "--- リモートにブランチ '$currentBranch' がまだ存在しないため、pull をスキップします。"
}

# 4. リモートにプッシュ
Write-Host "--- 4. リモートリポジトリに変更を送信しています (push)..."
git push -u origin $currentBranch
if ($LASTEXITCODE -ne 0) {
    Write-Host "[エラー] リモートへのプッシュに失敗しました。" -ForegroundColor Red
    exit
}

Write-Host "=== リモートバックアップが正常に完了しました ===" -ForegroundColor Cyan
