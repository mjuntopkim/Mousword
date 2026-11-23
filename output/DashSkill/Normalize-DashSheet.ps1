# Mechanical production export: crop alpha bounds, uniform nearest-neighbor sampling,
# map to the reference palette, and align all four foot lines. Does not redraw poses.
Add-Type -AssemblyName System.Drawing
$taskRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$taskSource = [System.Drawing.Bitmap]::new((Join-Path $PSScriptRoot 'PlayerDash-generated.png'))
$taskReference = [System.Drawing.Bitmap]::new((Join-Path $taskRoot 'mousword_character-Sheet.png'))
$taskPalette = @{}
for ($x=0; $x -lt $taskReference.Width; $x++) {
    for ($y=0; $y -lt $taskReference.Height; $y++) {
        $c=$taskReference.GetPixel($x,$y)
        if ($c.A -gt 200) { $taskPalette[$c.ToArgb()]=$c }
    }
}
$taskColors = @($taskPalette.Values)
$taskBounds = Get-Content (Join-Path $PSScriptRoot 'frame-bounds.json') -Raw | ConvertFrom-Json
$taskSheet = [System.Drawing.Bitmap]::new(192,40)
$taskScale = 28.0 / 270.0
foreach ($b in $taskBounds) {
    $w=[int][Math]::Round($b.width*$taskScale)
    $h=[int][Math]::Round($b.height*$taskScale)
    $left=$b.frame*48+[int][Math]::Floor((48-$w)/2)
    for ($x=0; $x -lt $w; $x++) {
        for ($y=0; $y -lt $h; $y++) {
            $c=$taskSource.GetPixel([int][Math]::Floor($b.x+($x+0.5)*$b.width/$w),[int][Math]::Floor($b.y+($y+0.5)*$b.height/$h))
            if ($c.A -lt 128) { continue }
            $nearest=$taskColors[0]; $distance=[double]::MaxValue
            foreach ($p in $taskColors) {
                $d=($c.R-$p.R)*($c.R-$p.R)+($c.G-$p.G)*($c.G-$p.G)+($c.B-$p.B)*($c.B-$p.B)
                if ($d -lt $distance) { $distance=$d; $nearest=$p }
            }
            # Keep the eye pixels in the reference's green accent family.
            if ($c.G -gt 100 -and $c.G -gt $c.R*1.2 -and $c.B -gt $c.R*1.2) {
                $nearest=[System.Drawing.Color]::FromArgb(20,230,10)
            }
            $taskSheet.SetPixel($left+$x,36-$h+$y,[System.Drawing.Color]::FromArgb(255,$nearest.R,$nearest.G,$nearest.B))
        }
    }
}
$taskSheet.Save((Join-Path $taskRoot 'Assets/Resources/Dash/PlayerDash.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$taskPreview = [System.Drawing.Bitmap]::new(1536,320)
$taskGraphics = [System.Drawing.Graphics]::FromImage($taskPreview)
$taskGraphics.Clear([System.Drawing.Color]::FromArgb(22,28,37))
$taskGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$taskGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$taskGraphics.DrawImage($taskSheet,[System.Drawing.Rectangle]::new(0,0,1536,320),0,0,192,40,[System.Drawing.GraphicsUnit]::Pixel)
$taskPreview.Save((Join-Path $PSScriptRoot 'PlayerDash-poses.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$taskGraphics.Dispose(); $taskPreview.Dispose(); $taskSheet.Dispose(); $taskReference.Dispose(); $taskSource.Dispose()
