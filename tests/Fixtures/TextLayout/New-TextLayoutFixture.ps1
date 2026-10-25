# Regenerate the PNG and matching word boxes used by the isolated desktop tests.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$fixtureLines = [System.Collections.Generic.List[object]]::new()
function Add-Line([int]$paragraph, [float]$x, [float]$y, [float]$size, [string]$weight, [string]$text) {
    $fixtureLines.Add([pscustomobject]@{ Paragraph = $paragraph; X = $x; Y = $y; Size = $size; Weight = $weight; Text = $text })
}

Add-Line 0 72 52 44 Bold 'A morning in the studio'
Add-Line 1 72 116 20 Regular 'Notes from a photography review'
Add-Line 2 72 192 30 Bold 'Project update'
Add-Line 3 72 252 24 Regular 'Morning light filled the studio as the team'
Add-Line 3 72 288 24 Regular 'reviewed a new collection of photographs.'
Add-Line 3 72 324 24 Regular 'Each image told a small part of the story.'
Add-Line 4 72 398 24 Regular 'The first review focused on composition.'
Add-Line 4 72 434 24 Regular 'We compared the balance of light and'
Add-Line 4 72 470 24 Regular 'shadow, then wrote a few practical notes.'
Add-Line 5 108 544 24 Regular 'Later, we returned to the strongest ideas.'
Add-Line 5 72 580 24 Regular 'Small changes made the images easier to'
Add-Line 5 72 616 24 Regular 'read without changing their character.'

Add-Line 6 784 192 30 Bold 'Review checklist'
Add-Line 7 784 252 24 Regular '1. Keep related lines in one paragraph.'
Add-Line 7 812 288 24 Regular 'Wrapped text should stay with its item.'
Add-Line 8 784 362 24 Regular '2. Leave a clear gap before the next idea.'
Add-Line 8 812 398 24 Regular 'Preserve the original reading order.'
Add-Line 9 784 472 24 Regular '3. Keep the two columns separate.'
Add-Line 9 812 508 24 Regular 'Selecting a passage should highlight'
Add-Line 9 812 544 24 Regular 'only the words that belong to it.'
Add-Line 10 784 618 24 Regular 'The review ended with a short list of'
Add-Line 10 784 654 24 Regular 'changes to try during the next session.'

Add-Line 11 72 766 30 Bold 'Field notes'
Add-Line 12 72 826 24 Regular 'A good extract preserves the words, the order of the ideas, and the space between paragraphs.'
Add-Line 12 72 862 24 Regular 'Headings and lists add structure; the source image supplies the evidence.'

$fixtureWidth = 1440
$fixtureHeight = 964
$fixtureBitmap = [System.Drawing.Bitmap]::new($fixtureWidth, $fixtureHeight)
$fixtureGraphics = [System.Drawing.Graphics]::FromImage($fixtureBitmap)
$fixtureFormat = [System.Drawing.StringFormat]::GenericTypographic.Clone()
$fixtureFormat.FormatFlags = $fixtureFormat.FormatFlags -bor [System.Drawing.StringFormatFlags]::MeasureTrailingSpaces
$fixtureInk = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#26313F'))
$fixtureAccent = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#175E8C'))
$fixtureRule = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#D9E2E8'), 2)
$fixtureWords = [System.Collections.Generic.List[object]]::new()
try {
    $fixtureGraphics.Clear([System.Drawing.Color]::White)
    $fixtureGraphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $fixtureGraphics.DrawLine($fixtureRule, 72, 164, 1368, 164)
    $fixtureGraphics.DrawLine($fixtureRule, 728, 200, 728, 708)
    $fixtureGraphics.DrawLine($fixtureRule, 72, 736, 1368, 736)
    $fixtureLineIndex = 0
    foreach ($fixtureLine in ($fixtureLines | Sort-Object Y, X)) {
        $fixtureFont = [System.Drawing.Font]::new('Segoe UI', $fixtureLine.Size,
            [System.Drawing.FontStyle]::$($fixtureLine.Weight), [System.Drawing.GraphicsUnit]::Pixel)
        try {
            $fixtureSpace = $fixtureGraphics.MeasureString(' ', $fixtureFont, [System.Drawing.PointF]::Empty, $fixtureFormat).Width
            $fixtureX = $fixtureLine.X
            $fixtureWordIndex = 0
            foreach ($fixtureWord in $fixtureLine.Text.Split(' ')) {
                $fixtureSize = $fixtureGraphics.MeasureString($fixtureWord, $fixtureFont, [System.Drawing.PointF]::Empty, $fixtureFormat)
                if ($fixtureX + $fixtureSize.Width -gt $fixtureWidth - 72) { throw "Fixture text exceeds the page: $($fixtureLine.Text)" }
                $fixtureBrush = if ($fixtureLine.Weight -eq 'Bold') { $fixtureAccent } else { $fixtureInk }
                $fixtureGraphics.DrawString($fixtureWord, $fixtureFont, $fixtureBrush,
                    [System.Drawing.PointF]::new($fixtureX, $fixtureLine.Y), $fixtureFormat)
                $fixtureWords.Add([pscustomobject]@{
                    Text = $fixtureWord; X = $fixtureX; Y = $fixtureLine.Y
                    Width = $fixtureSize.Width; Height = $fixtureSize.Height
                    LineIndex = $fixtureLineIndex; WordIndex = $fixtureWordIndex
                })
                $fixtureX += $fixtureSize.Width + $fixtureSpace
                $fixtureWordIndex++
            }
        } finally { $fixtureFont.Dispose() }
        $fixtureLineIndex++
    }
    $fixtureBitmap.Save((Join-Path $PSScriptRoot 'text-layout.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    $fixtureParagraphs = @($fixtureLines | Group-Object Paragraph | Sort-Object { [int]$_.Name } | ForEach-Object {
        ($_.Group.Text -join "`n")
    })
    [ordered]@{ Width = $fixtureWidth; Height = $fixtureHeight; Words = $fixtureWords; Paragraphs = $fixtureParagraphs } |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'text-layout.json') -Encoding utf8
} finally {
    $fixtureGraphics.Dispose(); $fixtureBitmap.Dispose(); $fixtureFormat.Dispose()
    $fixtureInk.Dispose(); $fixtureAccent.Dispose(); $fixtureRule.Dispose()
}
