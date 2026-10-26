// BoulderDashNet — rendu canvas, clavier/tactile et sons.
// La logique du jeu tourne en C# (BoulderDashNet.Core) ; ce module ne fait qu'afficher.

const T = 16;            // taille native d'un sprite
const S = 2;             // facteur d'agrandissement
const TS = T * S;        // taille d'une tuile à l'écran
const HUD = 32;
const HEADER_INTS = 16;

const STATE = { Title: 0, LevelIntro: 1, Playing: 2, Paused: 3, Dying: 4, LevelComplete: 5, GameOver: 6, Victory: 7 };
const EV = {
    BoulderLand: 1, DiamondLand: 2, Collect: 4, Explosion: 8, ExitOpened: 16, Dig: 32,
    Push: 64, Magic: 128, Step: 256, ExtraLife: 512, Exited: 1024,
};
const CODE = {
    Empty: 0, Dirt: 1, Wall: 2, Steel: 3, Boulder: 4, Diamond: 5, PlayerIdle: 6, PlayerLeft: 7, PlayerRight: 8,
    ExitClosed: 9, ExitOpen: 10, Firefly: 11, Butterfly: 12, Amoeba: 13, MagicWall: 14, MagicWallActive: 15,
    Explosion: 16, DiamondBirth: 20, Count: 24,
};

let dotnet = null, canvas = null, ctx = null, atlas = null;
let viewW = 20, viewH = 12;
let rafId = 0, lastTs = 0, clock = 0;
let flash = 0, lastTimeLeft = 0;

// ---------------------------------------------------------------- API

export function start(dotnetRef, canvasEl, vw, vh) {
    dotnet = dotnetRef;
    canvas = canvasEl;
    viewW = vw; viewH = vh;
    canvas.width = viewW * TS;
    canvas.height = viewH * TS + HUD;
    ctx = canvas.getContext('2d');
    ctx.imageSmoothingEnabled = false;
    atlas = buildAtlas();
    attachInput();
    lastTs = performance.now();
    rafId = requestAnimationFrame(loop);
}

export function isMusicOn() { return music.enabled; }

export function toggleMusic() {
    sound.unlock();
    music.enabled = !music.enabled;
    try { localStorage.setItem('bdnet.music', music.enabled ? '1' : '0'); } catch { /* stockage indisponible */ }
    return music.enabled;
}

export function stop() {
    cancelAnimationFrame(rafId);
    stopMusic(true);
    detachInput();
    dotnet = null;
}

export function loadProgress() {
    try {
        return [parseInt(localStorage.getItem('bdnet.level') ?? '0') || 0,
                parseInt(localStorage.getItem('bdnet.hiscore') ?? '0') || 0];
    } catch { return [0, 0]; }
}

let lang = 'fr';

export function loadLanguage() {
    try {
        const saved = localStorage.getItem('bdnet.lang');
        if (saved) return saved;
    } catch { /* stockage indisponible */ }
    return (navigator.language || 'fr').toLowerCase().startsWith('fr') ? 'fr' : 'en';
}

export function setLanguage(code) {
    lang = code === 'en' ? 'en' : 'fr';
    document.documentElement.lang = lang;
    try { localStorage.setItem('bdnet.lang', lang); } catch { /* stockage indisponible */ }
}

// Pseudo du Hall of Fame, mémorisé dans un cookie (1 an).
export function getPseudo() {
    const m = document.cookie.match(/(?:^|;\s*)bdnet_pseudo=([^;]*)/);
    try { return m ? decodeURIComponent(m[1]) : ''; } catch { return ''; }
}

export function setPseudo(name) {
    const secure = location.protocol === 'https:' ? '; Secure' : '';
    document.cookie = `bdnet_pseudo=${encodeURIComponent(name)}; Max-Age=31536000; Path=/; SameSite=Lax${secure}`;
}

export function saveProgress(level, hiscore) {
    try {
        localStorage.setItem('bdnet.level', String(level));
        localStorage.setItem('bdnet.hiscore', String(hiscore));
    } catch { /* stockage indisponible */ }
}

// ---------------------------------------------------------------- boucle

function loop(ts) {
    if (!dotnet) return;
    const dt = Math.min(250, ts - lastTs);
    lastTs = ts;
    clock += dt / 1000;

    const flags = (input.snap ? 1 : 0) | input.latched;
    const pressed = input.pressed;
    input.latched = 0;
    input.pressed = 0;

    let frame;
    try {
        frame = dotnet.invokeMethod('Frame', dt, currentDir(), pressed, flags);
    } catch (e) {
        console.error(e);
        rafId = requestAnimationFrame(loop);
        return;
    }
    if (typeof frame === 'string') frame = base64ToBytes(frame);
    render(frame);
    rafId = requestAnimationFrame(loop);
}

