New-Item Releases -type directory -force

$wc = New-Object System.Net.WebClient

$wc.DownloadFile("https://s3.amazonaws.com/espera/Releases/Dev/RELEASES", ".\Releases/RELEASES");

Get-Content ".\Releases\RELEASES" | ForEach-Object {
    $_ -match ".* (.*) .*" >$null
    $filename = $matches[1]

    $wc.DownloadFile("https://s3.amazonaws.com/espera/Releases/Dev/" + $filename, ".\Releases/" + $filename);

    "Downloaded " + $filename
}