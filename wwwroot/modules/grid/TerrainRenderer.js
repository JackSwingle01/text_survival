/**
 * TerrainRenderer - Shared terrain texture rendering
 * Used by both CanvasGridRenderer (world map) and CombatOverlay (combat grid)
 */

export const TERRAIN_COLORS = {
    Forest: '#2a4038',      // Dark green - evergreen cover
    Clearing: '#d8d8d8',    // Neutral gray - tundra snow
    Plain: '#d8d8d8',       // Neutral gray - tundra snow
    Hills: '#8090a0',       // Blue-gray - snow-dusted hills
    Water: '#90b0c8',       // Light ice blue - frozen
    Marsh: '#607068',       // Muted green-gray - frozen wetland
    Rock: '#606068',        // Medium gray - exposed stone
    Mountain: '#404048',    // Dark gray - high peaks
    DeepWater: '#6090b0',   // Deeper blue - thick ice
    unexplored: '#080a0c'   // Nearly black
};

/**
 * Seeded random for consistent terrain patterns per tile
 */
export function seededRandom(worldX, worldY, seed) {
    const h = (worldX * 73856093) ^ (worldY * 19349663) ^ (seed * 83492791);
    return Math.abs(Math.sin(h)) % 1.0;
}

/**
 * Main terrain texture dispatcher
 * @param {CanvasRenderingContext2D} ctx - Canvas context
 * @param {string} terrain - Terrain type name
 * @param {number} px - X position on canvas
 * @param {number} py - Y position on canvas
 * @param {number} size - Tile size in pixels
 * @param {number} worldX - World X coordinate (for seeded random)
 * @param {number} worldY - World Y coordinate (for seeded random)
 */
export function renderTerrainTexture(ctx, terrain, px, py, size, worldX, worldY) {
    switch (terrain) {
        case 'Forest':
            renderForestTexture(ctx, px, py, size, worldX, worldY);
            break;
        case 'Water':
        case 'DeepWater':
            renderWaterTexture(ctx, px, py, size, worldX, worldY, terrain === 'DeepWater');
            break;
        case 'Plain':
            renderPlainTexture(ctx, px, py, size, worldX, worldY);
            break;
        case 'Clearing':
            renderClearingTexture(ctx, px, py, size, worldX, worldY);
            break;
        case 'Hills':
            renderHillsTexture(ctx, px, py, size, worldX, worldY);
            break;
        case 'Rock':
        case 'Mountain':
            renderRockTexture(ctx, px, py, size, worldX, worldY, terrain === 'Mountain');
            break;
        case 'Marsh':
            renderMarshTexture(ctx, px, py, size, worldX, worldY);
            break;
    }
}

/**
 * Forest - recognizable evergreen trees
 */
export function renderForestTexture(ctx, px, py, size, worldX, worldY) {
    ctx.fillStyle = 'rgba(15, 35, 25, 0.7)';

    // Generate 5-7 trees at seeded random positions
    const treeCount = 5 + Math.floor(seededRandom(worldX, worldY, 100) * 3);
    for (let i = 0; i < treeCount; i++) {
        const tx = px + size * (0.1 + seededRandom(worldX, worldY, i * 10) * 0.8);
        const ty = py + size * (0.15 + seededRandom(worldX, worldY, i * 10 + 1) * 0.7);
        const treeHeight = size * (0.1 + seededRandom(worldX, worldY, i * 10 + 2) * 0.067);
        const treeWidth = treeHeight * 0.6;

        // Draw layered triangle tree (evergreen shape)
        ctx.beginPath();
        ctx.moveTo(tx, ty - treeHeight);
        ctx.lineTo(tx - treeWidth / 2, ty);
        ctx.lineTo(tx + treeWidth / 2, ty);
        ctx.closePath();
        ctx.fill();

        // Second layer (slightly smaller, overlapping)
        ctx.beginPath();
        ctx.moveTo(tx, ty - treeHeight * 0.85);
        ctx.lineTo(tx - treeWidth * 0.35, ty - treeHeight * 0.2);
        ctx.lineTo(tx + treeWidth * 0.35, ty - treeHeight * 0.2);
        ctx.closePath();
        ctx.fill();
    }

    // Snow on some trees (white highlights)
    ctx.fillStyle = 'rgba(255, 255, 255, 0.15)';
    for (let i = 0; i < 3; i++) {
        const sx = px + size * (0.2 + seededRandom(worldX, worldY, i + 50) * 0.6);
        const sy = py + size * (0.2 + seededRandom(worldX, worldY, i + 51) * 0.4);
        ctx.fillRect(sx - size * 0.025, sy, size * 0.05, size * 0.017);
    }
}

