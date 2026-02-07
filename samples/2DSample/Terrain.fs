module MiboSample.Terrain

open System
open Microsoft.Xna.Framework
open Mibo.Layout
open Mibo.Elmish
open Mibo.Elmish.Graphics2D
open Mibo.Elmish.Graphics2D.DSL
open MiboSample.Domain


// ─────────────────────────────────────────────────────────────
// Simple Noise Generator for Procedural Terrain
// ─────────────────────────────────────────────────────────────

/// Simple pseudo-random number generator for deterministic terrain
module SimpleNoise =
  let private permute(x: int) : int =
    let x' =
      ((x >>> 13) ^^^ x) * (x * (x * x * 60493 + 19990303) + 1376312589)
      &&& 0x7fffffff

    x'

  let private hash(x: int, y: int) : int = permute(permute(x) + y)

  /// Generate a value in range [0, 1] for given coordinates and seed
  let noise2d(x: int, y: int, seed: int) : float32 =
    let h = hash(x + seed, y + seed)
    float32 h / 2147483648.0f

  /// Generate a value in range [min, max] for given coordinates
  let range
    (minVal: float32, maxVal: float32, x: int, y: int, seed: int)
    : float32 =
    let n = noise2d(x, y, seed)
    minVal + n * (maxVal - minVal)

  /// Generate a random integer in range [min, max]
  let rangeInt(minVal: int, maxVal: int, x: int, y: int, seed: int) : int =
    int(range(float32 minVal, float32(maxVal + 1), x, y, seed))

  /// Generate a boolean with given probability [0, 1]
  let chance(probability: float32, x: int, y: int, seed: int) : bool =
    noise2d(x, y, seed) < probability

  /// Generate 1D smooth noise (interpolated)
  let smoothNoise(x: float32, seed: int, scale: float32) : float32 =
    let scaledX = x / scale
    let x0 = int(floor scaledX)
    let x1 = x0 + 1
    let t = scaledX - float32 x0
    // Smoothstep interpolation
    let tSmooth = t * t * (3.0f - 2.0f * t)

    // Use y=0 for 1D noise
    let n0 = noise2d(x0, 0, seed)
    let n1 = noise2d(x1, 0, seed)

    n0 + (n1 - n0) * tSmooth

// ─────────────────────────────────────────────────────────────
// Terrain Patterns
// ─────────────────────────────────────────────────────────────

type TerrainPattern =
  | FlatGround // Simple flat ground
  | RollingHills // Gentle rolling hills
  | Mountains // Tall mountains with gaps
  | FloatingPlatforms // Floating platforms in the air
  | Cavernous // Gaps and ceilings
  | StaircaseUp // Ascending platforms
  | StaircaseDown // Descending platforms

/// Get terrain pattern for a chunk based on noise
let getChunkPattern(chunkX: int, seed: int) : TerrainPattern =
  if chunkX = 0 then
      FlatGround // Force safe spawn area
  else
      let patternNoise = SimpleNoise.noise2d(chunkX, 0, seed)

      match patternNoise with
      | n when n < 0.15f -> FlatGround
      | n when n < 0.35f -> RollingHills
      | n when n < 0.50f -> Mountains
      | n when n < 0.65f -> FloatingPlatforms
      | n when n < 0.80f -> Cavernous
      | n when n < 0.90f -> StaircaseUp
      | _ -> StaircaseDown

// ─────────────────────────────────────────────────────────────
// Terrain Generation Functions
// ─────────────────────────────────────────────────────────────

/// Ensure height difference between adjacent columns is jumpable
/// Maximum jump height is ~3 tiles, so we clamp differences to 3 tiles
let clampHeightDifference
  (prevHeight: int, currentHeight: int, maxHeightDiff: int)
  : int =
  let diff = prevHeight - currentHeight

  if abs diff <= maxHeightDiff then currentHeight
  elif diff > 0 then prevHeight - maxHeightDiff // Current is too low, raise it
  else prevHeight + maxHeightDiff // Current is too high, lower it

