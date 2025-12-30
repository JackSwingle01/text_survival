#!/usr/bin/env node

/**
 * Detects orphaned CSS variables - variables used but not declared in :root
 */

const fs = require('fs');
const path = require('path');

const cssFile = path.join(__dirname, 'wwwroot/style.css');

// Read the CSS file
const css = fs.readFileSync(cssFile, 'utf8');

// Extract all variable declarations from :root
const rootMatch = css.match(/:root\s*{([^}]*)}/s);
if (!rootMatch) {
    console.error('❌ Could not find :root section in CSS');
    process.exit(1);
}

const rootContent = rootMatch[1];
const declaredVars = new Set();

// Match all variable declarations like --var-name:
const declRegex = /--([a-z0-9-]+)\s*:/g;
let match;
while ((match = declRegex.exec(rootContent)) !== null) {
    declaredVars.add(`--${match[1]}`);
}

// Extract all variable usages like var(--var-name)
const usedVars = new Set();
const useRegex = /var\((--[a-z0-9-]+)(?:,|\))/g;
while ((match = useRegex.exec(css)) !== null) {
    usedVars.add(match[1]);
}

// Find orphaned variables (used but not declared)
const orphaned = [];
for (const varName of usedVars) {
    if (!declaredVars.has(varName)) {
        orphaned.push(varName);
    }
}

// Find unused variables (declared but never used)
const unused = [];
for (const varName of declaredVars) {
    // Check if variable is used anywhere outside :root
    const cssOutsideRoot = css.replace(/:root\s*{[^}]*}/s, '');
    if (!cssOutsideRoot.includes(`var(${varName}`)) {
        unused.push(varName);
    }
}

// Report results
console.log('🔍 CSS Variable Analysis\n');
console.log(`📊 Statistics:`);
console.log(`   Declared: ${declaredVars.size}`);
console.log(`   Used: ${usedVars.size}`);
console.log(`   Orphaned: ${orphaned.length}`);
console.log(`   Unused: ${unused.length}\n`);

if (orphaned.length > 0) {
    console.log('❌ ORPHANED VARIABLES (used but not declared in :root):');
    orphaned.sort().forEach(v => {
        // Find where it's used
        const lines = css.split('\n');
        const usageLines = [];
        lines.forEach((line, i) => {
            if (line.includes(`var(${v}`)) {
                usageLines.push(i + 1);
            }
        });
        console.log(`   ${v}`);
        console.log(`      Used on lines: ${usageLines.slice(0, 5).join(', ')}${usageLines.length > 5 ? '...' : ''}`);
    });
    console.log('');
}

if (unused.length > 0) {
    console.log('⚠️  UNUSED VARIABLES (declared but never used):');
    unused.sort().forEach(v => console.log(`   ${v}`));
    console.log('');
}

if (orphaned.length === 0 && unused.length === 0) {
    console.log('✅ All CSS variables are properly declared and used!');
}

process.exit(orphaned.length > 0 ? 1 : 0);