/**
 * Water/Ice - frozen surface with concentric pressure rings
 */
export function renderWaterTexture(ctx, px, py, size, worldX, worldY, isDeep) {
    const centerX = px + size * (0.4 + seededRandom(worldX, worldY, 1) * 0.2);
    const centerY = py + size * (0.4 + seededRandom(worldX, worldY, 2) * 0.2);

    // Concentric pressure rings (arcs, not full circles)
    const ringColor = isDeep ? 'rgba(40, 70, 100, 0.4)' : 'rgba(60, 100, 130, 0.35)';
    ctx.strokeStyle = ringColor;
    ctx.lineWidth = Math.max(1, size * 0.017);

    for (let i = 0; i < 3; i++) {
        const radius = size * (0.15 + i * 0.12);
        const startAngle = seededRandom(worldX, worldY, i + 10) * Math.PI * 0.5;
        const arcLength = Math.PI * (0.6 + seededRandom(worldX, worldY, i + 11) * 0.8);

        ctx.beginPath();
        ctx.arc(centerX, centerY, radius, startAngle, startAngle + arcLength);
        ctx.stroke();
    }

    // Central snow patch (distinctive frozen lake feature)
    ctx.fillStyle = 'rgba(255, 255, 255, 0.25)';
    ctx.beginPath();
    const snowRadius = size * 0.12;
    // Irregular blob shape
    for (let i = 0; i < 8; i++) {
        const angle = (i / 8) * Math.PI * 2;
        const r = snowRadius * (0.7 + seededRandom(worldX, worldY, i + 20) * 0.5);
        const x = centerX + Math.cos(angle) * r;
        const y = centerY + Math.sin(angle) * r;
        if (i === 0) ctx.moveTo(x, y);
        else ctx.lineTo(x, y);
    }
    ctx.closePath();
    ctx.fill();

    // Radiating cracks from pressure points
    ctx.strokeStyle = isDeep ? 'rgba(30, 55, 80, 0.45)' : 'rgba(50, 80, 110, 0.35)';
    ctx.lineWidth = Math.max(1, size * 0.008);
    const crackCount = 2 + Math.floor(seededRandom(worldX, worldY, 30) * 2);
    for (let i = 0; i < crackCount; i++) {
        const angle = seededRandom(worldX, worldY, i + 31) * Math.PI * 2;
        const len = size * (0.25 + seededRandom(worldX, worldY, i + 32) * 0.2);
        ctx.beginPath();
        ctx.moveTo(centerX, centerY);
        ctx.lineTo(
            centerX + Math.cos(angle) * len,
            centerY + Math.sin(angle) * len
        );
        ctx.stroke();
    }

    // Subtle ice shimmer patches
    ctx.fillStyle = 'rgba(200, 220, 240, 0.12)';
    for (let i = 0; i < 3; i++) {
        const shimmerX = px + size * (0.1 + seededRandom(worldX, worldY, i + 40) * 0.8);
        const shimmerY = py + size * (0.1 + seededRandom(worldX, worldY, i + 41) * 0.8);
        ctx.beginPath();
        ctx.ellipse(shimmerX, shimmerY, size * 0.083, size * 0.05, seededRandom(worldX, worldY, i + 42) * Math.PI, 0, Math.PI * 2);
        ctx.fill();
    }

    // Additional jagged ice cracks
    ctx.strokeStyle = isDeep ? 'rgba(40, 70, 100, 0.35)' : 'rgba(60, 100, 130, 0.3)';
    ctx.lineWidth = Math.max(1, size * 0.008);
    const startX = px + size * (0.1 + seededRandom(worldX, worldY, 50) * 0.3);
    const startY = py + size * (0.6 + seededRandom(worldX, worldY, 51) * 0.3);
    ctx.beginPath();
    ctx.moveTo(startX, startY);
    let cx = startX, cy = startY;
    for (let i = 0; i < 3; i++) {
        cx += size * (0.12 + seededRandom(worldX, worldY, i + 52) * 0.1);
        cy -= size * (0.05 + seededRandom(worldX, worldY, i + 53) * 0.08);
        ctx.lineTo(cx, cy);
    }
    ctx.stroke();
}

/**
 * Plain - Arctic tundra with snow, dirt, lichen, tussock grass, and low shrubs
 */
