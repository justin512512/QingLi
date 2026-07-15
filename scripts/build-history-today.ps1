param(
    [Parameter(Mandatory = $true)]
    [string] $OutputPath,

    [int] $MaxEntriesPerDay = 10,

    [int] $BatchSize = 1
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http

if ($MaxEntriesPerDay -lt 1 -or $MaxEntriesPerDay -gt 10) {
    throw 'MaxEntriesPerDay must be between 1 and 10.'
}

if ($BatchSize -lt 1 -or $BatchSize -gt 16) {
    throw 'BatchSize must be between 1 and 16.'
}

$client = [System.Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(45)
$client.DefaultRequestHeaders.UserAgent.ParseAdd('QingLiCalendar/0.1 (offline history data builder)')

try {
    $dates = 0..365 | ForEach-Object { [DateTime]::new(2024, 1, 1).AddDays($_) }
    $days = [ordered]@{}

    for ($offset = 0; $offset -lt $dates.Count; $offset += $BatchSize) {
        $last = [Math]::Min($offset + $BatchSize - 1, $dates.Count - 1)
        $batch = $dates[$offset..$last]
        $attempt = 0
        do {
            $attempt++
            $requests = [ordered]@{}

            foreach ($date in $batch) {
                $key = $date.ToString('MM-dd', [Globalization.CultureInfo]::InvariantCulture)
                $pathDate = $date.ToString('yyyy/MM/dd', [Globalization.CultureInfo]::InvariantCulture)
                $uri = "https://zh.wikipedia.org/api/rest_v1/feed/featured/$pathDate"
                $requests[$key] = $client.GetStringAsync($uri)
            }

            try {
                [System.Threading.Tasks.Task]::WaitAll([System.Threading.Tasks.Task[]]$requests.Values)
                $downloaded = $true
            }
            catch {
                $downloaded = $false
                $failed = $requests.GetEnumerator() | Where-Object { $_.Value.IsFaulted } | Select-Object -First 1
                if ($attempt -ge 6) {
                    if ($null -ne $failed) {
                        throw "Download failed for $($failed.Key): $($failed.Value.Exception.GetBaseException().Message)"
                    }

                    throw
                }

                Start-Sleep -Seconds ([Math]::Pow(2, $attempt))
            }
        } while (-not $downloaded)

        foreach ($key in $requests.Keys) {
            $response = $requests[$key].Result | ConvertFrom-Json
            $entries = @($response.onthisday | Select-Object -First $MaxEntriesPerDay | ForEach-Object {
                $page = @($_.pages | Where-Object { $_.content_urls.desktop.page -like 'https://*' } | Select-Object -First 1)[0]
                $sourceUrl = if ($null -ne $page) {
                    $page.content_urls.desktop.page
                } else {
                    "https://zh.wikipedia.org/wiki/$($_.year)%E5%B9%B4"
                }

                [ordered]@{
                    year = [int]$_.year
                    summary = ([string]$_.text).Trim()
                    sourceName = 'Wikipedia'
                    sourceUrl = [string]$sourceUrl
                }
            })

            $days[$key] = $entries
        }

        Write-Progress -Activity 'Building history-today snapshot' -Status "$($last + 1) / $($dates.Count) days" -PercentComplete ((($last + 1) / $dates.Count) * 100)
        Start-Sleep -Milliseconds 350
    }

    $package = [ordered]@{
        version = [DateTimeOffset]::UtcNow.ToString('yyyy.MM.dd')
        generatedAt = [DateTimeOffset]::UtcNow.ToString('O')
        license = 'CC BY-SA 4.0'
        sourceName = 'Wikipedia'
        sourceUrl = 'https://zh.wikipedia.org/'
        days = $days
    }

    $fullPath = [IO.Path]::GetFullPath($OutputPath)
    $directory = [IO.Path]::GetDirectoryName($fullPath)
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    $utf8WithoutBom = [Text.UTF8Encoding]::new($false)
    [IO.File]::WriteAllText($fullPath, ($package | ConvertTo-Json -Depth 8), $utf8WithoutBom)
    Write-Output $fullPath
}
finally {
    $client.Dispose()
}
