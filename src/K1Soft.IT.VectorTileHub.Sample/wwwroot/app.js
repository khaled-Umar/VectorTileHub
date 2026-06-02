// Renders VectorTileHub PBF tiles in OpenLayers using the SLD-derived Mapbox GL style
// (wwwroot/ol-style.json), produced by: `dotnet run --project ... -- gen-style tmp/layerStyle.sld wwwroot/ol-style.json`.
(function () {
  const message = document.getElementById('message');
  const setMessage = (text) => { if (message) message.textContent = text; };

  async function loadMetadata() {
    try {
      const res = await fetch('/vector-tile-hub/layers/82');
      if (!res.ok) return null;
      const meta = await res.json();
      const name = document.getElementById('layerName');
      const zoom = document.getElementById('zoomRange');
      if (name) name.textContent = meta.layerName ?? 'Layer 82';
      if (zoom) zoom.textContent = `${meta.minZoom} – ${meta.maxZoom}`;
      return meta;
    } catch (e) { return null; }
  }

  async function fitToData(map) {
    try {
      const res = await fetch('/sample/layers/82/extent');
      if (!res.ok) return false;
      const e = await res.json();
      if (![e.minX, e.minY, e.maxX, e.maxY].every(Number.isFinite)) return false;
      map.getView().fit([e.minX, e.minY, e.maxX, e.maxY], { maxZoom: 17, padding: [24, 24, 24, 24] });
      return true;
    } catch (err) {
      return false;
    }
  }

  async function render() {
    const meta = await loadMetadata();

    // Base map + view so the data has geographic context and the map opens somewhere useful.
    const map = new ol.Map({
      target: 'map',
      layers: [new ol.layer.Tile({ source: new ol.source.OSM() })],
      view: new ol.View({ center: ol.proj.fromLonLat([0, 0]), zoom: 2 })
    });

    try {
      // Load the SLD-derived GL style and clamp the vector source's zoom range to the layer's
      // configured min/maxZoom. Setting the source maxzoom makes the renderer OVER-ZOOM beyond it
      // (keep drawing the top-zoom tiles, scaled) instead of requesting tiles that don't exist
      // above maxZoom — so the layer stays visible when you zoom past the layer's max.
      const style = await (await fetch('/ol-style.json')).json();
      if (meta && style.sources && style.sources.vth) {
        if (Number.isFinite(meta.minZoom)) style.sources.vth.minzoom = meta.minZoom;
        if (Number.isFinite(meta.maxZoom)) style.sources.vth.maxzoom = meta.maxZoom;
      }
      await olms.apply(map, style);
    } catch (err) {
      setMessage('Could not load ol-style.json. Generate it: dotnet run -- gen-style tmp/layerStyle.sld wwwroot/ol-style.json');
      console.error(err);
      return;
    }

    const fitted = await fitToData(map);
    if (fitted) {
      setMessage('');
    } else {
      // Data location unknown (extent query unavailable) — leave a hint instead of a blank world view.
      setMessage('Style loaded. Zoom in to the layer area (z ≥ 12) to see tiles — or check the DB/extent endpoint.');
    }
  }

  render();
})();