export function renderPlainTexture(ctx, px, py, size, worldX, worldY) {
    // Lichen patches - clusters of small squares
    const lichenCount = 1 + Math.floor(seededRandom(worldX, worldY, 50) * 2);
    for (let i = 0; i < lichenCount; i++) {
        const cx = px + size * (0.15 + seededRandom(worldX, worldY, i + 51) * 0.7);
        const cy = py + size * (0.15 + seededRandom(worldX, worldY, i + 52) * 0.7);

        // Cluster of small spots
        const spotCount = 4 + Math.floor(seededRandom(worldX, worldY, i + 53) * 4);
        for (let j = 0; j < spotCount; j++) {
            const spotX = cx + (seededRandom(worldX, worldY, i * 10 + j + 54) - 0.5) * size * 0.1;
            const spotY = cy + (seededRandom(worldX, worldY, i * 10 + j + 55) - 0.5) * size * 0.083;
            const spotSize = size * (0.0125 + seededRandom(worldX, worldY, i * 10 + j + 56) * 0.021);

            const greenVar = 90 + seededRandom(worldX, worldY, i * 10 + j + 57) * 20;
            ctx.fillStyle = `rgba(${greenVar}, 100, 70, 0.35)`;
            ctx.fillRect(spotX, spotY, spotSize, spotSize);
        }
    }

    // Snow drift curves
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.3)';
    ctx.lineWidth = Math.max(1, size * 0.017);
    for (let i = 0; i < 2; i++) {
        const startY = py + size * (0.3 + i * 0.4 + seededRandom(worldX, worldY, i) * 0.1);
        ctx.beginPath();
        ctx.moveTo(px + size * 0.04, startY);
        ctx.bezierCurveTo(
            px + size * 0.3, startY + (seededRandom(worldX, worldY, i + 10) - 0.5) * size * 0.1,
            px + size * 0.7, startY + (seededRandom(worldX, worldY, i + 11) - 0.5) * size * 0.1,
            px + size * 0.96, startY + (seededRandom(worldX, worldY, i + 12) - 0.5) * size * 0.067
        );
        ctx.stroke();
    }

    // Scattered snow sparkles
    ctx.fillStyle = 'rgba(255, 255, 255, 0.3)';
    for (let i = 0; i < 6; i++) {
        const sx = px + size * (0.1 + seededRandom(worldX, worldY, i + 20) * 0.8);
        const sy = py + size * (0.1 + seededRandom(worldX, worldY, i + 21) * 0.8);
        ctx.fillRect(sx, sy, size * 0.017, size * 0.017);
    }

    // Tussock grass - golden-brown clumps
    ctx.strokeStyle = 'rgba(140, 120, 80, 0.5)';
    ctx.lineWidth = Math.max(1, size * 0.0125);
    const tussockCount = 5 + Math.floor(seededRandom(worldX, worldY, 30) * 4);
    for (let i = 0; i < tussockCount; i++) {
        const gx = px + size * (0.15 + seededRandom(worldX, worldY, i + 31) * 0.7);
        const gy = py + size * (0.35 + seededRandom(worldX, worldY, i + 32) * 0.45);

        // Tuft of grass blades
        for (let j = 0; j < 5; j++) {
            const angle = -Math.PI / 2 + (j - 2) * 0.2 + (seededRandom(worldX, worldY, i + j + 33) - 0.5) * 0.25;
            const len = size * (0.067 + seededRandom(worldX, worldY, i + j + 36) * 0.067);
            ctx.beginPath();
            ctx.moveTo(gx, gy);
            ctx.lineTo(gx + Math.cos(angle) * len, gy + Math.sin(angle) * len);
            ctx.stroke();
        }
    }

    // Low shrubs - branching twigs
    ctx.strokeStyle = 'rgba(60, 50, 40, 0.45)';
    ctx.lineWidth = Math.max(1, size * 0.0125);
    const shrubCount = 1 + Math.floor(seededRandom(worldX, worldY, 60) * 2);
    for (let i = 0; i < shrubCount; i++) {
        const sx = px + size * (0.2 + seededRandom(worldX, worldY, i + 61) * 0.6);
        const sy = py + size * (0.45 + seededRandom(worldX, worldY, i + 62) * 0.35);

        // Main stem
        ctx.beginPath();
        ctx.moveTo(sx, sy);
        ctx.lineTo(sx, sy - size * 0.083);
        ctx.stroke();

        // Branches
        for (let j = 0; j < 4; j++) {
            const branchY = sy - size * (0.025 + j * 0.017);
            const branchLen = size * (0.05 + seededRandom(worldX, worldY, i + j + 63) * 0.033);
            const dir = (j % 2 === 0) ? 1 : -1;
            ctx.beginPath();
            ctx.moveTo(sx, branchY);
            ctx.lineTo(sx + dir * branchLen, branchY - size * 0.017);
            ctx.stroke();
        }
    }
}

