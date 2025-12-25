class GameClient {
    constructor() {
        this.socket = null;
        this.reconnectAttempts = 0;
        this.maxReconnectAttempts = 5;
        this.connect();
    }

    connect() {
        const protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
        this.socket = new WebSocket(`${protocol}//${location.host}/ws`);

        this.socket.onopen = () => {
            this.reconnectAttempts = 0;
            this.hideConnectionOverlay();
        };

        this.socket.onmessage = (event) => {
            const frame = JSON.parse(event.data);
            this.handleFrame(frame);
        };

        this.socket.onclose = () => {
            this.showConnectionOverlay('Connection lost. Reconnecting...');
            this.attemptReconnect();
        };

        this.socket.onerror = () => {
            this.showConnectionOverlay('Connection error', true);
        };
    }

    attemptReconnect() {
        if (this.reconnectAttempts < this.maxReconnectAttempts) {
            this.reconnectAttempts++;
            setTimeout(() => this.connect(), 2000);
        } else {
            this.showConnectionOverlay('Failed to connect. Refresh to try again.', true);
        }
    }

    showConnectionOverlay(message, isError = false) {
        const overlay = document.getElementById('connectionOverlay');
        const msgEl = document.getElementById('connectionMessage');
        overlay.classList.remove('hidden');
        msgEl.textContent = message;
        msgEl.classList.toggle('error', isError);
    }

    hideConnectionOverlay() {
        document.getElementById('connectionOverlay').classList.add('hidden');
    }

    handleFrame(frame) {
        if (frame.state) {
            this.renderState(frame.state);
        }

        // Handle inventory overlay
        if (frame.inventory) {
            this.showInventory(frame.inventory, frame.input);
            // Input is rendered inside the overlay, not in the action area
        } else {
            this.hideInventory();
            this.renderInput(frame.input, frame.statusText, frame.progress);
        }
    }

    renderState(state) {
        // Debug: log raw capacity values
        if (state.debugCapacities || state.debugEffectModifiers) {
            console.log('=== VITALITY DEBUG ===');
            console.log('Final Vitality:', state.vitality);
            console.log('Effect Modifiers:', state.debugEffectModifiers);
            console.log('Final Capacities:', state.debugCapacities);
            console.log('======================');
        }

        // CSS variables
        document.documentElement.style.setProperty('--warmth', state.warmth);
        document.documentElement.style.setProperty('--vitality', state.vitality);

        // Deep Ocean time-based background interpolation
        this.updateDeepOceanBackground(state.clockTime);

        // Time panel
        document.getElementById('dayNumber').textContent = `Day ${state.dayNumber}`;
        document.getElementById('timeDetail').textContent = `${state.clockTime} — ${state.timeOfDay}`;
        const transitionType = state.isDaytime ? 'dusk' : 'dawn';
        document.getElementById('timeWarning').textContent =
            `${state.hoursUntilTransition.toFixed(1)} hrs til ${transitionType}`;

        // Weather
        const weatherEl = document.getElementById('weatherCond');
        weatherEl.textContent = state.weatherCondition;
        weatherEl.className = 'weather-cond ' + state.weatherCondition.toLowerCase().replace(' ', '-');
        document.getElementById('windLabel').textContent = state.wind;
        document.getElementById('precipLabel').textContent = state.precipitation;

        // Location
        document.getElementById('locationName').textContent = state.locationName;
        this.renderLocationTags(state.locationTags);
        this.renderFeatures(state.features);

        // Fire
        this.renderFire(state.fire);

        // Temperature
        this.renderTemperature(state);

        // Survival stats
        this.renderSurvival(state);

        // Effects & Injuries
        this.renderEffects(state.effects);
        this.renderInjuries(state.injuries, state.bloodPercent);

        // Inventory summary
        document.getElementById('carryDisplay').textContent =
            `${state.carryWeightKg.toFixed(1)} / ${state.maxWeightKg.toFixed(0)} kg`;
        const carryPct = Math.min(100, (state.carryWeightKg / state.maxWeightKg) * 100);
        this.renderSegmentBar('carrySegmentBar', carryPct);
        document.getElementById('insulationPct').textContent = `${state.insulationPercent}%`;
        document.getElementById('fuelReserve').textContent =
            `${state.fuelKg.toFixed(1)}kg (${state.fuelBurnTime})`;

        // Gear summary (from inventory)
        if (state.gearSummary) {
            this.renderGearSummary(state.gearSummary);
        }

        // Narrative log
        this.renderLog(state.log);
    }

    renderGearSummary(summary) {
        // Weapon display
        const weaponEl = document.getElementById('gearWeapon');
        if (summary.weaponName) {
            weaponEl.style.display = '';
            document.getElementById('weaponName').textContent = summary.weaponName;
            document.getElementById('weaponStat').textContent =
                `${summary.weaponDamage?.toFixed(0) || 0} dmg`;
        } else {
            weaponEl.style.display = 'none';
        }

        // Tool pills
        const pillsContainer = document.getElementById('toolPills');
        this.clearElement(pillsContainer);

        if (summary.cuttingToolCount > 0) {
            this.addToolPill(pillsContainer, 'cutting',
                summary.cuttingToolCount > 1
                    ? `${summary.cuttingToolCount} blades`
                    : 'Blade');
        }
        if (summary.fireStarterCount > 0) {
            this.addToolPill(pillsContainer, 'fire',
                summary.fireStarterCount > 1
                    ? `${summary.fireStarterCount} fire tools`
                    : 'Fire tool');
        }
        if (summary.otherToolCount > 0) {
            this.addToolPill(pillsContainer, 'other',
                summary.otherToolCount > 1
                    ? `${summary.otherToolCount} tools`
                    : 'Tool');
        }

        // Food/Water combined summary
        const foodWaterSummary = document.getElementById('foodWaterSummary');
        if (summary.foodPortions === 0 && summary.waterPortions === 0) {
            foodWaterSummary.textContent = 'None';
            foodWaterSummary.className = 'gear-value food-empty';
        } else {
            let parts = [];
            if (summary.foodPortions > 0) {
                let foodText = `${summary.foodPortions} food`;
                if (summary.hasPreservedFood) foodText += ' +dried';
                parts.push(foodText);
            }
            if (summary.waterPortions > 0) {
                parts.push(`${summary.waterPortions} water`);
            }
            foodWaterSummary.textContent = parts.join(' • ');
            foodWaterSummary.className = 'gear-value';
        }

        // Medicinals summary
        const medicinalEl = document.getElementById('gearMedicinals');
        if (summary.medicinalCount > 0) {
            medicinalEl.style.display = '';
            document.getElementById('medicinalsSummary').textContent =
                `${summary.medicinalCount} item${summary.medicinalCount > 1 ? 's' : ''}`;
        } else {
            medicinalEl.style.display = 'none';
        }

        // Materials summary
        const materialsEl = document.getElementById('gearMaterials');
        if (summary.craftingMaterialCount > 0) {
            materialsEl.style.display = '';
            document.getElementById('materialsCount').textContent = summary.craftingMaterialCount.toString();
            const rareIndicator = document.getElementById('rareIndicator');
            rareIndicator.style.display = summary.hasRareMaterials ? '' : 'none';
        } else {
            materialsEl.style.display = 'none';
        }
    }

    addToolPill(container, type, text) {
        const pill = document.createElement('span');
        pill.className = `tool-pill ${type}`;
        pill.textContent = text;
        container.appendChild(pill);
    }

    updateDeepOceanBackground(clockTime) {
        // Parse clock time (format: "h:mm tt" e.g. "9:00 AM")
        const minutesSinceMidnight = this.parseClockTime(clockTime);

        // Calculate time factor t (0 = midnight, 1 = noon)
        let t;
        if (minutesSinceMidnight <= 720) {
            // 12:00 AM → 12:00 PM (ascending toward noon)
            t = minutesSinceMidnight / 720;
        } else {
            // 12:00 PM → 12:00 AM (descending from noon)
            t = (1440 - minutesSinceMidnight) / 720;
        }

        // Deep Ocean anchor values
        const midnight = { h: 215, s: 30, l: 5 };
        const noon = { h: 212, s: 25, l: 26 };

        // Linear interpolation
        const h = midnight.h + (noon.h - midnight.h) * t;
        const s = midnight.s + (noon.s - midnight.s) * t;
        const l = midnight.l + (noon.l - midnight.l) * t;

        // Update CSS custom properties
        document.documentElement.style.setProperty('--bg-h', h.toFixed(1));
        document.documentElement.style.setProperty('--bg-s', s.toFixed(1) + '%');
        document.documentElement.style.setProperty('--bg-l', l.toFixed(1) + '%');
    }

    parseClockTime(clockTime) {
        // Parse "h:mm tt" format (e.g., "9:00 AM", "12:30 PM")
        const match = clockTime.match(/(\d+):(\d+)\s*(AM|PM)/i);
        if (!match) return 0;

        let hours = parseInt(match[1]);
        const minutes = parseInt(match[2]);
        const meridiem = match[3].toUpperCase();

        // Convert to 24-hour format
        if (meridiem === 'AM') {
            if (hours === 12) hours = 0; // 12 AM = 00:00
        } else {
            if (hours !== 12) hours += 12; // PM times except 12 PM
        }

        return hours * 60 + minutes;
    }

    renderLocationTags(tags) {
        const container = document.getElementById('locationDesc');
        this.clearElement(container);

        if (!tags || tags.length === 0) return;

        tags.forEach(tag => {
            const pill = document.createElement('span');
            pill.className = 'location-tag';
            pill.textContent = tag;
            container.appendChild(pill);
        });
    }

    renderFeatures(features) {
        const container = document.getElementById('locationFeatures');
        this.clearElement(container);

        if (!features || features.length === 0) return;

        features.forEach(f => {
            const span = document.createElement('span');
            span.className = 'feature-tag';
            span.textContent = f.label;
            if (f.detail) {
                span.textContent += ': ';
                const valueSpan = document.createElement('span');
                valueSpan.className = `feature-value ${f.type}`;
                valueSpan.textContent = f.detail;
                span.appendChild(valueSpan);
            }
            container.appendChild(span);
        });
    }

    renderFire(fire) {
        const phaseEl = document.getElementById('firePhase');
        const phaseText = phaseEl.querySelector('.fire-phase-text');
        const timeEl = document.getElementById('fireTime');
        const fuelEl = document.getElementById('fireFuel');
        const heatEl = document.getElementById('fireHeat');

        if (!fire) {
            phaseText.textContent = 'No fire pit';
            phaseEl.className = 'fire-phase cold';
            timeEl.textContent = '';
            fuelEl.textContent = '';
            heatEl.textContent = '';
            return;
        }

        if (fire.phase === 'Cold') {
            phaseText.textContent = 'Cold';
            phaseEl.className = 'fire-phase cold';
            timeEl.textContent = '';
            // Show fuel if any is loaded
            this.clearElement(fuelEl);
            const icon = document.createElement('span');
            icon.className = 'material-symbols-outlined';
            icon.textContent = 'local_fire_department';
            fuelEl.appendChild(icon);

            if (fire.totalKg > 0) {
                const litPercent = fire.totalKg > 0 ? Math.round(fire.burningKg / fire.totalKg * 100) : 0;
                const text = document.createTextNode(`${fire.totalKg.toFixed(1)}kg fuel (${litPercent}% lit)`);
                fuelEl.appendChild(text);
            } else {
                const text = document.createTextNode('No fuel');
                fuelEl.appendChild(text);
            }
            heatEl.textContent = '';
            return;
        }

        // Active fire
        phaseText.textContent = fire.phase;
        phaseEl.className = 'fire-phase ' + fire.phase.toLowerCase();

        // Time remaining with burn rate
        timeEl.textContent = `${fire.minutesRemaining} min (${fire.burnRateKgPerHour.toFixed(1)} kg/hr)`;

        // Fuel breakdown: burning vs unlit, or total/max
        this.clearElement(fuelEl);
        const fuelIcon = document.createElement('span');
        fuelIcon.className = 'material-symbols-outlined';
        fuelIcon.textContent = 'local_fire_department';
        fuelEl.appendChild(fuelIcon);

        if (fire.unlitKg > 0.1) {
            const burningSpan = document.createElement('span');
            burningSpan.className = 'fire-burning';
            burningSpan.textContent = `${fire.burningKg.toFixed(1)}kg burning`;
            const unlitSpan = document.createElement('span');
            unlitSpan.className = 'fire-unlit';
            unlitSpan.textContent = ` (+${fire.unlitKg.toFixed(1)}kg unlit)`;
            fuelEl.appendChild(burningSpan);
            fuelEl.appendChild(unlitSpan);
        } else {
            const fuelText = document.createTextNode(`${fire.totalKg.toFixed(1)}/${fire.maxCapacityKg.toFixed(0)} kg fuel`);
            fuelEl.appendChild(fuelText);
        }

        // Heat output
        if (fire.heatOutput > 0) {
            heatEl.textContent = `+${fire.heatOutput.toFixed(0)}°F heat`;
        } else {
            heatEl.textContent = '';
        }
    }

    renderTemperature(state) {
        const bodyTemp = state.bodyTemp;
        const feelsLike = state.airTemp + (state.fireHeat || 0);

        // Temperature badge (feels like temp - prominent display)
        const tempBadge = document.getElementById('tempBadge');
        const tempBadgeValue = document.getElementById('tempBadgeValue');
        tempBadgeValue.textContent = `${feelsLike.toFixed(0)}°F`;

        // Set badge color class based on feels like temp
        tempBadge.className = 'temp-badge';
        if (feelsLike < 20) tempBadge.classList.add('danger');
        else if (feelsLike < 40) tempBadge.classList.add('cold');
        else if (feelsLike < 60) tempBadge.classList.add('cool');
        else if (feelsLike < 80) tempBadge.classList.add('normal');
        else tempBadge.classList.add('hot');

        // Temperature segmented bar (87-102 range)
        const tempPct = Math.max(0, Math.min(100, (bodyTemp - 87) / (102 - 87) * 100));
        let tempState = 'normal';
        if (bodyTemp < 95) tempState = 'cold';
        else if (bodyTemp < 97) tempState = 'cool';
        else if (bodyTemp > 100) tempState = 'hot';
        this.renderSegmentBar('tempSegmentBar', tempPct, tempState);

        document.getElementById('bodyTempDisplay').textContent = `${bodyTemp.toFixed(1)}°F`;

        const statusEl = document.getElementById('tempStatus');
        statusEl.textContent = state.tempStatus;
        statusEl.className = 'temp-status ' + state.tempStatus.toLowerCase();

        // Air breakdown
        document.getElementById('airTempDisplay').textContent = `${state.airTemp.toFixed(0)}°F`;

        const fireContrib = document.getElementById('fireContrib');
        if (state.fireHeat > 0) {
            fireContrib.textContent = ` + Fire +${state.fireHeat.toFixed(0)}°F`;
        } else {
            fireContrib.textContent = '';
        }

        // Trend
        const trendEl = document.getElementById('tempTrend');
        const rate = state.trendPerHour;
        if (Math.abs(rate) < 0.05) {
            trendEl.textContent = '→ Stable';
            trendEl.className = 'temp-trend stable';
        } else if (rate < 0) {
            trendEl.textContent = `↓ Cooling (${rate.toFixed(1)}°/hr)`;
            trendEl.className = 'temp-trend cooling';
        } else {
            trendEl.textContent = `↑ Warming (+${rate.toFixed(1)}°/hr)`;
            trendEl.className = 'temp-trend warming';
        }
    }

    renderSegmentBar(containerId, percent, state = 'normal') {
        const container = document.getElementById(containerId);
        this.clearElement(container);

        // All bars now use the simple fill style
        const fill = document.createElement('div');
        fill.className = 'bar-fill';
        fill.style.width = percent + '%';
        container.appendChild(fill);
    }

    renderSurvival(state) {
        this.updateStatSegments('health', state.healthPercent, this.getHealthStatus);
        this.updateStatSegments('food', state.foodPercent, this.getFoodStatus);
        this.updateStatSegments('water', state.waterPercent, this.getWaterStatus);
        this.updateStatSegments('energy', state.energyPercent, this.getEnergyStatus);
    }

    updateStatSegments(stat, percent, statusFn) {
        const pctEl = document.getElementById(stat + 'Pct');
        const statusEl = document.getElementById(stat + 'Status');

        pctEl.textContent = percent + '%';
        statusEl.textContent = statusFn(percent);

        pctEl.className = 'stat-value';
        if (percent < 20) {
            pctEl.classList.add('critical');
        } else if (percent < 40) {
            pctEl.classList.add('low');
        }

        // Render segmented bar
        this.renderSegmentBar(stat + 'SegmentBar', percent);
    }

    getHealthStatus(pct) {
        if (pct >= 90) return 'Healthy';
        if (pct >= 70) return 'Fine';
        if (pct >= 50) return 'Hurt';
        if (pct >= 25) return 'Wounded';
        return 'Critical';
    }

    getFoodStatus(pct) {
        if (pct >= 80) return 'Well Fed';
        if (pct >= 60) return 'Satisfied';
        if (pct >= 40) return 'Peckish';
        if (pct >= 20) return 'Hungry';
        return 'Starving';
    }

    getWaterStatus(pct) {
        if (pct >= 80) return 'Hydrated';
        if (pct >= 60) return 'Fine';
        if (pct >= 40) return 'Thirsty';
        if (pct >= 20) return 'Parched';
        return 'Dehydrated';
    }

    getEnergyStatus(pct) {
        if (pct >= 90) return 'Energized';
        if (pct >= 80) return 'Alert';
        if (pct >= 40) return 'Normal';
        if (pct >= 30) return 'Tired';
        if (pct >= 20) return 'Very Tired';
        return 'Exhausted';
    }

    renderEffects(effects) {
        const container = document.getElementById('effectsList');
        const section = container.parentElement;
        this.clearElement(container);

        if (!effects || effects.length === 0) {
            section.style.display = 'none';
            return;
        }

        section.style.display = '';

        effects.forEach(e => {
            const div = document.createElement('div');
            div.className = `effect-item ${e.trend}`;

            const nameSpan = document.createElement('span');
            nameSpan.textContent = e.name;
            div.appendChild(nameSpan);

            const rightSpan = document.createElement('span');
            const sevSpan = document.createElement('span');
            sevSpan.className = 'effect-severity';
            sevSpan.textContent = `${e.severityPercent}%`;
            rightSpan.appendChild(sevSpan);

            const trend = e.trend === 'worsening' ? '↑' : e.trend === 'improving' ? '↓' : '';
            if (trend) {
                rightSpan.appendChild(document.createTextNode(trend));
            }
            div.appendChild(rightSpan);

            // Add tooltip
            const tooltip = this.createEffectTooltip(e);
            if (tooltip) {
                div.appendChild(tooltip);
                div.classList.add('has-tooltip');
            }

            container.appendChild(div);
        });
    }

    createEffectTooltip(effect) {
        const lines = [];

        // Capacity impacts
        if (effect.capacityImpacts) {
            for (const [cap, impact] of Object.entries(effect.capacityImpacts)) {
                const sign = impact > 0 ? '+' : '';
                lines.push(`${cap}: ${sign}${impact}%`);
            }
        }

        // Stat impacts
        if (effect.statsImpact) {
            const s = effect.statsImpact;
            if (s.temperaturePerHour) {
                const sign = s.temperaturePerHour > 0 ? '+' : '';
                lines.push(`Temp: ${sign}${s.temperaturePerHour.toFixed(1)}\u00B0F/hr`);
            }
            if (s.hydrationPerHour) {
                const sign = s.hydrationPerHour > 0 ? '+' : '';
                lines.push(`Hydration: ${sign}${s.hydrationPerHour.toFixed(0)}ml/hr`);
            }
            if (s.caloriesPerHour) {
                const sign = s.caloriesPerHour > 0 ? '+' : '';
                lines.push(`Calories: ${sign}${s.caloriesPerHour.toFixed(0)}/hr`);
            }
            if (s.energyPerHour) {
                const sign = s.energyPerHour > 0 ? '+' : '';
                lines.push(`Energy: ${sign}${s.energyPerHour.toFixed(0)}/hr`);
            }
            if (s.damagePerHour) {
                lines.push(`${s.damageType || 'Damage'}: ${s.damagePerHour.toFixed(1)}/hr`);
            }
        }

        // Treatment status
        if (effect.requiresTreatment) {
            lines.push('Requires treatment');
        }

        if (lines.length === 0) return null;

        const tooltip = document.createElement('div');
        tooltip.className = 'effect-tooltip';
        // Use safe DOM methods instead of innerHTML
        lines.forEach((line, i) => {
            if (i > 0) tooltip.appendChild(document.createElement('br'));
            tooltip.appendChild(document.createTextNode(line));
        });
        return tooltip;
    }

    renderInjuries(injuries, bloodPercent) {
        const container = document.getElementById('injuriesList');
        const section = container.parentElement;
        this.clearElement(container);

        const hasBloodLoss = bloodPercent && bloodPercent < 95;
        const hasInjuries = injuries && injuries.length > 0;

        if (!hasBloodLoss && !hasInjuries) {
            section.style.display = 'none';
            return;
        }

        section.style.display = '';

        if (hasBloodLoss) {
            const div = document.createElement('div');
            div.className = `injury-item ${this.getInjurySeverityClass(bloodPercent)} has-tooltip`;
            div.textContent = 'Blood loss ';
            const pctSpan = document.createElement('span');
            pctSpan.className = 'injury-pct';
            pctSpan.textContent = `(${bloodPercent}%)`;
            div.appendChild(pctSpan);

            // Blood loss tooltip
            const tooltip = document.createElement('div');
            tooltip.className = 'effect-tooltip';
            tooltip.textContent = 'Affects: Consciousness, Moving, Manipulation';
            div.appendChild(tooltip);

            container.appendChild(div);
        }

        if (hasInjuries) {
            injuries.forEach(i => {
                const div = document.createElement('div');
                div.className = `injury-item ${this.getInjurySeverityClass(i.conditionPercent)}`;
                const label = i.isOrgan ? `${i.partName} (organ) ` : `${i.partName} `;
                div.textContent = label;
                const pctSpan = document.createElement('span');
                pctSpan.className = 'injury-pct';
                pctSpan.textContent = `(${i.conditionPercent}%)`;
                div.appendChild(pctSpan);

                // Add tooltip for affected capacities
                if (i.affectedCapacities && i.affectedCapacities.length > 0) {
                    const tooltip = document.createElement('div');
                    tooltip.className = 'effect-tooltip';
                    tooltip.textContent = `Affects: ${i.affectedCapacities.join(', ')}`;
                    div.appendChild(tooltip);
                    div.classList.add('has-tooltip');
                }

                container.appendChild(div);
            });
        }
    }

    getInjurySeverityClass(percent) {
        if (percent <= 20) return 'critical';
        if (percent <= 50) return 'severe';
        if (percent <= 70) return 'moderate';
        return 'minor';
    }

    renderLog(log) {
        const container = document.getElementById('narrativeLog');
        this.clearElement(container);

        if (!log || log.length === 0) return;

        log.forEach(entry => {
            const div = document.createElement('div');
            div.className = `log-entry ${entry.level}`;
            div.textContent = entry.text;
            container.appendChild(div);
        });

        container.scrollTop = container.scrollHeight;
    }

    renderProgressSegments(percent, complete = false) {
        const container = document.getElementById('progressSegmentBar');
        if (!container.classList.contains('progress')) {
            container.classList.add('progress');
        }
        this.clearElement(container);

        // Create single fill div instead of segments
        const fill = document.createElement('div');
        fill.className = 'progress-fill';
        fill.style.width = percent + '%';

        if (complete) {
            container.classList.add('complete');
        } else {
            container.classList.remove('complete');
        }

        container.appendChild(fill);
    }

    renderInput(input, statusText, progress) {
        const actionsArea = document.getElementById('actionsArea');
        const statusTextEl = document.getElementById('statusText');
        const statusIcon = document.getElementById('statusIcon');
        const progressContainer = document.getElementById('progressSegmentBar');
        const progressPercent = document.getElementById('progressPercent');

        // Update progress/status display
        if (progress && progress.total > 0) {
            const pct = Math.round(progress.current / progress.total * 100);
            statusTextEl.textContent = statusText || 'Working...';
            statusIcon.style.display = '';
            progressContainer.style.display = '';
            this.renderProgressSegments(pct, pct >= 100);
            progressPercent.style.display = '';
            progressPercent.textContent = pct + '%';
        } else if (statusText) {
            statusTextEl.textContent = statusText;
            statusIcon.style.display = 'none';
            progressContainer.style.display = 'none';
            progressPercent.style.display = 'none';
        } else {
            statusTextEl.textContent = '—';
            statusIcon.style.display = 'none';
            progressContainer.style.display = 'none';
            progressPercent.style.display = 'none';
        }

        // Clear and render input UI
        this.clearElement(actionsArea);

        if (!input) return;

        if (input.type === 'select') {
            const promptDiv = document.createElement('div');
            promptDiv.className = 'action-prompt';
            promptDiv.textContent = input.prompt;
            actionsArea.appendChild(promptDiv);

            const listDiv = document.createElement('div');
            listDiv.className = 'action-list';

            input.choices.forEach((choice, i) => {
                const btn = document.createElement('button');
                btn.className = 'action-btn';
                btn.textContent = choice;
                btn.onclick = () => this.respond(i);
                listDiv.appendChild(btn);
            });

            actionsArea.appendChild(listDiv);

        } else if (input.type === 'confirm') {
            const promptDiv = document.createElement('div');
            promptDiv.className = 'action-prompt';
            promptDiv.textContent = input.prompt;
            actionsArea.appendChild(promptDiv);

            const listDiv = document.createElement('div');
            listDiv.className = 'action-list';

            const yesBtn = document.createElement('button');
            yesBtn.className = 'action-btn';
            yesBtn.textContent = 'Yes';
            yesBtn.onclick = () => this.respond(0);
            listDiv.appendChild(yesBtn);

            const noBtn = document.createElement('button');
            noBtn.className = 'action-btn';
            noBtn.textContent = 'No';
            noBtn.onclick = () => this.respond(1);
            listDiv.appendChild(noBtn);

            actionsArea.appendChild(listDiv);

        } else if (input.type === 'anykey') {
            const btn = document.createElement('button');
            btn.className = 'action-btn';
            btn.textContent = input.prompt || 'Continue';
            btn.onclick = () => this.respond(null);
            actionsArea.appendChild(btn);
        }
    }

    respond(choiceIndex) {
        // Disable all action buttons immediately to prevent double-clicks
        document.querySelectorAll('.action-btn').forEach(btn => {
            btn.disabled = true;
        });

        if (this.socket.readyState === WebSocket.OPEN) {
            this.socket.send(JSON.stringify({ choiceIndex }));
        }
    }

    clearElement(el) {
        while (el.firstChild) {
            el.removeChild(el.firstChild);
        }
    }

    showInventory(inv, input) {
        const overlay = document.getElementById('inventoryOverlay');
        overlay.classList.remove('hidden');

        document.getElementById('inventoryTitle').textContent = inv.title;
        document.getElementById('inventoryWeight').textContent =
            `${inv.currentWeightKg.toFixed(1)} / ${inv.maxWeightKg.toFixed(0)} kg`;

        // Render each section
        this.renderInvGear(inv);
        this.renderInvFuel(inv);
        this.renderInvFood(inv);
        this.renderInvWater(inv);
        this.renderInvMaterials(inv);
        this.renderInvMedicinals(inv);

        // Render action buttons
        const actionsContainer = document.getElementById('inventoryActions');
        this.clearElement(actionsContainer);

        if (input && input.type === 'select' && input.choices) {
            input.choices.forEach((choice, i) => {
                const btn = document.createElement('button');
                btn.className = 'action-btn';
                btn.textContent = choice;
                btn.onclick = () => this.respond(i);
                actionsContainer.appendChild(btn);
            });
        } else {
            const closeBtn = document.createElement('button');
            closeBtn.className = 'action-btn';
            closeBtn.textContent = 'Close';
            closeBtn.onclick = () => this.respond(null);
            actionsContainer.appendChild(closeBtn);
        }
    }

    createSlotElement(label, item, stat, statClass = '') {
        const slot = document.createElement('div');
        slot.className = 'inv-slot';

        const labelSpan = document.createElement('span');
        labelSpan.className = 'slot-label';
        labelSpan.textContent = label;
        slot.appendChild(labelSpan);

        if (item) {
            const itemSpan = document.createElement('span');
            itemSpan.className = 'slot-item';
            itemSpan.textContent = item;
            slot.appendChild(itemSpan);

            if (stat) {
                const statSpan = document.createElement('span');
                statSpan.className = 'slot-stat' + (statClass ? ' ' + statClass : '');
                statSpan.textContent = stat;
                slot.appendChild(statSpan);
            }
        } else {
            const emptySpan = document.createElement('span');
            emptySpan.className = 'slot-empty';
            emptySpan.textContent = '-';
            slot.appendChild(emptySpan);
        }

        return slot;
    }

    renderInvGear(inv) {
        const content = document.querySelector('#invGear .inv-content');
        this.clearElement(content);

        // Weapon slot (always show)
        const weaponSlot = this.createSlotElement(
            'Weapon',
            inv.weapon,
            inv.weapon ? `${inv.weaponDamage?.toFixed(0) || 0} dmg` : null
        );
        weaponSlot.classList.add('weapon');
        content.appendChild(weaponSlot);

        // Armor slots (always show all 5)
        const armorSlots = ['Head', 'Chest', 'Hands', 'Legs', 'Feet'];
        armorSlots.forEach(slotName => {
            const equipped = inv.armor?.find(a => a.slot === slotName);
            const slot = this.createSlotElement(
                slotName,
                equipped?.name,
                equipped ? `+${(equipped.insulation * 100).toFixed(0)}%` : null,
                'insulation'
            );
            slot.classList.add('armor');
            content.appendChild(slot);
        });

        // Tools list
        if (inv.tools && inv.tools.length > 0) {
            const toolsDiv = document.createElement('div');
            toolsDiv.className = 'inv-tools';

            inv.tools.forEach(t => {
                const toolEl = document.createElement('div');
                toolEl.className = 'inv-tool';

                let nameText = t.name;
                if (t.damage) {
                    nameText += ` (${t.damage.toFixed(0)} dmg)`;
                }

                const nameSpan = document.createElement('span');
                nameSpan.className = 'tool-name';
                nameSpan.textContent = nameText;
                toolEl.appendChild(nameSpan);

                // Show durability warning if provided
                if (inv.toolWarnings) {
                    const warning = inv.toolWarnings.find(w => w.name === t.name);
                    if (warning) {
                        const warnSpan = document.createElement('span');
                        warnSpan.className = 'tool-warning';
                        warnSpan.textContent = `${warning.durabilityRemaining} uses left`;
                        toolEl.appendChild(warnSpan);
                        toolEl.classList.add('durability-low');
                    }
                }

                toolsDiv.appendChild(toolEl);
            });

            content.appendChild(toolsDiv);
        }

        // Total insulation summary
        if (inv.totalInsulation > 0) {
            const totalDiv = document.createElement('div');
            totalDiv.className = 'inv-total';

            const labelSpan = document.createElement('span');
            labelSpan.textContent = 'Total Insulation';
            totalDiv.appendChild(labelSpan);

            const valueSpan = document.createElement('span');
            valueSpan.className = 'total-value';
            valueSpan.textContent = `+${(inv.totalInsulation * 100).toFixed(0)}%`;
            totalDiv.appendChild(valueSpan);

            content.appendChild(totalDiv);
        }
    }

    renderInvFuel(inv) {
        const content = document.querySelector('#invFuel .inv-content');
        this.clearElement(content);

        // Generic fuel
        if (inv.logCount > 0)
            this.addInvItem(content, `${inv.logCount} logs`, `${inv.logsKg.toFixed(1)}kg`);
        if (inv.stickCount > 0)
            this.addInvItem(content, `${inv.stickCount} sticks`, `${inv.sticksKg.toFixed(1)}kg`);
        if (inv.tinderCount > 0)
            this.addInvItem(content, `${inv.tinderCount} tinder`, `${inv.tinderKg.toFixed(2)}kg`);

        // Wood types
        if (inv.pineCount > 0)
            this.addInvItem(content, `${inv.pineCount} pine`, `${inv.pineKg.toFixed(1)}kg`, 'wood-pine');
        if (inv.birchCount > 0)
            this.addInvItem(content, `${inv.birchCount} birch`, `${inv.birchKg.toFixed(1)}kg`, 'wood-birch');
        if (inv.oakCount > 0)
            this.addInvItem(content, `${inv.oakCount} oak`, `${inv.oakKg.toFixed(1)}kg`, 'wood-oak');
        if (inv.birchBarkCount > 0)
            this.addInvItem(content, `${inv.birchBarkCount} birch bark`, `${inv.birchBarkKg.toFixed(2)}kg`, 'tinder');

        // Burn time summary
        if (content.children.length > 0) {
            this.addInvItem(content, 'Burn time', `~${inv.fuelBurnTimeHours.toFixed(1)} hrs`, 'summary');
        } else {
            this.addNoneItem(content);
        }
    }

    renderInvFood(inv) {
        const content = document.querySelector('#invFood .inv-content');
        this.clearElement(content);

        // Cooked (best)
        if (inv.cookedMeatCount > 0)
            this.addInvItem(content, `${inv.cookedMeatCount} cooked meat`, `${inv.cookedMeatKg.toFixed(1)}kg`, 'food-cooked');

        // Preserved
        if (inv.driedMeatCount > 0)
            this.addInvItem(content, `${inv.driedMeatCount} dried meat`, `${inv.driedMeatKg.toFixed(1)}kg`, 'food-preserved');
        if (inv.driedBerriesCount > 0)
            this.addInvItem(content, `${inv.driedBerriesCount} dried berries`, `${inv.driedBerriesKg.toFixed(2)}kg`, 'food-preserved');

        // Raw
        if (inv.rawMeatCount > 0)
            this.addInvItem(content, `${inv.rawMeatCount} raw meat`, `${inv.rawMeatKg.toFixed(1)}kg`, 'food-raw');

        // Foraged
        if (inv.berryCount > 0)
            this.addInvItem(content, `${inv.berryCount} berries`, `${inv.berriesKg.toFixed(2)}kg`, 'food-foraged');
        if (inv.nutsCount > 0)
            this.addInvItem(content, `${inv.nutsCount} nuts`, `${inv.nutsKg.toFixed(2)}kg`, 'food-foraged');
        if (inv.rootsCount > 0)
            this.addInvItem(content, `${inv.rootsCount} roots`, `${inv.rootsKg.toFixed(2)}kg`, 'food-raw');

        if (content.children.length === 0) {
            this.addNoneItem(content);
        }
    }

    renderInvWater(inv) {
        const content = document.querySelector('#invWater .inv-content');
        this.clearElement(content);

        if (inv.waterLiters > 0) {
            this.addInvItem(content, 'Clean water', `${inv.waterLiters.toFixed(1)}L`);
        } else {
            this.addNoneItem(content);
        }
    }

    renderInvMaterials(inv) {
        const content = document.querySelector('#invMaterials .inv-content');
        this.clearElement(content);

        // Stone types (highlight rare)
        if (inv.stoneCount > 0)
            this.addInvItem(content, `${inv.stoneCount} stone`, `${inv.stoneKg.toFixed(1)}kg`);
        if (inv.shaleCount > 0)
            this.addInvItem(content, `${inv.shaleCount} shale`, `${inv.shaleKg.toFixed(1)}kg`, 'material-stone');
        if (inv.flintCount > 0)
            this.addInvItem(content, `${inv.flintCount} flint`, `${inv.flintKg.toFixed(1)}kg`, 'material-rare');
        if (inv.pyriteKg > 0)
            this.addInvItem(content, 'Pyrite', `${inv.pyriteKg.toFixed(2)}kg`, 'material-precious');

        // Organics
        if (inv.boneCount > 0)
            this.addInvItem(content, `${inv.boneCount} bone`, `${inv.boneKg.toFixed(1)}kg`);
        if (inv.hideCount > 0)
            this.addInvItem(content, `${inv.hideCount} hide`, `${inv.hideKg.toFixed(1)}kg`);
        if (inv.plantFiberCount > 0)
            this.addInvItem(content, `${inv.plantFiberCount} plant fiber`, `${inv.plantFiberKg.toFixed(2)}kg`);
        if (inv.sinewCount > 0)
            this.addInvItem(content, `${inv.sinewCount} sinew`, `${inv.sinewKg.toFixed(2)}kg`);

        // Processed
        if (inv.scrapedHideCount > 0)
            this.addInvItem(content, `${inv.scrapedHideCount} scraped hide`, '', 'material-processed');
        if (inv.curedHideCount > 0)
            this.addInvItem(content, `${inv.curedHideCount} cured hide`, '', 'material-processed');
        if (inv.rawFiberCount > 0)
            this.addInvItem(content, `${inv.rawFiberCount} raw fiber`, '');
        if (inv.rawFatCount > 0)
            this.addInvItem(content, `${inv.rawFatCount} raw fat`, '');
        if (inv.tallowCount > 0)
            this.addInvItem(content, `${inv.tallowCount} tallow`, '', 'material-processed');
        if (inv.charcoalKg > 0)
            this.addInvItem(content, 'Charcoal', `${inv.charcoalKg.toFixed(2)}kg`);

        if (content.children.length === 0) {
            this.addNoneItem(content);
        }
    }

    renderInvMedicinals(inv) {
        const content = document.querySelector('#invMedicinals .inv-content');
        this.clearElement(content);

        // Fungi
        if (inv.birchPolyporeCount > 0)
            this.addInvItem(content, `${inv.birchPolyporeCount} birch polypore`, '', 'medicinal-wound');
        if (inv.chagaCount > 0)
            this.addInvItem(content, `${inv.chagaCount} chaga`, '', 'medicinal-health');
        if (inv.amadouCount > 0)
            this.addInvItem(content, `${inv.amadouCount} amadou`, '', 'medicinal-versatile');

        // Plants
        if (inv.roseHipsCount > 0)
            this.addInvItem(content, `${inv.roseHipsCount} rose hips`, '', 'medicinal-vitamin');
        if (inv.juniperBerriesCount > 0)
            this.addInvItem(content, `${inv.juniperBerriesCount} juniper berries`, '', 'medicinal-antiseptic');
        if (inv.willowBarkCount > 0)
            this.addInvItem(content, `${inv.willowBarkCount} willow bark`, '', 'medicinal-pain');
        if (inv.pineNeedlesCount > 0)
            this.addInvItem(content, `${inv.pineNeedlesCount} pine needles`, '', 'medicinal-vitamin');

        // Tree products
        if (inv.pineResinCount > 0)
            this.addInvItem(content, `${inv.pineResinCount} pine resin`, '', 'medicinal-wound');
        if (inv.usneaCount > 0)
            this.addInvItem(content, `${inv.usneaCount} usnea`, '', 'medicinal-antiseptic');
        if (inv.sphagnumCount > 0)
            this.addInvItem(content, `${inv.sphagnumCount} sphagnum moss`, '', 'medicinal-wound');

        if (content.children.length === 0) {
            this.addNoneItem(content);
        }
    }

    hideInventory() {
        document.getElementById('inventoryOverlay').classList.add('hidden');
    }

    addInvItem(container, label, value, styleClass = '') {
        const div = document.createElement('div');
        div.className = 'inv-item' + (styleClass ? ' ' + styleClass : '');
        const labelSpan = document.createElement('span');
        labelSpan.textContent = label;
        const valueSpan = document.createElement('span');
        valueSpan.className = 'qty';
        valueSpan.textContent = value;
        div.appendChild(labelSpan);
        div.appendChild(valueSpan);
        container.appendChild(div);
    }

    addNoneItem(container) {
        const div = document.createElement('div');
        div.className = 'inv-none';
        div.textContent = 'None';
        container.appendChild(div);
    }
}

// Start the client
new GameClient();
