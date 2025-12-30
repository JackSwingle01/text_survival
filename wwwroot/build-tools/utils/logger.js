/**
 * Colored console output for validation results
 */
class Logger {
    constructor() {
        this.colors = {
            reset: '\x1b[0m',
            red: '\x1b[31m',
            green: '\x1b[32m',
            yellow: '\x1b[33m',
            blue: '\x1b[34m',
            gray: '\x1b[90m'
        };
    }

    header(text) {
        console.log(`\n${this.colors.blue}=== ${text} ===${this.colors.reset}\n`);
    }

    section(text) {
        console.log(`${this.colors.blue}${text}${this.colors.reset}`);
    }

    success(text) {
        console.log(`${this.colors.green}✓ ${text}${this.colors.reset}`);
    }

    error(text) {
        console.log(`${this.colors.red}✗ ${text}${this.colors.reset}`);
    }

    warn(text) {
        console.log(`${this.colors.yellow}⚠ ${text}${this.colors.reset}`);
    }

    info(text) {
        console.log(`${this.colors.gray}  ${text}${this.colors.reset}`);
    }

    summary(results) {
        console.log('\n' + this.colors.blue + '=== Summary ===' + this.colors.reset);
        results.forEach(r => {
            const icon = r.passed ? '✓' : '✗';
            const color = r.passed ? this.colors.green : this.colors.red;
            const duration = r.durationMs ? ` (${r.durationMs}ms)` : '';
            console.log(`${color}${icon} ${r.name}${duration}${this.colors.reset}`);
            if (!r.passed && r.errorCount) {
                console.log(`${this.colors.red}  ${r.errorCount} error(s)${this.colors.reset}`);
            }
        });
    }
}

module.exports = { Logger };