/**
 * Clearing - sheltered forest gap with dirt, vegetation, and occasional trees
 */
export function renderClearingTexture(ctx, px, py, size, worldX, worldY) {
    // Dirt patches - exposed earth showing through snow
    ctx.fillStyle = 'rgba(80, 65, 50, 0.35)';
    const dirtCount = 3 + Math.floor(seededRandom(worldX, worldY, 75) * 3);
    for (let i = 0; i < dirtCount; i++) {
        const dx = px + size * (0.15 + seededRandom(worldX, worldY, i + 76) * 0.7);
        const dy = py + size * (0.2 + seededRandom(worldX, worldY, i + 77) * 0.6);
        const dw = size * (0.05 + seededRandom(worldX, worldY, i + 78) * 0.083);
        const dh = size * (0.033 + seededRandom(worldX, worldY, i + 79) * 0.05);

        ctx.beginPath();
        ctx.ellipse(dx, dy, dw, dh, seededRandom(worldX, worldY, i + 80) * Math.PI, 0, Math.PI * 2);
        ctx.fill();
    }

    // Softer snow drifts - less wind-blown, more settled
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.18)';
    ctx.lineWidth = Math.max(1, size * 0.0125);
    for (let i = 0; i < 2; i++) {
        const baseY = py + size * (0.4 + i * 0.3);
        ctx.beginPath();
        ctx.moveTo(px + size * 0.1, baseY);
        ctx.quadraticCurveTo(
            px + size * 0.5, baseY + size * (0.033 + seededRandom(worldX, worldY, i + 81) * 0.033),
            px + size * 0.9, baseY + (seededRandom(worldX, worldY, i + 82) - 0.5) * size * 0.025
        );
        ctx.stroke();
    }

    // Grass tufts - V-shapes poking through snow
    ctx.strokeStyle = 'rgba(70, 90, 60, 0.4)';
    ctx.lineWidth = Math.max(1, size * 0.008);
    for (let i = 0; i < 4; i++) {
        const gx = px + size * (0.2 + seededRandom(worldX, worldY, i + 90) * 0.6);
        const gy = py + size * (0.3 + seededRandom(worldX, worldY, i + 91) * 0.5);
        const spread = size * (0.017 + seededRandom(worldX, worldY, i + 92) * 0.017);
        const height = size * (0.033 + seededRandom(worldX, worldY, i + 93) * 0.025);
        ctx.beginPath();
        ctx.moveTo(gx - spread, gy);
        ctx.lineTo(gx, gy - height);
        ctx.lineTo(gx + spread, gy);
        ctx.stroke();
    }

    // Tree stumps - 1-2 small circles with ring detail
    const stumpCount = 1 + Math.floor(seededRandom(worldX, worldY, 100) * 1.5);
    for (let i = 0; i < stumpCount; i++) {
        const sx = px + size * (0.25 + seededRandom(worldX, worldY, i + 101) * 0.5);
        const sy = py + size * (0.55 + seededRandom(worldX, worldY, i + 102) * 0.3);
        const radius = size * (0.025 + seededRandom(worldX, worldY, i + 103) * 0.017);

        ctx.fillStyle = 'rgba(60, 50, 40, 0.5)';
        ctx.beginPath();
        ctx.arc(sx, sy, radius, 0, Math.PI * 2);
        ctx.fill();

        ctx.strokeStyle = 'rgba(80, 70, 55, 0.4)';
        ctx.lineWidth = Math.max(0.5, size * 0.004);
        ctx.beginPath();
        ctx.arc(sx, sy, radius * 0.5, 0, Math.PI * 2);
        ctx.stroke();
    }

    // Fallen branches - thin dark lines at angles
    ctx.strokeStyle = 'rgba(50, 45, 35, 0.35)';
    ctx.lineWidth = Math.max(1, size * 0.008);
    for (let i = 0; i < 2; i++) {
        const bx = px + size * (0.15 + seededRandom(worldX, worldY, i + 110) * 0.7);
        const by = py + size * (0.4 + seededRandom(worldX, worldY, i + 111) * 0.4);
        const angle = seededRandom(worldX, worldY, i + 112) * Math.PI * 0.5 - Math.PI * 0.25;
        const len = size * (0.067 + seededRandom(worldX, worldY, i + 113) * 0.05);
        ctx.beginPath();
        ctx.moveTo(bx, by);
        ctx.lineTo(bx + Math.cos(angle) * len, by + Math.sin(angle) * len);
        ctx.stroke();
    }

    // Occasional tree - simple triangle like forest (50% chance)
    if (seededRandom(worldX, worldY, 130) > 0.5) {
        const tx = px + size * (0.2 + seededRandom(worldX, worldY, 131) * 0.6);
        const ty = py + size * (0.4 + seededRandom(worldX, worldY, 132) * 0.35);
        const treeHeight = size * (0.1 + seededRandom(worldX, worldY, 133) * 0.067);
        const treeWidth = treeHeight * 0.6;

        // Simple triangle tree (same as forest)
        ctx.fillStyle = 'rgba(15, 35, 25, 0.7)';
        ctx.beginPath();
        ctx.moveTo(tx, ty - treeHeight);
        ctx.lineTo(tx - treeWidth / 2, ty);
        ctx.lineTo(tx + treeWidth / 2, ty);
        ctx.closePath();
        ctx.fill();

        // Second layer
        ctx.beginPath();
        ctx.moveTo(tx, ty - treeHeight * 0.85);
        ctx.lineTo(tx - treeWidth * 0.35, ty - treeHeight * 0.2);
        ctx.lineTo(tx + treeWidth * 0.35, ty - treeHeight * 0.2);
        ctx.closePath();
        ctx.fill();

        // Snow highlight
        ctx.fillStyle = 'rgba(255, 255, 255, 0.15)';
        ctx.fillRect(tx - size * 0.025, ty - treeHeight * 0.6, size * 0.05, size * 0.017);
    }

    // Edge tree hints - subtle partial triangles suggesting surrounding forest
    ctx.fillStyle = 'rgba(30, 50, 40, 0.25)';
    const edgeCount = 2 + Math.floor(seededRandom(worldX, worldY, 120) * 2);
    for (let i = 0; i < edgeCount; i++) {
        const edge = Math.floor(seededRandom(worldX, worldY, i + 121) * 4);
        const offset = seededRandom(worldX, worldY, i + 122) * 0.6 + 0.2;
        const treeSize = size * (0.05 + seededRandom(worldX, worldY, i + 123) * 0.033);

        ctx.beginPath();
        if (edge === 0) {
            const etx = px + size * offset;
            ctx.moveTo(etx - treeSize * 0.5, py);
            ctx.lineTo(etx, py + treeSize * 0.7);
            ctx.lineTo(etx + treeSize * 0.5, py);
        } else if (edge === 1) {
            const ety = py + size * offset;
            ctx.moveTo(px + size, ety - treeSize * 0.5);
            ctx.lineTo(px + size - treeSize * 0.7, ety);
            ctx.lineTo(px + size, ety + treeSize * 0.5);
        } else if (edge === 2) {
            const etx = px + size * offset;
            ctx.moveTo(etx - treeSize * 0.5, py + size);
            ctx.lineTo(etx, py + size - treeSize * 0.7);
            ctx.lineTo(etx + treeSize * 0.5, py + size);
        } else {
            const ety = py + size * offset;
            ctx.moveTo(px, ety - treeSize * 0.5);
            ctx.lineTo(px + treeSize * 0.7, ety);
            ctx.lineTo(px, ety + treeSize * 0.5);
        }
        ctx.fill();
    }
}

