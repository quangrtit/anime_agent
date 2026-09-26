Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class RegTest {
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    public static extern int RegGetValueW(UIntPtr hkey, string subKey, string valueName,
        uint flags, out uint type, StringBuilder data, ref uint dataSize);
}
"@
$hkey = [UIntPtr]::new([uint64]2147483669)  # 0x80000002 HKEY_CURRENT_USER
$flags = [uint32]2   # RRF_RT_REG_SZ
$typeNull = [uint32]0
$sb = New-Object System.Text.StringBuilder 256
$dataSize = [uint32]512
$result = [RegTest]::RegGetValueW($hkey, "Control Panel\Colors", "Background", $flags, [ref]$typeNull, $sb, [ref]$dataSize)
Write-Output "call1: result=$result data=[$($sb.ToString())] size=$dataSize"

$sb2 = New-Object System.Text.StringBuilder 256
$ds2 = [uint32]512
$typeNull2 = [uint32]0
$r2 = [RegTest]::RegGetValueW($hkey, "Control Panel\Colors", "Background", $flags, [ref]$typeNull2, $sb2, [ref]$ds2)
Write-Output "call2: result=$r2 data=[$($sb2.ToString())]"
