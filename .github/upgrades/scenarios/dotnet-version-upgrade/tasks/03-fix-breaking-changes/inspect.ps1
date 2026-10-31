param()
Add-Type -AssemblyName System.Reflection.Metadata
function Get-MD($path) {
  $pe = New-Object System.Reflection.PortableExecutable.PEReader([IO.MemoryStream]::new([IO.File]::ReadAllBytes($path)))
  return [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
}
$names = 'QualifiedNameCollection','DataValueCollection','CertificateValidator','CertificateValidationEventArgs','CertificateValidationEventHandler','VariantCollection','BrowseDescriptionCollection','ReferenceDescriptionCollection','SimpleAttributeOperandCollection'
$out = @()
foreach ($d in Get-ChildItem "$env:USERPROFILE\.nuget\packages\opcfoundation.netstandard.opc.ua.*\2.0.1\lib\net10.0\*.dll") {
  $md = Get-MD $d.FullName
  foreach ($h in $md.TypeDefinitions) { $t = $md.GetTypeDefinition($h); $n = $md.GetString($t.Name)
	if ($names -contains $n) { $out += "$n -> $($md.GetString($t.Namespace)) [$($d.Name)]" } }
}
$out | Sort-Object -Unique
"--- C-DEngine 7"
$md = Get-MD "$env:USERPROFILE\.nuget\packages\c-dengine\7.110.1\lib\net10.0\C-DEngine.dll"
$out = @()
foreach ($h in $md.TypeDefinitions) { $t=$md.GetTypeDefinition($h); $n=$md.GetString($t.Name)
  if ($n -in 'TheCDEngines','TheCommonUtils') {
	foreach ($mh in $t.GetMethods()) { $mn=$md.GetString($md.GetMethodDefinition($mh).Name); if ($mn -match 'Mono|Platform|Linux|Windows|OnPlatform|IsHost') { $out += "$n.$mn()" } }
	foreach ($eh in $t.GetEvents()) { $out += "$n event $($md.GetString($md.GetEventDefinition($eh).Name))" }
	foreach ($fh in $t.GetFields()) { $fn=$md.GetString($md.GetFieldDefinition($fh).Name); if ($fn -match '^event|Started') { $out += "$n field $fn" } }
  } }
$out | Sort-Object -Unique
