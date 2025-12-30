/**
 * JavaScript Import/Export Validator
 * Verifies that all imports resolve to actual exports
 */
const fs = require('fs');
const path = require('path');

function validateJsImports(wwwroot, logger) {
    const start = Date.now();
    logger.section('Validating JavaScript imports...');

    // Discover all JS files
    const jsFiles = discoverJsFiles(wwwroot);
    logger.info(`Found ${jsFiles.length} JavaScript files`);

    // Build export map (file -> exported names)
    const exportMap = buildExportMap(jsFiles);

    // Validate all imports
    const errors = [];

    for (const filePath of jsFiles) {
        const imports = parseImports(filePath);

        for (const imp of imports) {
            const resolvedPath = resolveImportPath(filePath, imp.source);

            // Check file exists
            if (!fs.existsSync(resolvedPath)) {
                errors.push({
                    file: path.relative(wwwroot, filePath),
                    error: `Import not found: ${imp.source}`,
                    line: imp.line
                });
                continue;
            }

            // Check exports match
            const exports = exportMap.get(resolvedPath) || new Set();

            for (const name of imp.names) {
                if (!exports.has(name)) {
                    errors.push({
                        file: path.relative(wwwroot, filePath),
                        error: `'${name}' not exported from ${imp.source}`,
                        line: imp.line
                    });
                }
            }
        }
    }

    if (errors.length > 0) {
        logger.error(`Found ${errors.length} import error(s):`);
        errors.forEach(e => {
            logger.info(`${e.file}:${e.line || '?'} - ${e.error}`);
        });
        return {
            name: 'JavaScript Imports',
            passed: false,
            errorCount: errors.length,
            durationMs: Date.now() - start
        };
    }

    logger.success(`All imports validated across ${jsFiles.length} files`);
    return { name: 'JavaScript Imports', passed: true, durationMs: Date.now() - start };
}

function discoverJsFiles(wwwroot) {
    const files = [];

    function scan(dir) {
        const entries = fs.readdirSync(dir, { withFileTypes: true });
        for (const entry of entries) {
            const fullPath = path.join(dir, entry.name);
            if (entry.isDirectory()) {
                // Skip node_modules, build-tools
                if (!['node_modules', 'build-tools'].includes(entry.name)) {
                    scan(fullPath);
                }
            } else if (entry.name.endsWith('.js')) {
                files.push(fullPath);
            }
        }
    }

    scan(wwwroot);
    return files;
}

function parseImports(filePath) {
    const content = fs.readFileSync(filePath, 'utf8');
    const imports = [];
    const lines = content.split('\n');

    for (let i = 0; i < lines.length; i++) {
        const line = lines[i];
        // Match: import { A, B } from './path.js'
        const matches = line.matchAll(/import\s+\{([^}]+)\}\s+from\s+['"]([^'"]+)['"]/g);

        for (const match of matches) {
            // Handle aliased imports: "show as showEl" -> extract "show"
            const names = match[1].split(',').map(n => {
                const trimmed = n.trim();
                // If using "as" syntax, extract the original name (before "as")
                const asIndex = trimmed.indexOf(' as ');
                return asIndex !== -1 ? trimmed.substring(0, asIndex).trim() : trimmed;
            });
            const source = match[2];

            imports.push({
                names,
                source,
                line: i + 1
            });
        }
    }

    return imports;
}

function parseExports(filePath) {
    const content = fs.readFileSync(filePath, 'utf8');
    const exports = new Set();

    // Match: export class Name, export const Name, export function name
    const classMatches = content.matchAll(/export\s+class\s+(\w+)/g);
    const constMatches = content.matchAll(/export\s+const\s+(\w+)/g);
    const funcMatches = content.matchAll(/export\s+function\s+(\w+)/g);

    for (const match of classMatches) exports.add(match[1]);
    for (const match of constMatches) exports.add(match[1]);
    for (const match of funcMatches) exports.add(match[1]);

    return exports;
}

function buildExportMap(jsFiles) {
    const map = new Map();

    for (const file of jsFiles) {
        const exports = parseExports(file);
        map.set(file, exports);
    }

    return map;
}

function resolveImportPath(fromFile, importSource) {
    // Handle relative paths
    const fromDir = path.dirname(fromFile);
    let resolvedPath = path.resolve(fromDir, importSource);

    // Add .js if missing
    if (!resolvedPath.endsWith('.js')) {
        resolvedPath += '.js';
    }

    return resolvedPath;
}

module.exports = { validateJsImports };