/// Generate height for a ground tile at given column
let generateGroundHeight
  (
    column: int,
    chunkX: int,
    pattern: TerrainPattern,
    seed: int,
    prevHeight: int option
  ) : int =
  let baseHeight = int Constants.worldHeight - 2 // Near bottom (tile index 10)

  // Use smooth noise for organic transitions
  // Scale 5.0 means height changes gradually over 5 tiles
  let noise = SimpleNoise.smoothNoise(float32 column, seed, 5.0f)

  // Map 0..1 noise to height variation (max 3 tiles for jumpability)
  let variation = int(noise * 4.0f) // 0 to 3 tiles variation

  let rawHeight =
    match pattern with
    | FlatGround -> baseHeight
    | RollingHills -> baseHeight - variation
    | Mountains -> baseHeight - variation // Max 3 tiles - within jump range
    | FloatingPlatforms -> baseHeight - 3 // Higher up (lower tile index)
    | Cavernous -> baseHeight - variation // Max 3 tiles - within jump range
    | StaircaseUp -> baseHeight - column % 3
    | StaircaseDown -> baseHeight + column % 3

  // Clamp height difference from previous column to ensure jumpability
  match prevHeight with
  | Some prev -> clampHeightDifference(prev, rawHeight, 3)
  | None -> rawHeight

/// Calculate raw ground height without clamping (used to get previous height)
let getRawGroundHeight(x: int, seed: int) : int =
  // Handle negative coordinates correctly for chunk calculation
  let chunkX =
    if x >= 0 then
      x / Constants.chunkWidth
    else
      (x - Constants.chunkWidth + 1) / Constants.chunkWidth

  let pattern = getChunkPattern(chunkX, seed)
  generateGroundHeight(x, chunkX, pattern, seed, None)

/// Calculate ground height for any global tile X coordinate with clamping
let getGlobalGroundHeight(x: int, seed: int) : int =
  let prevHeight = getRawGroundHeight(x - 1, seed)

  // Handle negative coordinates correctly for chunk calculation
  let chunkX =
    if x >= 0 then
      x / Constants.chunkWidth
    else
      (x - Constants.chunkWidth + 1) / Constants.chunkWidth

  let pattern = getChunkPattern(chunkX, seed)
  generateGroundHeight(x, chunkX, pattern, seed, Some prevHeight)

/// Check if a tile at (x, y) would be solid based on the heightmap
let isSolid(x: int, y: int, seed: int) : bool =
  let h = getGlobalGroundHeight(x, seed)
  y >= h

/// Determine the tile shape based on 4-neighbors (Auto-Tiling)
/// Returns index 0-8 matching SpriteLoader mapping
let getTileShape(x: int, y: int, seed: int) : int =
  let n = isSolid(x, y - 1, seed)
  let s = isSolid(x, y + 1, seed)
  let w = isSolid(x - 1, y, seed)
  let e = isSolid(x + 1, y, seed)

  if not n then // Surface (Top row)
    if not w then 4 // Top Left
    elif not e then 5 // Top Right
    else 0 // Top Center
  else if // Underground
    not s
  then // Bottom row (Ceiling/Floating bottom)
    if not w then 7 // Bottom Left
    elif not e then 8 // Bottom Right
    else 6 // Bottom Center
  else if // Middle
    not w
  then
    2 // Center Left (Wall)
  elif not e then
    3 // Center Right (Wall)
  else
    1 // Center (Full fill)

/// Generate occluders for a tile if it is solid
let generateOccluders(tile: GridTile, pos: Vector2) : Occluder2D array =
  match tile.TileType with
  | TileType.Platform ->
    let x, y = pos.X, pos.Y
    let size = Constants.tileSize
    // Only add occluder to the bottom part of the platform
    [|
      {
        P1 = Vector2(x + size, y + size)
        P2 = Vector2(x, y + size)
        Height = 10.0f
      }
    |]
  | _ -> [||]

/// Generate a light source for specific tile types
let generateLight
  (tile: GridTile)
  (pos: Vector2)
  (seed: int)
  : PointLight2D option =
  None