function base64ToBytes(b64) {
    const bin = atob(b64);
    const out = new Uint8Array(bin.length);
    for (let i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
    return out;
}

// ---------------------------------------------------------------- rendu

function render(buf) {
    const dv = new DataView(buf.buffer, buf.byteOffset, buf.byteLength);
    const h = i => dv.getInt32(i * 4, true);
    const st = {
        state: h(0), w: h(1), hgt: h(2), camX: h(3) / 1000, camY: h(4) / 1000,
        got: h(5), need: h(6), value: h(7), time: h(8), score: h(9), lives: h(10), level: h(11),
        events: h(12), exitOpen: h(13) === 1, uiChanged: h(14) === 1, hiscore: h(15),
    };
    const cells = buf.subarray(HEADER_INTS * 4);

    if (st.uiChanged && dotnet) dotnet.invokeMethodAsync('SyncUi');
    if (st.events) playEvents(st.events);
    if (st.events & EV.ExitOpened) flash = 1;
    if (st.state === STATE.Playing && st.time !== lastTimeLeft && st.time <= 10 && st.time > 0) sfx.tick();
    lastTimeLeft = st.time;
    updateMusic(st.state === STATE.Title);

    ctx.fillStyle = '#000';
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    if (st.w === 0) {
        drawAttract();
    } else {
        drawCave(st, cells);
        drawHud(st);
    }

    if (flash > 0) {
        ctx.fillStyle = `rgba(255,255,255,${flash * 0.7})`;
        ctx.fillRect(0, HUD, canvas.width, canvas.height - HUD);
        flash = Math.max(0, flash - 0.08);
    }
}

function frameFor(code, x, y) {
    switch (code) {
        case CODE.Diamond: return Math.floor(clock * 10 + ((x * 7 + y * 3) % 4) * 0) % 4;
        case CODE.PlayerIdle: return (clock % 3.2) < 0.15 ? 1 : (Math.floor(clock * 2) % 8 === 5 ? 2 : 0);
        case CODE.PlayerLeft:
        case CODE.PlayerRight: return Math.floor(clock * 9) % 4;
        case CODE.Firefly:
        case CODE.Butterfly: return Math.floor(clock * 10) % 4;
        case CODE.Amoeba: return Math.floor(clock * 6 + (x + y)) % 4;
        case CODE.MagicWallActive: return Math.floor(clock * 14) % 4;
        case CODE.ExitOpen: return Math.floor(clock * 5) % 4;
        default: return 0;
    }
}

function drawCave(st, cells) {
    const cx = Math.floor(st.camX), cy = Math.floor(st.camY);
    const ox = Math.round((st.camX - cx) * TS), oy = Math.round((st.camY - cy) * TS);
    for (let vy = 0; vy <= viewH; vy++) {
        const y = cy + vy;
        if (y < 0 || y >= st.hgt) continue;
        for (let vx = 0; vx <= viewW; vx++) {
            const x = cx + vx;
            if (x < 0 || x >= st.w) continue;
            const code = cells[y * st.w + x];
            if (code === CODE.Empty) continue;
            const f = frameFor(code, x, y);
            ctx.drawImage(atlas, f * T, code * T, T, T, vx * TS - ox, HUD + vy * TS - oy, TS, TS);
        }
    }
    if (st.state === STATE.Dying) {
        ctx.fillStyle = `rgba(120,0,0,${0.15 + 0.1 * Math.sin(clock * 10)})`;
        ctx.fillRect(0, HUD, canvas.width, canvas.height - HUD);
    }
}

function drawHud(st) {
    ctx.fillStyle = '#0b0b14';
    ctx.fillRect(0, 0, canvas.width, HUD);
    ctx.fillStyle = '#2a2a40';
    ctx.fillRect(0, HUD - 2, canvas.width, 2);

    ctx.drawImage(atlas, 0, CODE.Diamond * T, T, T, 8, 4, 24, 24);
    ctx.font = 'bold 18px "Courier New", monospace';
    ctx.textBaseline = 'middle';
    const y = HUD / 2;

    ctx.fillStyle = st.got >= st.need ? '#ffe14d' : '#ffffff';
    ctx.fillText(`${pad(st.got, 2)}/${pad(st.need, 2)}`, 38, y);
    ctx.fillStyle = '#7fe7ff';
    ctx.fillText(`×${st.value}`, 120, y);

    ctx.fillStyle = '#9aa4b4';
    ctx.fillText(`${lang === 'en' ? 'L' : 'N'}${st.level}`, 196, y);

    const low = st.time <= 10 && Math.floor(clock * 4) % 2 === 0;
    ctx.fillStyle = st.time <= 10 ? (low ? '#ff4040' : '#ff9090') : '#ffffff';
    ctx.fillText(`⏱${pad(st.time, 3)}`, 262, y);

    ctx.fillStyle = '#ffffff';
    ctx.fillText(`${pad(st.score, 6)}`, 380, y);

    ctx.fillStyle = '#ff6b8a';
    ctx.fillText(`♥${st.lives}`, 520, y);

    if (!sound.enabled) {
        ctx.fillStyle = '#666';
        ctx.fillText('🔇', 596, y);
    }
}

function pad(n, len) { return String(Math.max(0, n)).padStart(len, '0'); }

// Fond animé de l'écran titre : pluie de rochers et de diamants.
const rain = [];
function drawAttract() {
    const top = 0, rows = Math.ceil(canvas.height / TS);
    if (rain.length === 0) {
        for (let i = 0; i < 36; i++)
            rain.push({ x: 1 + Math.random() * (viewW - 3), y: Math.random() * rows, v: 1.5 + Math.random() * 3, c: Math.random() < 0.35 ? CODE.Diamond : CODE.Boulder });
    }
    for (let y = 0; y < rows; y++)
        for (let x = 0; x < viewW; x++) {
            const edge = y === 0 || y === rows - 1 || x === 0 || x === viewW - 1;
            if (!edge && hash(x, y, 7) > 0.18) continue;
            ctx.globalAlpha = edge ? 0.7 : 0.35;
            ctx.drawImage(atlas, 0, (edge ? CODE.Steel : CODE.Dirt) * T, T, T, x * TS, top + y * TS, TS, TS);
        }
    ctx.globalAlpha = 0.6;
    for (const r of rain) {
        r.y += r.v / 60;
        if (r.y > rows - 1) { r.y = 0; r.x = 1 + Math.random() * (viewW - 3); }
        const f = r.c === CODE.Diamond ? Math.floor(clock * 10) % 4 : 0;
        ctx.drawImage(atlas, f * T, r.c * T, T, T, r.x * TS, top + r.y * TS, TS, TS);
    }
    ctx.globalAlpha = 1;
}

// ---------------------------------------------------------------- sprites (pixel art original, généré par code)

function buildAtlas() {
    const c = document.createElement('canvas');
    c.width = T * 4;
    c.height = T * CODE.Count;
    const g = c.getContext('2d');
    for (let code = 0; code < CODE.Count; code++)
        for (let f = 0; f < 4; f++) {
            g.save();
            g.translate(f * T, code * T);
            g.beginPath(); g.rect(0, 0, T, T); g.clip();
            drawSprite(g, code, f);
            g.restore();
        }
    return c;
}

function px(g, x, y, color) { g.fillStyle = color; g.fillRect(x, y, 1, 1); }

function fromArt(g, art, palette, mirror = false) {
    for (let y = 0; y < art.length; y++)
        for (let x = 0; x < art[y].length; x++) {
            const ch = art[y][x];
            const col = palette[ch];
            if (col) px(g, mirror ? T - 1 - x : x, y, col);
        }
}

function hash(x, y, s = 0) {
    let h = (x * 374761393 + y * 668265263 + s * 2147483647) | 0;
    h = (h ^ (h >>> 13)) * 1274126177 | 0;
    return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
}

function drawSprite(g, code, f) {
    switch (code) {
        case CODE.Dirt: return drawDirt(g);
        case CODE.Wall: return drawBricks(g, ['#2b1a14', '#8f3f22', '#b9592f', '#5f2814']);
        case CODE.Steel:
        case CODE.ExitClosed: return drawSteel(g);
        case CODE.Boulder: return drawBoulder(g);
        case CODE.Diamond: return drawDiamond(g, f);
        case CODE.PlayerIdle: return drawPlayer(g, 'idle', f);
        case CODE.PlayerLeft: return drawPlayer(g, 'left', f);
        case CODE.PlayerRight: return drawPlayer(g, 'right', f);
        case CODE.ExitOpen: return drawExit(g, f);
        case CODE.Firefly: return drawFirefly(g, f);
        case CODE.Butterfly: return drawButterfly(g, f);
        case CODE.Amoeba: return drawAmoeba(g, f);
        case CODE.MagicWall: return drawBricks(g, ['#1d1d2b', '#4a4f73', '#6c74a8', '#30344f']);
        case CODE.MagicWallActive: {
            const pal = [['#ff4df0', '#ffd24d'], ['#4dffe1', '#ff4df0'], ['#ffd24d', '#4d7bff'], ['#4d7bff', '#4dffe1']][f];
            return drawBricks(g, ['#14101f', pal[0], pal[1], '#2a1f45']);
        }
        default:
            if (code >= CODE.Explosion && code < CODE.Explosion + 4) return drawExplosion(g, code - CODE.Explosion, false);
            if (code >= CODE.DiamondBirth && code < CODE.DiamondBirth + 4) return drawExplosion(g, code - CODE.DiamondBirth, true);
    }
}

function drawDirt(g) {
    g.fillStyle = '#5a3417';
    g.fillRect(0, 0, T, T);
    for (let y = 0; y < T; y++)
        for (let x = 0; x < T; x++) {
            const r = hash(x, y, 3);
            if (r < 0.14) px(g, x, y, '#7d4a22');
            else if (r < 0.24) px(g, x, y, '#3d220e');
            else if (r < 0.27) px(g, x, y, '#9a6a3a');
        }
}

function drawBricks(g, [mortar, brick, light, dark]) {
    g.fillStyle = mortar;
    g.fillRect(0, 0, T, T);
    for (let row = 0; row < 4; row++) {
        const off = row % 2 ? 4 : 0;
        for (let col = -1; col < 2; col++) {
            const x = col * 8 + off, y = row * 4;
            g.fillStyle = brick; g.fillRect(x, y, 7, 3);
            g.fillStyle = light; g.fillRect(x, y, 7, 1);
            g.fillStyle = dark; g.fillRect(x, y + 2, 7, 1);
        }
    }
}

function drawSteel(g) {
    g.fillStyle = '#5b6474'; g.fillRect(0, 0, T, T);
    g.fillStyle = '#a7b1c2'; g.fillRect(0, 0, T, 1); g.fillRect(0, 0, 1, T);
    g.fillStyle = '#272c35'; g.fillRect(0, T - 1, T, 1); g.fillRect(T - 1, 0, 1, T);
    g.fillStyle = '#6c7586'; g.fillRect(3, 3, 10, 10);
    g.fillStyle = '#4b5361'; g.fillRect(3, 12, 10, 1); g.fillRect(12, 3, 1, 10);
    for (const [x, y] of [[2, 2], [13, 2], [2, 13], [13, 13]]) {
        px(g, x, y, '#dfe6f0'); px(g, x + 1, y + 1, '#30353f');
    }
}

function drawBoulder(g) {
    const cols = ['#2e2925', '#5d554c', '#8a8073', '#b9ae9f', '#e2d9cb'];
    for (let y = 0; y < T; y++)
        for (let x = 0; x < T; x++) {
            const dx = (x + 0.5 - 8) / 7.2, dy = (y + 0.5 - 8.6) / 6.9;
            const d = dx * dx + dy * dy;
            if (d > 1) continue;
            const z = Math.sqrt(1 - d);
            let l = (-dx * 0.55 - dy * 0.65 + z * 0.6);
            l += (hash(x, y, 11) - 0.5) * 0.25;
            let i = d > 0.82 ? 0 : l < 0.15 ? 1 : l < 0.5 ? 2 : l < 0.85 ? 3 : 4;
            px(g, x, y, cols[i]);
        }
}

const DIAMOND_ART = [
    '................',
    '................',
    '....AAAAAAAA....',
    '...ABBCBBCBBA...',
    '..ABBCCBBCCBBA..',
    '.ABBCCCBBCCCBBA.',
    '.AAAAAAAAAAAAAA.',
    '.ADDEDDDDDDEDDA.',
    '..ADDEDDDDEDDA..',
    '...ADDEDDEDDA...',
    '....ADDEEDDA....',
    '.....ADEEDA.....',
    '......ADDA......',
    '.......AA.......',
    '................',
    '................',
];

function drawDiamond(g, f) {
    const pals = [
        { A: '#1a6f9e', B: '#7fe7ff', C: '#ffffff', D: '#22a6dd', E: '#bff4ff' },
        { A: '#1a6f9e', B: '#a8f0ff', C: '#d8fbff', D: '#2fb5e8', E: '#e8fdff' },
        { A: '#1a6f9e', B: '#5fd8f7', C: '#ffffff', D: '#1b93c9', E: '#9feaff' },
        { A: '#1a6f9e', B: '#7fe7ff', C: '#e8fdff', D: '#27a0d6', E: '#ffffff' },
    ];
    fromArt(g, DIAMOND_ART, pals[f]);
    const [sx, sy] = [[4, 4], [11, 3], [8, 8], [5, 7]][f];
    px(g, sx, sy, '#ffffff'); px(g, sx - 1, sy, '#dffcff'); px(g, sx + 1, sy, '#dffcff');
    px(g, sx, sy - 1, '#dffcff'); px(g, sx, sy + 1, '#dffcff');
}

// Mineur original : casque jaune avec lampe, salopette bleue.
function drawPlayer(g, mode, f) {
    const head = [
        '................',
        '.....YYYYYY.....',
        '....YYYYYYLL....',
        '....HHHHHHHHH...',
        '.....SSSSSS.....',
        '.....SSSSSS.....',
        '.....SSSSSS.....',
        '......SSSS......',
    ];
    const body = [
        '...SBBBBBBBBS...',
        '...SBBOBBOBBS...',
        '...SBBBBBBBBS...',
        '....BBBBBBBB....',
    ];
    const legsStand = ['....PPP..PPP....', '....PPP..PPP....', '...KKKK..KKKK...', '................'];
    const legsA = ['....PPP...PPP...', '...PPP.....PP...', '..KKKK....KKKK..', '................'];
    const legsB = ['.....PPP.PPP....', '.....PPP.PPP....', '....KKKK.KKKK...', '................'];
    const pal = { Y: '#ffd23f', L: '#fffbe0', H: '#b88a10', S: '#f2b48c', B: '#2f62d8', O: '#ffd23f', P: '#6b4a2b', K: '#2a2a2a' };

    const walking = mode !== 'idle';
    const legs = walking ? (f % 2 ? legsA : legsB) : legsStand;
    const art = [...head, ...body, ...legs];
    const mirror = mode === 'left';
    const bob = walking && f % 2 ? 1 : 0;
    g.save();
    g.translate(0, bob);
    fromArt(g, art, pal, mirror);
    g.restore();

    // yeux
    const eye = '#1a1a1a';
    const blink = mode === 'idle' && f === 1;
    const ey = 5 + bob;
    if (!blink) {
        if (mode === 'idle') { px(g, 6, ey, eye); px(g, 9, ey, eye); if (f === 2) { px(g, 7, ey, eye); px(g, 10, ey, eye); px(g, 6, ey, '#f2b48c'); px(g, 9, ey, '#f2b48c'); } }
        else {
            const ex = mirror ? [6, 8] : [7, 9];
            px(g, ex[0], ey, eye); px(g, ex[1], ey, eye);
        }
    } else {
        px(g, 6, ey, '#c98d6a'); px(g, 9, ey, '#c98d6a');
    }
    px(g, 7, 7 + bob, '#c06a50'); px(g, 8, 7 + bob, '#c06a50');
}

function drawExit(g, f) {
    drawSteel(g);
    if (f % 2 === 0) return;
    const glow = f === 1 ? '#ffe14d' : '#ffffff';
    g.fillStyle = glow; g.fillRect(2, 2, 12, 12);
    g.fillStyle = '#000'; g.fillRect(4, 4, 8, 10);
    g.fillStyle = f === 1 ? '#4dd2ff' : '#ffe14d'; g.fillRect(5, 5, 6, 8);
    g.fillStyle = '#000'; g.fillRect(6, 6, 4, 7);
}

function drawFirefly(g, f) {
    const cols = ['#ff7a1a', '#ffd23f', '#ff3b3b', '#fff3b0'];
    const ring = (inset, color) => {
        g.fillStyle = color;
        g.fillRect(inset, inset, T - inset * 2, 2);
        g.fillRect(inset, T - inset - 2, T - inset * 2, 2);
        g.fillRect(inset, inset, 2, T - inset * 2);
        g.fillRect(T - inset - 2, inset, 2, T - inset * 2);
    };
    ring(1, cols[f % 4]);
    ring(5, cols[(f + 1) % 4]);
    g.fillStyle = cols[(f + 2) % 4];
    g.fillRect(7, 7, 2, 2);
}

function drawButterfly(g, f) {
    const span = [6, 4, 2, 4][f];
    for (let y = 2; y < 14; y++) {
        const upper = y < 8;
        const t = upper ? (y - 2) / 6 : (14 - y) / 6;
        const w = Math.max(1, Math.round(span * (0.5 + 0.5 * Math.sin(t * Math.PI * 0.95))));
        for (let i = 1; i <= w; i++) {
            const spot = (i === Math.ceil(w / 2) && (y === 5 || y === 10));
            const col = spot ? '#ffe14d' : upper ? '#d43cff' : '#4d7bff';
            px(g, 8 - 1 - i + 1 - 1, y, col);
            px(g, 8 + i - 1 + 1, y, col);
        }
    }
    g.fillStyle = '#1a1a1a'; g.fillRect(7, 3, 2, 10);
    g.fillStyle = '#fff'; px(g, 7, 3, '#ffffff'); px(g, 8, 3, '#ffffff');
    px(g, 6, 1, '#d43cff'); px(g, 9, 1, '#d43cff');
}

function drawAmoeba(g, f) {
    g.fillStyle = '#1f6b1f'; g.fillRect(0, 0, T, T);
    for (let y = 0; y < T; y++)
        for (let x = 0; x < T; x++) {
            const v = Math.sin((x + f * 1.3) * 0.9) + Math.cos((y - f) * 0.8) + hash(x, y, f) * 0.8;
            if (v > 1.2) px(g, x, y, '#5fd35f');
            else if (v > 0.5) px(g, x, y, '#35a035');
            else if (v < -1.1) px(g, x, y, '#124212');
        }
    for (let i = 0; i < 3; i++) {
        const bx = Math.floor(hash(i, f, 5) * 13) + 1, by = Math.floor(hash(f, i, 9) * 13) + 1;
        px(g, bx, by, '#c8ffc8'); px(g, bx + 1, by, '#8de68d');
    }
}

function drawExplosion(g, stage, diamond) {
    const sets = diamond
        ? [['#ffffff', '#bff4ff', '#7fe7ff'], ['#e8fdff', '#7fe7ff', '#2fb5e8'], ['#7fe7ff', '#2fb5e8', '#1a6f9e'], ['#bff4ff', '#2fb5e8', '#0f3b52']]
        : [['#ffffff', '#fff3b0', '#ffd23f'], ['#fff3b0', '#ffd23f', '#ff7a1a'], ['#ffd23f', '#ff7a1a', '#c0281e'], ['#ff7a1a', '#7a1a12', '#2a0a06']];
    const [c1, c2, c3] = sets[stage];
    const radius = [4, 6.5, 8, 7.5][stage];
    for (let y = 0; y < T; y++)
        for (let x = 0; x < T; x++) {
            const dx = x + 0.5 - 8, dy = y + 0.5 - 8;
            const d = Math.sqrt(dx * dx + dy * dy) + (hash(x, y, stage + 20) - 0.5) * 3;
            if (d > radius) continue;
            const k = d / radius;
            px(g, x, y, k < 0.35 ? c1 : k < 0.7 ? c2 : c3);
        }
    if (stage === 3) {
        for (let i = 0; i < 10; i++) {
            const x = Math.floor(hash(i, 3, 42) * 16), y = Math.floor(hash(3, i, 43) * 16);
            px(g, x, y, '#000');
        }
    }
}

// ---------------------------------------------------------------- entrées

const input = { stack: [], pressed: 0, latched: 0, snap: false, snapKeys: new Set() };
const DIR_KEYS = {
    ArrowUp: 1, KeyW: 1, ArrowRight: 2, KeyD: 2, ArrowDown: 3, KeyS: 3, ArrowLeft: 4, KeyA: 4,
    Numpad8: 1, Numpad6: 2, Numpad2: 3, Numpad4: 4,
};
const GAME_KEYS = new Set([...Object.keys(DIR_KEYS), 'Space', 'Enter', 'NumpadEnter', 'Escape', 'KeyP']);

function currentDir() { return input.stack.length ? input.stack[input.stack.length - 1] : 0; }

function pressDir(d) {
    input.stack = input.stack.filter(x => x !== d);
    input.stack.push(d);
    input.pressed = d;
}
function releaseDir(d) { input.stack = input.stack.filter(x => x !== d); }
function updateSnap() { input.snap = input.snapKeys.size > 0; }

function isTyping(e) {
    const t = e.target;
    return t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA' || t.isContentEditable);
}

function onKeyDown(e) {
    sound.unlock();
    if (isTyping(e)) return;
    if (GAME_KEYS.has(e.code) && !(e.code.startsWith('Key') && (e.ctrlKey || e.metaKey) && e.code !== 'KeyP'))
        e.preventDefault();
    if (e.ctrlKey && DIR_KEYS[e.code] && e.code.startsWith('Arrow')) input.snapKeys.add('Ctrl');
    if (e.repeat) return;
    const d = DIR_KEYS[e.code];
    if (d) { pressDir(d); return; }
    switch (e.code) {
        case 'Space': input.snapKeys.add('Space'); updateSnap(); break;
        case 'ControlLeft': case 'ControlRight': input.snapKeys.add('Ctrl'); updateSnap(); break;
        case 'Enter': case 'NumpadEnter': input.latched |= 2; break;
        case 'Escape': case 'KeyP': input.latched |= 4; break;
        case 'KeyR': input.latched |= 8; break;
        case 'KeyM': sound.enabled = !sound.enabled; break;
        case 'KeyN': dotnet?.invokeMethodAsync('SetMusicState', toggleMusic()); break;
    }
    updateSnap();
}

function onKeyUp(e) {
    if (isTyping(e)) return;
    const d = DIR_KEYS[e.code];
    if (d) releaseDir(d);
    if (e.code === 'Space') input.snapKeys.delete('Space');
    if (e.code === 'ControlLeft' || e.code === 'ControlRight') input.snapKeys.delete('Ctrl');
    updateSnap();
}

function onBlur() { input.stack = []; input.snapKeys.clear(); updateSnap(); }

function onPointerDown(e) {
    const b = e.target.closest('.touchpad button');
    if (!b) return;
    e.preventDefault();
    sound.unlock();
    b.setPointerCapture?.(e.pointerId);
    b.classList.add('on');
    if (b.dataset.dir) pressDir(+b.dataset.dir);
    else if (b.dataset.key === 'snap') { input.snapKeys.add('Touch'); updateSnap(); }
    else if (b.dataset.key === 'pause') input.latched |= 4;
}

function onPointerUp(e) {
    const b = e.target.closest('.touchpad button');
    if (!b) return;
    b.classList.remove('on');
    if (b.dataset.dir) releaseDir(+b.dataset.dir);
    else if (b.dataset.key === 'snap') { input.snapKeys.delete('Touch'); updateSnap(); }
}

function onCanvasClick() { sound.unlock(); }

function onAnyClick(e) {
    const b = e.target.closest?.('button');
    if (b && !b.closest('.touchpad')) b.blur();
}

function attachInput() {
    window.addEventListener('keydown', onKeyDown);
    window.addEventListener('keyup', onKeyUp);
    window.addEventListener('blur', onBlur);
    document.addEventListener('pointerdown', onPointerDown);
    document.addEventListener('pointerup', onPointerUp);
    document.addEventListener('pointercancel', onPointerUp);
    canvas.addEventListener('click', onCanvasClick);
    document.addEventListener('click', onAnyClick);
}

function detachInput() {
    window.removeEventListener('keydown', onKeyDown);
    window.removeEventListener('keyup', onKeyUp);
    window.removeEventListener('blur', onBlur);
    document.removeEventListener('pointerdown', onPointerDown);
    document.removeEventListener('pointerup', onPointerUp);
    document.removeEventListener('pointercancel', onPointerUp);
    canvas?.removeEventListener('click', onCanvasClick);
    document.removeEventListener('click', onAnyClick);
}

// ---------------------------------------------------------------- sons (synthèse WebAudio)

const sound = {
    ctx: null,
    enabled: true,
    unlock() {
        if (this.ctx) { if (this.ctx.state === 'suspended') this.ctx.resume(); return; }
        try { this.ctx = new (window.AudioContext || window.webkitAudioContext)(); } catch { this.ctx = null; }
    },
};

function tone(freq, dur, { type = 'square', vol = 0.08, slide = 0, delay = 0 } = {}) {
    const a = sound.ctx;
    if (!a || !sound.enabled) return;
    const t0 = a.currentTime + delay;
    const o = a.createOscillator(), gn = a.createGain();
    o.type = type;
    o.frequency.setValueAtTime(freq, t0);
    if (slide) o.frequency.exponentialRampToValueAtTime(Math.max(30, freq + slide), t0 + dur);
    gn.gain.setValueAtTime(vol, t0);
    gn.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
    o.connect(gn).connect(a.destination);
    o.start(t0); o.stop(t0 + dur + 0.02);
}

let noiseBuf = null;
function noise(dur, { vol = 0.15, freq = 800, q = 0.7, delay = 0 } = {}) {
    const a = sound.ctx;
    if (!a || !sound.enabled) return;
    if (!noiseBuf) {
        noiseBuf = a.createBuffer(1, a.sampleRate, a.sampleRate);
        const d = noiseBuf.getChannelData(0);
        for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1;
    }
    const t0 = a.currentTime + delay;
    const src = a.createBufferSource(), f = a.createBiquadFilter(), gn = a.createGain();
    src.buffer = noiseBuf;
    f.type = 'lowpass'; f.frequency.setValueAtTime(freq, t0); f.Q.value = q;
    gn.gain.setValueAtTime(vol, t0);
    gn.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
    src.connect(f).connect(gn).connect(a.destination);
    src.start(t0, Math.random() * 0.5); src.stop(t0 + dur + 0.02);
}

const sfx = {
    dig: () => noise(0.05, { vol: 0.05, freq: 2500 }),
    step: () => tone(90, 0.03, { vol: 0.02 }),
    collect: () => { tone(988, 0.06, { vol: 0.06 }); tone(1480, 0.08, { vol: 0.06, delay: 0.05 }); },
    boulder: () => noise(0.12, { vol: 0.22, freq: 380 }),
    diamondLand: () => tone(2100, 0.07, { type: 'triangle', vol: 0.06 }),
    explosion: () => { noise(0.7, { vol: 0.35, freq: 900 }); tone(110, 0.5, { type: 'sawtooth', vol: 0.06, slide: -70 }); },
    exitOpen: () => [523, 659, 784, 1047].forEach((f, i) => tone(f, 0.12, { vol: 0.06, delay: i * 0.07 })),
    push: () => noise(0.08, { vol: 0.08, freq: 250 }),
    magic: () => tone(700, 0.15, { type: 'sine', vol: 0.07, slide: 600 }),
    extraLife: () => [784, 988, 1175, 1568].forEach((f, i) => tone(f, 0.1, { type: 'triangle', vol: 0.07, delay: i * 0.06 })),
    exited: () => [392, 523, 659, 784, 1047].forEach((f, i) => tone(f, 0.14, { vol: 0.06, delay: i * 0.08 })),
    tick: () => tone(1800, 0.03, { type: 'square', vol: 0.03 }),
};

function playEvents(e) {
    if (e & EV.Explosion) sfx.explosion();
    if (e & EV.ExitOpened) sfx.exitOpen();
    if (e & EV.Exited) sfx.exited();
    if (e & EV.ExtraLife) sfx.extraLife();
    if (e & EV.Collect) sfx.collect();
    if (e & EV.BoulderLand) sfx.boulder();
    if (e & EV.DiamondLand) sfx.diamondLand();
    if (e & EV.Magic) sfx.magic();
    if (e & EV.Push) sfx.push();
    if (e & EV.Dig) sfx.dig();
    else if (e & EV.Step) sfx.step();
}

// ---------------------------------------------------------------- musique 8 bits (composition originale)
// Boucle de 4 mesures (La m - Fa - Do - Sol), 140 BPM, en doubles croches :
// basse triangle, arpège carré, mélodie carrée, batterie au bruit blanc.

const music = {
    enabled: (() => { try { return localStorage.getItem('bdnet.music') === '1'; } catch { return false; } })(),
    playing: false, timer: 0, nextTime: 0, step: 0, bus: null,
};

const STEP = 60 / 140 / 4;               // durée d'une double croche
const CHORDS = [                          // [basse, arpège]
    [45, [57, 60, 64, 69]],               // La mineur
    [41, [53, 57, 60, 65]],               // Fa majeur
    [48, [60, 64, 67, 72]],               // Do majeur
    [43, [55, 59, 62, 67]],               // Sol majeur
];
const LEAD = [                            // [pas, note MIDI, durée en pas]
    [0, 76, 2], [2, 81, 2], [4, 84, 2], [6, 83, 1], [7, 81, 1], [9, 79, 2], [11, 76, 4],
    [16, 77, 2], [18, 81, 2], [20, 84, 2], [22, 86, 2], [24, 84, 2], [26, 81, 2], [28, 77, 4],
    [32, 79, 2], [34, 84, 2], [36, 88, 2], [38, 86, 1], [39, 84, 1], [41, 79, 2], [43, 76, 4],
    [48, 74, 2], [50, 79, 2], [52, 83, 2], [54, 86, 2], [56, 83, 1], [57, 81, 1], [58, 79, 2], [60, 83, 4],
];
const LEAD_AT = new Map(LEAD.map(n => [n[0], n]));
const midi = n => 440 * Math.pow(2, (n - 69) / 12);

function updateMusic(onTitle) {
    const want = music.enabled && onTitle && sound.ctx && sound.ctx.state === 'running';
    if (want && !music.playing) startMusic();
    else if (!want && music.playing) stopMusic();
}

function startMusic() {
    const a = sound.ctx;
    music.bus = a.createGain();
    music.bus.gain.setValueAtTime(0.0001, a.currentTime);
    music.bus.gain.exponentialRampToValueAtTime(0.5, a.currentTime + 0.4);
    music.bus.connect(a.destination);
    music.playing = true;
    music.step = 0;
    music.nextTime = a.currentTime + 0.05;
    music.timer = setInterval(scheduleMusic, 25);
    scheduleMusic();
}

function stopMusic(immediate = false) {
    clearInterval(music.timer);
    music.playing = false;
    const bus = music.bus;
    music.bus = null;
    if (!bus || !sound.ctx) return;
    const t = sound.ctx.currentTime;
    bus.gain.cancelScheduledValues(t);
    bus.gain.setValueAtTime(bus.gain.value, t);
    bus.gain.exponentialRampToValueAtTime(0.0001, t + (immediate ? 0.02 : 0.3));
    setTimeout(() => bus.disconnect(), 500);
}

function scheduleMusic() {
    const a = sound.ctx;
    if (!a || !music.bus) return;
    while (music.nextTime < a.currentTime + 0.15) {
        playStep(music.step, music.nextTime);
        music.nextTime += STEP;
        music.step = (music.step + 1) % 64;
    }
}

function voice(type, freq, t, dur, vol) {
    const a = sound.ctx;
    const o = a.createOscillator(), g = a.createGain();
    o.type = type;
    o.frequency.setValueAtTime(freq, t);
    g.gain.setValueAtTime(vol, t);
    g.gain.setValueAtTime(vol, t + dur * 0.7);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    o.connect(g).connect(music.bus);
    o.start(t);
    o.stop(t + dur + 0.02);
}

function drum(t, kind) {
    const a = sound.ctx;
    if (kind === 'kick') {
        const o = a.createOscillator(), g = a.createGain();
        o.type = 'sine';
        o.frequency.setValueAtTime(140, t);
        o.frequency.exponentialRampToValueAtTime(40, t + 0.12);
        g.gain.setValueAtTime(0.35, t);
        g.gain.exponentialRampToValueAtTime(0.0001, t + 0.15);
        o.connect(g).connect(music.bus);
        o.start(t); o.stop(t + 0.17);
        return;
    }
    if (!noiseBuf) {
        noiseBuf = a.createBuffer(1, a.sampleRate, a.sampleRate);
        const d = noiseBuf.getChannelData(0);
        for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1;
    }
    const src = a.createBufferSource(), f = a.createBiquadFilter(), g = a.createGain();
    src.buffer = noiseBuf;
    f.type = kind === 'hat' ? 'highpass' : 'bandpass';
    f.frequency.value = kind === 'hat' ? 7000 : 1800;
    const dur = kind === 'hat' ? 0.03 : 0.1;
    g.gain.setValueAtTime(kind === 'hat' ? 0.05 : 0.14, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    src.connect(f).connect(g).connect(music.bus);
    src.start(t, Math.random() * 0.5); src.stop(t + dur + 0.02);
}

function playStep(step, t) {
    const [bass, arp] = CHORDS[Math.floor(step / 16)];
    const s = step % 16;
    // basse : fondamentale / octave en croches
    if (s % 2 === 0) voice('triangle', midi(s % 4 === 0 ? bass : bass + 12), t, STEP * 1.8, 0.22);
    // arpège léger
    voice('square', midi(arp[s % 4]), t, STEP * 0.9, 0.025);
    // mélodie
    const n = LEAD_AT.get(step);
    if (n) voice('square', midi(n[1]), t, STEP * n[2] * 0.95, 0.05);
    // batterie
    if (s === 0 || s === 8 || s === 10) drum(t, 'kick');
    if (s === 4 || s === 12) drum(t, 'snare');
    if (s % 2 === 1) drum(t, 'hat');
}
