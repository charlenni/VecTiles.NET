# Arbeitsnotizen: Merge zu einem RenderedTileLayer + PlacementService

> Stand: 2026-11-21. Wird vom Agenten (Mistral Vibe) gepflegt, falls die Session abbricht.
> Diese Datei ist nicht Teil der Projektdokumentation; sie kann nach Abschluss gelöscht oder committet werden.

## Ziel (freigegeben, umgesetzt; Einzelheiten unten)

Es gibt genau **einen** Karten-Layer (`RenderedTileLayer`), der:
- pro Frame zuerst Linien/Fills/Circles zeichnet (Variante A: pro Style-Layer über alle Tiles),
- danach Symbole zeichnet, aber **nur**, wenn ein `PlacementResult` vorliegt (leer/null → keine Symbole, alter Satz → dieser bleibt per Latch),
- im Hintergrund das Placement neu berechnen lässt und bei Fertigstellung einmalig neu zeichnet.

`RenderedSymbolsLayer` und `RenderedSymbolsLayerRenderer` fallen weg.

## Projekt-Zuordnung (Regel von Dirk, strikt einhalten; Positionen im Code inzwischen so umgesetzt)

| Baustein | Projekt |
|---|---|
| `PlacementService` (Zustandsmaschine), `SymbolPlacementEngine`, `PlacementResult`, `PlacementEntry` (Skia für Textmessung) | `VecTiles.Renderers.Skia` (committed `6db1d0f`) |
| `TileFetchScheduler`, `TileInfoExtensions`, `IRenderedTileSource` (Mapsui-frei, `Task<IRenderedTile?>`) | `VecTiles.Renderers.Common` |
| `TilesKey`, Tile-Cache, allgemeine Primitives | `VecTiles.Common` |
| `RenderedTileLayer : BaseLayer` + Renderer (`RenderedTileLayerRenderer`, "rendered-tile-layer") | `VecTiles.Controls.Mapsui` |

## Bereits von Dirk entschieden (Design, umsetzen!)
1. **Ein Layer statt zwei**: Placement-Logik (Cache, Scheduler, Latch, Cooldown, `SynchronousPlacement`) aus `RenderedSymbolsLayer` in den `RenderedTileLayer` (bzw. PlacementService) überführen.
2. **Fetch-Kette Mapsui-frei**: `IRenderedTileSource.GetTileAsync` liefert `IRenderedTile?` statt Mapsui-`IFeature`; `RenderedTileFeature` entfällt. `IRenderedTileSource` wandert nach `VecTiles.Renderers.Common`.
3. **Latch-Regel**: Latch hat einen Zoom. Gleicher Zoom (Pan) → alter Symbolsatz bleibt sichtbar, bis der neue fertig ist. **Zoom geändert → Latch leeren**, erst wieder Symbole nach neuem Placement.
4. **Abbruch bei Zoom/Set-Wechsel**: genau ein aktiver Placement-Lauf mit eigener `CancellationTokenSource`; Token im `TileFetchScheduler.FetchAsync` (zwischen Tiles) und in `SymbolPlacementEngine.Compute` (zwischen Style-Layern) prüfen. Abgebrochenes Ergebnis verwerfen; fertige Ergebnisse anderer Keys bleiben im Cache (Rückzoom ohne Neuberechnung).
5. **Zoom-Stabilisierung (Debounce ~250 ms)**: Trigger ist "Zoom über die Zeitspanne konstant", beobachtet über die Frames (`UpdateViewport` pro Frame). Debounce gilt **nur für die Placement-Berechnung** — das Tile-Fetchen zum neuen Zoom startet sofort (Kacheln nützen später, gecacht). Timer resetted bei jeder Key-Änderung (deckt Oszillieren um Zoom-Schwellen ab).
6. **`GetResult` liefert immer ein Ergebnis** (leeres `PlacementResult` statt null beim ersten Aufruf) — kein Null-Check im Renderer.
7. **Geometrie liefert der Service**: PlacementService besitzt den einzigen Tile-Cache (Ein-Fetch-Pfad für Geometrie UND Placement). Pro angekommener Kachel Event `TileArrived` → Layer → `DataHasChanged()` → progressive Geometrie. `GetResult(key)` liefert Kacheln (fürs Zeichnen) + PlacementResult (für die Symbole).
8. **Ortsfeste Symbole bei Kachelset-Änderung** (Dirk-Wunsch):
   - a) Deterministischer Tie-Break statt Center-first-Fetch-Reihenfolge (Kachel-Koordinate + Feature-Id) — kleine Änderung in der Placement-Engine, mit umsetzen.
   - b) Placement über gepolstertes Kachel-Set (eine extra Ringrolle außerhalb des Bildschirms; gezeichnet wird nur der Bildschirmbereich) — mit umsetzen.
   - c) Linien-Anker aus Heimatkachel statt dynamisch verschmolzenem Lauf — **separater späterer Schritt**, erst nach a/b evaluieren.
9. Symbolblock kommt komplett nach der Geometrie, in Style-Layer-Reihenfolge (kein Interleaving mit Geometrie-Layern — geht nur mit Variante B / ganzem Viewport-Pass). Von Dirk so abgesegnet (vgl. Google-Verhalten).

## Umsetzungs-Schritte (ursprüngliche Planung; veraltet, massgeblich ist "Noch zu tun" unten)

1. `SymbolPlacement.cs` (Engine + Result-Records) nach `VecTiles.Renderers.Skia` verschieben.
2. `IRenderedTileSource` Mapsui-frei (Rückgabe `IRenderedTile?`) und nach `VecTiles.Renderers.Common` verschieben; `RenderedTileFeature` entfernen; alle Aufrufer/Tests anpassen.
3. `TileFetchScheduler` Mapsui-frei (`MPoint` → `(double, double)`) und nach `VecTiles.Common` verschieben; CancellationToken durchreichen.
4. `PlacementService` neu in `VecTiles.Common`: Tile-Cache, Placement-Cache, Latch (mit Zoom), einzelner Lauf + CTS, Debounce-Timer, Incomplete-Cooldown, `ClearCache()`/Generation; Events `TileArrived`/`PlacementCompleted`.
5. `RenderedTileLayer : BaseLayer` in `VecTiles.Controls.Mapsui` mit Custom-Layer-Renderer: Viewport → (Tiles, kanonischer Zoom mit Snap-Epsilon, unrotiertes worldToScreen in Placement-Space, Center), Variant-A-Zeichnung (Style-Layer × Tiles, symbol-Einträge überspringen), danach Symbolblock aus PlacementResult; Events an `DataHasChanged()`.
6. `RenderedSymbolsLayer` + `RenderedSymbolsLayerRenderer` löschen.
7. SampleApp (`Samples/Avalonia/SampleApp/Views/MainView.axaml.cs`) auf einen Layer umstellen; `ClearCache` nur einmal; DebugLayer bleibt.
8. Tests anpassen: `SynchronousPlacement` am `RenderedTileLayer`; RenderedTileTests + Schanzengraben-Tests (AppPipeline/CrossTile/FrameRender/OverlapScan) auf neue Klasse umbauen; Latch-/Placement-Tests entsprechen erweitern (latch bei Zoomwechsel leeren, Abbruch bei Zoomwechsel).
9. Tie-Break (8a) und gepolstertes Kachel-Set (8b) implementieren.
10. Volle Suite Debug + Release grün (`env -u LD_LIBRARY_PATH dotnet test`), RenderedTile-Golden ggf. aktualisieren (siehe Workflow unten) und Dirk zum Ansehen legen.
11. Danach separat: Font-Resolver-Thema (FontCacheTests flaky unter Volllast, Idee: Font-Resolver pro Layer-Instanz statt globaler "last resolver wins").

## Historisch: Stand vor dem Merge-Teil 3 (verifiziert, inzwischen alles committed)

- HEAD = `6db1d0f` (Move the placement engine to VecTiles.Renderers.Skia); davor `b2f5e66` u. `a737815`. Auf `master` sind die Merge-Schritte teils schon committed.
- Ungespeicherte Merge-Arbeit (M/R/?? gemäß `git status`):
  - M `Source/Controls/VecTiles.Control.Mapsui/OMTRenderedTileSource.cs` – Cache `MemoryCache<IRenderedTile>`, `GetTileAsync → Task<IRenderedTile?>`. **Achtung:** Usings `using Mapsui;` und `using VecTiles.Controls.Mapsui.Extensions;` sind noch drin und ungenutzt → aufräumen.
  - M `Source/Controls/VecTiles.Control.Mapsui/RenderedTileLayer.cs` – BaseLayer-Variante?
  - R `Extensions/TileInfoExtensions.cs` → `Source/Renderers/VecTiles.Renderers.Common/Extensions/`
  - R `TileFetchScheduler.cs` → `Source/Renderers/VecTiles.Renderers.Common/` – Mapsui-frei, record `TileFetchResult(IReadOnlyList<IRenderedTile>? Tiles, bool Incomplete)`, center-first paralleler Fetch.
  - M `VecTiles.Renderers.Common.csproj`
  - ?? neu: `Source/Controls/VecTiles.Control.Mapsui/RenderedTileLayerRenderer.cs`, `Source/Renderers/VecTiles.Renderers.Common/Interfaces/IRenderedTileSource.cs` (verifiziert: `Task<IRenderedTile?> GetTileAsync(TileInfo)`, CRLF, `using BruTile;`, Namespace `VecTiles.Renderers.Common.Interfaces`), `Source/Renderers/VecTiles.Renderers.Skia/PlacementService.cs`, `PLACEMENT_MERGE_NOTES.md` (ungetrackt).
