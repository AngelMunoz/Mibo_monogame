module MiboSample.Terrain

open System
open Microsoft.Xna.Framework
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

/// Generate height for a ground tile at given column
let generateGroundHeight
  (column: int, chunkX: int, pattern: TerrainPattern, seed: int)
  : int =
  let baseHeight = int Constants.worldHeight - 2 // Near bottom (tile index 10)
  let variation = SimpleNoise.rangeInt(0, 2, column, chunkX, seed)
  let noise = SimpleNoise.noise2d(column, chunkX, seed)

  match pattern with
  | FlatGround -> baseHeight
  | RollingHills -> baseHeight - variation
  | Mountains -> baseHeight - variation * 2
  | FloatingPlatforms -> baseHeight - 3 // Higher up (lower tile index)
  | Cavernous -> baseHeight - variation * 2
  | StaircaseUp -> baseHeight - (column % 3)
  | StaircaseDown -> baseHeight + (column % 3)

/// Calculate ground height for any global tile X coordinate
let getGlobalGroundHeight(x: int, seed: int) : int =
  // Handle negative coordinates correctly for chunk calculation
  let chunkX =
    if x >= 0 then
      x / Constants.chunkWidth
    else
      (x - Constants.chunkWidth + 1) / Constants.chunkWidth

  let pattern = getChunkPattern(chunkX, seed)
  generateGroundHeight(x, chunkX, pattern, seed)

/// Determine the tile shape based on neighbors
let getTileShape(x: int, y: int, height: int, seed: int) : int =
  let hL = getGlobalGroundHeight(x - 1, seed)
  let hR = getGlobalGroundHeight(x + 1, seed)

  // y is positive down. Larger Y = Lower Ground (Underground). Smaller Y = Higher Ground (Air).
  // height is the Y level of the surface.

  if y = height then
    // Surface
    if hL > height && hR > height then 0 // Isolated/Top (Both sides lower/open) - Use Top
    elif hL > height then 4 // Top Left (Left is open/lower)
    elif hR > height then 5 // Top Right (Right is open/lower)
    else 0 // Top Center
  elif y > height then
    // Underground
    // Check if we are exposed on sides (cliff face)
    // Exposed if neighbor height is below us (h > y)
    let exposedL = hL > y
    let exposedR = hR > y

    if exposedL then 2 // Center Left
    elif exposedR then 3 // Center Right
    else 1 // Center (Solid)
  else
    0 // Should not happen for air

/// Generate a single tile at the given position
let generateTile
  (x: int, y: int, chunkX: int, pattern: TerrainPattern, seed: int, theme: int)
  : Tile option =
  let groundHeight = generateGroundHeight(x, chunkX, pattern, seed)
  let columnNoise = SimpleNoise.noise2d(x, y, seed)
  let gapProbability = SimpleNoise.noise2d(x, chunkX + 100, seed)

  let shape = getTileShape(x, y, groundHeight, seed)
  let variant = theme * 10 + shape

  match pattern with
  | FlatGround ->
    if y = groundHeight then
      Some {
        Position =
          Vector2(
            float32 x * Constants.tileSize,
            float32 y * Constants.tileSize
          )
        TileType = Ground
        Variant = variant
      }
    elif y > groundHeight && y < groundHeight + 2 then
      // Fill below ground (2 tiles thick for solidity)
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
    if y = groundHeight then
      Some {
        Position =
          Vector2(
            float32 x * Constants.tileSize,
            float32 y * Constants.tileSize
          )
        TileType = Ground
        Variant = variant
      }
    elif y > groundHeight && y < groundHeight + 2 then
      // Fill below ground
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
    if y = groundHeight then
      Some {
        Position =
          Vector2(
            float32 x * Constants.tileSize,
            float32 y * Constants.tileSize
          )
        TileType = Ground
        Variant = variant
      }
    elif y > groundHeight && y < groundHeight + 4 then
      Some {
        Position =
          Vector2(
            float32 x * Constants.tileSize,
            float32 y * Constants.tileSize
          )
        TileType = Ground
        Variant = variant
      }
    // Add floating platforms above mountains
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

  | FloatingPlatforms ->
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
    // Create gaps in ground
    if y = groundHeight && gapProbability < 0.2f then
      None // Gap
    elif y = groundHeight then
      Some {
        Position =
          Vector2(
            float32 x * Constants.tileSize,
            float32 y * Constants.tileSize
          )
        TileType = Ground
        Variant = variant
      }
    elif y > groundHeight && y < groundHeight + 2 then
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

  | StaircaseUp ->
    if y = groundHeight then
      Some {
        Position =
          Vector2(
            float32 x * Constants.tileSize,
            float32 y * Constants.tileSize
          )
        TileType = Ground
        Variant = variant
      }
    elif y > groundHeight then
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

  | StaircaseDown ->
    if y = groundHeight then
      Some {
        Position =
          Vector2(
            float32 x * Constants.tileSize,
            float32 y * Constants.tileSize
          )
        TileType = Ground
        Variant = variant
      }
    elif y > groundHeight then
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

/// Generate a chunk of tiles
let generateChunk(chunkX: int, seed: int) : Tile array =
  let pattern = getChunkPattern(chunkX, seed)
  let startX = chunkX * Constants.chunkWidth
  // Consistent theme for the entire chunk
  let theme = SimpleNoise.rangeInt(0, 5, chunkX, 0, seed)

  let tiles = ResizeArray<Tile>()

  for x in 0 .. Constants.chunkWidth - 1 do
    for y in 0 .. Constants.worldHeight - 1 do
      match generateTile(startX + x, y, chunkX, pattern, seed, theme) with
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
          match generateTile(startX + x, y, chunkX, pattern, seed, theme) with
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

/// Ensure terrain is generated as player moves
let updateTerrain(model: Model) : Model =
  let model =
    if needsTerrainGeneration model then
      generateNextChunk model
    else
      model

  cleanupOldTiles model

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
  (cameraX: float32, screenWidth: float32, tiles: Tile array)
  : Tile array =
  let margin = Constants.tileSize * 2.0f
  let left = cameraX - margin
  let right = cameraX + screenWidth + margin

  tiles
  |> Array.filter(fun tile ->
    tile.Position.X >= left && tile.Position.X <= right)

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
