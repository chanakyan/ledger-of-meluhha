"use strict";
// The Ledger of Meluhha — Interactive Visualization
// Third Buyer Advisory LLC | Venugopal 2026
// TypeScript source — compiled to JS, embedded inline in ledger-of-meluhha.html
//
// Architecture
// ------------
// Single-page dashboard for the Indus Valley metrological accounting hypothesis.
// User drops indus_corpus.db (SQLite, built by indus_ingest_corpus.fsx) onto a
// landing page. sql.js WASM loads the DB in-browser; the dashboard renders:
//
//   1. Leaflet map — 22 archaeological sites, 13 trade route polylines
//   2. Seal cards — live decode of CISI seals via Parpola sign -> codebook lookup
//   3. D3 frequency chart — cumulative sign distribution (67 signs = 80%)
//   4. Weight bars — binary/decimal weight tiers (base unit 0.856 g)
//   5. Commodity manifest — clickable table; highlights matching map routes
//   6. Radiometric dating — top-5 calibrated dates from corpus DB
//
// Data sources
// ------------
// - Sites, routes, weights, commodities: hardcoded (static reference data)
// - Codebook (signRoleMap, commodityMap, routeMap): embedded from indus_codebook.db
//   to avoid requiring a second file drop. Keyed on Parpola sign numbers.
// - Seal inscriptions + concordance: queried live from the dropped corpus DB
//
// Build: npx tsc -p src/tsconfig.json  ->  dist/ledger.js  ->  paste into HTML
// === DATA ====================================================================
const sites = [
    // IVC
    { id: 'mohenjodaro', name: 'Mohenjo-daro', lat: 27.33, lon: 68.14, type: 'ivc-major', period: 'Mature Harappan' },
    { id: 'harappa', name: 'Harappa', lat: 30.63, lon: 72.87, type: 'ivc-major', period: 'Mature Harappan' },
    { id: 'lothal', name: 'Lothal', lat: 22.52, lon: 72.25, type: 'ivc-port', period: 'Mature Harappan' },
    { id: 'kalibangan', name: 'Kalibangan', lat: 29.47, lon: 74.13, type: 'ivc-major', period: 'Mature Harappan' },
    { id: 'dholavira', name: 'Dholavira', lat: 23.89, lon: 70.21, type: 'ivc-major', period: 'Mature Harappan' },
    { id: 'rojdi', name: 'Rojdi', lat: 22.28, lon: 71.11, type: 'ivc-minor', period: 'Late Harappan' },
    { id: 'shortughai', name: 'Shortughai', lat: 37.08, lon: 69.53, type: 'ivc-outpost', period: 'Mature Harappan' },
    // Tamil Nadu
    { id: 'kodumanal', name: 'Kodumanal', lat: 11.10, lon: 77.88, type: 'tn-supply', period: 'Iron Age' },
    { id: 'keeladi', name: 'Keeladi', lat: 9.88, lon: 78.90, type: 'tn-supply', period: 'Iron Age' },
    { id: 'thulukarpatti', name: 'Thulukarpatti', lat: 8.82, lon: 77.82, type: 'tn-supply', period: 'Iron Age' },
    { id: 'karur', name: 'Karur', lat: 10.96, lon: 78.08, type: 'tn-supply', period: 'Iron Age' },
    { id: 'teriruveli', name: 'Teriruveli', lat: 8.72, lon: 78.10, type: 'tn-supply', period: 'Iron Age' },
    { id: 'adichanallur', name: 'Adichanallur', lat: 8.68, lon: 77.72, type: 'tn-supply', period: 'Iron Age' },
    { id: 'sivagalai', name: 'Sivagalai', lat: 8.55, lon: 77.85, type: 'tn-supply', period: 'Iron Age, 3445 BCE' },
    // Mesopotamia / Gulf
    { id: 'ur', name: 'Ur', lat: 30.96, lon: 46.10, type: 'meso-dest', period: 'Akkadian' },
    { id: 'kish', name: 'Kish', lat: 32.55, lon: 44.65, type: 'meso-dest', period: 'Akkadian' },
    { id: 'tell-asmar', name: 'Tell Asmar', lat: 33.75, lon: 44.75, type: 'meso-dest', period: 'Akkadian' },
    { id: 'susa', name: 'Susa', lat: 32.19, lon: 48.26, type: 'meso-dest', period: 'Elamite' },
    { id: 'dilmun', name: 'Dilmun', lat: 26.04, lon: 50.55, type: 'gulf-hub', period: 'Bronze Age' },
    { id: 'magan', name: 'Magan', lat: 23.61, lon: 58.59, type: 'gulf-hub', period: 'Bronze Age' },
    // Resource
    { id: 'khetri', name: 'Khetri Mines', lat: 28.00, lon: 75.80, type: 'resource', period: 'Copper source' },
];
const tradeRoutes = [
    // Export: IVC -> Mesopotamia
    { from: 'lothal', via: 'dilmun', to: 'ur', commodity: 'Jar goods', color: '#cc6600', width: 8, note: 'S-342 = 10% corpus' },
    { from: 'lothal', via: 'dilmun', to: 'ur', commodity: 'Textiles', color: '#8844aa', width: 6, note: 'Primary bulk export' },
    { from: 'lothal', via: 'magan', to: null, commodity: 'Copper-bronze', color: '#dd6633', width: 5, note: 'Khetri to Oman route' },
    { from: 'lothal', via: 'dilmun', to: null, commodity: 'Carnelian', color: '#cc2222', width: 4, note: 'Luxury export' },
    // Supply: Tamil Nadu -> IVC
    { from: 'kodumanal', via: null, to: 'lothal', commodity: 'Iron', color: '#227744', width: 3, note: 'Fish sign supply corridor' },
    { from: 'karur', via: null, to: 'lothal', commodity: 'Iron', color: '#227744', width: 3, note: 'TN iron-working hub' },
    { from: 'keeladi', via: null, to: 'lothal', commodity: 'Textiles', color: '#8844aa', width: 3, note: 'Cotton textile supply' },
    { from: 'thulukarpatti', via: null, to: 'lothal', commodity: 'Ivory/Shell', color: '#997744', width: 2, note: 'Southern coast supply' },
    { from: 'teriruveli', via: 'magan', to: null, commodity: 'Timber', color: '#664422', width: 2, note: 'Timber to Gulf' },
    // Internal
    { from: 'harappa', via: null, to: 'mohenjodaro', commodity: 'Internal', color: '#555555', width: 2, note: 'Indus corridor' },
    { from: 'khetri', via: null, to: 'lothal', commodity: 'Copper ore', color: '#dd6633', width: 3, note: 'Khetri copper mines' },
    { from: 'dholavira', via: null, to: 'lothal', commodity: 'Internal', color: '#555555', width: 1, note: 'Gujarat coast' },
    { from: 'shortughai', via: null, to: 'harappa', commodity: 'Lapis lazuli', color: '#2244aa', width: 2, note: 'Afghanistan trade post' },
];
const BASE_UNIT = 0.856;
const weights = [
    { mult: 1, g: BASE_UNIT, series: 'binary', app: 'Gold dust' },
    { mult: 2, g: BASE_UNIT * 2, series: 'binary', app: '' },
    { mult: 4, g: BASE_UNIT * 4, series: 'binary', app: 'Carnelian' },
    { mult: 8, g: BASE_UNIT * 8, series: 'binary', app: '' },
    { mult: 16, g: BASE_UNIT * 16, series: 'binary', app: 'Copper ref' },
    { mult: 32, g: BASE_UNIT * 32, series: 'binary', app: '' },
    { mult: 64, g: BASE_UNIT * 64, series: 'binary', app: '' },
    { mult: 160, g: 137, series: 'decimal', app: 'Cotton bale' },
    { mult: 200, g: 171.2, series: 'decimal', app: '' },
    { mult: 500, g: 685, series: 'decimal', app: 'Oil jar' },
    { mult: 1000, g: 1370, series: 'decimal', app: 'Bulk grain' },
];
const commodities = [
    { sign: 'S-342', name: 'Jar goods', route: 'Mesopotamia', pct: '10%' },
    { sign: 'S-218', name: 'Iron', route: 'TN → North', pct: '—' },
    { sign: 'S-301', name: 'Textiles', route: 'Mesopotamia', pct: '—' },
    { sign: 'S-184', name: 'Copper-bronze', route: 'Dilmun/Magan', pct: '—' },
    { sign: 'S-176', name: 'Carnelian', route: 'Dilmun', pct: '—' },
    { sign: 'S-200', name: 'Timber', route: 'Magan', pct: '—' },
    { sign: 'S-211', name: 'Ivory/Shell', route: 'North', pct: '—' },
    { sign: 'S-89', name: 'Gold', route: 'Mesopotamia', pct: '—' },
];
const signRoleMap = new Map([
    [1, { role: 'weight', ref_code: null }], [2, { role: 'weight', ref_code: null }],
    [3, { role: 'weight', ref_code: null }], [4, { role: 'weight', ref_code: null }],
    [5, { role: 'weight', ref_code: null }], [6, { role: 'weight', ref_code: null }],
    [7, { role: 'weight', ref_code: null }],
    [10, { role: 'quantity', ref_code: null }], [11, { role: 'quantity', ref_code: null }],
    [12, { role: 'quantity', ref_code: null }], [13, { role: 'quantity', ref_code: null }],
    [14, { role: 'quantity', ref_code: null }], [15, { role: 'quantity', ref_code: null }],
    [59, { role: 'terminal', ref_code: 'mesopotamia' }],
    [60, { role: 'terminal', ref_code: 'dilmun' }],
    [61, { role: 'terminal', ref_code: 'magan' }],
    [62, { role: 'terminal', ref_code: 'internal_north' }],
    [63, { role: 'terminal', ref_code: 'internal_south' }],
    [89, { role: 'commodity', ref_code: 'gold' }],
    [99, { role: 'structural', ref_code: null }],
    [176, { role: 'commodity', ref_code: 'carnelian' }],
    [184, { role: 'commodity', ref_code: 'copper' }],
    [200, { role: 'commodity', ref_code: 'timber' }],
    [211, { role: 'commodity', ref_code: 'ivory' }],
    [218, { role: 'commodity', ref_code: 'iron' }],
    [267, { role: 'structural', ref_code: null }],
    [301, { role: 'commodity', ref_code: 'textile' }],
    [342, { role: 'commodity', ref_code: 'jar' }],
]);
const commodityMap = new Map([
    ['jar', { label: 'JAR GOODS' }], ['iron', { label: 'IRON GOODS' }],
    ['carnelian', { label: 'CARNELIAN' }], ['copper', { label: 'COPPER-BRONZE' }],
    ['textile', { label: 'TEXTILES' }], ['timber', { label: 'TIMBER' }],
    ['ivory', { label: 'IVORY/SHELL' }], ['gold', { label: 'GOLD' }],
]);
const routeMap = new Map([
    ['mesopotamia', { label: 'MESOPOTAMIA', destination: 'Ur / Kish / Tell Asmar' }],
    ['dilmun', { label: 'DILMUN', destination: 'Bahrain entrepot' }],
    ['magan', { label: 'MAGAN', destination: 'Oman copper return' }],
    ['internal_north', { label: 'NORTH', destination: 'Harappa / Mohenjo-daro' }],
    ['internal_south', { label: 'SOUTH', destination: 'Tamil Nadu supply' }],
]);
// === SITE LOOKUP =============================================================
function findSite(id) {
    return sites.find(s => s.id === id);
}
function siteRadius(type) {
    const m = {
        'ivc-major': 7, 'ivc-port': 8, 'ivc-minor': 5, 'ivc-outpost': 4,
        'tn-supply': 5, 'meso-dest': 6, 'gulf-hub': 6, 'resource': 4
    };
    return m[type];
}
function siteColor(type) {
    const m = {
        'ivc-major': '#c4924a', 'ivc-port': '#c4924a', 'ivc-minor': '#c4924a', 'ivc-outpost': '#c4924a',
        'tn-supply': '#227744', 'meso-dest': '#b5121b', 'gulf-hub': '#8b7355', 'resource': '#dd6633'
    };
    return m[type];
}
function isMajorSite(type) {
    return type === 'ivc-major' || type === 'ivc-port' || type === 'meso-dest';
}
/** Quadratic Bezier curve between two lat/lon points.
 *  Curvature > 0 bows left (from a's perspective), < 0 bows right. */
