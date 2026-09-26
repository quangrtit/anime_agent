Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class RegT {
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    public static extern int RegGetValueW(UIntPtr hkey, string subKey, string valueName,
        uint flags, out uint type, StringBuilder data, ref uint dataSize);
}
"@
$hk = [UIntPtr]::new([uint64]2147483650)   # 0x80000002 HKEY_CURRENT_USER
$sb = New-Object System.Text.StringBuilder 256
$ds = [uint32]512
$t = [uint32]0
$r = [RegT]::RegGetValueW($hk, "Control Panel\Colors", "Background", [uint32]2, [ref]$t, $sb, [ref]$ds)
Write-Output "RegGetValueW(HKCU correct): result=$r type=$t data=[$($sb.ToString())]"

Write-Output "---- log grep ----"
$log = "$env:USERPROFILE\AppData\LocalLow\Wibu Project\Anime Desktop Assistant\Player.log"
Select-String -Path $log -Pattern "captured|color|Companion folder" | ForEach-Object { $_.Line }
Write-Output "---- Companion folder now ----"
Get-ChildItem "$env:USERPROFILE\AppData\LocalLow\Wibu Project\Anime Desktop Assistant\Companion" | ForEach-Object { "{0,10} {1}" -f $_.Length, $_.Name }
