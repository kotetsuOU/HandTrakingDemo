# Git リポジトリ URL
$repoUrl = "https://github.com/kotetsuOU/HandTrakingDemo"
# ローカルに置くフォルダ
$localFolder = "C:\Users\hongo\Documents\tsutsumi\HandTrackingDemo_ver0.2"

# 現在の日付（YYYY/MM/DD）
$today = (Get-Date -Format "yyyy/MM/dd")
Write-Host "=== Git 更新開始 ($today) ==="

# フォルダが存在するかチェック
if (-not (Test-Path "$localFolder\.git")) {
    Write-Host "[Info] Gitリポジトリが存在しません。クローンを作成します..."
    git clone $repoUrl $localFolder
} else {
    Write-Host "[Info] Gitリポジトリを確認済み。更新します..."
    Set-Location $localFolder

    # main ブランチにチェックアウト
    git checkout main

    # ローカルの変更をコミット
    git add .
    git commit -m "Update: $today" 2>$null

    # リモート変更を取り込み（rebase）
    git pull --rebase origin main

    # プッシュ
    git push origin main
}

Write-Host "=== Git 更新完了 ($today) ==="