/**
 * Hills - stacked rounded mounds with snow caps
 */
export function renderHillsTexture(ctx, px, py, size, worldX, worldY) {
    // Three stacked hill mounds (back to front for depth)
    const hills = [
        { cx: 0.7, baseY: 0.55, width: 0.5, height: 0.4, color: 'rgba(55, 65, 80, 0.45)' },
        { cx: 0.25, baseY: 0.65, width: 0.45, height: 0.35, color: 'rgba(65, 75, 90, 0.4)' },
        { cx: 0.55, baseY: 0.85, width: 0.6, height: 0.45, color: 'rgba(75, 85, 100, 0.35)' }
    ];

    for (let i = 0; i < hills.length; i++) {
        const hill = hills[i];
        const cx = px + size * hill.cx;
        const baseY = py + size * hill.baseY;
        const halfWidth = size * hill.width / 2;
        const peakHeight = size * hill.height;

        // Hill shadow (underneath)
        ctx.fillStyle = 'rgba(40, 50, 60, 0.25)';
        ctx.beginPath();
        ctx.moveTo(cx - halfWidth, baseY + size * 0.033);
        ctx.quadraticCurveTo(cx, baseY - peakHeight + size * 0.067, cx + halfWidth, baseY + size * 0.033);
        ctx.lineTo(cx - halfWidth, baseY + size * 0.033);
        ctx.fill();

        // Hill body (arc/mound shape)
        ctx.fillStyle = hill.color;
        ctx.beginPath();
        ctx.moveTo(cx - halfWidth, baseY);
        ctx.quadraticCurveTo(cx, baseY - peakHeight, cx + halfWidth, baseY);
        ctx.lineTo(cx - halfWidth, baseY);
        ctx.fill();

        // Snow cap on top
        ctx.fillStyle = 'rgba(255, 255, 255, 0.3)';
        ctx.beginPath();
        const capWidth = halfWidth * 0.6;
        const capTop = baseY - peakHeight;
        ctx.moveTo(cx - capWidth, capTop + peakHeight * 0.25);
        ctx.quadraticCurveTo(cx, capTop - size * 0.017, cx + capWidth, capTop + peakHeight * 0.25);
        ctx.lineTo(cx - capWidth, capTop + peakHeight * 0.25);
        ctx.fill();
    }

    // Exposed rock patches on slopes
    ctx.fillStyle = 'rgba(70, 80, 90, 0.4)';
    for (let i = 0; i < 2; i++) {
        const rx = px + size * (0.2 + seededRandom(worldX, worldY, i + 20) * 0.6);
        const ry = py + size * (0.35 + seededRandom(worldX, worldY, i + 21) * 0.35);
        ctx.beginPath();
        ctx.ellipse(rx, ry, size * 0.067, size * 0.042, seededRandom(worldX, worldY, i + 22) * Math.PI, 0, Math.PI * 2);
        ctx.fill();
    }

    // Sparse grass on lower slopes
    ctx.strokeStyle = 'rgba(90, 100, 80, 0.35)';
    ctx.lineWidth = Math.max(1, size * 0.008);
    for (let i = 0; i < 5; i++) {
        const gx = px + size * (0.1 + seededRandom(worldX, worldY, i + 30) * 0.8);
        const gy = py + size * (0.65 + seededRandom(worldX, worldY, i + 31) * 0.25);
        ctx.beginPath();
        ctx.moveTo(gx - size * 0.017, gy);
        ctx.lineTo(gx, gy - size * 0.042);
        ctx.lineTo(gx + size * 0.017, gy);
        ctx.stroke();
    }

    // Subtle contour hints
    ctx.strokeStyle = 'rgba(60, 80, 100, 0.15)';
    ctx.lineWidth = Math.max(1, size * 0.008);
    for (let i = 0; i < 2; i++) {
        const baseY = py + size * (0.35 + i * 0.25);
        ctx.beginPath();
        ctx.moveTo(px + size * 0.083, baseY);
        ctx.bezierCurveTo(
            px + size * 0.35, baseY - size * (0.042 + seededRandom(worldX, worldY, i + 40) * 0.033),
            px + size * 0.65, baseY - size * (0.025 + seededRandom(worldX, worldY, i + 41) * 0.042),
            px + size * 0.917, baseY + seededRandom(worldX, worldY, i + 42) * size * 0.025
        );
        ctx.stroke();
    }
}