/// Generate a chunk of tiles using Layout DSL
let generateChunk(chunkX: int, seed: int) : LayeredGrid2D<GridTile> =
  let pattern = getChunkPattern(chunkX, seed)
  let startX = chunkX * Constants.chunkWidth
  let theme = SimpleNoise.rangeInt(0, 5, chunkX, 0, seed)
  let origin = Vector2(float32 chunkX * Constants.chunkSize.X, 0f)

  let chunk =
    LayeredGrid2D.create
      Constants.chunkWidth
      Constants.worldHeight
      (Vector2(Constants.tileSize))
      origin

  chunk
  |> LayeredLayout.layer 0 (fun section ->
    // Generate Terrain Column by Column
    for x = 0 to Constants.chunkWidth - 1 do
      let globalX = startX + x

      let prevHeight =
        if globalX = 0 then
          None
        else
          Some(getGlobalGroundHeight(globalX - 1, seed))

      let groundHeight =
        generateGroundHeight(globalX, chunkX, pattern, seed, prevHeight)

      let gapProbability = SimpleNoise.noise2d(globalX, chunkX + 100, seed)

      let fillGround() =
        // Fill from groundHeight to Bottom
        let h = Constants.worldHeight - groundHeight

        if h > 0 then
          // We need to set individual tiles to handle variants (auto-tiling logic simulation)
          for y = groundHeight to Constants.worldHeight - 1 do
            let shape = getTileShape(globalX, y, seed)
            let variant = theme * 10 + shape

            Layout.set
              x
              y
              {
                TileType = TileType.Ground
                Variant = variant
              }
              section
            |> ignore

      match pattern with
      | FlatGround ->
        // Optional floating platform
        if SimpleNoise.chance(0.3f, globalX, groundHeight - 3, seed) then
          Layout.set
            x
            (groundHeight - 3)
            {
              TileType = TileType.Platform
              Variant = 0
            }
            section
          |> ignore

        fillGround()

      | RollingHills ->
        // Climb Assist
        let hL = getGlobalGroundHeight(globalX - 1, seed)
        let hR = getGlobalGroundHeight(globalX + 1, seed)
        let isClimbAssist = (hL < groundHeight - 2 || hR < groundHeight - 2)

        if isClimbAssist then
          Layout.set
            x
            (groundHeight - 2)
            {
              TileType = TileType.Platform
              Variant = 0
            }
            section
          |> ignore
        elif SimpleNoise.chance(0.2f, globalX, groundHeight - 3, seed) then
          Layout.set
            x
            (groundHeight - 3)
            {
              TileType = TileType.Platform
              Variant = 0
            }
            section
          |> ignore

        fillGround()

      | Mountains ->
        let hL = getGlobalGroundHeight(globalX - 1, seed)
        let hR = getGlobalGroundHeight(globalX + 1, seed)
        let isClimbAssist = (hL < groundHeight - 2 || hR < groundHeight - 2)

        if isClimbAssist then
          Layout.set
            x
            (groundHeight - 2)
            {
              TileType = TileType.Platform
              Variant = 0
            }
            section
          |> ignore
        elif
          SimpleNoise.chance(0.3f, globalX + 50, groundHeight - 4, seed)
        then
          Layout.set
            x
            (groundHeight - 4)
            {
              TileType = TileType.Platform
              Variant = 0
            }
            section
          |> ignore

        fillGround()

      | StaircaseUp
      | StaircaseDown -> fillGround()

      | FloatingPlatforms ->
        // Only 1 block thick
        if SimpleNoise.chance(0.4f, globalX, groundHeight, seed) then
          Layout.set
            x
            groundHeight
            {
              TileType = TileType.Platform
              Variant = SimpleNoise.rangeInt(0, 1, globalX, groundHeight, seed)
            }
            section
          |> ignore

        if SimpleNoise.chance(0.3f, globalX + 100, groundHeight + 3, seed) then
          Layout.set
            x
            (groundHeight + 3)
            {
              TileType = TileType.Platform
              Variant = 0
            }
            section
          |> ignore

      | Cavernous ->
        if gapProbability >= 0.2f then
          fillGround()
        elif
          SimpleNoise.chance(0.15f, globalX + 200, groundHeight - 1, seed)
        then
          Layout.set
            x
            (groundHeight - 1)
            {
              TileType = TileType.Hazard
              Variant = 0
            }
            section
          |> ignore

    section)

