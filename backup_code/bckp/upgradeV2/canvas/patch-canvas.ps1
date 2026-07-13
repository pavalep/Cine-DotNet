$path = Join-Path $PSScriptRoot 'action-plan.canvas'
$jsonText = Get-Content -Path $path -Raw
$json = $jsonText | ConvertFrom-Json
$replacements = @{
  '  .cbtn {
    width:34px;height:34px;border-radius:50%;border:none;background:transparent;
    cursor:pointer;display:flex;align-items:center;justify-content:center;
    flex-shrink:0;transition:background 0.12s;
    filter:drop-shadow(0 1px 5px rgba(0,0,0,0.6));
  }
  .cbtn:hover { background:rgba(255,255,255,0.17); }
  .cbtn:active { background:rgba(255,255,255,0.25); }
  .cbtn.checked { background:white; }
  .cbtn.checked svg { filter:invert(1); }
  .wctrl {
    width:46px;height:32px;border:none;background:transparent;cursor:pointer;
    display:flex;align-items:center;justify-content:center;
    transition:background 0.1s;color:white;
  }
  .wctrl:hover { background:rgba(255,255,255,0.13); }
  .wctrl.close:hover { background:#e81123; }
  * { box-sizing:border-box; }
' = '  .cbtn {
    width:44px;height:44px;border-radius:14px;border:none;background:rgba(255,255,255,0.08);
    cursor:pointer;display:flex;align-items:center;justify-content:center;
    flex-shrink:0;transition:background 0.14s, transform 0.14s;
    filter:drop-shadow(0 6px 18px rgba(0,0,0,0.24));
  }
  .cbtn:hover { background:rgba(255,255,255,0.16); transform:translateY(-1px); }
  .cbtn:active { background:rgba(255,255,255,0.24); transform:translateY(0); }
  .cbtn.checked { background:white; }
  .cbtn.checked svg { filter:invert(1); }
  .wctrl {
    width:48px;height:36px;border:none;background:rgba(255,255,255,0.06);cursor:pointer;
    display:flex;align-items:center;justify-content:center;
    transition:background 0.14s, transform 0.14s;color:white;
    border-radius:12px;
  }
  .wctrl:hover { background:rgba(255,255,255,0.14); transform:translateY(-1px); }
  .wctrl.close:hover { background:#e81123; }
  * { box-sizing:border-box; }
';
  '  .cbtn {
    width:34px;height:34px;border-radius:50%;border:none;background:transparent;
    cursor:pointer;display:flex;align-items:center;justify-content:center;
    flex-shrink:0;filter:drop-shadow(0 1px 5px rgba(0,0,0,0.6));
    transition:background 0.12s;
  }
  .cbtn:hover { background:rgba(255,255,255,0.17); }
  .wctrl {
    width:46px;height:32px;border:none;background:transparent;cursor:pointer;
    display:flex;align-items:center;justify-content:center;transition:background 0.1s;
  }
  .wctrl:hover { background:rgba(255,255,255,0.13); }
  .wctrl.close:hover { background:#e81123; }
  .sp-btn {
    height:40px;border-radius:99px;border:none;cursor:pointer;
    padding:0 32px;font-size:14px;font-weight:600;
    font-family:'Inter',sans-serif;transition:background 0.12s;
    display:flex;align-items:center;justify-content:center;
  }
  .sp-btn-primary {
    background:#e5e5e5;color:black;
  }
  .sp-btn-primary:hover { background:white; }
  .sp-btn-secondary {
    background:rgba(255,255,255,0.12);color:#e5e5e5;
  }
  .sp-btn-secondary:hover { background:rgba(255,255,255,0.15); }
' = '  .cbtn {
    width:44px;height:44px;border-radius:14px;border:none;background:rgba(255,255,255,0.08);
    cursor:pointer;display:flex;align-items:center;justify-content:center;
    flex-shrink:0;filter:drop-shadow(0 6px 18px rgba(0,0,0,0.24));
    transition:background 0.14s, transform 0.14s;
  }
  .cbtn:hover { background:rgba(255,255,255,0.16); transform:translateY(-1px); }
  .wctrl {
    width:48px;height:36px;border:none;background:rgba(255,255,255,0.06);cursor:pointer;
    display:flex;align-items:center;justify-content:center;transition:background 0.14s, transform 0.14s;
    border-radius:12px;
  }
  .wctrl:hover { background:rgba(255,255,255,0.14); transform:translateY(-1px); }
  .wctrl.close:hover { background:#e81123; }
  .sp-btn {
    height:44px;border-radius:999px;border:none;cursor:pointer;
    padding:0 34px;font-size:14px;font-weight:600;
    font-family:'Inter',sans-serif;transition:background 0.14s, transform 0.14s;
    display:flex;align-items:center;justify-content:center;
    min-width:160px;
  }
  .sp-btn-primary {
    background:#e5e5e5;color:black;
  }
  .sp-btn-primary:hover { background:white; transform:translateY(-1px); }
  .sp-btn-secondary {
    background:rgba(255,255,255,0.12);color:#e5e5e5;
  }
  .sp-btn-secondary:hover { background:rgba(255,255,255,0.18); transform:translateY(-1px); }
';
  '    ① Open button — visible when playing (was hidden 0×0)' = '    ① Open button surfaced with consistent spacing and larger hit targets.'
}
$changed = $false
foreach ($prop in $json.nodes.PSObject.Properties) {
    $node = $prop.Value
    if ($null -ne $node.html) {
        foreach ($old in $replacements.Keys) {
            if ($node.html.Contains($old)) {
                $node.html = $node.html.Replace($old, $replacements[$old])
                $changed = $true
            }
        }
    }
}
if (-not $changed) { throw 'No replacements applied' }
$json | ConvertTo-Json -Depth 20 | Set-Content -Path $path -Encoding utf8
Write-Host 'Replaced blocks successfully'
