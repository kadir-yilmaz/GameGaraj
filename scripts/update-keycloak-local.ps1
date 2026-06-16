param(
    [string]$EnvFile = ".env",
    [string]$KeycloakBaseUrl = "http://localhost:8080",
    [string]$Realm = "GameGaraj",
    [switch]$SkipGoogleProvider
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-EnvMap {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Env file not found: $Path"
    }

    $map = @{}
    foreach ($line in Get-Content -LiteralPath $Path) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith("#")) {
            continue
        }

        $separatorIndex = $trimmed.IndexOf("=")
        if ($separatorIndex -lt 1) {
            continue
        }

        $key = $trimmed.Substring(0, $separatorIndex).Trim()
        $value = $trimmed.Substring($separatorIndex + 1).Trim()
        $map[$key] = $value
    }

    return $map
}

function Get-RequiredValue {
    param(
        [hashtable]$Map,
        [string]$Key
    )

    if (-not $Map.ContainsKey($Key) -or [string]::IsNullOrWhiteSpace($Map[$Key])) {
        throw "Missing required key in env file: $Key"
    }

    return $Map[$Key]
}

function Invoke-KeycloakJson {
    param(
        [string]$Method,
        [string]$Uri,
        [string]$Token,
        $Body = $null
    )

    $headers = @{
        Authorization = "Bearer $Token"
    }

    $params = @{
        Method      = $Method
        Uri         = $Uri
        Headers     = $headers
        ContentType = "application/json"
    }

    if ($null -ne $Body) {
        $params.Body = ($Body | ConvertTo-Json -Depth 20)
    }

    return Invoke-RestMethod @params
}

$envMap = Get-EnvMap -Path $EnvFile

$adminUsername = if ($envMap.ContainsKey("KEYCLOAK_ADMIN")) { $envMap["KEYCLOAK_ADMIN"] } else { $envMap["KEYCLOAK_ADMIN_USERNAME"] }
$adminPassword = if ($envMap.ContainsKey("KEYCLOAK_ADMIN_PASSWORD")) { $envMap["KEYCLOAK_ADMIN_PASSWORD"] } else { $null }

if ([string]::IsNullOrWhiteSpace($adminUsername)) {
    throw "Missing KEYCLOAK_ADMIN or KEYCLOAK_ADMIN_USERNAME in env file."
}

if ([string]::IsNullOrWhiteSpace($adminPassword)) {
    throw "Missing KEYCLOAK_ADMIN_PASSWORD in env file."
}

$tokenResponse = Invoke-RestMethod -Method Post -Uri "$KeycloakBaseUrl/realms/master/protocol/openid-connect/token" -ContentType "application/x-www-form-urlencoded" -Body @{
    client_id  = "admin-cli"
    grant_type = "password"
    username   = $adminUsername
    password   = $adminPassword
}

if ([string]::IsNullOrWhiteSpace($tokenResponse.access_token)) {
    throw "Failed to get Keycloak admin access token."
}

$token = $tokenResponse.access_token
$realmAdminBase = "$KeycloakBaseUrl/admin/realms/$Realm"

$client = Invoke-KeycloakJson -Method Get -Uri "$realmAdminBase/clients?clientId=web-ui" -Token $token | Select-Object -First 1
if ($null -eq $client) {
    throw "Client 'web-ui' not found in realm '$Realm'."
}

$clientDetails = Invoke-KeycloakJson -Method Get -Uri "$realmAdminBase/clients/$($client.id)" -Token $token
$clientDetails.standardFlowEnabled = $true
$clientDetails.directAccessGrantsEnabled = $true
$clientDetails.publicClient = $true

Invoke-KeycloakJson -Method Put -Uri "$realmAdminBase/clients/$($client.id)" -Token $token -Body $clientDetails | Out-Null
Write-Host "Updated client 'web-ui': standard flow enabled."

if (-not $SkipGoogleProvider) {
    $googleClientId = Get-RequiredValue -Map $envMap -Key "GOOGLE_CLIENT_ID"
    $googleClientSecret = Get-RequiredValue -Map $envMap -Key "GOOGLE_CLIENT_SECRET"

    $providerBody = @{
        alias                      = "google"
        displayName                = "Google"
        providerId                 = "google"
        enabled                    = $true
        updateProfileFirstLoginMode = "on"
        trustEmail                 = $true
        storeToken                 = $false
        addReadTokenRoleOnCreate   = $false
        authenticateByDefault      = $false
        linkOnly                   = $false
        firstBrokerLoginFlowAlias  = "first broker login"
        config                     = @{
            syncMode       = "FORCE"
            clientId       = $googleClientId
            clientSecret   = $googleClientSecret
            defaultScope   = "openid profile email"
            prompt         = "select_account"
            useJwksUrl     = "true"
        }
    }

    $providerExists = $true
    try {
        Invoke-KeycloakJson -Method Get -Uri "$realmAdminBase/identity-provider/instances/google" -Token $token | Out-Null
    }
    catch {
        $providerExists = $false
    }

    if ($providerExists) {
        Invoke-KeycloakJson -Method Put -Uri "$realmAdminBase/identity-provider/instances/google" -Token $token -Body $providerBody | Out-Null
        Write-Host "Updated Google identity provider."
    }
    else {
        Invoke-KeycloakJson -Method Post -Uri "$realmAdminBase/identity-provider/instances" -Token $token -Body $providerBody | Out-Null
        Write-Host "Created Google identity provider."
    }
}

Write-Host "Keycloak local update completed."
