module MiboSample.DayNight

open Microsoft.Xna.Framework
open System

type State = {
  TimeOfDay: float32 // 0.0 to 24.0
  DayDuration: float32 // Real-time seconds for a full 24h cycle
}

let initial = {
  TimeOfDay = 12.0f // Start at noon
  DayDuration = 120.0f // 120 seconds per day for smoother shadows
}

let update (dt: float32) (state: State) =
  let hoursPerSecond = 24.0f / state.DayDuration
  let newTime = (state.TimeOfDay + dt * hoursPerSecond) % 24.0f
  { state with TimeOfDay = newTime }

// ─────────────────────────────────────────────────────────────
// Color Palettes
// ─────────────────────────────────────────────────────────────

let private lerpColor (c1: Color) (c2: Color) (t: float32) =
  Color.Lerp(c1, c2, MathHelper.Clamp(t, 0.0f, 1.0f))

// Key colors
let cMidnightTop = Color(10, 10, 30)
let cMidnightBot = Color(20, 20, 40)

let cSunriseTop = Color(60, 60, 100)
let cSunriseBot = Color(255, 100, 50)

let cDayTop = Color(100, 149, 237)
let cDayBot = Color(173, 216, 230)

let cSunsetTop = Color(50, 50, 100)
let cSunsetBot = Color(255, 80, 50)

let getSkyColors(time: float32) : struct (Color * Color * float32) =
  // Returns TopColor, BottomColor, StarIntensity

  // 0-4: Night
  // 4-6: Sunrise
  // 6-18: Day
  // 18-20: Sunset
  // 20-24: Night

  if time < 4.0f then
    // Deep Night
    struct (cMidnightTop, cMidnightBot, 1.0f)

  elif time < 6.0f then
    // Sunrise (Night -> Sunrise)
    let t = (time - 4.0f) / 2.0f
    let top = lerpColor cMidnightTop cSunriseTop t
    let bot = lerpColor cMidnightBot cSunriseBot t
    let stars = 1.0f - t
    struct (top, bot, stars)

  elif time < 8.0f then
    // Morning (Sunrise -> Day)
    let t = (time - 6.0f) / 2.0f
    let top = lerpColor cSunriseTop cDayTop t
    let bot = lerpColor cSunriseBot cDayBot t
    struct (top, bot, 0.0f)

  elif time < 16.0f then
    // Mid Day
    struct (cDayTop, cDayBot, 0.0f)

  elif time < 18.0f then
    // Afternoon (Day -> Sunset)
    let t = (time - 16.0f) / 2.0f
    let top = lerpColor cDayTop cSunsetTop t
    let bot = lerpColor cDayBot cSunsetBot t
    struct (top, bot, 0.0f)

  elif time < 20.0f then
    // Dusk (Sunset -> Night)
    let t = (time - 18.0f) / 2.0f
    let top = lerpColor cSunsetTop cMidnightTop t
    let bot = lerpColor cSunsetBot cMidnightBot t
    let stars = t
    struct (top, bot, stars)

  else
    // Deep Night
    struct (cMidnightTop, cMidnightBot, 1.0f)

let getSunPosition
  (time: float32)
  (center: Vector2)
  (radius: Vector2)
  : Vector2 =
  // Orbit: Sunrise at 6 (Left), Noon at 12 (Top), Sunset at 18 (Right), Midnight at 24/0 (Bottom)
  // -PI/2 is top.

  // We want 6am to be PI (Left), 12pm to be -PI/2 (Top), 18pm to be 0 (Right)
  // Angle = (Time - 18) / 24 * 2PI -> at 18: 0. at 12: -6/24 = -1/4 * 2PI = -PI/2. Correct.

  let angle = (time - 18.0f) / 24.0f * MathHelper.TwoPi
  let offset = Vector2(cos(angle), sin(angle)) * radius
  center + offset

let getMoonPosition
  (time: float32)
  (center: Vector2)
  (radius: Vector2)
  : Vector2 =
  // Moon is opposite to sun
  let sunPos = getSunPosition time center radius
  // Mirror around center
  center - (sunPos - center)

let getSunColor(time: float32) : Color =
  if time < 5.0f || time > 19.0f then
    Color.Transparent // Off
  elif time < 7.0f then
    lerpColor Color.Transparent Color.Orange ((time - 5.0f) / 2.0f)
  elif time < 17.0f then
    Color.LightYellow
  elif time < 19.0f then
    lerpColor Color.LightYellow Color.OrangeRed ((time - 17.0f) / 2.0f)
  else
    Color.Transparent

let getMoonColor(time: float32) : Color =
  if time > 5.0f && time < 19.0f then
    Color.Transparent // Invisible during day
  else
    Color.LightBlue