/**
 * Rock/Mountain - stacked boulders or mountain peak silhouette
 */
export function renderRockTexture(ctx, px, py, size, worldX, worldY, isMountain) {
    if (isMountain) {
        // MOUNTAIN: Large triangular peak silhouette

        // Secondary peak (behind, left)
        ctx.fillStyle = 'rgba(45, 50, 55, 0.5)';
        ctx.beginPath();
        ctx.moveTo(px + size * 0.05, py + size);
        ctx.lineTo(px + size * 0.25, py + size * 0.25);
        ctx.lineTo(px + size * 0.5, py + size);
        ctx.closePath();
        ctx.fill();

        // Main peak (dominant center-right)
        ctx.fillStyle = 'rgba(35, 40, 45, 0.65)';
        ctx.beginPath();
        ctx.moveTo(px + size * 0.2, py + size);
        ctx.lineTo(px + size * 0.55, py + size * 0.08);
        ctx.lineTo(px + size * 0.95, py + size);
        ctx.closePath();
        ctx.fill();

        // Snow cap on main peak
        ctx.fillStyle = 'rgba(255, 255, 255, 0.4)';
        ctx.beginPath();
        ctx.moveTo(px + size * 0.4, py + size * 0.35);
        ctx.lineTo(px + size * 0.55, py + size * 0.08);
        ctx.lineTo(px + size * 0.72, py + size * 0.35);
        ctx.closePath();
        ctx.fill();

        // Ridge line detail
        ctx.strokeStyle = 'rgba(25, 30, 35, 0.4)';
        ctx.lineWidth = Math.max(1, size * 0.008);
        ctx.beginPath();
        ctx.moveTo(px + size * 0.55, py + size * 0.08);
        ctx.lineTo(px + size * 0.75, py + size * 0.5);
        ctx.stroke();

    } else {
        // ROCK: Stacked angular boulders
        const boulders = [
            { x: 0.25, y: 0.45, w: 0.35, h: 0.25 },
            { x: 0.6, y: 0.35, w: 0.3, h: 0.22 },
            { x: 0.45, y: 0.7, w: 0.4, h: 0.28 },
            { x: 0.15, y: 0.75, w: 0.25, h: 0.2 }
        ];

        for (let i = 0; i < boulders.length; i++) {
            const b = boulders[i];
            const bx = px + size * b.x;
            const by = py + size * b.y;
            const bw = size * b.w;
            const bh = size * b.h;

            // Boulder shadow
            ctx.fillStyle = 'rgba(30, 35, 40, 0.35)';
            ctx.beginPath();
            ctx.moveTo(bx, by + bh * 0.3);
            ctx.lineTo(bx + bw * 0.15, by + bh);
            ctx.lineTo(bx + bw * 0.9, by + bh);
            ctx.lineTo(bx + bw, by + bh * 0.4);
            ctx.closePath();
            ctx.fill();

            // Boulder side (darker)
            ctx.fillStyle = 'rgba(55, 60, 65, 0.5)';
            ctx.beginPath();
            ctx.moveTo(bx + bw * 0.5, by);
            ctx.lineTo(bx + bw, by + bh * 0.4);
            ctx.lineTo(bx + bw * 0.9, by + bh);
            ctx.lineTo(bx + bw * 0.4, by + bh * 0.7);
            ctx.closePath();
            ctx.fill();

            // Boulder top (with snow)
            ctx.fillStyle = 'rgba(85, 90, 95, 0.5)';
            ctx.beginPath();
            ctx.moveTo(bx, by + bh * 0.3);
            ctx.lineTo(bx + bw * 0.5, by);
            ctx.lineTo(bx + bw, by + bh * 0.4);
            ctx.lineTo(bx + bw * 0.4, by + bh * 0.7);
            ctx.closePath();
            ctx.fill();

            // Snow on top
            ctx.fillStyle = 'rgba(255, 255, 255, 0.22)';
            ctx.beginPath();
            ctx.moveTo(bx + bw * 0.1, by + bh * 0.25);
            ctx.lineTo(bx + bw * 0.5, by + bh * 0.05);
            ctx.lineTo(bx + bw * 0.85, by + bh * 0.35);
            ctx.lineTo(bx + bw * 0.4, by + bh * 0.5);
            ctx.closePath();
            ctx.fill();
        }

        // Cracks between boulders
        ctx.strokeStyle = 'rgba(25, 30, 35, 0.4)';
        ctx.lineWidth = Math.max(1, size * 0.008);
        ctx.beginPath();
        ctx.moveTo(px + size * 0.35, py + size * 0.5);
        ctx.lineTo(px + size * 0.5, py + size * 0.65);
        ctx.stroke();
        ctx.beginPath();
        ctx.moveTo(px + size * 0.55, py + size * 0.55);
        ctx.lineTo(px + size * 0.65, py + size * 0.72);
        ctx.stroke();

        // Additional crack lines for detail
        for (let i = 0; i < 3; i++) {
            ctx.beginPath();
            ctx.moveTo(
                px + size * seededRandom(worldX, worldY, i + 60),
                py + size * seededRandom(worldX, worldY, i + 61)
            );
            ctx.lineTo(
                px + size * seededRandom(worldX, worldY, i + 62),
                py + size * seededRandom(worldX, worldY, i + 63)
            );
            ctx.stroke();
        }

        // Gravel scatter around boulders
        ctx.fillStyle = 'rgba(70, 75, 80, 0.35)';
        for (let i = 0; i < 5; i++) {
            const gx = px + size * (0.1 + seededRandom(worldX, worldY, i + 70) * 0.8);
            const gy = py + size * (0.1 + seededRandom(worldX, worldY, i + 71) * 0.8);
            const gs = size * (0.017 + seededRandom(worldX, worldY, i + 72) * 0.017);
            ctx.beginPath();
            ctx.arc(gx, gy, gs, 0, Math.PI * 2);
            ctx.fill();
        }
    }
}

