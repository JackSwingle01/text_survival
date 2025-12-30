#!/usr/bin/env node
/**
 * Frontend validation orchestrator for Text Survival
 * Runs all validators and reports results
 * Exit code 0 = pass, 1 = fail (blocks build)
 */

const path = require('path');

// Import validators
const { validateCssVars } = require('./validators/css-vars');
const { validateJsImports } = require('./validators/js-imports');
const { validateFileRefs } = require('./validators/file-refs');
const { Logger } = require('./utils/logger');

const WWWROOT = path.resolve(__dirname, '..');

async function main() {
    const logger = new Logger();
    logger.header('Frontend Validation');

    const results = [];

    // Run all validators
    results.push(validateCssVars(WWWROOT, logger));
    results.push(validateJsImports(WWWROOT, logger));
    results.push(validateFileRefs(WWWROOT, logger));

    // Summary
    const failed = results.filter(r => !r.passed);
    logger.summary(results);

    if (failed.length > 0) {
        logger.error(`\n${failed.length} validation(s) failed`);
        process.exit(1);
    } else {
        logger.success('\nAll validations passed');
        process.exit(0);
    }
}

main().catch(err => {
    console.error('Validation crashed:', err);
    process.exit(1);
});
