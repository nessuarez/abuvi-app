using System.Text;
using System.Text.Json;

namespace Abuvi.Setup.Geocoding;

/// <summary>
/// Writes a self-contained HTML page with every geocoded camp on a Leaflet map,
/// colour-coded by verification status. No server needed: open it in a browser.
///
/// This is the check no heuristic can replace — a pin in the sea, or one province
/// off, is obvious in seconds and invisible to the automatic rules.
/// </summary>
public static class ReviewMapWriter
{
    public static void Write(string outputPath, GeocodeReport report)
    {
        var payload = report.Rows.Select(r => new
        {
            name = r.Name,
            expectedProvince = r.ExpectedProvince,
            googleProvince = r.GoogleProvince,
            address = r.FormattedAddress,
            types = r.Types,
            status = string.IsNullOrWhiteSpace(r.Status) ? "pending" : r.Status,
            notes = r.Notes,
            lat = r.Latitude,
            lng = r.Longitude
        });

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        var html = Template
            .Replace("__DATA__", json)
            .Replace("__OK__", report.Ok.ToString())
            .Replace("__REVIEW__", report.Review.ToString())
            .Replace("__FAILED__", report.Failed.ToString())
            .Replace("__SKIPPED__", report.Skipped.ToString())
            .Replace("__TOTAL__", report.Total.ToString());

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(outputPath, html, new UTF8Encoding(false));
    }

    private const string Template = """
<!doctype html>
<html lang="es">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Revisión de geolocalización — campamentos ABUVI</title>
<link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css">
<script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
<style>
  :root { color-scheme: light dark; }
  * { box-sizing: border-box; }
  body { margin: 0; font: 15px/1.5 system-ui, -apple-system, "Segoe UI", sans-serif; }
  header { padding: 14px 20px; background: #78350f; color: #fef3c7; }
  h1 { margin: 0 0 6px; font-size: 18px; }
  .counts { display: flex; gap: 16px; flex-wrap: wrap; font-size: 14px; }
  .counts b { font-weight: 600; }
  .dot { display: inline-block; width: 10px; height: 10px; border-radius: 50%; margin-right: 5px; }
  .ok { background: #16a34a; } .review { background: #f59e0b; }
  .failed { background: #dc2626; } .pending { background: #9ca3af; }
  main { display: flex; height: calc(100vh - 76px); }
  #map { flex: 1 1 60%; }
  #list { flex: 1 1 40%; overflow-y: auto; border-left: 1px solid #d6d3d1; }
  .row { padding: 10px 14px; border-bottom: 1px solid #e7e5e4; cursor: pointer; }
  .row:hover { background: #fef3c7; }
  .row h2 { margin: 0 0 3px; font-size: 15px; font-weight: 600; }
  .meta { font-size: 13px; color: #57534e; }
  .notes { font-size: 13px; color: #b45309; margin-top: 4px; }
  .mismatch { color: #dc2626; font-weight: 600; }
  @media (max-width: 800px) { main { flex-direction: column; height: auto; }
    #map { height: 55vh; } #list { border-left: 0; border-top: 1px solid #d6d3d1; } }
</style>
</head>
<body>
<header>
  <h1>Revisión de geolocalización — campamentos ABUVI</h1>
  <div class="counts">
    <span><span class="dot ok"></span><b>__OK__</b> correctos</span>
    <span><span class="dot review"></span><b>__REVIEW__</b> a revisar</span>
    <span><span class="dot failed"></span><b>__FAILED__</b> sin resolver</span>
    <span><span class="dot pending"></span><b>__SKIPPED__</b> ya fijados</span>
    <span>Total: <b>__TOTAL__</b></span>
  </div>
</header>
<main>
  <div id="map"></div>
  <div id="list"></div>
</main>
<script>
const DATA = __DATA__;
const COLORS = { ok: '#16a34a', review: '#f59e0b', failed: '#dc2626', pending: '#9ca3af' };

const map = L.map('map').setView([40.4, -3.7], 6);
L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
  attribution: '&copy; OpenStreetMap', maxZoom: 19
}).addTo(map);

const esc = s => String(s ?? '').replace(/[&<>"]/g, c =>
  ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

const markers = [];
const bounds = [];

DATA.forEach((d, i) => {
  const provinceMismatch = d.googleProvince &&
    d.googleProvince.toLowerCase().indexOf(d.expectedProvince.toLowerCase()) === -1;

  const popup = `<strong>${esc(d.name)}</strong><br>
    Provincia esperada: ${esc(d.expectedProvince)}<br>
    Google dice: <span class="${provinceMismatch ? 'mismatch' : ''}">${esc(d.googleProvince) || '—'}</span><br>
    ${esc(d.address)}<br>
    <small>${esc(d.types)}</small>
    ${d.notes ? `<br><em>${esc(d.notes)}</em>` : ''}
    ${d.lat ? `<br><a href="https://www.google.com/maps/search/?api=1&query=${d.lat},${d.lng}"
      target="_blank" rel="noopener">Ver en Google Maps</a>` : ''}`;

  if (d.lat != null && d.lng != null) {
    const m = L.circleMarker([d.lat, d.lng], {
      radius: 8, color: '#fff', weight: 2,
      fillColor: COLORS[d.status] || COLORS.pending, fillOpacity: 1
    }).addTo(map).bindPopup(popup);
    markers[i] = m;
    bounds.push([d.lat, d.lng]);
  }

  const row = document.createElement('div');
  row.className = 'row';
  row.innerHTML = `<h2><span class="dot ${d.status}"></span>${esc(d.name)}</h2>
    <div class="meta">${esc(d.expectedProvince)}
      ${d.googleProvince ? `&rarr; <span class="${provinceMismatch ? 'mismatch' : ''}">${esc(d.googleProvince)}</span>` : ''}
    </div>
    <div class="meta">${esc(d.address) || '<em>sin dirección</em>'}</div>
    ${d.notes ? `<div class="notes">${esc(d.notes)}</div>` : ''}`;
  row.onclick = () => {
    if (!markers[i]) return;
    map.setView(markers[i].getLatLng(), 11);
    markers[i].openPopup();
  };
  document.getElementById('list').appendChild(row);
});

if (bounds.length) map.fitBounds(bounds, { padding: [40, 40] });
</script>
</body>
</html>
""";
}
