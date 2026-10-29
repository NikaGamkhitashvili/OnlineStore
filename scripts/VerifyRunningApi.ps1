param([string]$BaseUrl = 'https://localhost:7156')
$ErrorActionPreference = 'Stop'

$specResponse = Invoke-WebRequest "$BaseUrl/swagger/v1/swagger.json"
$spec = $specResponse.Content | ConvertFrom-Json
$operationCount = 0
foreach ($path in $spec.paths.PSObject.Properties) {
    $operationCount += @($path.Value.PSObject.Properties).Count
}
if ($operationCount -ne 21) { throw "Expected 20 mandatory operations plus exchange rates; found $operationCount" }

$counts = @{}
foreach ($resource in @('categories', 'products', 'customers', 'orders')) {
    $items = Invoke-RestMethod "$BaseUrl/api/$resource"
    if ($items.Count -lt 1) { throw "No demonstration data in $resource" }
    $detail = Invoke-RestMethod "$BaseUrl/api/$resource/$($items[0].id)"
    if ($detail.id -ne $items[0].id) { throw "Detail mismatch for $resource" }
    $counts[$resource] = $items.Count
}

$invalid = Invoke-WebRequest "$BaseUrl/api/categories" -Method Post -ContentType 'application/json' -Body '{"name":" "}' -SkipHttpErrorCheck
if ($invalid.StatusCode -ne 400) { throw 'Invalid request should return 400' }
$missing = Invoke-WebRequest "$BaseUrl/api/products/2147483647" -SkipHttpErrorCheck
if ($missing.StatusCode -ne 404) { throw 'Missing product should return 404' }

foreach ($asset in @('index.html', 'swagger-ui-bundle.js', 'swagger-ui.css')) {
    if ((Invoke-WebRequest "$BaseUrl/swagger/$asset").StatusCode -ne 200) { throw "Swagger asset failed: $asset" }
}
$rateResponse = Invoke-WebRequest "$BaseUrl/api/exchange-rates?baseCurrency=USD&targetCurrency=GEL" -SkipHttpErrorCheck
if ($rateResponse.StatusCode -ne 200) { throw "Real exchange-rate request failed: $($rateResponse.StatusCode) $($rateResponse.Content)" }
$rate = $rateResponse.Content | ConvertFrom-Json
if ($rate.rate -le 0 -or $rate.baseCurrency -ne 'USD' -or $rate.targetCurrency -ne 'GEL') { throw 'Invalid real exchange-rate response' }

$report = [ordered]@{
    verifiedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    baseUrl = $BaseUrl
    openApiOperations = $operationCount
    swaggerAssets = 'PASS'
    listAndDetailEndpoints = 'PASS'
    sampleDataCounts = $counts
    invalidInputStatus = [int]$invalid.StatusCode
    notFoundStatus = [int]$missing.StatusCode
    realExternalApiStatus = [int]$rateResponse.StatusCode
    exchangeRate = $rate
    note = 'Read-only checks plus rejected invalid input. All 20 CRUD operations are tested separately on isolated SQL Server databases.'
}
$reportDirectory = Join-Path $PSScriptRoot '..\verification'
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
$report | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $reportDirectory 'LiveVerification.json') -Encoding utf8
$report | ConvertTo-Json -Depth 5
