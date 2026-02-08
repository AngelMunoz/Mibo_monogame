---
title: Camera
category: Rendering
categoryindex: 3
index: 13
---

# Camera

Mibo uses different camera types for different rendering contexts:

- **2D rendering**: `Camera2D` module creates cameras for orthographic 2D rendering
- **3D rendering**: `Mibo.Rendering.Graphics3D.Camera` type with rich configuration options

Both 2D and 3D renderers consume cameras via render commands (`SetCamera`).

## 2D cameras (`Camera2D`)

Use `Camera2D.create` to build an orthographic camera centered on a world position.

```fsharp
let cam = Camera2D.create playerPos 1.5f (Point(1280, 720))

// In a 2D view:
Draw2D.camera cam 0<RenderLayer> buffer
```

Helpers:

- `Camera2D.screenToWorld` / `worldToScreen`
- `Camera2D.viewportBounds` (useful for 2D culling)

---

# Rendering3D Camera

The Rendering3D pipeline uses a rich `Camera` type that stores both matrices and configuration parameters, enabling easy modification and rebuilding.

```fsharp
namespace Mibo.Rendering.Graphics3D

type Camera = {
  View: Matrix
  Projection: Matrix
  Position: Vector3
  Target: Vector3
  Up: Vector3
  Fov: float32
  Aspect: float32
  Near: float32
  Far: float32
}
```

The camera is a struct with both computed matrices (View/Projection) and configuration fields. This allows you to modify properties and have matrices rebuilt automatically.

## Creating Cameras

### Perspective camera

Create a standard perspective camera from scratch:

```fsharp
open Mibo.Rendering.Graphics3D

let camera =
  Camera.perspective
    (Vector3(0f, 10f, 20f))  // position
    Vector3.Zero              // target
    Vector3.Up                // up
    MathHelper.PiOver4        // fov (45°)
    (16f / 9f)                // aspect ratio
    0.1f                      // near plane
    1000f                     // far plane
```

### Default perspective camera

Get a camera with sensible defaults:

```fsharp
let camera = Camera.perspectiveDefaults
// Position: (0, 0, 10), looking at origin, 45° FOV, 16:9 aspect
```

### Orthographic camera

For orthographic rendering (useful for isometric views):

```fsharp
let camera =
  Camera.orthographic
    (Vector3(0f, 10f, 20f))  // position
    Vector3.Zero              // target
    Vector3.Up                // up
    20f                       // width
    11.25f                    // height (20 * 9/16)
    0.1f                      // near plane
    100f                      // far plane
```

### LookAt camera

Configure an existing camera to look at a target:

```fsharp
let camera =
  Camera.perspectiveDefaults
  |> Camera.lookAt (Vector3(0f, 5f, 10f)) Vector3.Zero
```

### Orbit camera

Create a camera orbiting around a target point:

```fsharp
let camera =
  Camera.orbit
    Vector3.Zero      // target to orbit around
    0.0f              // yaw (radians)
    0.35f             // pitch (radians)
    12.0f             // distance/radius
    Camera.perspectiveDefaults
```

Or using the chaining pattern:

```fsharp
let camera =
  Camera.perspectiveDefaults
  |> Camera.orbit Vector3.Zero 0.0f 0.35f 12.0f
```

## Builder Pattern

The camera module provides fluent setter functions that rebuild matrices automatically:

```fsharp
let camera =
  Camera.perspectiveDefaults
  |> Camera.at (Vector3(5f, 5f, 5f))           // set position
  |> Camera.lookingAt Vector3.Zero            // set target
  |> Camera.withFov (MathHelper.PiOver3)      // change FOV to 60°
  |> Camera.withAspect (4f / 3f)              // change aspect ratio
  |> Camera.withRange 0.5f 500f               // change near/far planes
```

Available builder functions:

- `at position` - set camera position
- `lookingAt target` - set what the camera looks at
- `withUp up` - set the up vector
- `withFov fov` - set field of view (radians)
- `withAspect aspect` - set aspect ratio
- `withRange near far` - set near and far clipping planes
- `withDistance distance` - set distance from target along forward axis
- `withAngles yaw pitch` - orbit using spherical angles

