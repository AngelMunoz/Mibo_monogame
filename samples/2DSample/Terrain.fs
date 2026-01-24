module MiboSample.Terrain

open System
open Microsoft.Xna.Framework
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
    | StaircaseUp -> baseHeight - (column % 3)
    | StaircaseDown -> baseHeight + (column % 3)

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

/// Generate a single tile at the given position
let generateTile
  (
    x: int,
    y: int,
    chunkX: int,
    pattern: TerrainPattern,
    seed: int,
    theme: int,
    prevHeight: int option
  ) : Tile option =
  let groundHeight = generateGroundHeight(x, chunkX, pattern, seed, prevHeight)
  let columnNoise = SimpleNoise.noise2d(x, y, seed)
  let gapProbability = SimpleNoise.noise2d(x, chunkX + 100, seed)

  // Auto-tiling shape
  let shape = getTileShape(x, y, seed)
  let variant = theme * 10 + shape

  // Climb Assist: Check if neighbors are significantly higher (smaller Y)
  let hL = getGlobalGroundHeight(x - 1, seed)
  let hR = getGlobalGroundHeight(x + 1, seed)
  let needsStepL = hL < groundHeight - 2
  let needsStepR = hR < groundHeight - 2
  let isClimbAssist = (needsStepL || needsStepR) && y = groundHeight - 2

  match pattern with
  | FlatGround ->
    if y = groundHeight - 3 && SimpleNoise.chance(0.3f, x, y, seed) then
      Some {
        Position =
          Vector2(
            float32 x * Constants.tileSize,
            float32 y * Constants.tileSize
          )
        TileType = TileType.Platform
        Variant = 0
      }
    elif y >= groundHeight && y < Constants.worldHeight then
      Some {
        Position =
          Vector2(
            float32 x * Constants.tileSize,
            float32 y * Constants.tileSize
          )
        TileType = Ground
        Variant = variant
      }
    else
      None

  | RollingHills ->
    if isClimbAssist then
      Some {
        Position =
          Vector2(
            float32 x * Constants.tileSize,
            float32 y * Constants.tileSize
          )
        TileType = TileType.Platform
        Variant = 0
      }
    elif y = groundHeight - 3 && SimpleNoise.chance(0.2f, x, y, seed) then
      Some {
        Position =
          Vector2(
            float32 x * Constants.tileSize,
            float32 y * Constants.tileSize
          )
        TileType = TileType.Platform
        Variant = 0
      }
    elif y >= groundHeight && y < Constants.worldHeight then
      Some {
        Position =
          Vector2(
            float32 x * Constants.tileSize,
            float32 y * Constants.tileSize
          )
        TileType = Ground
        Variant = variant
      }
    else
      None

  | Mountains ->
    if isClimbAssist then
      Some {
        Position =
          Vector2(
            float32 x * Constants.tileSize,
            float32 y * Constants.tileSize
          )
        TileType = TileType.Platform
        Variant = 0
      }
    elif y >= groundHeight && y < Constants.worldHeight then
      Some {
        Position =
          Vector2(
            float32 x * Constants.tileSize,
            float32 y * Constants.tileSize
          )
        TileType = Ground
        Variant = variant
      }
    elif y = groundHeight - 4 && SimpleNoise.chance(0.3f, x, y + 50, seed) then
      Some {
        Position =
          Vector2(
            float32 x * Constants.tileSize,
            float32 y * Constants.tileSize
          )
        TileType = TileType.Platform
        Variant = 0
      }
    else
      None

  | StaircaseUp
  | StaircaseDown ->
    if y >= groundHeight && y < Constants.worldHeight then
      Some {
        Position =
          Vector2(
            float32 x * Constants.tileSize,
            float32 y * Constants.tileSize
          )
        TileType = Ground
        Variant = variant
      }
    else
      None

  | FloatingPlatforms ->
    // Only 1 block thick for floating platforms
    if y = groundHeight && SimpleNoise.chance(0.4f, x, y, seed) then
      Some {
        Position =
          Vector2(
            float32 x * Constants.tileSize,
            float32 y * Constants.tileSize
          )
        TileType = TileType.Platform
        Variant = SimpleNoise.rangeInt(0, 1, x, y, seed)
      }
    // Add lower platforms
    elif y = groundHeight + 3 && SimpleNoise.chance(0.3f, x, y + 100, seed) then
      Some {
        Position =
          Vector2(
            float32 x * Constants.tileSize,
            float32 y * Constants.tileSize
          )
        TileType = TileType.Platform
        Variant = 0
      }
    else
      None

  | Cavernous ->
    // Create gaps in ground (pits)
    if gapProbability < 0.2f then
      None // Full column gap
    elif y >= groundHeight && y < Constants.worldHeight then
      Some {
        Position =
          Vector2(
            float32 x * Constants.tileSize,
            float32 y * Constants.tileSize
          )
        TileType = Ground
        Variant = variant
      }
    // Add hazards
    elif
      y = groundHeight - 1 && SimpleNoise.chance(0.15f, x, y + 200, seed)
    then
      Some {
        Position =
          Vector2(
            float32 x * Constants.tileSize,
            float32 y * Constants.tileSize
          )
        TileType = Hazard
        Variant = 0
      }
    else
      None