- Alt-Dateien existieren noch in `Source/Controls/VecTiles.Control.Mapsui/`: `IRenderedTileSource.cs` (alte IFeature-Signatur), `RenderedTileFeature.cs`, `RenderedSymbolsLayer.cs`, `RenderedSymbolsLayerRenderer.cs`, `RenderedTileStyle.cs`, `RenderedTileStyleRenderer.cs`, `RenderedTileLayer.cs`, `RenderedTileLayerRenderer.cs`, `Extensions/ZoomExtensions.cs`.
- Referenzen auf Alt-Typen (grep verifiziert): `Samples/Avalonia/SampleApp/Views/MainView.axaml.cs`; Tests: `RenderedTileTests.cs`, `RenderedSymbolsLayerLatchTests.cs`, `TileFetchSchedulerTests.cs`, `Schanzengraben{AppPipeline,CrossTile,FrameRender,OverlapScan}Tests.cs`, `Lindenhof{FrameRender,SingleFrame}Tests.cs`, `StrehlgasseFrameRenderTests.cs`; `Benchmarks/VecTiles.OpenMapTiles.Benchmarks/OMTDrawTileBenchmarks.cs`; Kommentar `Source/Styles/VecTiles.Styles.OpenMapTiles/OMTSymbolFactory.cs:69` (nur Kommentar).

### Technische Facts (Design von Dirk)

## Status NACH dem Merge (verifiziert; alle Commits lokal auf master, KEIN Push)
- `f1db934` Compile the unified tile layer (TilesKey-Operatoren, Extent.Grow, Usings)
- `9ae21d5` Sample app and benchmarks on the single tile layer
- `9c01514` Placement tests on the PlacementService, deadlock-free synchronous pass, new tile golden
- WICHTIGER eigener Bugfix dabei: `PlacementService.Update` hielt `_lock` während des blockierenden `SynchronousPlacement`-Laufs → Deadlock mit `AddTileToView` (nur bei echt asynchroner Quelle, z. B. RenderedTileTests). Der Sync-Lauf läuft jetzt außerhalb des Locks.
- Suite komplett grün: Debug+Release je Skia-Test 191, Common 59, Converters 2, DataSources 12, Styles 264. `VecTiles.Renderers.Test` enthält keine Test-(cs)Dateien (Hilfsprojekt).
- Golden aktualisiert: neu `md5 8302494f…` (committed). Vergleich in `Tests/VecTiles.Renderers.Skia.Test/Files/Results/`: `-PREVIOUS.png` (= alter Stand `2bfe99b2…`), `-diff.png` (rot = Unterschied new vs. previous), dazu `-OLD.png` (noch älterer Kachel-weiser Stand). **Dirk soll Result/Golden ansehen**; Rollback über Git jederzeit möglich.
- Offen für spätere Sessions: (a) Linien-Anker aus Heimatkachel statt verschmolzenem Lauf; (b) Font-Resolver pro Layer-Instanz (FontCacheTests flaky unter Volllast); (c) `PLACEMENT_MERGE_NOTES.md` ist ungetrackt — aufnehmen oder ignorieren; (d) `Program.cs` im Repo-Root ist ein Überbleibsel eines Absturzes — klären, ob gelöscht; (e) `tools.process.start` ist in dieser Umgebung defekt (process_start_failed) — lange Testläufe über klassenweise Filter (`--filter …` mit timeout 280)fahren.

### Technische Facts (Design von Dirk, Original)
- `PlacementService` liegt in `VecTiles.Renderers.Skia` (braucht Skia für Textmessung). State-Machine, Placement-Cache, Latch mit Zoom (Zoomwechsel → Latch leeren, erstmal keine Symbole), CTS (Abbruch bei Zoomwechsel), Debounce 250 ms, Events `TileArrived`/`PlacementCompleted`, `SynchronousPlacement`, Key = `(TilesKey, int Zoom)`.
- `TilesKey` in `VecTiles.Common/Primitives/TilesKey.cs`. Placement-Engine: Layer rückwärts (höchster Style-Layer gewinnt — letzter Style-File-Eintrag = Wichtigster, z. B. place_city > place_town > …), Quadtree nur wachsend (Platz belegt → nada, kein Entfernen), `LineSymbolMerger.MergeById`, OrderBy(SortOrder).ThenBy(Rank), `PlacementResult { PlacedByLayer, Rejected, Tree, Incomplete }`, `PlacementResult.Empty()` (nie null → kein Null-Check im Renderer).
- Envelopes für map-orientierte Symbole mit Rotations-Maximum vergrößern (Dirk-Frage dazu: Ja).
- Renderer-Signatur: `PointSymbolRenderer.Draw(canvas, context, pointSymbol, ref tree, placementWorldToScreen, rotation, showValidBorders, drawContext, drawWorldToScreen, checkForSpace)`; `LineSymbolRenderer.Draw(..., entry.Run)`.
- Zoom: `(int)Math.Floor(viewport.Resolution.ToZoomLevel() + 1e-6)`; `placementScale = zoomLevel.ToResolution() / viewport.Resolution`.
- Placement über gepolstertes Kachel-Set (placementExtent = screenExtent.Grow(tileWorldSize)); Zoom-Snap-Epsilon; unrotierter Placement-Viewport; deterministischer Tie-Break für ortsfeste Symbole bei Kachelset-Änderung.
- Icons dürfen während Neuberechnung NICHT verschwinden (Latch). Stabilisierung: Zoom-Debounce, Trigger über Frames.
- Overzoom-Konzept (Dirk): Z15+ wird aus Z14-Tile berechnet, keine neuen Daten — weitere Tiles unnötig.

## Test-Infrastruktur
- `Tests/VecTiles.Renderers.Skia.Test/Utilities.cs` löst `Utilities.DataFiles` (Repo-`Files/`) und `Utilities.TestFiles` (`Tests/…/Files/`) über Aufwärtssuche nach `VecTiles.NET.sln`; Tests laufen mit cwd `bin/<Config>/<TFM>/` — NIE cwd-relativ.
- Golden-Update: `VECTILES_RENDER_TEST_UPDATE_GOLDEN=true env -u LD_LIBRARY_PATH dotnet test --filter FullyQualifiedName~RenderedTileTests` schreibt in bin-Kopie; danach cp nach `Tests/VecTiles.Renderers.Skia.Test/Files/Originals/`. Aktueller Golden (committed): `RenderedTile-Z19-X274582-Y183615.png`, md5 `2bfe99b2…`; alte Ansicht liegt als `RenderedTile-Z19-X274582-Y183615-OLD.png` bei. Diff (3350 px, Labelzonen) ist Folge des center-first-Fetchings (Kollision-Ties entschieden anders). **Dirk freigeben lassen.**

## Umgebung / Workflow

- Builds/Tests nur mit `env -u LD_LIBRARY_PATH dotnet …` (sonst GLIBCXX-Fehler).
- Tests laufen mit cwd `bin/<Config>/<TFM>/`; immer über `Utilities.*`-Pfade (siehe oben), nicht `Path.Combine(_path, …)` relativ zu cwd.
- Golden-Update: `VECTILES_RENDER_TEST_UPDATE_GOLDEN=true dotnet test --filter FullyQualifiedName~RenderedTileTests` schreibt in die bin-Kopie; danach per cp zurück in Source-`Files/Originals/`.
- In offizieller `.scratch/` im Repo darf der Agent arbeiten; dort liegen (wiederhergestellt) `reflecttest.csproj` + `Program.cs`.
- Commit-Format: `git commit` mit Heredoc und Zeilen `Generated by Mistral Vibe.` + `Co-Authored-By: Mistral Vibe <vibe@mistral.ai>`; kein Push ohne Freigabe.
- AGENTS.md: .NET 10, xUnit; Builds/Tests nur via oben stehendem env-Kommando.

## Freigaben von Dirk
- Ohne Rückfragen arbeiten (Merge dauert länger; bei Crash hilft diese Datei). Kleine lokale Commits erlaubt (Rollback), KEIN Push ohne Freigabe. Golden-Diff vor/nach zeigen.

## Runde 2026-11-21b: Interleaving + Zoom-/Visibility-Filter (Dirk-Aufgabe)

Dirks Punkt: "Es werden nochimmer alle Linien/Fills/Circle gezeichnet und dann erst die Symbole." Zudem: visible=false und minzoom/maxzoom werden beim Zeichnen ignoriert.

