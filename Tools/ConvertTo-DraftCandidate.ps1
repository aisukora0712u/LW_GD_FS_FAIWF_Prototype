function ConvertTo-DraftCandidate {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Text,
        [Parameter(Mandatory)][ValidateSet('contentVersion', 'localizationVersion')][string]$VersionField,
        [Parameter(Mandatory)][string]$BaseVersion,
        [Parameter(Mandatory)][string]$TargetVersion
    )
    if ([string]::IsNullOrWhiteSpace($TargetVersion) -or $TargetVersion -ceq $BaseVersion) {
        throw 'Target version must be nonempty and different from the base version.'
    }
    $document = [System.Text.Json.JsonDocument]::Parse($Text)
    $stream = $null
    $writer = $null
    try {
        $root = $document.RootElement
        if ($root.ValueKind -ne [System.Text.Json.JsonValueKind]::Object) { throw 'Candidate source must be an object.' }
        $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($property in $root.EnumerateObject()) {
            if (-not $names.Add($property.Name)) { throw "Duplicate source field: $($property.Name)" }
        }
        if (-not $names.Contains($VersionField) -or -not $names.Contains('status')) { throw 'Missing candidate metadata.' }
        if ($root.GetProperty($VersionField).GetString() -cne $BaseVersion) { throw 'Source version does not match the active base.' }
        if ($root.GetProperty('status').GetString() -cne 'approved') { throw 'Source status must be approved.' }

        $stream = [IO.MemoryStream]::new()
        $options = [System.Text.Json.JsonWriterOptions]::new()
        $options.Indented = $true
        $writer = [System.Text.Json.Utf8JsonWriter]::new($stream, $options)
        $writer.WriteStartObject()
        foreach ($property in $root.EnumerateObject()) {
            if ($property.Name -ceq $VersionField) {
                $writer.WriteString($property.Name, $TargetVersion)
            } elseif ($property.Name -ceq 'status') {
                $writer.WriteString($property.Name, 'draft')
            } else {
                $property.WriteTo($writer)
            }
        }
        $writer.WriteEndObject()
        $writer.Flush()
        return [Text.Encoding]::UTF8.GetString($stream.ToArray())
    } finally {
        if ($null -ne $writer) { $writer.Dispose() }
        if ($null -ne $stream) { $stream.Dispose() }
        $document.Dispose()
    }
}