/// Generate a chunk of tiles
let generateChunk(chunkX: int, seed: int) : Tile array =
  let pattern = getChunkPattern(chunkX, seed)
  let startX = chunkX * Constants.chunkWidth
  // Consistent theme for the entire chunk
  let theme = SimpleNoise.rangeInt(0, 5, chunkX, 0, seed)

  let tiles = ResizeArray<Tile>()

  for x in 0 .. Constants.chunkWidth - 1 do
    for y in 0 .. Constants.worldHeight - 1 do
      let globalX = startX + x

      let prevHeight =
        if globalX = 0 then
          None
        else
          Some(getGlobalGroundHeight(globalX - 1, seed))

      match
        generateTile(globalX, y, chunkX, pattern, seed, theme, prevHeight)
      with
      | Some tile -> tiles.Add(tile)
      | None -> ()

  tiles.ToArray()

/// Generate initial terrain (few chunks around spawn)
let generateInitialTerrain(seed: int) : Tile array =
  // Generate 3 chunks: [-1, 0, 1]
  let tiles = ResizeArray<Tile>()

  for chunkX in -1 .. 1 do
    // Force FlatGround pattern for initial safe area
    let chunkTiles =
      // Manually generate chunk with FlatGround pattern
      let pattern = FlatGround
      let startX = chunkX * Constants.chunkWidth
      // Use Grass theme (0) for spawn area
      let theme = 0
      let chunkTiles = ResizeArray<Tile>()

      for x in 0 .. Constants.chunkWidth - 1 do
        for y in 0 .. Constants.worldHeight - 1 do
          let globalX = startX + x
          let prevHeight = getGlobalGroundHeight(globalX - 1, seed)

          match
            generateTile(
              globalX,
              y,
              chunkX,
              pattern,
              seed,
              theme,
              Some prevHeight
            )
          with
          | Some tile -> chunkTiles.Add(tile)
          | None -> ()

      chunkTiles.ToArray()

    tiles.AddRange(chunkTiles)

  tiles.ToArray()

// ─────────────────────────────────────────────────────────────
// Terrain Management
// ─────────────────────────────────────────────────────────────

/// Get the chunk X coordinate from a world X position
let worldXToChunkX(worldX: float32) : int =
  int(worldX / (float32 Constants.chunkWidth * Constants.tileSize))

/// Check if we need to generate new terrain
let needsTerrainGeneration(model: Model) : bool =
  let currentChunkX = worldXToChunkX model.PlayerPosition.X
  // Generate 2 chunks ahead to prevent seeing the void
  currentChunkX + 2 > model.LastGeneratedChunk

/// Generate next terrain chunk and update model
let generateNextChunk(model: Model) : Model =
  let nextChunk = model.LastGeneratedChunk + 1
  let newTiles = generateChunk(nextChunk, model.Seed)

  // Combine existing tiles with new chunk
  let allTiles = Array.append model.Tiles newTiles

  {
    model with
        Tiles = allTiles
        LastGeneratedChunk = nextChunk
  }