function curvePoints(a, b, curvature) {
    const n = 20;
    const midLat = (a[0] + b[0]) / 2;
    const midLon = (a[1] + b[1]) / 2;
    const dLat = b[0] - a[0];
    const dLon = b[1] - a[1];
    const offLat = -dLon * curvature;
    const offLon = dLat * curvature;
    const cLat = midLat + offLat;
    const cLon = midLon + offLon;
    const pts = [];
    for (let i = 0; i <= n; i++) {
        const t = i / n;
        const lat = (1 - t) * (1 - t) * a[0] + 2 * (1 - t) * t * cLat + t * t * b[0];
        const lon = (1 - t) * (1 - t) * a[1] + 2 * (1 - t) * t * cLon + t * t * b[1];
        pts.push([lat, lon]);
    }
    return pts;
}
/** Resolve a route to an array of LatLon points.
 *  BUG FIX: handles all four cases:
 *    1. from -> to (no via)
 *    2. from -> via -> to (multi-segment)
 *    3. from -> via (to is null — route ends at via)
 *    4. from only (via and to both null — skip)
 */
function resolveRoutePoints(route) {
    const from = findSite(route.from);
    if (!from)
        return null;
    const fromLL = [from.lat, from.lon];
    // Case 4: nowhere to go
    if (!route.via && !route.to)
        return null;
    // Case 1: direct from -> to
    if (!route.via && route.to) {
        const to = findSite(route.to);
        if (!to)
            return null;
        return curvePoints(fromLL, [to.lat, to.lon], 0.15);
    }
    // Case 3: from -> via (to is null, route terminates at via)
    if (route.via && !route.to) {
        const via = findSite(route.via);
        if (!via)
            return null;
        return curvePoints(fromLL, [via.lat, via.lon], 0.15);
    }
    // Case 2: from -> via -> to (multi-segment)
    if (route.via && route.to) {
        const via = findSite(route.via);
        const to = findSite(route.to);
        if (!via || !to)
            return null;
        const seg1 = curvePoints(fromLL, [via.lat, via.lon], 0.15);
        const seg2 = curvePoints([via.lat, via.lon], [to.lat, to.lon], 0.15);
        return [...seg1, ...seg2];
    }
    return null;
}
const routeLines = [];
/** Render OpenTopoMap tiles, 13 trade route polylines, 22 site markers,
 *  and a volume legend. Routes render first (z-order under sites).
 *  Polyline refs are stored in routeLines[] for commodity filtering. */