Umgesetzt (alle grün, Golden UNVERÄNDERT gültig, md5 8302494f…):
1. `RenderedGeometryLayer`/`RenderedSymbolLayer` (Renderers.Common) tragen jetzt `MinZoom`/`MaxZoom` (float, -1 = ungesetzt, aus `ILayerStyle`) und `StyleIndex` (Position im UNGEFILTERTEN Style-File). `Renderer.Render` füllt beides an allen 7 Konstruktionsstellen. `IRenderedLayerEntry` hat `Name`, `StyleIndex` (Doc-Grund: Tiles sind unvollständig — Styles ohne Daten erzeugen keinen Eintrag, First-Occurrence über Tiles ist NICHT die Style-Reihenfolge!).
2. `RenderedTileLayerRenderer.DrawInStyleLayerOrder` ersetzt `DrawGeometry`+`DrawSymbols`: pro gemergtem Style-Layer (Namen aufsteigend nach `StyleIndex`) zuerst Geometrie über alle Screen-Tiles, direkt danach die platzierten Symbole dieses Layers. `DrawInvalidBorders` danach (unverändert).
3. Zeichnen filtert GEGEN DEN FRAKTIONALEN Draw-Zoom (`viewport.Resolution.ToZoomLevel()`); Semantik wie `Renderer.IsVisible` — `MinZoom > -1 && zoom < MinZoom → skip`, `MaxZoom > -1 && zoom >= MaxZoom → skip`. Grund: Visibility ist im Tile auf das SCHEMA-Nächst-Level gebacken, das eine Stufe ÜBER dem Draw-Zoom liegen kann; Hilfsfunktion `SymbolPlacementEngine.IsVisibleAtZoom((min,max), zoom)` (public, von Controls.Mapsui genutzt).
4. `SymbolPlacementEngine.Compute` filtert Layers jetzt mit derselben Semantik auf dem kanonischen Zoom (`zoomLevel`) — bisher SymbolLayer-MinZoom/MaxZoom (Defaults 0..24, also wirkungslos). Layer-Reihenfolge jetzt deterministisch: `OrderByDescending(StyleIndex)` mit Tie-Break Listenposition (späterer Eintrag = höherer Layer; nötig für synthetische Tests ohne StyleIndex), Ausgabe-Dictionary `PlacedByLayer` aufsteigend nach Style-Index.

Wichtige Falle entdeckt: Tiles können Lücken in `Layers` haben → gemergte First-Occurrence-Reihenfolge war falsch (water/road_minor/road_secondary_tertiary nach country_1 → übermalten beim ersten Interleaving-Versuch die Straßenlabels; das RenderedTileTests-Diff zeigte fehlende Labels). Fix = StyleIndex. SymbolPlacementEngineTests `Collision_HigherStyleLayerWins` verlangt bei gleichem StyleIndex: Listenposition entscheidet (später gewinnt).

Suite: Skia 191 grün, Common 59, Converters 2, DataSources 12, Styles 264. Zwei IconPointSymbol-Fails NUR unter Volllast (Font-Bitmap-Flakes, in Isolation + im Finalrun grün — bestehendes Problem, s. o. (b)).
Temporäres: `Tests/…/ScratchDebugTests.cs` (Diagnose Placement-Keys/Names) wurde erstellt und wieder gelöscht. `.scratch/goldendiff` wieder entfernt. `-PREVIOUS.png`/`-diff.png` im Results-Ordner wiederhergestellt (Golden hat sich dieses Mal NICHT geändert).
Offen: Release-Testlauf der Skia-Suite ist gelaufen (191 grün). SampleApp nochmal manuell ansehen (Zoom-Filter bei fraktionalem Zoom, z. B. 16.9: Layers mit minzoom 17 dürfen erst ab 17.0 erscheinen).

### Nachtrag (Dirk-Hinweis)
Der Style-Layer-Durchlauf im Zeichner lief fuer reine Symbol-Layer weiterhin in die Geometrie-Schleife. `layerIndex` merkt sich jetzt `(StyleIndex, IsGeometry)`; der jeweilige Pass (Geometrie bzw. Symbole) wird fuer den anderen Layer-Typ uebersprungen. Suite 191 gruen (Debug), Golden unveraendert.

## Runde 2026-11-21c: Pan-Flackern / Tile-Retention (committet 5e63207)

Dirks Punkt: "Das Bild flackert beim Pan. Ich nehme an, dass das sichtbare Bild geloescht wird und dann neu gezeichnet wird."

Diagnose: Kein Off-Screen-Rendering noetig (Mapsui/Skia rendert pro Frame ohnehin atomar). Ursache: `PlacementService.Update` hat beim Key-Wechsel (auch beim blossen Pan) `_viewTiles.Clear()` ausgefuehrt. Beim Pan aendert sich das Tile-Set um eine halbe Kachelbreite; alle bereits gelieferten Kacheln wurden weggeworfen, bis der neue Fetch-Task liefert → Beige-Blank-Frames.

Umsetzung:
1. `PlacementService.Update` (Key-Wechsel-Zweig): `previousKey` merken; statt `_viewTiles.Clear()` jetzt `RetainTilesOnViewChange(previousKey, key, infoArray)`.
2. Neue private Methode `RetainTilesOnViewChange`: leeres View → return; previousKey null ODER Zoom-Wechsel → Clear (Zoom behaelt nichts); sonst nur die Kacheln behalten, deren `Info.Index` im neuen Tile-Set liegt (Keep-Set aus `tileInfos`). Gelieferte, aber nicht mehr sichtbare Kacheln fallen raus; neue Kacheln kommen via Fetch-Scheduler dazu und loesen `TileArrived` → Redraw aus.
3. Tests `PlacementServiceTileRetentionTests` (3 neue):
   - `Pan_KeepsOverlappingTilesOfThePreviousView`: Pan um eine Kachel — ueberlappende Kachel bleibt sofort im Snapshot, alte raus.
   - `PanOfTheWholeView_TileSetUntouched_KeepsAllTiles`: gleicher Key → keine neuen Fetches.
   - `ZoomChange_DropsAllTiles`: Zoom-Wechsel behaelt nichts (Level-Check im Snapshot).
   - Hilfsklassen `GatedRenderedTileSource`/`FakeRenderedTileSource`/`TileInfo` wurden aus `PlacementServiceTests` in namespace-level `PlacementServiceTestSupport` gehoben (MIT `using static` oben in der Datei), damit beide Testklassen sie nutzen. Inhalt der Fakes UNVERAENDERT.

WICHTIG (Falle, zweimal reingelaufen): Die alten Tests `RetainedSet_KeepsSymbolsWhileNewPassRuns` und `ZoomChange_ClearsTheRetainedSet` brauchen `{ PlacementDebounceMs = 0 }` (HINTERGRUND-Lauf, Gate blockiert dort ohne Deadlock); `IncompleteResult_NoNewFetchAttemptsWithinCooldown` und `TileArrived_FiresForEachTileOfTheView` brauchen `{ SynchronousPlacement = true }`. Vertauscht man das, haengt der Testlauf (Gate blockiert synchron) bzw. `first.Placement.Incomplete` schlaegt fehl (Race).

Status: Commit 5e63207. Skia-Suite 194/194 gruen (191 + 3 neue).

## Runde 2026-11-21d: Zoom-Limit 24 + Overzoom-Analyse

Dirk: 1) "MaxZoom auf 24 begrenzen" (im Augenblick bis 25 moeglich). 2) "Nach dem Zoomen scheint es beim ersten Zeichnen der Symbole noch zu flackern." 3) Frage: "Datenquelle Hat MaxZoom 14 — warum wird ueber 14 nachgeladen? Erwartet: Z14-Tile mit Z15-Styles zeichnen, kein Nachladen."

Umgesetzt (Punkt 1, committed):
- Mapsui erlaubt per Default EINE Stufe UEBER den Layer-Resolutions (`Map.LayersChanged → GetMinMaxResolution: mostZoomedIn = res * 0.5`). Fix: `RenderedTileLayer.ZoomBounds` (MMinMax: kleinste/groesste UnitsPerPixel des Schemas) neu; SampleApp setzt `Navigator.OverrideZoomBounds = _tileLayer.ZoomBounds`. Zoom-Range ist jetzt 0..24.

Point 3 NICHT umgesetzt — Analyse (wichtig!):
- Die Datenquelle kann Overzoom SCHON: `VectorTileDataSource.GetVectorTileAsync` seeked den Ancestor am `dataSource.MaxZoom` und konvertiert die Daten aufs angeforderte Tile (`OMTTileConverter.Convert(requestedTile, providedTile, data)`). Nachgeladen werden also KEINE neuen Daten über Z14 — aber pro Zoomstufe wird das Tile NEU synthetisiert und NEU gerendert, deshalb erscheinen Stufen > 14 "nach und nach".
- Schema-level Overzoom (klemme `GetTileInfos` auf Z14, skaliere beim Zeichnen) wurde PROBIERT (`OverzoomTileSchema`, Dekorator ueber ITileSchema — Achtung: `TileSchema.GetTileInfos(Extent,double)` ist virtual aber SEALED, Override ging nicht) und WIEDER ENTFERNT, weil:
  a) `Renderer.Render` backt die Layer-Visibilitaet mit dem TILE-Zoom (`IsVisible(tile.Zoom, style)`): ein Z14-Tile enthaelt ALLE Layer mit minzoom > 14 NICHT → beim Skalieren waeren z. B. alle Z15+-Strassen und Label weg (Tests: placed boxes=0, Golden-Diff).
  b) Die Symbol-Builder backen zoomabhaengige px-Werte beim Tile-Zoom (OMTTextBuilder: lineHeight, letterSpacing, padding, offset, rotation — NICHT evaluators), waehrend FontSize als evaluator beim Draw-Zoom evaluiert. Ein bei Z14 gebackenes Symbol haette zu kleine Abstaende/Offsets relativ zu seiner Draw-Zoom-Schriftgroesse.
