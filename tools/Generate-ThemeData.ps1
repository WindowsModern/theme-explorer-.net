<#
.SYNOPSIS
    Generates ThemeData.Generated.cs from the original C implementation's db.c.

.DESCRIPTION
    The original Theme Explorer keeps its visual-style database in a hand maintained
    C file (src/db.c).  Porting those tables by hand would be error prone, so this
    script parses the C macros and emits equivalent C# object initialisers.

    The generated file is checked in; run this script again only when the source
    database changes.

.PARAMETER Source
    Path to the original db.c.

.PARAMETER Destination
    Path of the C# file to write.
#>
[CmdletBinding()]
param(
    [string] $Source = (Join-Path $PSScriptRoot '..\..\theme-explorer\src\db.c'),
    [string] $Destination = (Join-Path $PSScriptRoot '..\src\ThemeExplorer.Core\ThemeData.Generated.cs')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$sourcePath = (Resolve-Path -LiteralPath $Source).Path
$destPath = [IO.Path]::GetFullPath($Destination)
$text = [IO.File]::ReadAllText($sourcePath)

$options = [Text.RegularExpressions.RegexOptions]::Singleline

# ---------------------------------------------------------------------------
# States:  static const db_state_t db_states_XXX[] = { DB_DEFINE_STATE(...) };
# ---------------------------------------------------------------------------
$states = New-Object 'System.Collections.Generic.List[object]'
$stateArrayPattern = 'static\s+const\s+db_state_t\s+(?<field>\w+)\[\]\s*=\s*\{(?<body>.*?)\n\};'
foreach ($m in [regex]::Matches($text, $stateArrayPattern, $options)) {
    $field = $m.Groups['field'].Value
    $items = New-Object 'System.Collections.Generic.List[object]'
    foreach ($s in [regex]::Matches($m.Groups['body'].Value,
            'DB_DEFINE_STATE\s*\(\s*(?<id>-?\d+)\s*,\s*(?<name>\w+)\s*,\s*(?<display>\w+)\s*\)')) {
        $items.Add([pscustomobject]@{
            Id      = [int] $s.Groups['id'].Value
            Name    = $s.Groups['name'].Value
            Display = $s.Groups['display'].Value
        })
    }
    if ($items.Count -eq 0) { throw "No states parsed from $field" }
    $states.Add([pscustomobject]@{ Field = $field; Items = $items })
}

# ---------------------------------------------------------------------------
# Parts:   static const db_part_t db_parts_XXX[] = { DB_DEFINE_PART(...), ... };
# ---------------------------------------------------------------------------
$parts = New-Object 'System.Collections.Generic.List[object]'
$partArrayPattern = 'static\s+const\s+db_part_t\s+(?<field>\w+)\[\]\s*=\s*\{(?<body>.*?)\n\};'
foreach ($m in [regex]::Matches($text, $partArrayPattern, $options)) {
    $field = $m.Groups['field'].Value
    $items = New-Object 'System.Collections.Generic.List[object]'
    foreach ($p in [regex]::Matches($m.Groups['body'].Value,
            'DB_DEFINE_PART(?<nostates>_)?\s*\(\s*(?<id>\d+)\s*,\s*(?<name>\w+)\s*,\s*(?<display>\w+)(?:\s*,\s*(?<states>\w+))?\s*\)')) {
        $stateField = $null
        if (-not $p.Groups['nostates'].Success) {
            $stateField = $p.Groups['states'].Value
            if ([string]::IsNullOrEmpty($stateField)) { throw "Part without states in $field" }
        }
        $items.Add([pscustomobject]@{
            Id      = [int] $p.Groups['id'].Value
            Name    = $p.Groups['name'].Value
            Display = $p.Groups['display'].Value
            States  = $stateField
        })
    }
    if ($items.Count -eq 0) { throw "No parts parsed from $field" }
    $parts.Add([pscustomobject]@{ Field = $field; Items = $items })
}

# ---------------------------------------------------------------------------
# Classes: the db_classes_helper[] table.
# ---------------------------------------------------------------------------
$classes = New-Object 'System.Collections.Generic.List[object]'
$classBlock = [regex]::Match($text,
    'static\s+const\s+db_class_t\s+db_classes_helper\[\]\s*=\s*\{(?<body>.*?)\n\};', $options)
if (-not $classBlock.Success) { throw 'db_classes_helper[] not found' }

foreach ($line in ($classBlock.Groups['body'].Value -split "`r?`n")) {
    $line = $line.Trim()
    if ($line.Length -eq 0 -or $line.StartsWith('/*')) { continue }

    $m = [regex]::Match($line, '^DB_DEFINE_(?<kind>CLASS|SUBCLASS)\s*\(\s*(?<name>\w+)\s*,\s*(?<rest>.+?)\s*\)\s*,?\s*$')
    if (-not $m.Success) { continue }

    $rest = @($m.Groups['rest'].Value -split ',' | ForEach-Object { $_.Trim() })
    if ($m.Groups['kind'].Value -eq 'CLASS') {
        $classes.Add([pscustomobject]@{ Name = $m.Groups['name'].Value; SubClass = $null; Parts = $rest[0] })
    } else {
        $classes.Add([pscustomobject]@{ Name = $m.Groups['name'].Value; SubClass = $rest[0]; Parts = $rest[1] })
    }
}
if ($classes.Count -eq 0) { throw 'No classes parsed' }

# ---------------------------------------------------------------------------
# Properties: db_props_helper[] table.
# ---------------------------------------------------------------------------
$properties = New-Object 'System.Collections.Generic.List[object]'
$symbolicIds = @{ TS_MIN = 0; TS_TRUE = 1; TS_DRAW = 2 }
$typeNames = @{
    ENUM = 'Enum'; STRING = 'String'; INT = 'Int'; BOOL = 'Bool'; COLOR = 'Color'
    MARGINS = 'Margins'; FILENAME = 'Filename'; SIZE = 'Size'; POSITION = 'Position'
    RECT = 'Rect'; FONT = 'Font'; INTLIST = 'IntList'; DISKSTREAM = 'DiskStream'; STREAM = 'Stream'
}

$propBlock = [regex]::Match($text,
    'static\s+const\s+db_prop_t\s+db_props_helper\[\]\s*=\s*\{(?<body>.*?)\n\};', $options)
if (-not $propBlock.Success) { throw 'db_props_helper[] not found' }

$propPattern = 'DB_DEFINE_(?<type>ENUM|STRING|INT|BOOL|COLOR|MARGINS|FILENAME|SIZE|POSITION|RECT|FONT|INTLIST|DISKSTREAM|STREAM)\s*\(\s*(?<id>\w+)\s*,\s*(?<name>\w+)\s*,\s*(?<display>\w+)\s*\)'
foreach ($m in [regex]::Matches($propBlock.Groups['body'].Value, $propPattern)) {
    $idToken = $m.Groups['id'].Value
    $id = if ($idToken -match '^\d+$') { [int] $idToken } elseif ($symbolicIds.ContainsKey($idToken)) { [int] $symbolicIds[$idToken] } else { throw "Unknown property id token: $idToken" }
    $properties.Add([pscustomobject]@{
        Type    = $typeNames[$m.Groups['type'].Value]
        Id      = $id
        Name    = $m.Groups['name'].Value
        Display = $m.Groups['display'].Value
    })
}
if ($properties.Count -eq 0) { throw 'No properties parsed' }

# ---------------------------------------------------------------------------
# Emit C#.
# ---------------------------------------------------------------------------
$sb = New-Object Text.StringBuilder
[void] $sb.AppendLine('// <auto-generated>')
[void] $sb.AppendLine('//     Generated by tools/Generate-ThemeData.ps1 from the original theme-explorer/src/db.c.')
[void] $sb.AppendLine('//     Do not edit this file by hand; rerun the generator instead.')
[void] $sb.AppendLine('// </auto-generated>')
[void] $sb.AppendLine()
[void] $sb.AppendLine('using System.Collections.Generic;')
[void] $sb.AppendLine()
[void] $sb.AppendLine('namespace ThemeExplorer.Core')
[void] $sb.AppendLine('{')
[void] $sb.AppendLine('    internal static class ThemeData')
[void] $sb.AppendLine('    {')

[void] $sb.AppendLine('        // ------------------------------------------------------------------')
[void] $sb.AppendLine('        // States')
[void] $sb.AppendLine('        // ------------------------------------------------------------------')
foreach ($array in $states) {
    $field = 'States_' + $array.Field.Substring('db_states_'.Length)
    [void] $sb.AppendLine(('        internal static readonly ThemeStateInfo[] {0} =' -f $field))
    [void] $sb.AppendLine('        {')
    foreach ($item in $array.Items) {
        [void] $sb.AppendLine(('            new ThemeStateInfo({0}, "{1}", "{2}"),' -f $item.Id, $item.Name, $item.Display))
    }
    [void] $sb.AppendLine('        };')
    [void] $sb.AppendLine()
}

[void] $sb.AppendLine('        // ------------------------------------------------------------------')
[void] $sb.AppendLine('        // Parts')
[void] $sb.AppendLine('        // ------------------------------------------------------------------')
foreach ($array in $parts) {
    $field = 'Parts_' + $array.Field.Substring('db_parts_'.Length)
    [void] $sb.AppendLine(('        internal static readonly ThemePartInfo[] {0} =' -f $field))
    [void] $sb.AppendLine('        {')
    foreach ($item in $array.Items) {
        if ([string]::IsNullOrEmpty($item.States)) {
            [void] $sb.AppendLine(('            new ThemePartInfo({0}, "{1}", "{2}", null),' -f $item.Id, $item.Name, $item.Display))
        } else {
            $statesField = 'States_' + $item.States.Substring('db_states_'.Length)
            [void] $sb.AppendLine(('            new ThemePartInfo({0}, "{1}", "{2}", {3}),' -f $item.Id, $item.Name, $item.Display, $statesField))
        }
    }
    [void] $sb.AppendLine('        };')
    [void] $sb.AppendLine()
}

[void] $sb.AppendLine('        // ------------------------------------------------------------------')
[void] $sb.AppendLine('        // Visual style classes (including window subclasses).')
[void] $sb.AppendLine('        // ------------------------------------------------------------------')
[void] $sb.AppendLine('        internal static readonly ThemeClassInfo[] Classes =')
[void] $sb.AppendLine('        {')
foreach ($cls in $classes) {
    $partsField = 'Parts_' + $cls.Parts.Substring('db_parts_'.Length)
    if ([string]::IsNullOrEmpty($cls.SubClass)) {
        [void] $sb.AppendLine(('            new ThemeClassInfo("{0}", null, {1}),' -f $cls.Name, $partsField))
    } else {
        [void] $sb.AppendLine(('            new ThemeClassInfo("{0}", "{1}", {2}),' -f $cls.Name, $cls.SubClass, $partsField))
    }
}
[void] $sb.AppendLine('        };')
[void] $sb.AppendLine()

[void] $sb.AppendLine('        // ------------------------------------------------------------------')
[void] $sb.AppendLine('        // Theme properties (TMT_*).')
[void] $sb.AppendLine('        // ------------------------------------------------------------------')
[void] $sb.AppendLine('        internal static readonly ThemePropertyInfo[] Properties =')
[void] $sb.AppendLine('        {')
foreach ($prop in $properties) {
    [void] $sb.AppendLine(('            new ThemePropertyInfo(ThemePropertyType.{0}, {1}, "{2}", "{3}"),' -f $prop.Type, $prop.Id, $prop.Name, $prop.Display))
}
[void] $sb.AppendLine('        };')

[void] $sb.AppendLine('    }')
[void] $sb.AppendLine('}')

$utf8 = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllText($destPath, $sb.ToString(), $utf8)

Write-Host ("Generated {0}" -f $destPath)
Write-Host ("  classes: {0} (subclasses: {1})" -f $classes.Count, @($classes | Where-Object { $_.SubClass }).Count)
Write-Host ("  parts:   {0}" -f (($parts | ForEach-Object { $_.Items.Count }) | Measure-Object -Sum).Sum)
Write-Host ("  states:  {0}" -f (($states | ForEach-Object { $_.Items.Count }) | Measure-Object -Sum).Sum)
Write-Host ("  props:   {0}" -f $properties.Count)