function renderMap(map) {
    // Tile layer
    L.tileLayer('https://{s}.tile.opentopomap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; OpenTopoMap CC-BY-SA &copy; OSM',
        maxZoom: 17
    }).addTo(map);
    // Routes FIRST (under sites)
    let routeCount = 0;
    tradeRoutes.forEach(route => {
        const points = resolveRoutePoints(route);
        if (!points || points.length === 0) {
            console.warn(`ROUTE SKIPPED: ${route.commodity} (${route.from} -> ${route.via || '-'} -> ${route.to || '-'})`);
            return;
        }
        console.log(`ROUTE OK: ${route.commodity} — ${points.length} points`);
        routeCount++;
        const line = L.polyline(points, {
            color: route.color,
            weight: route.width,
            opacity: 0.8,
            smoothFactor: 1.5,
            lineCap: 'round'
        }).addTo(map);
        routeLines.push({ line, route });
        line.bindPopup(`<strong>${route.commodity}</strong><br>${route.note}<br>` +
            `<span style="opacity:0.5">width = ${route.width}px (relative volume)</span>`);
        line.on('mouseover', () => line.setStyle({ opacity: 1, weight: route.width + 2 }));
        line.on('mouseout', () => {
            const dimmed = line.options._dimmed;
            line.setStyle({ opacity: dimmed ? 0.15 : 0.8, weight: route.width });
        });
    });
    // Sites ON TOP (after routes)
    sites.forEach(site => {
        const r = siteRadius(site.type);
        const col = siteColor(site.type);
        const marker = L.circleMarker([site.lat, site.lon], {
            radius: r,
            fillColor: col,
            fillOpacity: 0.85,
            color: col,
            weight: 1.5,
            opacity: 0.5
        }).addTo(map);
        marker.bindPopup(`<strong>${site.name}</strong><br>${site.period}<br>` +
            `<span style="opacity:0.5">${site.lat.toFixed(2)}°N, ${site.lon.toFixed(2)}°E</span>`);
        const labelClass = 'site-label' + (isMajorSite(site.type) ? ' site-label-major' : '');
        const dir = site.lon > 65 ? 'right' : 'left';
        const off = site.lon > 65 ? [10, 0] : [-10, 0];
        marker.bindTooltip(site.name, {
            permanent: true,
            direction: dir,
            offset: off,
            className: labelClass
        });
    });
    console.log(`ROUTES RENDERED: ${routeCount} / ${tradeRoutes.length}`);
    // Legend
    const legend = L.control({ position: 'bottomleft' });
    legend.onAdd = () => {
        const div = L.DomUtil.create('div');
        div.style.cssText = 'background:rgba(15,12,7,0.8);padding:8px 12px;border:1px solid rgba(196,146,74,0.2);font-family:JetBrains Mono,monospace;font-size:9px;color:rgba(240,230,208,0.5);';
        div.innerHTML =
            '<div style="display:flex;align-items:center;gap:6px;margin-bottom:4px">' +
                '<div style="width:40px;height:8px;background:#cc6600;border-radius:2px;opacity:0.8"></div> width ∝ trade volume</div>' +
                '<div style="font-size:8px;opacity:0.4">● IVC  ● TN supply  ● Mesopotamia  ● Gulf</div>';
        return div;
    };
    legend.addTo(map);
}
// === DB QUERIES ==============================================================
function queryScalar(db, sql) {
    const r = db.exec(sql);
    if (r.length > 0 && r[0].values.length > 0)
        return r[0].values[0][0];
    return null;
}
function queryRows(db, sql) {
    const r = db.exec(sql);
    if (r.length > 0)
        return r[0].values;
    return [];
}
// === SEAL CARDS ==============================================================
/** Decode two CISI seals from the corpus DB. Resolution chain:
 *  1. cisi_inscription -> Parpola sign list (from corpus DB)
 *  2. Parpola number -> signRoleMap (embedded codebook)
 *  3. role=commodity -> commodityMap, role=terminal -> routeMap */
