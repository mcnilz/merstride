# Reuse the reviewed production parser assembly; policy parsing runs only during development.
$packageRoot = (& dotnet msbuild "$PSScriptRoot/../src/Merstride/Merstride.csproj" -nologo -getProperty:NuGetPackageRoot | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot locate restored YamlDotNet package; run locked restore first.' }
$yamlAssembly = Join-Path $packageRoot 'yamldotnet/18.1.0/lib/netstandard2.1/YamlDotNet.dll'
[Reflection.Assembly]::LoadFrom($yamlAssembly) | Out-Null
function Convert-YamlNode($Node) {
    if ($Node -is [YamlDotNet.RepresentationModel.YamlMappingNode]) {
        $map = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
        foreach ($pair in $Node.Children.GetEnumerator()) { $map.Add($pair.Key.Value, (Convert-YamlNode $pair.Value)) }
        return ,$map
    }
    if ($Node -is [YamlDotNet.RepresentationModel.YamlSequenceNode]) {
        $items = @($Node.Children | ForEach-Object { Convert-YamlNode $_ })
        return ,$items
    }
    if ($Node -is [YamlDotNet.RepresentationModel.YamlScalarNode]) { return $Node.Value }
    throw 'Unsupported workflow YAML node.'
}
function Read-WorkflowYaml([string]$Text) {
    $yaml = [YamlDotNet.RepresentationModel.YamlStream]::new()
    $yaml.Load([IO.StringReader]::new($Text))
    if ($yaml.Documents.Count -ne 1) { throw 'Expected one workflow YAML document.' }
    return Convert-YamlNode $yaml.Documents[0].RootNode
}