/// Remove tiles that are too far behind the player (cleanup)
let cleanupOldTiles(model: Model) : Model =
  let currentChunkX = worldXToChunkX model.PlayerPosition.X
  let cleanupThreshold = currentChunkX - 3 // Remove chunks 3 chunks behind

  let filteredTiles =
    model.Tiles
    |> Array.filter(fun tile ->
      let tileChunkX =
        int(
          tile.Position.X / (float32 Constants.chunkWidth * Constants.tileSize)
        )

      tileChunkX >= cleanupThreshold)

  { model with Tiles = filteredTiles }

/// Create platforms array from tiles array
let createPlatformsFromTiles(tiles: Tile array) : Platform array =
  tiles
  |> Array.filter(fun t -> t.TileType <> TileType.Empty)
  |> Array.map(fun tile -> {
    Bounds =
      Rectangle(
        int tile.Position.X,
        int tile.Position.Y,
        int Constants.tileSize,
        int Constants.tileSize
      )
    Type = tile.TileType
    Variant = tile.Variant
  })

/// Ensure terrain is generated as player moves
let update (model: Model) : struct (Model * Cmd<'Msg>) =

  let model =
    if needsTerrainGeneration model then
      generateNextChunk model
    else
      model

  let model = cleanupOldTiles model

  // Update platforms when terrain changes
  let platforms = createPlatformsFromTiles model.Tiles

  { model with Platforms = platforms }, Cmd.none


/// Reset terrain to initial state
let reset (model: Model) : Model =
  let tiles = generateInitialTerrain model.Seed
  {
    model with
        Tiles = tiles
        LastGeneratedChunk = 1
  }



// ─────────────────────────────────────────────────────────────
// Terrain Query Functions
// ─────────────────────────────────────────────────────────────

/// Find ground height at given world X position
let findGroundAt(worldX: float32, tiles: Tile array) : float32 option =
  let tileX = int(worldX / Constants.tileSize)

  tiles
  |> Array.tryFind(fun tile ->
    let tileXPos = int(tile.Position.X / Constants.tileSize)
    tileXPos = tileX && tile.TileType <> Empty && tile.TileType <> Hazard)
  |> Option.map(fun tile -> tile.Position.Y)

/// Get all tiles visible in the given camera range
let getVisibleTiles
  (cameraX: float32)
  (screenWidth: float32)
  (tiles: Tile array)
  : Tile array =
  let margin = Constants.tileSize * 2.0f
  let left = cameraX - margin
  let right = cameraX + screenWidth + margin

  tiles
  |> Array.filter(fun tile ->
    tile.Position.X >= left && tile.Position.X <= right)

/// Render terrain tiles
let view (model: Model) (buffer: RenderBuffer<RenderCmd2D>) =
  let viewportWidth = 1280.0f // Default window width
  let visibleTiles = getVisibleTiles model.CameraX viewportWidth model.Tiles

  for tile in visibleTiles do
    let rect =
      match tile.TileType with
      | Ground -> SpriteLoader.TileRegions.getGroundVariant tile.Variant
      | Platform -> SpriteLoader.TileRegions.getPlatformVariant tile.Variant
      | Hazard -> SpriteLoader.TileRegions.getHazardTile()
      | Empty -> Rectangle.Empty

    if rect <> Rectangle.Empty then
      buffer.Sprite(
        sprite {
          texture model.TerrainAssets.GroundTile
          sourceRect rect
          at tile.Position.X tile.Position.Y
          size Constants.tileSize Constants.tileSize
          layer 0<RenderLayer>
        }
      )
      |> ignore



// ─────────────────────────────────────────────────────────────
// Terrain Statistics and Debugging
// ─────────────────────────────────────────────────────────────

module TerrainStats =
  /// Count tiles of each type
  let countTilesByType(tiles: Tile array) : Map<TileType, int> =
    tiles
    |> Array.groupBy(fun tile -> tile.TileType)
    |> Array.map(fun (tileType, tileArray) -> (tileType, tileArray.Length))
    |> Map.ofArray

  /// Get total number of tiles
  let totalTiles(tiles: Tile array) : int = tiles.Length

  /// Get the furthest generated chunk
  let getFurthestChunk(model: Model) : int = model.LastGeneratedChunk

  /// Estimate world width based on generated chunks
  let estimateWorldWidth(model: Model) : float32 =
    float32(
      (model.LastGeneratedChunk + 2)
      * Constants.chunkWidth
      * int Constants.tileSize
    )