- Saubere Loesung (naechster Schritt, von Dirk zu entscheiden): Symbol-/Style-Properties zoomabhaengig LAZY evaluieren (wie FontSize Bereits), dann Z14-Tiles ueber alle Zooms verwenden; Renderer-Visibility-Filter beim Backen streichen (der Draw-Zoom-Filter im Renderer/Placement ist bereits vollstaendig vorhanden!). Golden-Refresh dann noetig (Z17/19-Tests zeichnen aus Z14-Tiles skaliert).

Punkt 2 (Flackern nach Zoom): kein separates Problem gefunden. Beim Zoomwechsel ist der Symbolverlust Design (Dirk-Regel: keine Symbole der alten Zoomstufe). Die Wartezeit bis zum neuen Satz entsteht, weil ALLE Tiles der neuen Stufe erst gerendert sein muessen (Pass ist sonst Incomplete). Mit echtem Schema-Overzoom (siehe oben) entfaellt das Neurendern komplett — dann sind Symbole nach dem Zoom sofort da. Deshalb schlagen: Punkt 3 sauber implementieren statt am Flackern zu basteln.

Status: Build gruen. Skia-Suite 193 gruen + 1 fail = bekannter Flaky `IconPointSymbolTests.OpacityTest(0.25)` unter Volllast (isoliert 56/56 gruen). Commit: ZoomBounds-Layer + SampleApp.

## Runde 2026-11-21e: Off-Screen-Rasterisierung + Frame-Retention gegen Flackern

Dirk: Beim Fertigwerden der Symbole wird der Bildschirm geloescht und neu gezeichnet → Flackern. Gleiches beim Rauszoomen: Screen geloescht, aber noch nichts zum Zeichnen.

Umgesetzt (RenderedTileLayerRenderer.Draw + RenderedTileLayer):
1. OFF-SCREEN-RASTERISIERUNG: `Draw` zeichnet den Layer-Inhalt NICHT mehr direkt auf den Screen-Canvas, sondern in eine wiederverwendete Raster-Surface (viewport-Groesse, RGBA8888 premul, transparenter Hintergrund — src-over ist assoziativ, Ergebnis pixelgleich zum direkten Zeichnen). Danach EIN `canvas.DrawImage(...)`-Blit. Der langsame Style-Layer-Durchlauf (50-200 ms) hinterlaesst nie einen sichtbaren Zwischenstand.
2. FRAME-RETENTION: Der Snapshot jedes vollstaendig gezeichneten Frames bleibt am Layer (`_retainedFrame`, `SKImage`, Besitz im Layer; altes Image disposiert in `SetRetainedFrame`). Zwei Faelle blitten den Retained-Frame, statt nichts zu zeichnen:
   a) `screenTileInfos` leer (z. B. Rauszoomen, bevor neue Kacheln da sind) — frueher EARLY RETURN auf geloeschtem Screen.
   b) `Update` ergebenes `view.Tiles` schneidet screenExtent nicht (Kacheln noch unterwegs).