## Camera Movement Examples

### Orbit camera

Control camera orbit around a target with mouse input:

```fsharp
let updateOrbit (camera: Camera) (deltaYaw: float32) (deltaPitch: float32) =
  let currentYaw = 0f // track these in your game state
  let currentPitch = 0.35f

  camera
  |> Camera.withAngles (currentYaw + deltaYaw) (currentPitch + deltaPitch)
```

### Third-person follow camera

```fsharp
let updateFollowCamera (camera: Camera) playerPos yaw pitch distance =
  camera
  |> Camera.orbit playerPos yaw pitch distance
```

### Zoom with scroll wheel

```fsharp
let updateZoom (camera: Camera) (scrollDelta: float32) =
  let currentDist = Vector3.Distance(camera.Position, camera.Target)
  let newDist = max 1f (currentDist + scrollDelta)
  camera |> Camera.withDistance newDist
```

## Utility Functions

### Screen picking

Convert a screen point to a 3D ray for mouse/touch picking:

```fsharp
let ray = Camera.screenPointToRay camera mousePos viewport

// Test intersection with geometry
let intersection = ray.Intersects(boundingBox)
if intersection.HasValue then
    let distance = intersection.Value
    let hitPoint = ray.Position + ray.Direction * distance
    // Handle click
```

### Frustum culling

Get the bounding frustum for visibility checks:

```fsharp
let frustum = Camera.boundingFrustum camera

if frustum.Intersects(entity.BoundingSphere) then
    // Entity is visible, render it
```

See also: [Culling](culling.html).

---

# Legacy Elmish Camera3D

> **[DEPRECATED]** This camera API is deprecated. Use the [Rendering3D Camera](#rendering3d-camera) above instead.

The Elmish `Camera` type is a simple struct with just View and Projection matrices:

```fsharp
type Camera = { View: Matrix; Projection: Matrix }
```

While still functional, it lacks the rich configuration and builder pattern of the Rendering3D camera.

## Basic 3D cameras

`Camera3D.lookAt` is the common default:

```fsharp
let cam =
  Camera3D.lookAt
    (Vector3(0f, 10f, 20f))
    Vector3.Zero
    Vector3.Up
    MathHelper.PiOver4
    (16f/9f)
    0.1f
    1000f
```

`Camera3D.orbit` is handy for third-person cameras, inspection views, or editor cameras:

```fsharp
let cam =
  Camera3D.orbit
    Vector3.Zero      // target
    0.0f              // yaw (radians)
    0.35f             // pitch (radians)
    12.0f             // radius
    MathHelper.PiOver4
    (16f/9f)
    0.1f
    1000f
```

If you need a different style (FPS/free-fly/etc.), you can always construct a `Camera` directly by providing your own `View`/`Projection` matrices.

### Custom camera (simple)

This example builds a minimal "FPS-ish" camera from a position plus yaw/pitch:

```fsharp
let customCamera
  (position: Vector3)
  (yaw: float32)
  (pitch: float32)
  (fov: float32)
  (aspect: float32)
  (nearPlane: float32)
  (farPlane: float32)
  : Camera =

  let rot = Matrix.CreateFromYawPitchRoll(yaw, pitch, 0.0f)
  let forward = Vector3.Transform(Vector3.Forward, rot)
  let up = Vector3.Transform(Vector3.Up, rot)

  {
    View = Matrix.CreateLookAt(position, position + forward, up)
    Projection = Matrix.CreatePerspectiveFieldOfView(fov, aspect, nearPlane, farPlane)
  }
```

Picking helper:

- `Camera3D.screenPointToRay` creates a `Ray` you can intersect with `BoundingBox`/`BoundingSphere`.

Frustum helper:

- `Camera3D.boundingFrustum` returns `BoundingFrustum` for visibility checks.

See also: [Culling](culling.html).
