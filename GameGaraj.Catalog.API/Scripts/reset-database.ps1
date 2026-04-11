# PowerShell script to reset MongoDB database
# Usage: .\reset-database.ps1

Write-Host "Resetting MongoDB Catalog Database..." -ForegroundColor Cyan

# Check if mongosh is available
$mongoshPath = Get-Command mongosh -ErrorAction SilentlyContinue

if (-not $mongoshPath) {
    Write-Host "ERROR: mongosh not found. Please install MongoDB Shell." -ForegroundColor Red
    Write-Host "Download from: https://www.mongodb.com/try/download/shell" -ForegroundColor Yellow
    exit 1
}

# MongoDB connection string (adjust if needed)
$mongoUri = "mongodb://localhost:27017"
$database = "catalogdb"

Write-Host "Connecting to MongoDB at $mongoUri..." -ForegroundColor Yellow

# Create temporary JavaScript file
$tempScript = [System.IO.Path]::GetTempFileName() + ".js"
$jsContent = @"
use $database;
db.products.drop();
db.categories.drop();
db.categoryAttributes.drop();
db._seed_metadata.drop();
print('Collections dropped successfully!');
"@

Set-Content -Path $tempScript -Value $jsContent -Encoding UTF8

# Execute the script
mongosh $mongoUri --quiet --file $tempScript

# Clean up
Remove-Item $tempScript -ErrorAction SilentlyContinue

if ($LASTEXITCODE -eq 0) {
    Write-Host "SUCCESS: Database reset successfully!" -ForegroundColor Green
    Write-Host "Now restart GameGaraj.Catalog.API to re-seed the database." -ForegroundColor Cyan
} else {
    Write-Host "ERROR: Failed to reset database." -ForegroundColor Red
    exit 1
}
