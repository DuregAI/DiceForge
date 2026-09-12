$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$outputDir = Join-Path $repo 'Assets/_Project/07_Art/UI/GlimblehopMenu'
$sourceDir = Join-Path $PSScriptRoot 'Sources'
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$manifest = [Collections.Generic.List[object]]::new()
function Export-Crop($bitmap, $name, $rect) {
    $left=$rect.Right; $top=$rect.Bottom; $right=-1; $bottom=-1
    for($y=$rect.Top; $y -lt $rect.Bottom; $y++) {
        for($x=$rect.Left; $x -lt $rect.Right; $x++) {
            if($bitmap.GetPixel($x,$y).A -gt 8) {
                $left=[Math]::Min($left,$x); $top=[Math]::Min($top,$y)
                $right=[Math]::Max($right,$x); $bottom=[Math]::Max($bottom,$y)
            }
        }
    }
    if($right -lt 0){throw "Empty slice: $name"}
    $left=[Math]::Max($rect.Left,$left-4); $top=[Math]::Max($rect.Top,$top-4)
    $right=[Math]::Min($rect.Right-1,$right+4); $bottom=[Math]::Min($rect.Bottom-1,$bottom+4)
    $crop=[Drawing.Rectangle]::new($left,$top,$right-$left+1,$bottom-$top+1)
    $slice=$bitmap.Clone($crop,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        if($slice.GetPixel(0,0).A -ne 0){throw "Nontransparent corner: $name"}
        $slice.Save((Join-Path $outputDir "$name.png"),[Drawing.Imaging.ImageFormat]::Png)
        $manifest.Add([pscustomobject]@{file="$name.png";width=$slice.Width;height=$slice.Height;sourceRect=@($left,$top,$crop.Width,$crop.Height);transparent=$true})
    } finally {$slice.Dispose()}
}
$sheet=[Drawing.Bitmap]::new((Join-Path $sourceDir 'ui-sheet.png'))
try {
    if($sheet.Width -ne 1254 -or $sheet.Height -ne 1254){throw 'Unexpected sheet dimensions; review slice rectangles.'}
    $slices=@(
        @('button-primary',0,80,465,195), @('button-secondary',465,80,385,195), @('counter-panel',855,80,399,195),
        @('settings',50,290,345,325), @('audio-on',465,290,330,325), @('coin',885,300,315,315),
        @('avatar',35,620,380,350), @('play-arrow',520,655,230,285), @('forest',870,655,350,285),
        @('progress-active',110,990,220,230), @('progress-idle',510,990,225,230), @('audio-off',900,965,300,270)
    )
    foreach($s in $slices){Export-Crop $sheet $s[0] ([Drawing.Rectangle]::new($s[1],$s[2],$s[3],$s[4]))}
} finally {$sheet.Dispose()}
$logo=[Drawing.Bitmap]::new((Join-Path $sourceDir 'logo-source.png'))
try {Export-Crop $logo 'logo' ([Drawing.Rectangle]::new(0,0,$logo.Width,$logo.Height))} finally {$logo.Dispose()}
foreach($name in @('background-landscape','background-portrait')) {
    Copy-Item -LiteralPath (Join-Path $sourceDir "$name.png") -Destination (Join-Path $outputDir "$name.png")
    $bitmap=[Drawing.Bitmap]::new((Join-Path $outputDir "$name.png"))
    $manifest.Add([pscustomobject]@{file="$name.png";width=$bitmap.Width;height=$bitmap.Height;transparent=$false})
    $bitmap.Dispose()
}
# Use the project's current TextureImporter schema. Preserve GUIDs on re-export.
$template=[IO.File]::ReadAllText((Join-Path $repo 'Assets/_Project/07_Art/UI/MainMenu/lp_button_primary_9s.png.meta'))
foreach($item in $manifest) {
    $metaPath=Join-Path $outputDir ($item.file+'.meta')
    $guid=if(Test-Path $metaPath){[regex]::Match([IO.File]::ReadAllText($metaPath),'(?m)^guid: (\w+)').Groups[1].Value}else{[guid]::NewGuid().ToString('N')}
    $meta=$template -replace '(?m)^guid: \w+',"guid: $guid"
    $meta=$meta -replace 'maxTextureSize: 2048','maxTextureSize: 4096' -replace 'nPOTScale: 1','nPOTScale: 0'
    $meta=$meta -replace 'textureCompression: 1','textureCompression: 0' -replace 'wrapU: 0','wrapU: 1' -replace 'wrapV: 0','wrapV: 1' -replace 'wrapW: 0','wrapW: 1'
    [IO.File]::WriteAllText($metaPath,$meta)
}
$folderMeta=$outputDir+'.meta'
if(!(Test-Path $folderMeta)){[IO.File]::WriteAllText($folderMeta,"fileFormatVersion: 2`nguid: $([guid]::NewGuid().ToString('N'))`nfolderAsset: yes`nDefaultImporter:`n  externalObjects: {}`n  userData: `n  assetBundleName: `n  assetBundleVariant: `n")}
$manifest | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $PSScriptRoot 'manifest.json') -Encoding utf8
# A contact sheet on a light background reveals alpha edges and crop mistakes.
$contact=[Drawing.Bitmap]::new(1200,1000)
$g=[Drawing.Graphics]::FromImage($contact)
$g.Clear([Drawing.Color]::FromArgb(235,227,211))
$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$font=[Drawing.Font]::new('Arial',14)
try {
    for($i=0;$i -lt $manifest.Count;$i++) {
        $item=$manifest[$i]; $b=[Drawing.Bitmap]::new((Join-Path $outputDir $item.file))
        try {
            $scale=[Math]::Min(270.0/$b.Width,150.0/$b.Height)
            $x=($i%4)*300; $y=[Math]::Floor($i/4)*250
            $g.DrawImage($b,[Drawing.Rectangle]::new([int]($x+15),[int]($y+15),[int]($b.Width*$scale),[int]($b.Height*$scale)))
            $g.DrawString($item.file,$font,[Drawing.Brushes]::Black,[single]($x+15),[single]($y+185))
        } finally {$b.Dispose()}
    }
    $contact.Save((Join-Path $PSScriptRoot 'contact-sheet.png'),[Drawing.Imaging.ImageFormat]::Png)
} finally {$font.Dispose();$g.Dispose();$contact.Dispose()}
$manifest | Format-Table file,width,height,transparent
