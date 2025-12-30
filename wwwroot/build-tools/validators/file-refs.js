/**
 * File Reference Validator
 * Checks that referenced files exist (scripts, styles, etc.)
 */
const fs = require('fs');
const path = require('path');

function validateFileRefs(wwwroot, logger) {
    const start = Date.now();
    logger.section('Validating file references...');

    const errors = [];

    // Check index.html exists
    const indexPath = path.join(wwwroot, 'index.html');
    if (!fs.existsSync(indexPath)) {
        errors.push('index.html not found');
    } else {
        // Parse index.html for script/link tags
        const content = fs.readFileSync(indexPath, 'utf8');

        // Match: <script src="...">
        const scriptMatches = content.matchAll(/<script[^>]+src=["']([^"']+)["']/g);
        for (const match of scriptMatches) {
            const src = match[1];
            if (!src.startsWith('http')) {
                const fullPath = path.join(wwwroot, src);
                if (!fs.existsSync(fullPath)) {
                    errors.push(`Script not found: ${src}`);
                }
            }
        }

        // Match: <link href="...">
        const linkMatches = content.matchAll(/<link[^>]+href=["']([^"']+)["']/g);
        for (const match of linkMatches) {
            const href = match[1];
            if (!href.startsWith('http')) {
                const fullPath = path.join(wwwroot, href);
                if (!fs.existsSync(fullPath)) {
                    errors.push(`Stylesheet not found: ${href}`);
                }
            }
        }
    }

    // Check style.css exists
    const cssPath = path.join(wwwroot, 'style.css');
    if (!fs.existsSync(cssPath)) {
        errors.push('style.css not found');
    }

    if (errors.length > 0) {
        logger.error(`Found ${errors.length} missing file(s):`);
        errors.forEach(e => logger.info(e));
        return {
            name: 'File References',
            passed: false,
            errorCount: errors.length,
            durationMs: Date.now() - start
        };
    }

    logger.success('All file references validated');
    return { name: 'File References', passed: true, durationMs: Date.now() - start };
}

module.exports = { validateFileRefs };
