// modules/core/Animator.js

export class Animator {
    /**
     * Animate a numeric value change
     */
    static tweenValue(from, to, duration, onUpdate, onComplete = null) {
        const startTime = Date.now();
        
        const animate = () => {
            const elapsed = Date.now() - startTime;
            const progress = Math.min(1, elapsed / duration);
            const eased = 1 - Math.pow(1 - progress, 3); // ease-out cubic
            
            const current = from + (to - from) * eased;
            onUpdate(current, progress);
            
            if (progress < 1) {
                requestAnimationFrame(animate);
            } else if (onComplete) {
                onComplete();
            }
        };
        
        requestAnimationFrame(animate);
    }

    /**
     * Animate a progress bar from 0 to 100%
     */
    static progressBar(barElement, durationMs, onComplete = null) {
        barElement.style.width = '0%';
        
        Animator.tweenValue(0, 100, durationMs,
            (value) => {
                barElement.style.width = `${Math.round(value)}%`;
            },
            onComplete
        );
    }

    /**
     * Animate a distance value (for hunt/encounter)
     */
    static distance(element, from, to, duration = 800) {
        Animator.tweenValue(from, to, duration,
            (value) => {
                element.textContent = `${Math.round(value)}m`;
            }
        );
    }

    /**
     * Animate a time value
     */
    static time(element, from, to, duration = 800) {
        Animator.tweenValue(from, to, duration,
            (value) => {
                element.textContent = `${Math.round(value)} min`;
            }
        );
    }

    /**
     * Animate a distance bar mask (hunt/encounter style)
     */
    static distanceMask(maskElement, from, to, maxDistance = 100, duration = 800) {
        const fromPct = Math.max(0, Math.min(100, from / maxDistance * 100));
        const toPct = Math.max(0, Math.min(100, to / maxDistance * 100));

        // Reset transition and set initial
        maskElement.style.transition = 'none';
        maskElement.style.width = `${fromPct}%`;
        
        // Force reflow
        maskElement.offsetHeight;
        
        // Trigger animation
        requestAnimationFrame(() => {
            requestAnimationFrame(() => {
                maskElement.style.transition = `width ${duration}ms ease-out`;
                maskElement.style.width = `${toPct}%`;
            });
        });
    }
}