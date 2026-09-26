Write-Output "=== UIA probe of desktop icons (real screen rects) ==="
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$root = [System.Windows.Automation.AutomationElement]::RootElement
$progmanCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ClassNameProperty, "Progman")
$progman = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $progmanCond)
if (-not $progman) { Write-Output "no Progman"; exit }
$listCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ClassNameProperty, "SysListView32")
$list = $progman.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $listCond)
if (-not $list) { Write-Output "no SysListView32 under Progman"; exit }
Write-Output ("list rect: " + $list.Current.BoundingRectangle.ToString())

$items = $list.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
Write-Output ("item count: " + $items.Count)
foreach ($it in $items) {
  $r = $it.Current.BoundingRectangle
  $sel = "n/a"
  try {
    $sp = $it.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    $sel = $sp.Current.IsSelected
  } catch {}
  Write-Output ("icon: [{0}] rect=({1},{2} {3}x{4}) selected={5}" -f $it.Current.Name, [int]$r.X, [int]$r.Y, [int]$r.Width, [int]$r.Height, $sel)
}

Write-Output "=== also check WorkerW-hosted desktop list ==="
$workerCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ClassNameProperty, "WorkerW")
$workers = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $workerCond)
foreach ($w in $workers) {
  $l2 = $w.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $listCond)
  if ($l2) {
    Write-Output ("WorkerW list rect: " + $l2.Current.BoundingRectangle.ToString())
    $items2 = $l2.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
    Write-Output ("WorkerW item count: " + $items2.Count)
  }
}