/// Create a GameMap from a collection of chunks
let createMapFromChunks
  (chunks: Map<int, LayeredGrid2D<GridTile>>)
  (seed: int)
  : GameMap =
  // Gather all tiles to generate secondary data (lights/occluders)
  // In a real optimized system, we would cache this per chunk instead of rebuilding globally
  let occluders = ResizeArray<Occluder2D>()
  let lights = ResizeArray<PointLight2D>()

  for kvp in chunks do
    let chunk = kvp.Value
    // We assume layer 0 is the physical layer
    let (grid, _) = LayeredGrid2D.getOrAddLayer 0 chunk

    CellGrid2D.iter
      (fun x y tile ->
        let pos = CellGrid2D.getWorldPos x y grid
        occluders.AddRange(generateOccluders(tile, pos))

        match generateLight tile pos seed with
        | Some l -> lights.Add(l)
        | None -> ())
      grid

  {
    Chunks = chunks
    Occluders = occluders.ToArray()
    PointLights = lights.ToArray()
  }

/// Generate initial terrain (few chunks around spawn)
let generateInitialTerrain(seed: int) : Map<int, LayeredGrid2D<GridTile>> =
  // Generate 3 chunks: [-1, 0, 1]
  let chunks =
    [ -1; 0; 1 ]
    |> List.map(fun cx -> cx, generateChunk(cx, seed))
    |> Map.ofList

  chunks

// ─────────────────────────────────────────────────────────────
// Terrain Management
// ─────────────────────────────────────────────────────────────

/// Check if we need to generate new terrain
let needsTerrainGeneration(model: Model) : bool =
  let currentChunkX = Helpers.worldXToChunkX model.PlayerPosition.X
  // Generate 2 chunks ahead to prevent seeing the void
  currentChunkX + 2 > model.LastGeneratedChunk

/// Generate next terrain chunk and update model
let generateNextChunk(model: Model) : Model =
  let nextChunk = model.LastGeneratedChunk + 1
  let newChunk = generateChunk(nextChunk, model.Seed)

  // Add new chunk to map
  let newChunks = model.Map.Chunks.Add(nextChunk, newChunk)

  {
    model with
        Map = createMapFromChunks newChunks model.Seed
        LastGeneratedChunk = nextChunk
  }

/// Remove tiles that are too far behind the player (cleanup)
let cleanupOldTiles(model: Model) : Model =
  let currentChunkX = Helpers.worldXToChunkX model.PlayerPosition.X
  let cleanupThreshold = currentChunkX - 3 // Remove chunks 3 chunks behind

  // Filter keys < threshold
  let filteredChunks =
    model.Map.Chunks |> Map.filter(fun cx _ -> cx >= cleanupThreshold)

  if filteredChunks.Count = model.Map.Chunks.Count then
    model
  else
    {
      model with
          Map = createMapFromChunks filteredChunks model.Seed
    }

/// Create platforms array from chunks
let createPlatformsFromChunks
  (chunks: Map<int, LayeredGrid2D<GridTile>>)
  : Platform array =
  let platforms = ResizeArray<Platform>()

  for kvp in chunks do
    let chunk = kvp.Value
    let (grid, _) = LayeredGrid2D.getOrAddLayer 0 chunk

    CellGrid2D.iter
      (fun x y tile ->
        if tile.TileType <> TileType.Empty then
          let pos = CellGrid2D.getWorldPos x y grid

          platforms.Add(
            {
              Bounds =
                Rectangle(
                  int pos.X,
                  int pos.Y,
                  int Constants.tileSize,
                  int Constants.tileSize
                )
              Type = tile.TileType
              Variant = tile.Variant
            }
          ))
      grid

  platforms.ToArray()

