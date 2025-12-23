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
        // CSS variables
        document.documentElement.style.setProperty('--warmth', state.warmth);
        document.documentElement.style.setProperty('--vitality', state.vitality);

        // Time mode
        // document.body.classList.remove('night', 'twilight', 'day');
        // document.body.classList.add(state.isDaytime ? 'day' : 'night');

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
        document.getElementById('insulationPct').textContent = `${state.insulationPercent}%`;
        document.getElementById('fuelReserve').textContent =
            `${state.fuelKg.toFixed(1)}kg (${state.fuelBurnTime})`;

        // Narrative log
        this.renderLog(state.log);
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
            if (fire.totalKg > 0) {
                const litPercent = fire.totalKg > 0 ? Math.round(fire.burningKg / fire.totalKg * 100) : 0;
                fuelEl.textContent = `${fire.totalKg.toFixed(1)}kg fuel (${litPercent}% lit)`;
            } else {
                fuelEl.textContent = 'No fuel';
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
            fuelEl.textContent = `${fire.totalKg.toFixed(1)}/${fire.maxCapacityKg.toFixed(0)} kg fuel`;
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

        const filledSegments = Math.round(percent / 10);
        const isCritical = percent < 20;
        const isLow = percent < 40 && percent >= 20;

        for (let i = 0; i < 10; i++) {
            const segment = document.createElement('div');
            segment.className = 'segment';
            if (i < filledSegments) {
                segment.classList.add('filled');
                if (isCritical) segment.classList.add('critical');
                else if (isLow) segment.classList.add('low');
            }
            container.appendChild(segment);
        }
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
        this.clearElement(container);

        const filledSegments = Math.round(percent / 10);
        for (let i = 0; i < 10; i++) {
            const segment = document.createElement('div');
            segment.className = 'segment';
            if (i < filledSegments) {
                segment.classList.add('filled');
            }
            container.appendChild(segment);
        }

        if (complete) {
            container.classList.add('complete');
        } else {
            container.classList.remove('complete');
        }
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
                btn.className = 'action-btn' + (i === 0 ? ' primary' : '');
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
            yesBtn.className = 'action-btn primary';
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
            btn.className = 'action-btn primary';
            btn.textContent = input.prompt || 'Continue';
            btn.onclick = () => this.respond(null);
            actionsArea.appendChild(btn);
        }
    }

    respond(choiceIndex) {
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

        // Gear section
        const gearContent = document.querySelector('#invGear .inv-content');
        this.clearElement(gearContent);

        if (inv.weapon) {
            this.addInvItem(gearContent, `Weapon: ${inv.weapon}`, `${inv.weaponDamage?.toFixed(0) || 0} dmg`);
        }

        if (inv.armor && inv.armor.length > 0) {
            inv.armor.forEach(a => {
                this.addInvItem(gearContent, `${a.slot}: ${a.name}`, `+${(a.insulation * 100).toFixed(0)}%`);
            });
        }

        if (inv.tools && inv.tools.length > 0) {
            inv.tools.forEach(t => {
                const dmg = t.damage ? ` (${t.damage.toFixed(0)} dmg)` : '';
                this.addInvItem(gearContent, t.name + dmg, '');
            });
        }

        if (inv.totalInsulation > 0) {
            this.addInvItem(gearContent, 'Total Insulation', `+${(inv.totalInsulation * 100).toFixed(0)}%`);
        }

        if (gearContent.children.length === 0) {
            this.addNoneItem(gearContent);
        }

        // Fuel section
        const fuelContent = document.querySelector('#invFuel .inv-content');
        this.clearElement(fuelContent);

        if (inv.logCount > 0) this.addInvItem(fuelContent, `${inv.logCount} logs`, `${inv.logsKg.toFixed(1)}kg`);
        if (inv.stickCount > 0) this.addInvItem(fuelContent, `${inv.stickCount} sticks`, `${inv.sticksKg.toFixed(1)}kg`);
        if (inv.tinderCount > 0) this.addInvItem(fuelContent, `${inv.tinderCount} tinder`, `${inv.tinderKg.toFixed(2)}kg`);

        if (fuelContent.children.length > 0) {
            this.addInvItem(fuelContent, 'Burn time', `~${inv.fuelBurnTimeHours.toFixed(1)} hrs`);
        } else {
            this.addNoneItem(fuelContent);
        }

        // Food section
        const foodContent = document.querySelector('#invFood .inv-content');
        this.clearElement(foodContent);

        if (inv.cookedMeatCount > 0) this.addInvItem(foodContent, `${inv.cookedMeatCount} cooked meat`, `${inv.cookedMeatKg.toFixed(1)}kg`);
        if (inv.rawMeatCount > 0) this.addInvItem(foodContent, `${inv.rawMeatCount} raw meat`, `${inv.rawMeatKg.toFixed(1)}kg`);
        if (inv.berryCount > 0) this.addInvItem(foodContent, `${inv.berryCount} berries`, `${inv.berriesKg.toFixed(2)}kg`);
        if (inv.waterLiters > 0) this.addInvItem(foodContent, 'Water', `${inv.waterLiters.toFixed(1)}L`);

        if (foodContent.children.length === 0) {
            this.addNoneItem(foodContent);
        }

        // Materials section
        const matContent = document.querySelector('#invMaterials .inv-content');
        this.clearElement(matContent);

        if (inv.stoneCount > 0) this.addInvItem(matContent, `${inv.stoneCount} stone`, `${inv.stoneKg.toFixed(1)}kg`);
        if (inv.boneCount > 0) this.addInvItem(matContent, `${inv.boneCount} bone`, `${inv.boneKg.toFixed(1)}kg`);
        if (inv.hideCount > 0) this.addInvItem(matContent, `${inv.hideCount} hide`, `${inv.hideKg.toFixed(1)}kg`);
        if (inv.plantFiberCount > 0) this.addInvItem(matContent, `${inv.plantFiberCount} plant fiber`, `${inv.plantFiberKg.toFixed(2)}kg`);
        if (inv.sinewCount > 0) this.addInvItem(matContent, `${inv.sinewCount} sinew`, `${inv.sinewKg.toFixed(2)}kg`);

        if (matContent.children.length === 0) {
            this.addNoneItem(matContent);
        }

        // Render action buttons inside the overlay
        const actionsContainer = document.getElementById('inventoryActions');
        this.clearElement(actionsContainer);

        if (input && input.type === 'select' && input.choices) {
            // Render selection buttons inside overlay
            input.choices.forEach((choice, i) => {
                const btn = document.createElement('button');
                btn.className = 'action-btn' + (choice === 'Back' ? '' : ' primary');
                btn.textContent = choice;
                btn.onclick = () => this.respond(i);
                actionsContainer.appendChild(btn);
            });
        } else {
            // Close button only (for read-only view or anykey)
            const closeBtn = document.createElement('button');
            closeBtn.className = 'action-btn';
            closeBtn.textContent = 'Close';
            closeBtn.onclick = () => this.respond(null);
            actionsContainer.appendChild(closeBtn);
        }
    }

    hideInventory() {
        document.getElementById('inventoryOverlay').classList.add('hidden');
    }

    addInvItem(container, label, value) {
        const div = document.createElement('div');
        div.className = 'inv-item';
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