/**
 * Marsh - cattail clusters in frozen wetland
 */
export function renderMarshTexture(ctx, px, py, size, worldX, worldY) {
    // Murky ice patches
    ctx.fillStyle = 'rgba(40, 55, 50, 0.3)';
    for (let i = 0; i < 2; i++) {
        const patchX = px + size * (0.2 + seededRandom(worldX, worldY, i) * 0.6);
        const patchY = py + size * (0.3 + seededRandom(worldX, worldY, i + 1) * 0.4);
        ctx.beginPath();
        ctx.ellipse(patchX, patchY, size * 0.125, size * 0.083, 0, 0, Math.PI * 2);
        ctx.fill();
    }

    // Cattail clusters - smaller, more subtle (3-4 clusters)
    const clusterCount = 3 + Math.floor(seededRandom(worldX, worldY, 10) * 2);
    const clusterPositions = [
        { x: 0.2, y: 0.6 },
        { x: 0.55, y: 0.5 },
        { x: 0.8, y: 0.65 },
        { x: 0.35, y: 0.75 },
        { x: 0.7, y: 0.4 }
    ];

    for (let c = 0; c < clusterCount; c++) {
        const cluster = clusterPositions[c];
        const baseX = px + size * cluster.x;
        const baseY = py + size * cluster.y;

        // Each cluster has 2-3 stalks (smaller)
        const stalkCount = 2 + Math.floor(seededRandom(worldX, worldY, c + 20) * 2);
        for (let i = 0; i < stalkCount; i++) {
            const stalkX = baseX + (seededRandom(worldX, worldY, c * 10 + i + 30) - 0.5) * size * 0.05;
            const stalkHeight = size * (0.12 + seededRandom(worldX, worldY, c * 10 + i + 31) * 0.08);
            const lean = (seededRandom(worldX, worldY, c * 10 + i + 32) - 0.5) * size * 0.025;

            // Stalk (thinner)
            ctx.strokeStyle = 'rgba(80, 90, 70, 0.6)';
            ctx.lineWidth = Math.max(1, size * 0.0125);
            ctx.beginPath();
            ctx.moveTo(stalkX, baseY);
            ctx.lineTo(stalkX + lean, baseY - stalkHeight);
            ctx.stroke();

            // Cattail head (smaller sausage)
            ctx.fillStyle = 'rgba(60, 50, 40, 0.7)';
            ctx.beginPath();
            const headX = stalkX + lean;
            const headY = baseY - stalkHeight - size * 0.017;
            ctx.ellipse(headX, headY, size * 0.017, size * 0.033, 0, 0, Math.PI * 2);
            ctx.fill();
        }
    }

    // Dead reeds/grass scattered around
    ctx.strokeStyle = 'rgba(80, 90, 70, 0.5)';
    ctx.lineWidth = Math.max(1, size * 0.008);
    const reedCount = 4 + Math.floor(seededRandom(worldX, worldY, 50) * 3);
    for (let i = 0; i < reedCount; i++) {
        const rx = px + size * (0.1 + seededRandom(worldX, worldY, i + 51) * 0.8);
        const ry = py + size * (0.4 + seededRandom(worldX, worldY, i + 52) * 0.5);
        const height = size * (0.067 + seededRandom(worldX, worldY, i + 53) * 0.067);
        const lean = (seededRandom(worldX, worldY, i + 54) - 0.5) * size * 0.033;
        ctx.beginPath();
        ctx.moveTo(rx, ry);
        ctx.lineTo(rx + lean, ry - height);
        ctx.stroke();
    }

    // Ice crack
    ctx.strokeStyle = 'rgba(50, 70, 65, 0.3)';
    ctx.lineWidth = Math.max(1, size * 0.008);
    ctx.beginPath();
    ctx.moveTo(px + size * 0.083, py + size * 0.7);
    ctx.lineTo(px + size * 0.6, py + size * 0.8);
    ctx.stroke();
}
