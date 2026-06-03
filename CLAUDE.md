<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
at specs/002-vector-tile-server/plan.md
<!-- SPECKIT END -->

## Conventions

- **HTTP endpoints: use MVC controllers, never minimal APIs.** Define endpoints with
  `[ApiController]` classes deriving from `ControllerBase` and `[HttpGet]`/`[HttpPost]`
  attributes, mapped via `app.MapControllers()`. Do not use `app.MapGet`/`MapPost`/etc.
- The library exposes **no HTTP endpoints** — it registers services only
  (`IVectorTileService`, `IVectorTileLayerConfigProvider`, `IVectorTileCacheAdmin`). The host
  owns and secures the HTTP surface via its own controllers.
