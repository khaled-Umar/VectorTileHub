const layerName = document.querySelector("#layerName");
const zoomRange = document.querySelector("#zoomRange");
const tileSource = document.querySelector("#tileSource");
const message = document.querySelector("#message");

const parcelStyle = new ol.style.Style({
  fill: new ol.style.Fill({ color: "rgba(216, 171, 84, 0.42)" }),
  stroke: new ol.style.Stroke({ color: "#5b4a26", width: 1.15 })
});

const selectedStyle = new ol.style.Style({
  fill: new ol.style.Fill({ color: "rgba(47, 109, 87, 0.36)" }),
  stroke: new ol.style.Stroke({ color: "#174536", width: 2 })
});

const baseLayer = new ol.layer.Tile({
  source: new ol.source.XYZ({
    url: "https://tile.openstreetmap.org/{z}/{x}/{y}.png",
    attributions: "© OpenStreetMap contributors",
    maxZoom: 19
  })
});

const map = new ol.Map({
  target: "map",
  layers: [baseLayer],
  view: new ol.View({
    center: ol.proj.fromLonLat([39.2453, 21.8808]),
    zoom: 12,
    maxZoom: 21
  })
});

fetch("/vector-tile-hub/layers")
  .then((response) => {
    if (!response.ok) {
      throw new Error(`Layer metadata returned ${response.status}`);
    }
    return response.json();
  })
  .then((payload) => {
    const layer = payload.layers?.find((item) => item.id === 82) ?? payload.layers?.[0];
    if (!layer) {
      throw new Error("No enabled VectorTileHub layers were returned.");
    }

    layerName.textContent = layer.layerName;
    zoomRange.textContent = `${layer.minZoom}-${layer.maxZoom}`;
    tileSource.textContent = layer.tileUrlTemplate.replace("{z}/{x}/{y}.pbf", "");

    const vectorLayer = new ol.layer.VectorTile({
      declutter: true,
      source: new ol.source.VectorTile({
        format: new ol.format.MVT(),
        url: layer.tileUrlTemplate
      }),
      style: (feature) => {
        const landUse = String(feature.get("PARCEL_LANDUSE") ?? "").toLowerCase();
        if (landUse.includes("service") || landUse.includes("public")) {
          return selectedStyle;
        }
        return parcelStyle;
      }
    });

    map.addLayer(vectorLayer);
    map.getView().fit(
      ol.proj.transformExtent([38.9324, 21.4075, 39.5582, 22.3542], "EPSG:4326", "EPSG:3857"),
      { padding: [36, 36, 36, 36], maxZoom: 13, duration: 300 }
    );
    message.textContent = "Layer metadata loaded. Tile requests will use the configured SQL Server source.";
    window.setTimeout(() => {
      message.textContent = "";
    }, 5000);
  })
  .catch((error) => {
    layerName.textContent = "Unavailable";
    message.textContent = error.message;
  });
