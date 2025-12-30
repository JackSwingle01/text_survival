/**
 * CSS Variable Validator
 * Detects orphaned CSS variables - variables used but not declared in :root
 * Adapted from check-css-vars.js
 */
const fs = require('fs');
const path = require('path');

function validateCssVars(wwwroot, logger) {
    const start = Date.now();
    logger.section('Validating CSS variables...');

    const cssFile = path.join(wwwroot, 'style.css');
    if (!fs.existsSync(cssFile)) {
        logger.error('style.css not found');
        return { name: 'CSS Variables', passed: false, durationMs: Date.now() - start };
    }

    const css = fs.readFileSync(cssFile, 'utf8');

    // Extract all variable declarations from :root
    const rootMatch = css.match(/:root\s*\{([^}]*)\}/s);
    if (!rootMatch) {
        logger.error('Could not find :root section in CSS');
        return { name: 'CSS Variables', passed: false, durationMs: Date.now() - start };
    }

    const rootContent = rootMatch[1];
    const declaredVars = new Set();

    // Match all variable declarations like --var-name:
    const declMatches = rootContent.matchAll(/--([a-z0-9-]+)\s*:/g);
    for (const match of declMatches) {
        declaredVars.add(`--${match[1]}`);
    }

    // Extract all variable usages like var(--var-name)
    const usedVars = new Set();
    const useMatches = css.matchAll(/var\((--[a-z0-9-]+)(?:,|\))/g);
    for (const match of useMatches) {
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
        const cssOutsideRoot = css.replace(/:root\s*\{[^}]*\}/s, '');
        if (!cssOutsideRoot.includes(`var(${varName}`)) {
            unused.push(varName);
        }
    }

    // Report results
    logger.info(`Declared: ${declaredVars.size}, Used: ${usedVars.size}`);

    if (orphaned.length > 0) {
        logger.error(`Found ${orphaned.length} orphaned CSS variable(s):`);
        orphaned.sort().forEach(v => {
            // Find where it's used
            const lines = css.split('\n');
            const usageLines = [];
            lines.forEach((line, i) => {
                if (line.includes(`var(${v}`)) {
                    usageLines.push(i + 1);
                }
            });
            logger.info(`${v} (lines: ${usageLines.slice(0, 5).join(', ')}${usageLines.length > 5 ? '...' : ''})`);
        });
    }

    if (unused.length > 0) {
        logger.warn(`Found ${unused.length} unused CSS variable(s):`);
        unused.sort().forEach(v => logger.info(v));
    }

    if (orphaned.length === 0 && unused.length === 0) {
        logger.success('All CSS variables are properly declared and used');
    } else if (orphaned.length === 0) {
        logger.success('All used CSS variables are defined');
    }

    // Only fail on orphaned variables (used but not declared)
    // Unused variables are just warnings
    return {
        name: 'CSS Variables',
        passed: orphaned.length === 0,
        errorCount: orphaned.length,
        durationMs: Date.now() - start
    };
}

module.exports = { validateCssVars };