function renderSealCards(db) {
    const container = document.getElementById('seal-cards');
    if (!container)
        return;
    const sealIds = ['M-67A', 'M-52A'];
    for (const sealId of sealIds) {
        let signs = [];
        let desc = '';
        try {
            const rows = queryRows(db, `SELECT signs, description FROM cisi_inscription WHERE id='${sealId}'`);
            if (rows.length > 0) {
                signs = JSON.parse(rows[0][0]);
                desc = rows[0][1] || '';
            }
        }
        catch { }
        // Resolve signs via Parpola number -> codebook (embedded maps)
        // sign_role.sign_id uses Parpola numbers (P342 -> 342), NOT Mahadevan
        let commName = '—';
        let routeName = 'domestic';
        let resolved = 0;
        for (const p of signs) {
            const pNum = parseInt(p.replace('P', '') || '0');
            const sr = signRoleMap.get(pNum);
            if (!sr)
                continue;
            resolved++;
            if (sr.role === 'commodity' && sr.ref_code) {
                const ce = commodityMap.get(sr.ref_code);
                if (ce)
                    commName = ce.label;
            }
            if (sr.role === 'terminal' && sr.ref_code) {
                const re = routeMap.get(sr.ref_code);
                if (re)
                    routeName = `${re.label} → ${re.destination}`;
            }
        }
        const card = document.createElement('div');
        card.className = 'seal-card';
        card.innerHTML = `
      <div class="seal-id">${sealId} · ${desc}</div>
      <div class="field-row"><span class="field-label">SIGNS</span><span class="field-value sign">${signs.join(' ')}</span></div>
      <div class="field-row"><span class="field-label">F2 COMMODITY</span><span class="field-value">${commName}</span></div>
      <div class="field-row"><span class="field-label">F5 ROUTE</span><span class="field-value">${routeName}</span></div>
      <div class="field-row"><span class="field-label">DECODED</span><span class="field-value sign">${resolved}/${signs.length} signs</span></div>
      <div class="tag-line">${commName} / ${routeName}</div>`;
        container.appendChild(card);
    }
}
// === FREQUENCY CHART =========================================================
function renderFreqChart() {
    const data = [
        { rank: 1, cum: 10 }, { rank: 5, cum: 28 }, { rank: 10, cum: 40 }, { rank: 20, cum: 55 },
        { rank: 40, cum: 70 }, { rank: 67, cum: 80 }, { rank: 100, cum: 90 }, { rank: 120, cum: 93 }
    ];
    const svg = d3.select('#freq-chart');
    const W = 250, H = 140;
    const m = { top: 10, right: 10, bottom: 25, left: 30 };
    const w = W - m.left - m.right;
    const h = H - m.top - m.bottom;
    const g = svg.append('g').attr('transform', `translate(${m.left},${m.top})`);
    const x = d3.scaleLinear().domain([1, 120]).range([0, w]);
    const y = d3.scaleLinear().domain([0, 100]).range([h, 0]);
    g.append('g').attr('transform', `translate(0,${h})`).call(d3.axisBottom(x).ticks(5).tickSize(0))
        .selectAll('text').attr('fill', 'var(--dim)').attr('font-size', '8px');
    g.append('g').call(d3.axisLeft(y).ticks(4).tickSize(0))
        .selectAll('text').attr('fill', 'var(--dim)').attr('font-size', '8px');
    g.selectAll('.domain').attr('stroke', 'var(--faint)');
    const line = d3.line().x((d) => x(d.rank)).y((d) => y(d.cum)).curve(d3.curveMonotoneX);
    g.append('path').datum(data).attr('d', line).attr('fill', 'none').attr('stroke', 'var(--clay)').attr('stroke-width', 2);
    data.forEach(d => g.append('circle').attr('cx', x(d.rank)).attr('cy', y(d.cum)).attr('r', 2.5).attr('fill', 'var(--clay)'));
    // 80% threshold
    g.append('line').attr('x1', x(67)).attr('y1', y(0)).attr('x2', x(67)).attr('y2', y(80))
        .attr('stroke', 'var(--tba-red)').attr('stroke-dasharray', '4,3').attr('stroke-width', 1);
    g.append('line').attr('x1', x(1)).attr('y1', y(80)).attr('x2', x(67)).attr('y2', y(80))
        .attr('stroke', 'var(--tba-red)').attr('stroke-dasharray', '4,3').attr('stroke-width', 1);
    g.append('text').attr('x', x(67) + 3).attr('y', y(80) - 3)
        .attr('font-family', 'JetBrains Mono').attr('font-size', '7px').attr('fill', 'var(--tba-red)').text('67 signs = 80%');
}
// === WEIGHT BARS =============================================================
function renderWeightBars() {
    const container = document.getElementById('weight-bars');
    if (!container)
        return;
    const maxG = 1370;
    weights.forEach(w => {
        const pct = Math.max(3, (Math.log(w.g + 1) / Math.log(maxG + 1)) * 100);
        const row = document.createElement('div');
        row.className = 'weight-row';
        row.innerHTML = `
      <span class="weight-label">×${w.mult}</span>
      <div class="weight-bar ${w.series}" style="width:${pct}%"></div>
      <span class="weight-grams">${w.g < 100 ? w.g.toFixed(2) : w.g.toFixed(0)}g${w.app ? ' ' + w.app : ''}</span>`;
        container.appendChild(row);
    });
}
// === COMMODITY TABLE =========================================================
let selectedCommodity = null;
/** Dim non-matching trade routes to 15% opacity; null clears filter. */
function highlightCommodity(name) {
    selectedCommodity = name;
    routeLines.forEach(({ line, route }) => {
        const match = !name || route.commodity === name;
        line.setStyle({ opacity: match ? 0.8 : 0.15, weight: route.width });
        line.options._dimmed = !match;
    });
}
function renderCommTable() {
    const table = document.getElementById('comm-table');
    if (!table)
        return;
    commodities.forEach(c => {
        const tr = document.createElement('tr');
        tr.innerHTML = `<td>${c.sign}</td><td>${c.name}</td><td>${c.route}</td>`;
        tr.addEventListener('click', () => {
            const next = selectedCommodity === c.name ? null : c.name;
            highlightCommodity(next);
            table.querySelectorAll('tr').forEach(r => r.classList.remove('active'));
            if (next)
                tr.classList.add('active');
        });
        table.appendChild(tr);
    });
}
// === DATING ==================================================================
function renderDating(db) {
    const container = document.getElementById('dating-content');
    if (!container)
        return;
    try {
        const rows = queryRows(db, `SELECT rs.site_name, rm.cal_bce_mid, rm.method
       FROM radiometric_measurement rm
       JOIN radiometric_sample rsp ON rsp.sample_id = rm.sample_id
       JOIN radiometric_site rs ON rs.site_id = rsp.site_id
       ORDER BY rm.cal_bce_mid DESC LIMIT 5`);
        if (rows.length > 0) {
            container.innerHTML = rows.map(row => `<div style="display:flex;justify-content:space-between;padding:2px 0;border-bottom:1px solid var(--faint)">` +
                `<span style="font-family:JetBrains Mono;font-size:9px;color:var(--dim)">${row[0]}</span>` +
                `<span style="font-family:JetBrains Mono;font-size:10px;color:var(--clay)">${row[1]} BCE</span>` +
                `<span style="font-family:JetBrains Mono;font-size:8px;color:var(--dim)">${row[2]}</span></div>`).join('');
        }
    }
    catch {
        container.innerHTML = '<span style="font-family:JetBrains Mono;font-size:9px;color:var(--dim)">No radiometric data</span>';
    }
}
// === MAIN ====================================================================
function renderDashboard(db) {
    // Map needs the container visible and sized before init
    const mapEl = document.getElementById('map');
    const map = L.map(mapEl, {
        center: [22, 62],
        zoom: 4,
        minZoom: 3,
        maxZoom: 8,
        zoomControl: true
    });
    // Force Leaflet to recalculate size after dashboard becomes visible
    setTimeout(() => map.invalidateSize(), 100);
    renderMap(map);
    renderSealCards(db);
    renderFreqChart();
    renderWeightBars();
    renderCommTable();
    renderDating(db);
}
// === DROP ZONE ===============================================================
/** Load a dropped .db file via sql.js WASM, fade out landing, render dashboard. */
async function handleFile(file) {
    const buf = await file.arrayBuffer();
    const SQL = await initSqlJs({
        locateFile: (f) => `https://cdn.jsdelivr.net/npm/sql.js@1.10.3/dist/${f}`
    });
    const db = SQL.Database ? new SQL.Database(new Uint8Array(buf)) : SQL.Database(new Uint8Array(buf));
    const landing = document.getElementById('landing');
    const dashboard = document.getElementById('dashboard');
    landing.style.opacity = '0';
    setTimeout(() => {
        landing.style.display = 'none';
        dashboard.style.display = 'block';
        renderDashboard(db);
    }, 600);
}
function setupDropZone() {
    const dropZone = document.getElementById('drop-zone');
    ['dragenter', 'dragover'].forEach(e => dropZone.addEventListener(e, ev => { ev.preventDefault(); dropZone.classList.add('dragover'); }));
    ['dragleave', 'drop'].forEach(e => dropZone.addEventListener(e, () => dropZone.classList.remove('dragover')));
    dropZone.addEventListener('drop', async (ev) => {
        ev.preventDefault();
        const file = ev.dataTransfer?.files[0];
        if (file)
            await handleFile(file);
    });
    dropZone.addEventListener('click', () => {
        const inp = document.createElement('input');
        inp.type = 'file';
        inp.accept = '.db';
        inp.onchange = async () => {
            const file = inp.files?.[0];
            if (file)
                await handleFile(file);
        };
        inp.click();
    });
}
// === THEME ===================================================================
function setupTheme() {
    document.querySelectorAll('.theme-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            const theme = btn.dataset.t;
            document.documentElement.setAttribute('data-theme', theme);
            document.querySelectorAll('.theme-btn').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            localStorage.setItem('ledger-theme', theme);
        });
    });
    const saved = localStorage.getItem('ledger-theme');
    if (saved) {
        document.documentElement.setAttribute('data-theme', saved);
        document.querySelectorAll('.theme-btn').forEach(b => {
            b.classList.toggle('active', b.dataset.t === saved);
        });
    }
}
// === INIT ====================================================================
setupDropZone();
setupTheme();
