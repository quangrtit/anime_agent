Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class RegTest2 {
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    public static extern int RegGetValueW(UIntPtr hkey, string subKey, string valueName,
        uint flags, out uint type, StringBuilder data, ref uint dataSize);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    public static extern int RegGetValueW(IntPtr hkey, string subKey, string valueName,
        uint flags, IntPtr typePtr, StringBuilder data, ref uint dataSize);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    public static extern int RegOpenKeyExW(UIntPtr hkey, string subKey, uint options, int access, out UIntPtr result);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    public static extern int RegQueryValueExW(UIntPtr hkey, string valueName, IntPtr reserved,
        out uint type, StringBuilder data, ref uint dataSize);
    [DllImport("advapi32.dll")]
    public static extern int RegCloseKey(UIntPtr hkey);
}
"@
$hkey = [UIntPtr]::new([uint64]2147483669)

# Variation A: original signature but type as IntPtr.Zero (NULL)
$sb = New-Object System.Text.StringBuilder 256
$ds = [uint32]512
$r = [RegTest2]::RegGetValueW($hkey, "Control Panel\Colors", "Background", [uint32]2, [IntPtr]::Zero, $sb, [ref]$ds)
Write-Output "A IntPtr.Zero type: result=$r data=[$($sb.ToString())]"

# Variation B: IntPtr hkey
$sb2 = New-Object System.Text.StringBuilder 256
$ds2 = [uint32]512
$r2 = [RegTest2]::RegGetValueW([IntPtr]2147483669, "Control Panel\Colors", "Background", [uint32]2, [IntPtr]::Zero, $sb2, [ref]$ds2)
Write-Output "B IntPtr hkey: result=$r2 data=[$($sb2.ToString())]"

# Variation C: flags=0 (no restriction)
$sb3 = New-Object System.Text.StringBuilder 256
$ds3 = [uint32]512
$dummy = [uint32]0
$r3 = [RegTest2]::RegGetValueW($hkey, "Control Panel\Colors", "Background", [uint32]0, [ref]$dummy, $sb3, [ref]$ds3)
Write-Output "C flags=0: result=$r3 data=[$($sb3.ToString())]"

# Variation D: RegOpenKeyEx + RegQueryValueEx (classic)
$hk = [UIntPtr]::Zero
$ro = [RegTest2]::RegOpenKeyExW($hkey, "Control Panel\Colors", 0, 0x20019, [ref]$hk)
Write-Output "D open: result=$ro hk=$hk"
if ($ro -eq 0) {
    $sb4 = New-Object System.Text.StringBuilder 256
    $ds4 = [uint32]512
    $t4 = [uint32]0
    $rq = [RegTest2]::RegQueryValueExW($hk, "Background", [IntPtr]::Zero, [ref]$t4, $sb4, [ref]$ds4)
    Write-Output "D query: result=$rq type=$t4 data=[$($sb4.ToString())]"
    [void][RegTest2]::RegCloseKey($hk)
}