/// Ensure terrain is generated as player moves
let update(model: Model) : struct (Model * Cmd<'Msg>) =

  let model =
    if needsTerrainGeneration model then
      generateNextChunk model
    else
      model

  let model = cleanupOldTiles model

  // Update platforms when terrain changes
  let platforms = createPlatformsFromChunks model.Map.Chunks

  { model with Platforms = platforms }, Cmd.none


/// Reset terrain to initial state
let reset(model: Model) : Model =
  let chunks = generateInitialTerrain model.Seed

  {
    model with
        Map = createMapFromChunks chunks model.Seed
        LastGeneratedChunk = 1
  }



// ─────────────────────────────────────────────────────────────
// Terrain Query Functions
// ─────────────────────────────────────────────────────────────

/// Find ground height at given world X position
let findGroundAt
  (worldX: float32, chunks: Map<int, LayeredGrid2D<GridTile>>)
  : float32 option =
  let chunkX = Helpers.worldXToChunkX worldX

  match chunks.TryFind chunkX with
  | Some chunk ->
    let (grid, _) = LayeredGrid2D.getOrAddLayer 0 chunk
    // Calculate local X in grid
    let localX = int((worldX - chunk.Origin.X) / chunk.CellSize.X)

    // Find the highest solid tile in this column
    let mutable result = None

    for y = 0 to grid.Height - 1 do
      // We search from top to bottom (y=0 is top)?
      // Actually Terrain generation puts ground at high Y indices (bottom of screen)
      // So we want the smallest Y that is not empty
      if result.IsNone then
        match CellGrid2D.get localX y grid with
        | ValueSome tile when
          tile.TileType <> TileType.Empty && tile.TileType <> TileType.Hazard
          ->
          let pos = CellGrid2D.getWorldPos localX y grid
          result <- Some pos.Y
        | _ -> ()

    result
  | None -> None

/// Render terrain tiles
let view (ctx: GameContext) (model: Model) (buffer: RenderBuffer<RenderCmd2D>) =
  let viewportWidth = float32 ctx.GraphicsDevice.Viewport.Width
  let cameraX = model.CameraX
  let margin = Constants.tileSize * 15.0f

  let viewBounds =
    Rectangle(
      int(cameraX - margin),
      0,
      int(viewportWidth + margin * 2.0f),
      Constants.worldHeight * int Constants.tileSize
    )

  // Determine which chunks are visible
  let startChunk = Helpers.worldXToChunkX(cameraX - margin)
  let endChunk = Helpers.worldXToChunkX(cameraX + viewportWidth + margin)

  for cx = startChunk to endChunk do
    match model.Map.Chunks.TryFind cx with
    | Some chunk ->
      let (grid, _) = LayeredGrid2D.getOrAddLayer 0 chunk

      grid
      |> CellGrid2D.iterVisible viewBounds (fun x y tile ->
        let pos = CellGrid2D.getWorldPos x y grid

        let rect =
          match tile.TileType with
          | TileType.Ground ->
            SpriteLoader.TileRegions.getGroundVariant tile.Variant
          | TileType.Platform ->
            SpriteLoader.TileRegions.getPlatformVariant tile.Variant
          | TileType.Hazard -> SpriteLoader.TileRegions.getHazardTile()
          | TileType.Empty -> Rectangle.Empty

        if rect <> Rectangle.Empty then
          buffer.Sprite(
            sprite {
              texture model.TerrainAssets.GroundTile
              sourceRect rect
              at pos.X pos.Y
              size Constants.tileSize Constants.tileSize
              layer 0<RenderLayer>
            }
          )
          |> ignore)
    | None -> ()

  for occluder in model.Map.Occluders do
    buffer.Occluder(occluder) |> ignore




// ─────────────────────────────────────────────────────────────
// Terrain Statistics and Debugging
// ─────────────────────────────────────────────────────────────

module TerrainStats =
  /// Count tiles of each type
  let countTilesByType
    (chunks: Map<int, LayeredGrid2D<GridTile>>)
    : Map<TileType, int> =
    let mutable counts = Map.empty

    for kvp in chunks do
      let (grid, _) = LayeredGrid2D.getOrAddLayer 0 kvp.Value

      CellGrid2D.iter
        (fun _ _ tile ->
          let current =
            counts |> Map.tryFind tile.TileType |> Option.defaultValue 0

          counts <- counts |> Map.add tile.TileType (current + 1))
        grid

    counts

  /// Get total number of tiles
  let totalTiles(chunks: Map<int, LayeredGrid2D<GridTile>>) : int =
    chunks
    |> Map.fold
      (fun acc _ v ->
        let (g, _) = LayeredGrid2D.getOrAddLayer 0 v
        // Iterating to count populated cells
        let mutable c = 0
        CellGrid2D.iter (fun _ _ _ -> c <- c + 1) g
        acc + c)
      0

  /// Get the furthest generated chunk
  let getFurthestChunk(model: Model) : int = model.LastGeneratedChunk

  /// Estimate world width based on generated chunks
  let estimateWorldWidth(model: Model) : float32 =
    float32(model.LastGeneratedChunk + 2)
    * float32 Constants.chunkWidth
    * Constants.tileSize