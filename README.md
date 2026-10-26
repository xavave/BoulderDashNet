# BoulderDashNet

🇬🇧 **English** · 🇫🇷 [Français](README.fr.md)

A **.NET 10 / Blazor WebAssembly** tribute to the Amiga classics **Boulder Dash** and **Emerald Mine**.
All the game logic is written in C#. It runs in the browser through WebAssembly, and everything is drawn on a `<canvas>` with pixel art generated in code (no external assets).

> The game is available in English and French: use the flag buttons above the game screen. The language is detected from the browser on first visit, then remembered.

<p align="center">
  <img src="docs/screenshots/title.png" alt="Title screen" width="720">
</p>

<table>
  <tr>
    <td><img src="docs/screenshots/gameplay.png" alt="Level 1: digging and collecting diamonds"></td>
    <td><img src="docs/screenshots/intro.png" alt="Level intro card"></td>
  </tr>
  <tr>
    <td align="center"><sub>Level 1: digging and collecting diamonds</sub></td>
    <td align="center"><sub>Level intro card</sub></td>
  </tr>
  <tr>
    <td><img src="docs/screenshots/fireflies.png" alt="Level 3: a firefly locked in its brick box"></td>
    <td><img src="docs/screenshots/butterflies.png" alt="Level 4: a butterfly waiting under a row of boulders"></td>
  </tr>
  <tr>
    <td align="center"><sub>Level 3: a firefly locked in its brick box</sub></td>
    <td align="center"><sub>Level 4: a butterfly waiting under a row of boulders</sub></td>
  </tr>
</table>

## Running the game

```powershell
dotnet run --project src/BoulderDashNet.Server
```

Then open <http://localhost:5046>. The ASP.NET Core host serves the game and keeps an access log (`logs/` folder, one file per day).

To run only the WebAssembly client, without the host: `dotnet run --project src/BoulderDashNet.Web` (<http://localhost:5018>).

Run the tests:

```powershell
dotnet test
```

Publish:

```powershell
# ASP.NET Core host + game (access log included)
dotnet publish src/BoulderDashNet.Server -c Release -o publish

# or a fully static site, for any web server (IIS, nginx, Web Station…)
dotnet publish src/BoulderDashNet.Web -c Release -o publish-static   # the site is in publish-static/wwwroot
```

### Access log

Each page load appends a line to `logs/access-YYYY-MM-DD.log`: date, client IP, path, HTTP status, referrer and user agent. Behind a reverse proxy, the real IP is read from `X-Forwarded-For`. Settings live in the `AccessLog` section of `appsettings.json`, or can be set with environment variables:

| Variable | Default | Purpose |
|---|---|---|
| `AccessLog__Enabled` | `true` | Turns the log on or off |
| `AccessLog__Directory` | `logs` | Folder for the log files |
| `AccessLog__RetentionDays` | `90` | Days to keep log files (`0` = keep forever) |

### Hall of Fame

At the end of a game (game over or victory), players can enter their name in the Hall of Fame. The name is remembered in a cookie (`bdnet_pseudo`, 1 year). Scores are stored on the server in `data/highscores.json` (best score per player, top 100) and everyone can see the leaderboard from the title screen.

- API: `GET /api/scores?top=10`, `POST /api/scores` `{ "name", "score", "level" }`
- Names: 1 to 16 letters, digits, spaces or `- _ . ' !`
- Basic anti-cheat: scores above what the reached level makes possible are rejected, and submissions are limited to 6 per minute per IP. The game runs in the browser, so a determined cheater can still forge a plausible score; edit or delete lines in `highscores.json` if needed.
- Settings: `HighScores__File` (default `data/highscores.json`), `HighScores__MaxEntries` (default `100`).

The `/health` endpoint returns `{"status":"ok"}` for monitoring.

## Controls

| Key | Action |
|---|---|
| Arrow keys / WASD (ZQSD on AZERTY) | Move the miner |
| Space (or Ctrl) + direction | Dig / collect **without moving** |
| Esc / P | Pause |
| R (while paused) | Give up the level (costs a life) |
| Enter | Start / skip the intro |
| M | Mute / unmute sound effects |
| N | Title screen 8-bit music on / off (off by default, also a 🎵 button) |

On touch screens, an on-screen D-pad appears automatically.

## Rules

- Collect the required number of diamonds: the exit opens (the screen flashes). Reach it before time runs out.
- A **falling** boulder or diamond is deadly. Resting ones are not, but they roll off boulders, diamonds and brick walls.
- You can push a boulder sideways if the cell behind it is empty.
- **Firefly** (orange square): follows walls on its left and explodes when it touches you. Crush it with a boulder.
- **Butterfly** (purple wings): follows walls on its right. When crushed, it turns into 9 diamonds.
- **Amoeba** (green): keeps growing. When enclosed, it turns into diamonds; when it gets too big, it turns into boulders. It makes any enemy it touches explode.
- **Magic wall**: triggered when an object falls on it. For a limited time it turns boulders into diamonds (and the other way round).
- Time left at the end of a level is added to your score. You earn an extra life every 500 points.

The highest level reached and the best score are saved in the browser (localStorage).

## Project structure

```
BoulderDashNet.sln
src/
  BoulderDashNet.Core/     game engine (pure C#, no dependencies)
    Tiles.cs               cell types, directions, events, render codes
    Cave.cs                physics and rules (grid scan on every tick)
    GameSession.cs         game session: levels, lives, score, timer, camera
    CaveDefinition.cs      cave definition + CaveBuilder (ASCII map or procedural)
    Levels.cs              the 7 caves
  BoulderDashNet.Server/   ASP.NET Core host: serves the game, access log, /health
  BoulderDashNet.Web/      Blazor WebAssembly app
    Pages/Home.razor(.cs)  menus and screens (HTML) + bridge to the engine
    wwwroot/js/game.js     canvas rendering, sprites, keyboard/touch input, WebAudio sounds
    wwwroot/css/app.css
tests/
  BoulderDashNet.Tests/    xUnit tests for the engine and levels
```

### Game loop

`game.js` calls `Home.Frame(dt, direction, ...)` on every `requestAnimationFrame`. The C# engine advances by 0, 1 or more ticks (about 7.7 per second, adjustable per level with `TickSeconds`). It then returns a `byte[]` made of a header (state, camera, HUD, sound events) followed by one sprite code per cell. The JavaScript side only draws.

### Adding a level

In `Levels.cs`, add a `CaveDefinition`:

- either with an ASCII map (`Map`). Legend: `W` steel, `w` brick, `.` dirt, space = empty, `r` boulder, `d` diamond, `P` player, `X` exit, `q` firefly, `B` butterfly, `a` amoeba, `m` magic wall;
- or with a Boulder Dash–style procedural build (`Build`): `RandomFill`, `Line`, `Box`, `FillRect`, `Set`…

The `Every_level_is_well_formed` test checks that every level has a player, an exit and enough diamonds.

## Credits

Fan project. The levels, sprites and sounds are original creations inspired by Boulder Dash (First Star Software, 1984) and Emerald Mine (Kingsoft, 1987). This project is not affiliated with either publisher.