3. Ressourcen-Verwaltung: `GetFrameSurface(w,h)` baut die Surface nur bei Groessenwechsel neu; `RenderedTileLayer` ist IDisposable (BaseLayer) → `Dispose(bool)` override disposiert Surface + Retained-Frame. `DrawGeometryTile` (public, Tests/Benchmarks) UNveraendert.
4. GOLDEN-REFRESH: `RenderedTileTests.RenderTile_CenterTileMatchesGoldenImage` zunaechst FAIL (Nur Text/Icon-AA-Rasterung unterscheidet sich bei transparentem Ziel zur opaken Flaeche; Geometrie pixelgleich; visuell identisch). Golden via VECTILES_RENDER_TEST_UPDATE_GOLDEN=true neu erzeugt. Alle Results/*.png (getrackte) mitaktualisiert.

Status: Build gruen, Skia-Suite 194/194, Gesamtloesung 531 Tests gruen.

## Runde 2026-11-21f: Fix Pixelverwischung/Langsamkeit des Off-Screen-Frames

Dirk (zu ecc2430): "Rückschritt: Alles ist deutlich verpixelt. Sehr langsam."

Ursache: Mapsui skaliert den Screen-Canvas um die Pixeldichte (canvas.TotalMatrix = Scale(density)), Viewport.Width/Hoehe sind logische Pixel. Das Off-Screen-Bild war in LOGISCHER Groesse gerastert und wurde beim Blit hochskaliert → unscharf/verpixelt; dazu pro Frame Snapshot+Upload ohne Wiederverwendung → langsam.

Fix (RenderedTileLayerRenderer.Draw + RenderedTileLayer):
1. DENSITY-KORREKTUR: density = canvas.TotalMatrix.ScaleX/Y; Off-Screen-Surface in Device-Pixelgroesse, surface.Canvas.Scale(density) vor dem Zeichnen (alle Koordinaten bleiben logisch). Blit mit ResetMatrix + Dest-Rect = matrix.MapPoint der Viewport-Ecken → 1:1 in Device-Pixeln, kein Resampling.
2. FAST PATH (Frame-Wiederverwendung): Update liefert jedes Mal eine NEUE PlacementView-Instanz, also Frame-Key aus INHALT: (Placement-Referenz [PlacementService gibt dieselbe Instanz zurueck, solange nichts geaendert ist], FNV-Hash ueber alle Tile-Ids, CenterX/Y, Resolution, Rotation, Breite, Hoehe, DestRect, zoomLevel). Uebereinstimmt → nur Blit des Retained-Frames, kein Rasterieren (TryReuseFrame am Layer). Dasselbe gilt fuer Widget-Repaints ohne Karten-Aenderung.
3. SetRetainedFrame(frame, destRect, key) + DrawRetainedFrame blitten jetzt mit ResetMatrix + Dest-Rect (nicht mehr DrawImage(0,0)). ClearCache verwirft den Retained-Frame (alte Styles nicht weiterzeigen).

Tests: Build gruen; RenderedTile/FrameRender/AppPipeline/PlacementService 29/29; Skia-Suite 194/194 (Goldens ohne Aenderung: Density ist im Test 1).


## Runde 2026-11-21g: Zweispuriges Zeichnen ( direkte GPU-Zeichnung + Idle-Off-Screen)

Dirk (zu d740762): "Auflösung wieder gut, aber 200 FPS → 20 FPS und darunter."

Ursache: Das Off-Screen-Raster findet auf einer CPU-Raster-Surface statt (kein GRContext von Mapsui erreichbar), also pro Frame CPU-Raster + Upload statt GPU-Zeichnen → ~10x langsamer.

Fix: ZWEISPURIG (RenderedTileLayerRenderer.Draw + RenderedTileLayer):
- Viewport seit letztem Draw UNVERAENDERT (Layer merkt Signatur over `ViewportEqualsLastDraw`, inkl. destRect/Density) → IDLE-Pfad (Placement fertig, Tile kam, Widget-Repaint): Off-Screen-Raster + Blit + Retention (atomarer Frame, danach Blit-only wiederverwendet).
- Viewport GEOENDERT (Pan/Zoom laeuft) → DIREKTZEICHNUNG auf den GPU-Canvas (alter schnelle Pfad; ein Skia-Paint kommt qua Double Buffering atomar auf den Screen).
- Retained-Frame + TryReuseFrame + DrawRetainedFrame bleiben wie gehabt.
- Golden fuer RenderedTileTests wieder auf DIREKT-Zeichnung zurueckgesetzt (env var), RotationAlignment-Flake nur unter Last bekannt.

Offen: Beim Heraussauscope aus Bewegung GERADE NACH einem Pan (keine Idle-Tick dazwischen) kann noch beige geblitzt werden, weil noch kein stabiler Frame gerastert wurde. Falls Dirk das sieht: naechster Schritt waere ein nachtraeglicher Idle-Raster-Lauf (Timer), der den Retained-Frame im Stand nachtfuellt.

Status: Build gruen; Skia-Suite 194/194.

## Runde 2026-11-21h: GPU-Surface statt CPU-Raster

Dirk-Hinweis: `canvas.Surface.Context` liefert in SkiaSharp 4.x den GRRecordingContext des Screen-Canvas (RecordingContext heisst die Property anders/ist nicht present; Weg: SKCanvas → SKSurface → GRContext). 

Fix: `RenderedTileLayer.GetFrameSurface(width, height, grContext?`)`:
- grContext != null → `SKSurface.Create(grContext, budgeted:true, info)` — Off-Screen-Drawing UND Blit laufen komplett auf der GPU (GPU-GPU-Kopie beim Blit, Snapshot = Textur).
-Else CPU-Raster wie bisher (Tests/non-GPU-Canvas).
- Surface-Invalidierung: Groessenwechsel, GRContext-Handle-Wechsel (Fenster/Uebergang), IsAbandoned.

Zweispur bleibt: Bewegung = direktes Zeichnen, Idle = Off-Screen+Blit+Retention — nur die Idle-Rasterung ist jetzt GPU-schnell.

Status: Build gruen; Skia-Suite 194/194 (Test-Canvas ist raster → CPU-Pfad getestet; GPU-Pfad in der SampleApp zu verifizieren).

## Runde 2026-11-21i: Zoomgrenzen-Flackern (frozen Pixels des alten Zooms)

Dirk: "Beim Zoomen an Zoomgrenzen flackert kurz ein falsches Bild — vermutlich vom Zoom darueber/darunter."

Ursache: Beim Ueberqueren einer Zoomgrenze wechselt der Tile-Matrix-Schluessel (schema.GetTileInfos waehlt die neue Matrix), die Kacheln werden gecleart (Zoomwechsel-Regel). In den Luecken-Frames blittete DrawRetainedFrame den Retained-Frame 1:1-Pixel-frozen — also Inhalt des ALTEN Zooms in ALTER Groesse, mitten in einem Zoomschritt → kurzes "falsches Bild".

Fix: Der Retained-Frame wird jetzt affin ins AKTUELLE Viewport gemappt:
- SetRetainedFrame merkt zusaetzlich den Viewport (center/res/rotation/size) des Frames.
- DrawRetainedFrame(canvas, currentViewport) rekonstruiert den alten Viewport, mappt die drei Bildecken via ScreenToWorld → current.WorldToScreen → canvas.TotalMatrix auf Device-Positionen und loest daraus die affine Bild→Device-Transform (neu: CreateAffine, Cramer 2x2, Empirisch verifizierte SKMatrix-Feldkonvention ScaleX/SkewX/TransX).
- Ergebnis: in der Luecke zeigt der Screen den ALTEN Inhalt in der SKALIERTEN neuen Ansicht (wie eine echte Zoomstufe), kein Sprung mehr.
- TryReuseFrame (identischer Inhalt) bleibt 1:1-Blit via destRect.
- InternalsVisibleTo "VecTiles.Renderers.Skia.Test" am Controls.Mapsui-Projekt (Stil wie Styles.OpenMapTiles); Tests: RetainedFrameAffineTests (3: gleich, Zoom 2x, Pan).

Status: Build gruen; Skia-Suite 197/197.

## Runde 2026-11-21j: Density-Bug im Retained-Frame-Blit (Font: verschoben+vergroessert)

Dirk: "Beim zoomen flackert kurz eine Karte, die verschoben ist und beim hineinzoomen stark vergroessert."

Ursache: Das Retained-Bild ist in DEVICE-Pixeln gerastert (Pixel = old.Width * Density). DrawRetainedFrame behandelte die Bildpixel aber als alte LOGISCHE Pixel (ScreenToWorld(imageWidth, 0)), bei Density 2 also doppelte Distanz → Bild 2x zu gross und verschoben. Nur auf HiDPI sichtbar; Tests rannten mit Density 1.

Fix:
- Neue interne Methode `CreateRetainedFrameMatrix(old, oldDestRect, imageW, imageH, current, canvasMatrix)` auf dem Layer (Testbar; DrawRetainedFrame nur Wrapper der gespeicherten Werte).
- Dichte aus dem gespeicherten Dest-Rect abgeleitet: densityX = destRect.Width / old.Width; Bildecken werden zu LOGISCHEN Pixeln zurueckgerechnet (imageWidth / densityX) BEVOR sie auf Weltkoordinaten gemappt werden.
- Test RetainedFrameAffineTests.SameViewport_HighDpi_MapsTheImageOneToOneInDevicePixels (Density 2) — schlaegt ohne Fix fehl (2x-Skalierung). 4/4 Affin-Tests, Suite 198/198 gruen.


## Runde 2026-11-21k: Underlay aus Retained-Frame bei unvollstaendigem Tile-Set

Dirk: "Wenn Tiles zum Zoom da sind, wird das ganze Bild geloescht und neu gezeichnet. Besser: solange ein Tile fehlt, den vorhandenen Hintergrund per BitBlt behalten — fehlende Teile verpixelt, aber kein Flackern."

Fix (RenderedTileLayerRenderer.Draw):
- Neue Hilfsmethode DrawRetainedFrameUnderlay(renderedLayer, canvas, viewport, incomplete): zeichnet den Retained-Frame (affin ins aktuelle Viewport gemappt, Zustand aus Runde 21i/j) als UNTERGRUND, nur wenn view.Placement.Incomplete (Kacheln fehlen).
- Aktiv in BEIDEN Paeden: direkte Zeichenung (interaktiv) UND Idle-Raster (vor DrawInStyleLayerOrder in die Off-Screen-Surface, unterhalb der Style-Layer).
- Folge: Bei eingehendem Zoom werden die bereits angekommenen Kacheln des neuen Zooms ueber dem hochskalierten alter Stand gezeichnet; fehlende Bereiche zeigen alten Stand statt beige. Kein Ganz-Screen-Loeschen mehr, bis ALLES da ist.
- RenderedTileLayer.HasRetainedFrame (internal) als Guard.
- Goldens unveraendert (SynchronousPlacement-Tests laufen mit Incomplete=false → Underlay inaktiv).

Status: Build gruen; Skia-Suite 198/198.

## Runde 2026-11-21l: Kein alter Text mehr im Underlay (Geometry-Frame getrennt)

Dirk: "Beim Hineinzoomen wird Text kurz auf doppelte Groesse angezeigt und gleich danach durch den richtigen ersetzt."

Ursache: NICHT das Placement (wird korrekt beim Zoomwechsel gecleart). Der alte Text steckt EINGEBACKEN im Retained-Frame (Idle-Raster enthaelt Symbole). Der Underlay-Blit mappt das ganze Bild affin ins neue Viewport — alte Labels beim Hineinzoomen also Faktor 2 zu gross, bis das neue Placement fertig ist.

Fix nach Dirks Regel "Zoomwechsel → erstmal keine Symbole":
- Retained-Frame in ZWEI Bilder geteilt:
  1. _retainedFrame (voll, Geometrie+Symbole) — NUR noch fuer den 1:1-Reuse-Blit (TryReuseFrame, identisches Viewport).
  2. NEU _retainedGeometryFrame/_retainedGeometryFrameRect/_retainedGeometryFrameViewport — Geometrie OHNE Symbole; fuer alle GEMAPPTEN Zeichnungen: Underlay bei Placement.Incomplete, leerer Screen (keine TileInfos), View ohne angekommene Tiles.
- DrawInStyleLayerOrder bekam Parameter `bool drawSymbols = true`; beim Symbol-Zweig `if (!drawSymbols || ...TryGetValue...) continue;` auch ShowInvalidBorders nur mit drawSymbols.
- Idle-Pfad: ZWEI Raster auf derselben Surface: (1) Clear+Scale+DrawInStyleLayerOrder(withSymbols) → snap FULL → Blit + SetRetainedFrame; (2) Clear+Scale+DrawRetainedFrameUnderlay+DrawInStyleLayerOrder(symbols:false) → snap GEOMETRY → SetRetainedGeometryFrame. (Nicht Geometrie-erst-Symbole-danach: Symbol-Pass ueber die komplette Geometrie wuerde die Reihenfolge aus dem Style-File brechen.)
- Layer: SetRetainedGeometryFrame, DrawRetainedGeometryFrame (ersetzt DrawRetainedFrame), HasRetainedGeometryFrame; _retainedFrameViewport entfernt (Full-Frame wird nur noch 1:1 geblittet, Parameter viewport aus SetRetainedFrame entfernt). Dispose/ClearCache raeumen beide Frames auf.
- Effekt: Beim Zoomwechsel verschwindet der alte Text SOFORT (kein 2x-Blitz), Geometrie wird weiterhin affin verpixelt weiterangezeigt, neuer Text kommt erst mit dem fertigen Placement derselben Regel folgend.

Status: Build gruen; Affine-Tests 5/5, Placement-Tests 16/16.

## Runde 2026-11-21m: Holdover — Symbole des alten Placement bleiben beim Zoomwechsel stehen

Dirk: "BitBlt ist ohne Text" — nach Runde 21l war die Luecke nach einem Zoomwechsel komplett textlos (Komplett-Verschwinden/Wiederauftachen ist auffaelliger als kurze Doppelung). Wunsch: Die Symbole des ZoomN-Placement weiterzeichnen, bis das neue Placement sie ersetzt; kurzzeitige Ueberlappung ist ok. Sie sollen mit den NEUEN Style-Groessen gezeichnet werden.

Analyse:
- PointSymbol-Anker sind WELTKoordinaten (symbol.Point), Zeichnung via drawWorldToScreen (aktuelles Viewport) und Stylegroessen via drawContext (aktueller fraktionaler Zoom) -> altes Placement laesst sich direkt im neuen Viewport korrekt zeichnen.
- LineSymbol-Runs (LineSymbolAnchorPlacement.Position, PathTextPlacement.DrawStart/PathLength) liegen im kanonischen Platzierungsraum des ALTEN Zooms (Laengen proportional 1/resolution(zoom) -> Faktor 2^(old-new)); ohne Stauchung wandern die Labels entlang der Linie. Fix: Runs umskalieren.

Implementierung (alles VecTiles.Renderers.Skia, Renderer unabhaengig):
- PlacementResult: NEU `int ZoomLevel` (wird in SymbolPlacementEngine.Compute und ComputePlacement gesetzt) und neue `PlacementResult Rescaled(int targetZoom)`: shallow copy, Line-Runs umskaliert (Position, DrawStart, PathLength; Boxes werden beim Zeichnen nicht genutzt), Punkt-Einträge via shared entry; setzt ZoomLevel = targetZoom.
- PlacementService.Update: Zoomwechsel CLEART das _lastResult NICHT mehr, nur _lastZoom wird aktualisiert.
- CurrentPlacement: kein vollstaendiges Cached-Ergebnis und _lastResult.ZoomLevel != zoomLevel -> Holdover: _lastResult.Rescaled(zoomLevel) with { Incomplete = true }, gecacht pro (Quellresultat, Zielzoom) (_holdoverSource/_holdoverZoom/_holdoverResult), damit die Zeichnung eine stabile Instanz (Frame-Keys/Reuse) erhaelt. Incomplete = true haelt den Underlay (Geometrie-Frame aus 21l) aktiv, solange neue Kacheln fehlen.
- ClearCache resettet auch die Holdover-Felder.
- RenderedTileLayer.Renderer DRAW-Zweige unveraendert: Zeichnen mit drawContext (neue Groessen) und drawWorldToScreen (neue Positionen) passiert automatisch; Underlay/Geometrie-Frame aus 21l bleibt (alter Text kann also nicht mehr als 2x-Blitz im Standbild auftauchen — er wird individuell gezeichnet).

Tests:
- ZoomChange_ClearsTheRetainedSet -> ZoomChange_HandsOutTheRetainedSetAsRescaledHoldover: NotSame Zoom-8-Instanz, ZoomLevel 9, Incomplete, stabile Instanz bei erneutem Update.
- Holdover_LineRunsAreRescaledIntoTheNewZoomSpace: Position/DrawStart/PathLength 8->9 halbiert, Rueckskalierung rekonstruiert Original.
- Bekanntes Rest-Window: direkt nach dem Zoomwechsel, BEVOR die erste neue Kachel da ist, zeichnet der "keine Tiles"-Pfad nur den Geometrie-Frame (ohne Text) — bis zur ersten Kachel (~100-300ms, Fetch startet sofort).

Status: Build gruen; Skia-Suite 198/199 (einziges FAIL: IconPointSymbolTests.RotationTest unter Volllast — bekannt flaky, isolated gruen).

## Runde 2026-11-21n: Crossfade Holdover -> neues Placement (100 ms)

Dirk: "Der Text flackert noch kurz bei der finalen Zoom-Erreichung. Besser: alten in neuen Zustand uebergehen lassen, ~100 ms, Opacity alt ab / neu zu."

Implementierung:
- PlacementResult: NEU `bool IsHoldover` (Service setzt sie auf der Holdover-Kopie).
- RenderedTileLayer: crossfade-Zustand (_lastDrawnPlacement, _lastDrawnWasHoldover, _fadingOutPlacement, _fadeStartTicks) + internal GetPlacementFade(current): Referenzwechsel Holdover->fertiges Placement startet den Fade (PlacementFadeMs = 100); Holdover->Holdover und jeder andere Wechsel wechselt sofort. Rueckgabe (Old, OldAlpha, NewAlpha) pro Frame; Reset in ClearCache/Dispose.
- Renderer: placementFade direkt nach Update (sonst frameKey um (fadeOld, fadeAlphaByte) erweitert — der TryReuseFrame-Blit wuerde sonst den ersten Fade-Frame einfrieren); Idle-Pfad: Grosses Raster MIT Fade-Parametern, Geometrie-Frame waehrend des Fades NICHT neu rastern (inhalt unveraendert); direkter Pfad mit Fade-Parametern.
- DrawInStyleLayerOrder: Symbol-Sektion zeichnet pro Style-Layer ZUERST die ausblendenden Holdover-Eintrage in canvas.SaveLayer(alpha = OldAlpha), dann die neuen in SaveLayer(alpha = NewAlpha) (nur wenn <1). Sichtbarkeits-Check (minzoom/maxzoom am fraktionalen drawZoom) gilt fuer beide; checkForSpace=false -> kein Tree-Insert. Zeichenroutine in DrawPlacementEntry extrahiert.

Tests: PlacementFade_StartsWhenHoldoverIsReplaced (erster Draw: kein Fade; Holdover->fertig: Fade laeuft; Holdover->Holdover: sofort).

Status: Build gruen; GESAMT-Suite 537/537 (alle Projekte).

## Runde 2026-11-21o: Incomplete Pass darf das Retained-Set nicht ersetzen

Dirk: "Wenn ich schnell zoome, verschwinden jegliche Symbole (nur Karte); nach einiger Zeit Symbole. Ausserdem faden die Symbole am Anfang nicht, sondern sind hart da."

Ursache (beides dieselbe): Bei schnellem Zoomen startet jeder Zoomwechsel einen Placement-Lauf ueber (fast) keine Kacheln -> ComputePlacement liefert LEER+Incomplete -> RunPass/StartRun ueberschrieben _lastResult mit dem leeren Ergebnis:
- Holdover fort -> alle Symbole weg, bis ein voller Lauf fertig ist.
- Beim Erscheinen des fertigen Ergebnisses war das zuletzt gezeichnete Resultat kein Holdover mehr -> GetPlacementFade startet keinen Fade -> Symbole "hart da".

Fix (PlacementService, RunPass UND StartRun): `if (!result.Incomplete) { _lastResult = result; _lastZoom = key.Zoom; }` — unvollstaendige Ergebnisse werden weiterhin unter _placements[key] gecacht (Retry-Cooldown), aber das Retained-Set bleibt. Der Holdover (Zeichnung mit aktuellen Style-Groessen) bleibt so waehrend des ganzen schnellen Zoomens sichtbar; beim Fertigwerden des neuen Placements startet der 100ms-Crossfade.

Test: IncompletePass_DoesNotReplaceTheRetainedSet (FakeRenderedTileSource, SynchronousPlacement; Same-Instanz nach incomplete Pass).

Status: Build gruen; GESAMT-Suite 538/538.

## Runde 2026-11-21p: Fade-Ticker, Symbole ohne schneidende Kacheln, Selbst-Retry

Dirk: "Beim Start werden keine Symbole mehr angezeigt. Beim Start vom Zoom-Vorgang verschwindet der Text und kommt dann nach einiger Zeit wieder." Danach: "Zoome ich schnell, werden sehr viele Symbole aus der letzten geplanten Ansicht angezeigt (gestapelt); nach externem Redraw ist alles richtig."

Implementierung (3 Teile):

1. Fade-Ticker (RenderedTileLayer): Wenn das Placement fertig wird waehrend der Viewport still steht, gibt es nur einen einzigen Draw mit Alpha≈0 — der Fade lief nie weiter. Neu: StartPlacementFadeTicker (Task.Run, Tick alle PlacementFadeMs/3 → DataHasChanged), gestartet bei Fade-Beginn in GetPlacementFade, gestoppt in StopPlacementFade (auch das Duplicate-ResetPlacementFade am Dateiende entfernt; Dispose/ClearCache raeumen auf). PlacementFadeMs 100 → 200. FIX im Ticker-Start: StartPlacementFadeTicker rief StopPlacementFade auf, das DAS SOEBEN GESETZTE _fadingOutPlacement loeschte → Fade startete nie, Symbole wechselten hart (wurde vom Test PlacementFade_StartsWhenHoldoverIsReplaced aufgedeckt). Jetzt cancelt der Ticker-Start nur den alten Ticker.

2. Symbole im Pfad ohne schneidende Kacheln (RenderedTileLayerRenderer): Beim Zoomwechsel, bevor die erste neue Kachel da ist, zeichnet der Pfad jetzt Geometrie-Frame + DrawInStyleLayerOrder mit Fade-Parametern; DrawInStyleLayerOrder bricht frueher nur ab, wenn der pass GEOMETRIE-only ist (`tiles.Count == 0 && !drawSymbols` → return). Der Symbol-Zweig kommt mit leeren tiles aus: layerIndex kommt aus view.Tiles (Padding-Ring), FindSymbolLayer ebenso.

3. Selbst-Heilung bei unvollstaendigem Ergebnis (PlacementService):
   - Cooldown-Bypass: _incompleteAttempts speichert jetzt (Tick, TileCount); UEbergeht den 2 s-Cooldown, wenn seit dem letzten Versuch neue Kacheln angekommen sind (mehr Daten → Retry sinnvoll statt Spin).
   - ScheduleHoldoverRetry: Nach einem unvollstaendigen Lauf, dessen Ergebnis ein Holdover eines ANDEREN Zooms ersetzen muesste, feuert der Service nach dem Cooldown einmal PlacementCompleted — der daraus folgende Redraw/Update startet den Pass neu. Vorher: stillstehender Viewport + alle Kacheln entschieden ⇒ kein Update mehr ⇒ das dichte Label-Set des letzten geplanten Zooms blieb dauerhaft stehen (Dirks Screenshot)/bis zum externen Redraw. Wiederholt sich solange das Ergebnis unvollstaendig bleibt; stoppt bei Fertigstellung/View-Wechsel/ClearCache.

Test: IncompleteResult_SchedulesARetryWhileTheHoldoverIsOnScreen (2. PlacementCompleted nach ~2 s, Retry-Pass fetcht erneut). Zusätzlich Test-Fix: ZoomChange_HandsOutTheRetainedSetAsRescaledHoldover verlor vorher ein Race (das Gate wurde vor dem Zoom-9-Lauf nicht geschlossen, der sofort fertige Pass ersetzte den Holdover; schlug auch ohne die neuen Aenderungen fehl) → Gate vor dem Zoomwechsel neu setzen.

Status: Build gruen; GESAMT-Suite 539/539.

## Runde 2026-11-21q: Text verschwindet kurz beim Zoomstart und erscheint verkleinert

Dirk: "Der Text verschwindet kurz, wenn ich anfange zu zoomen. Dann erscheint er etwas verkleinert und dann richtig."

Zwei Ursachen:

1. Verschwinden: Nach dem Wechsel auf die neue Zoomstufe ist view.Tiles VOR der ersten
   angekommenen Kachel KOMPLETT leer (RetainTilesOnViewChange cleart bei Zoomwechsel). Der
   Namens-Aufbau von DrawInStyleLayerOrder kam nur aus layerIndex (aus view.Tiles) → keine
   Namen → keine Symbole. Fix: Beim Zeichnen mit Symbolen werden fehlende Symbol-Layer-Namen
   ergaenzt — aus view.Placement.PlacedByLayer und fadeOutPlacement.PlacedByLayer (Dicts
   erhalten die aufsteigende Style-Order) — in die names-Liste angehaengt. Der Loop nutzt
   jetzt layerIndex.TryGetValue; Namen ohne Index-Eintrag laufen in den Symbol-Zweig.

2. "Etwas verkleinert": Die Glyphen werden mit fontSizeRatio skaliert = FontSize(fractional
   drawZoom) / FontSize(layout zoom). Fuer normale Eintraege ist der Layout-Zoom der int-Zoom
   des Views. Der HOLDOVER wurde aber im kanonischen Raum des QUELL-Zooms gemessen (z.B. 7),
   der Ratio zaehlte gegen den Zoom des aktuellen Views (8) → Ratio≈1 statt≈2 → Glyphen in
   halber Groesse. Fix: PlacementResult.SourceZoomLevel (Zoom, in dessen kanonischem Raum das
   Layout gemessen wurde; Rescaled setzt ihn auf den urspruenglichen ZoomLevel), Renderer:
   LayoutContextOf(placement, viewContext) liefert je Menge (Holdover? SourceZoom : View-Zoom)
   den Layout-Kontext; DrawPlacementEntry erhaelt oldLayoutContext/newLayoutContext.

Test: ZoomChange_HandsOutTheRetainedSetAsRescaledHoldover prueft zusaetzlich SourceZoomLevel=8.

Bekannter Rest: Der ICON-/Text-OFFSET (TextPointSymbol.Offset, Anchor) ist im kanonischen Raum
des Layout-Zooms gespeichert und wird fuer Holdover-Eintraege evtl. nicht skaliert — falls
Holdover-Labels versetzt erscheinen, ihn mit SourceZoomLevel ebenfalls skalieren.

Status: Build gruen (Solution); GESAMT-Suite 539/539.

## Runde 2026-11-21r: Entscheidungen am Zoom haften (TextLineSymbols am Tile befestigt)

Dirk: "Jetzt haette ich gerne die TextLineSymbols am Tile befestigt ... Wenn Tiles
verschwinden, dann werden die Symbole anders plaziert. Sie sollen immer an der
gleichen Stelle bleiben."

Design: Die Placement-Entscheidungen werden pro Zoomstufe PERSISTENT:

- NEU IncrementalPlacementState (SymbolPlacement.cs): grow-only Kollisionsbaum
  (Platz einmal belegt bleibt belegt) + Entscheidung je Symbol (PlacementEntry
  oder null = rejected). id != 0: Entscheidung nach FEATURE-ID; id == 0: nach
  Symbol-REFERENZ (stabil, solange die Kachel im Kachelspeicher ist).
- SymbolPlacementEngine.Compute(..., IncrementalPlacementState? incremental):
  vor der Pruefung TryGetDecision; bereits Entschiedenes wird NICHT erneut
  platziert (Position bleibt, auch wenn Kacheln erscheinen/verschwinden).
  Der Baum ist state.Tree (nicht mehr ein Baum pro Lauf). MaxDecisions=100_000:
  vor dem naechsten Lauf gemaess ResetIfFull zurueckgesetzt (Speicher bleibt
  begrenzt; Einmaliges Neueinpassen).
- PlacementService: _incrementalStates je Zoom (StateFor); StartRun und RunPass
  reichen den State des aktuellen Zooms; ClearCache leert die States.
- ABER: Unvollstaendige Laeufe (fetch.Incomplete = mind. eine Kachel fehl-
  geschlagen) beschreiben den State NICHT (null statt State) — konsistent mit
  21o: unveroeffentlichte Ergebnisse pinnen keine Labels.

WICHTIG: Erste-Okkurrenz-Regel je Lauf. Die Quelle wiederholt Features ohne
(ab und sogar MIT) Id ueber die Kachelgrenze (buffered repeat). Ohne State
kollidiert die spaetere Okkurrenz mit SICH SELBST (ihr eigener Envelope ist im
Baum) → rejected. Mit State waere der Entscheid bereits vorhanden → der Eintrag
waere DOPPELT in placements (Symbol doppelt gezeichnet — genau das war der
Golden-Test-Fehler: road_one_way_arrow 12 → 21). Fix: im Incremental-Pfad ein
pass-lokales HashSet<ISymbol> (seenSymbols) VOR TryGetDecision und examinedIds;
zweite Okkurrenz → continue. Der reine Pfad (ohne State) bleibt unveraendert
(Golden identisch, Pixel-identisch verifiziert).

Diagnostik-Wegwerkzeug (aufgeraeumt): PixelDiffDiagnostics (maxDelta-Abfrage)
ergab: 2344/262144 Pixel differieren, maxDelta=44, nur AA-Kanten/Labels. Probe-
Worktrees: 9c01514 (Golden-Erstellung) GRUEN, c6b0f6c GRUEN, 14710f1 GRUEN —
der Fehler kam ausschliesslich aus dem 21r-WIP.

Tests: NEU IncrementalPlacementStateTests (5):
- FirstPass_WithFreshState_PlacesLikeWithoutState
- SecondPass_KeepsPlacedPosition_AndDoesNotDecideTwice (gleiche Kachel zweimal:
  gleiche PlacementEntry-Instanz, kein Doppel-Eintrag)
- SecondPass_RejectedStaysRejected_WhenSpaceFreesUp (Platz wieder frei: Label
  bleibt weg)
- SecondPass_WithoutTheTile_KeepsItsDecision (Eintrag steht, ohne Neuplatzierung)
- MaxDecisions_Exceeded_NextPassStartsClean (Reset vor dem Lauf; Reihenfolge
  Reset → Baum-Holen war anfangs verkehrt → Baum-Instanz ersetzt, lokaler Baum
  blieb voll: alles rejected. Jetzt erster Reset, dann Tree nehmen.)

RenderedTileLayer: PlacementFadeMs 200 → 300 (weichere Angel-Gleitung beim
Zoomwechsel, Dirk-Wunsch aus der vorherigen Runde).

Status: Skia-Suite 207/207 (standalone), Gesamt: 59+2+12+207+264 = 544 gruen +
Renderers.Test (0 Tests, vorbestehend). Transienter Cluster von Font-/Text-
Test-Abbruechen direkt nach Parallel-Laeufen (alle Font-Tests gleichzeitig,
danach sofort wieder gruen, ohne Code-Aenderung) — Umgebungs-Flake, beobachten.

Runde 21s — Valid Borders + Invalid Borders + canoe_polo-Bildung

1. Valid Borders erst nach externem Redraw: frameKey (RenderedTileLayerRenderer
   Draw, ~Z167) enthielt ShowValidBorders nicht →ToggleButton blittete den
   GEHALTENEN Frame. Fix: renderedLayer.ShowValidBorders im frameKey-Tuple.
2. Invalid Borders entfernt (Feature tot, Dirk-Freigabe):
   - RenderedTileLayer: Property ShowInvalidBorders geloescht
   - RenderedTileLayerRenderer: Aufrufblock, DrawInvalidBorders, DebugPaint weg
   - SampleApp: Checkbox Invalid Borders + Handler entfernt
   - SchanzengrabenAppPipelineTests: ShowInvalidBorders=true entfernt
3. canoe_polo (Tile 17/68642/45902, rechts unten zwei Icons, Z17 fehlt):
   REPRO NIEMANDWO. Verifiziert (alle Diagnosetests wieder entfernt):
   - Features kommen aus zurich.mbtiles (MaxZoom=14!) als parcel-Overzoom:
     ids 4127302942 (Env 495,504), 4127302962 (Env 433,480), beide
     class=pitch, rank 122/123 → Style-Layer poi_z16 (rank>=20, minzoom 16,
     kein maxzoom; Sprite "pitch" vorhanden). POI-Daten-Layer selbst: minzoom
     12/maxzoom 14 → Ueberspiel-Zoom正常 moeglich.
   - SchanzengrabenDumpTests: keine parallelen Kopien der Features in den
     4 Nachbar-Kacheln (kein buffered repeat hier).
   - Placement at Z16/Z17/Z18: beide Icons immer platziert (poi_z16).
   - 20 Kamera-Varianten (5 Groessen x Offsets), alle platziert.
   - Async-Pfad (debounce/holdover, animierter Zoom 16.0→17.6→17): gezeichnet,
     PNG zeigt beide gruenen Icons im Wasser. Dito Sync-Seq 16→17→18.
   Folgerung: In der getesteten Pipeline (CPU-Canvas, SynchronousPlacement
   true/false) funktioniert Z17. Der App-区别 verbleibend: GPU-Canvas/density
   sowie der STALE-FRAME-Blit der alten Binaer (frameKey-Fix aus Punkt 1 liegt
   in diesem Working Tree, noch nicht im App-Build von Dirks Session!). Bitte
   naechste Session: App neu bauen und Z17 erneut pruefen; falls immer noch
   weg: im SampleApp eine Info-Zeile (oder Log) mit count der POI_z16-Entries
   des PlacementResults an der Stelle zeichnen, um App-seitig zu unterscheiden
   (a) nicht platziert vs. (b) platziert aber nicht gezeichnet vs. (c) Frame
   veraltet geblittet. Border-Fix zunaechst sauber halten.
   Kandidaten, falls Binaer neu war: (a) GPU-Canvas-Zweig (GRContext-Frame,
   density-Matrix), (b) Crossfade-Frames mit fadeInAlpha<1, die als retained
   frame haengen bleiben, falls der Abschluss-Redraw nicht kommt.

Status Runde 21s: Skia-Suite 232/232 gruen. Ergebnisbilder refreshet
(border-flag im frameKey → Frames mit sichtbaren Valid-Border-Boxen).
NICHT commitet bis hierher; Commit folgt (Borders-Fix + Invalid-Borders-Entfernung).

Runde 21t — Crossfade auch beim Cache-Sprung an der Zoomgrenze

Dirk: "Nach einem Zoom auf den naechst hoeheren Level erscheinen die Symbole
abbrupt. Besser langsam einblenden."

Ursache: GetPlacementFade startete den Crossfade nur, wenn das zuletzt
GEZEICHNETE Set ein Holdover war (Holdover → fertiges Ergebnis des neuen
Zooms). War das neue Level aber bereits berechnet (Placement-Cache), liefert
Update beim Ueberqueren der Zoomgrenze das fertige Ergebnis SOFORT — ohne
Holdover-Phase, ohne Fade: die Symbole des Levels knallen rein.

Fix (RenderedTileLayer.GetPlacementFade): Crossfade jetzt auch wenn
  !placement.IsHoldover && lastDrawn != null && ZoomLevel unterschiedlich
(unabhaengig vom Holdover-Flag des zuletzt Gezeichneten). Sofort-Wechsel
bleiben: neues Holdover (selbe Labels, nur neu verankert), eigener Lauf
fertig bei gleichem Zoom, Pan (Tileset-Aenderung bei gleichem Zoom),
Zoom-Anfang (lastDrawn null).

Tests: PlacementFade_StartsWhenHoldoverIsReplaced erweitert (layer2-Szenario:
gezeichnetes Set Zoom 9 → Cache-Sprung Zoom 10 → Fade; gleicher Zoom →
instant). Skia-Suite 207/207 gruen.

## Runde 2026-11-21u: canoe_polo@Z17 — WIP abgesichert, App-Diagnose eingebaut

Ausgangslage: Working Tree enthielt den uncommitteten WIP einer abgebrochenen Session:
(a) `TileFetchScheduler.FetchAsync` liefert jetzt `MissingTiles` (die Kacheln, die die Quelle
verweigert), (b) `PlacementService.Update(..., screenTileInfos)`: fehlt nur eine Kachel des
Padding-Rings (Quelle mit begrenzter Coverage), bleibt das Ergebnis VOLLSTAENDIG (published);
eine fehlende SCREEN-Kachel blockiert weiter (Holdover). Ohne screenTileInfos: altes Verhalten.

Verifikation dieser Session:
- Ring-Abdeckung der canoe_polo-Szene (17/68642/45902, Z16-Z19): ALLE Kacheln ladbar
  (CanoePoloTileFetchDiagnosticsTests) → Coverage-Löcher sind in DIESER Szene nicht der
  Auslöser; der WIP-Fix ist trotzdem richtig (generell für begrenzte Quellen).
- TEMP-DIAG-Logging (renderer + icon renderer, /tmp/vt-draw.log) ENTFERNT. Der alte Log
  (= Testlauf der vorigen Session, NICHT die App) zeigte Z17 korrekt.
- Diagnostic-Tests zu Regressionstests gehaertet: CanoePoloPlacementRingTests (beide Icons
  platziert, Z16-19, incomplete=false), CanoePoloTileFetchDiagnosticsTests (assert alle
  Ring-Kacheln OK), CanoePoloZoomScanTests (assert beide platziert), CanoePoloZoomInPipelineTests
  (assert Icon-Pixel + Hit-Test nach dem Settlen bei Z17/18/19). Neu: PlacementScreenTilePublicationTests
  (fehlender Ring-Kachel published, fehlende Screen-Kachel incomplete, ohne Screen-Set blockiert alles).
- Skia-Suite 238/238 gruen (zweimal; einmaliger Doppel-Flake IconPointSymbolTests unter
  Volllast, isoliert 56/56 gruen, bekannt). GESAMT-Suite 575 gruen.
- SampleApp-Diagnose: `RenderedTileLayer.CurrentPlacement` (letzter Update-Snapshot) +
  Statuszeile "Placement" im linken Panel (DispatcherTimer 500 ms): z/holdover/incomplete +
  Counts jeStyle-Layer. Erlaubt Dirks Klassifizierung bei fehlendem Icon: Count 0 = nicht
  platziert, Count > 0 = platziert-aber-nicht-gezeichnet (Zeichner-Bug), keine Aktualisierung
  = stale Frame.
- SampleApp neu gebaut (Debug), enthaelt den Fix.

OFFEN: Der app-seitige Repro (Dirk sieht Z17 leer) ist in der Test-Pipeline (CPU, 1024x768,
density 1) nicht reproduzierbar. Naechste Session: App laufen lassen, Statuszeile bei Z17
ablesen und Screenshots/Statuszeile melden. Falls Count > 0 bei fehlendem Icon: Zeichner
(GPU-Pfad, density<>1) untersuchen. Positionen der canoe Icons im Test: (543,396)/(481,372)
bei 1024x768 und Z17 (Mitte!), Dirk nennt "rechts unten" — andere Kamera verwenden, wenn
nötig Mitte-vs-Ecke ausprobieren.

## Runde 2026-11-21v: App-Statuszeile gelesen → Entscheidungs-Diagnose eingebaut

Dirk (App-Statuszeile bei Z17): "z17 holdover=- incomplete=false road_one_way_arrow:69,
water_name_line:2, poi_z16:209, poi_z15:14, poi_z14:10, road_label:55, road_shield:5"
→ Placement WIRD veröffentlicht, komplett, nicht Holdover. Und: "Icons nicht sichtbar".

Umsetzung:
- Statuszeile erweitert: `canoe:platziert/abgelehnt` (Counts der canoe_polo-Entries in
  PlacedByLayer bzw. Rejected). Liest sich beim nächsten Lauf so:
  canoe:2/0 + unsichtbar → Zeichner-App-Unterschied (Verdacht: GPU-Canvas-Pfad; Density
  ist ausgeschlossen, s. u.). canoe:0/2 → Kollision/Rejektion im Placement bei Z17.
  canoe:0/0 → Symbole gar nicht in den Kandidaten (Daten/Tiles). POI_z16-Zähler allein
  reicht nicht, die canoe sind evtl. NICHT unter den 209!
- Density ausgeschlossen: neu CanoePoloHiDpiTests — echtes Pipeline-Draw mit canvas.Scale(1.5/2)
  (Device-Bitmap, Probe in device px) bei Z17 und Z17.6: beide Icons grün sichtbar (3/3 grün).
- Suite: HiDpi 3/3; Kernel-Code unverändert seit 49ef940 (575 grün, 238 Skia).
