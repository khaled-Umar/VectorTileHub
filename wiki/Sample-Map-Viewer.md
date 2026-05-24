# Sample Map Viewer

The sample app serves a map viewer from `wwwroot/index.html`.

The viewer:

- loads layer metadata from `/vector-tile-hub/layers`
- builds a vector tile source from `tileUrlTemplate`
- renders layer 82 with a parcel-oriented style
- links to metadata, health, and Hangfire dashboard endpoints

The page uses OpenLayers from CDN. For isolated deployments, copy OpenLayers assets into `wwwroot` and update the script and stylesheet paths.
