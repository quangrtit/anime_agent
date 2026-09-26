Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class RegT4 {
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    public static extern int RegGetValueW(UIntPtr hkey, string subKey, string valueName,
        uint flags, out uint type, StringBuilder data, ref uint dataSize);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    public static extern int RegOpenKeyExW(UIntPtr hkey, string subKey, uint options, int access, out UIntPtr result);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    public static extern int RegQueryValueExW(UIntPtr hkey, string valueName, IntPtr reserved,
        out uint type, StringBuilder data, ref uint dataSize);
    [DllImport("advapi32.dll")]
    public static extern int RegCloseKey(UIntPtr hkey);
}
"@
$hk = [UIntPtr]::new([uint64]2147483650)   # HKEY_CURRENT_USER

$kind = (Get-Item "HKCU:\Control Panel\Colors").GetValueKind("Background")
Write-Output "PS value kind of Background: $kind  value=[$((Get-ItemProperty 'HKCU:\Control Panel\Colors').Background)]"

$sb = New-Object System.Text.StringBuilder 256
$ds = [uint32]512
$t = [uint32]0
$r = [RegT4]::RegGetValueW($hk, "Control Panel\Colors", "Background", [uint32]0, [ref]$t, $sb, [ref]$ds)
Write-Output "RegGetValueW flags=0: result=$r type=$t data=[$($sb.ToString())]"

$hkOpen = [UIntPtr]::Zero
$ro = [RegT4]::RegOpenKeyExW($hk, "Control Panel\Colors", [uint32]0, 0x20019, [ref]$hkOpen)
Write-Output "RegOpenKeyExW: result=$ro"
if ($ro -eq 0) {
    $sb2 = New-Object System.Text.StringBuilder 256
    $ds2 = [uint32]512
    $t2 = [uint32]0
    $rq = [RegT4]::RegQueryValueExW($hkOpen, "Background", [IntPtr]::Zero, [ref]$t2, $sb2, [ref]$ds2)
    Write-Output "RegQueryValueExW: result=$rq type=$t2 data=[$($sb2.ToString())]"
    [void][RegT4]::RegCloseKey($hkOpen)
}

# Enumerate all values under Colors via RegGetValueW on the default value and known names
foreach ($name in @("Background", "ActiveTitle", "HOTTRACKING")) {
    $sb3 = New-Object System.Text.StringBuilder 256
    $ds3 = [uint32]512
    $t3 = [uint32]0
    $r3 = [RegT4]::RegGetValueW($hk, "Control Panel\Colors", $name, [uint32]2, [ref]$t3, $sb3, [ref]$ds3)
    Write-Output "RegGetValueW(RRF_RT_REG_SZ) '$name': result=$r3 data=[$($sb3.ToString())]"
}
