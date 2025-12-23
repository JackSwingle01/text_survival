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
            this.showInventory(frame.inventory);
        } else {
            this.hideInventory();
        }

        this.renderInput(frame.input, frame.statusText, frame.progress);
    }

    renderState(state) {
        // CSS variables
        document.documentElement.style.setProperty('--warmth', state.warmth);
        document.documentElement.style.setProperty('--vitality', state.vitality);

        // Time mode
        document.body.classList.remove('night', 'twilight', 'day');
        document.body.classList.add(state.isDaytime ? 'day' : 'night');

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
        document.getElementById('locationDesc').textContent = state.locationDescription;
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
        document.getElementById('weightDisplay').textContent =
            `${state.carryWeightKg.toFixed(1)} / ${state.maxWeightKg.toFixed(0)} kg`;

        // Narrative log
        this.renderLog(state.log);
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
        const timeEl = document.getElementById('fireTime');
        const fuelEl = document.getElementById('fuelStatus');
        const heatEl = document.getElementById('fireHeatOutput');

        if (!fire || fire.phase === 'Cold') {
            phaseEl.textContent = fire ? 'Fire: Cold' : 'No fire pit';
            phaseEl.className = 'fire-phase cold';
            timeEl.textContent = '';
            timeEl.className = 'fire-time';
            fuelEl.textContent = fire ? `${fire.totalKg.toFixed(1)}kg fuel` : '';
            heatEl.textContent = '';
            return;
        }

        phaseEl.textContent = `Fire: ${fire.phase}`;
        phaseEl.className = 'fire-phase ' + fire.phase.toLowerCase();

        timeEl.textContent = `${fire.minutesRemaining} min remaining`;
        timeEl.className = 'fire-time';
        if (fire.minutesRemaining >= 30) timeEl.classList.add('ok');
        else if (fire.minutesRemaining >= 15) timeEl.classList.add('warning');
        else timeEl.classList.add('critical');

        if (fire.unlitKg > 0.1) {
            fuelEl.textContent = `${fire.burningKg.toFixed(1)}kg (+${fire.unlitKg.toFixed(1)}kg unlit)`;
        } else {
            fuelEl.textContent = `${fire.totalKg.toFixed(1)}kg`;
        }

        heatEl.textContent = fire.heatOutput > 0 ? `+${fire.heatOutput.toFixed(0)}°F` : '';
    }

    renderTemperature(state) {
        const bodyTemp = state.bodyTemp;

        // Temperature bar (87-102 range)
        const tempPct = Math.max(0, Math.min(100, (bodyTemp - 87) / (102 - 87) * 100));
        const tempBar = document.getElementById('tempBarFill');
        tempBar.style.width = tempPct + '%';

        tempBar.className = 'temp-bar-fill';
        if (bodyTemp < 95) tempBar.classList.add('hypothermia');
        else if (bodyTemp < 97) tempBar.classList.add('cool');
        else if (bodyTemp < 99) tempBar.classList.add('normal');
        else if (bodyTemp < 100) tempBar.classList.add('hot');
        else tempBar.classList.add('hyperthermia');

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

    renderSurvival(state) {
        this.updateStatBar('health', state.healthPercent, this.getHealthStatus);
        this.updateStatBar('food', state.foodPercent, this.getFoodStatus);
        this.updateStatBar('water', state.waterPercent, this.getWaterStatus);
        this.updateStatBar('energy', state.energyPercent, this.getEnergyStatus);
    }

    updateStatBar(stat, percent, statusFn) {
        const bar = document.getElementById(stat + 'Bar');
        const pctEl = document.getElementById(stat + 'Pct');
        const statusEl = document.getElementById(stat + 'Status');

        bar.style.width = percent + '%';
        pctEl.textContent = percent + '%';
        statusEl.textContent = statusFn(percent);

        pctEl.className = 'stat-value';
        bar.className = 'stat-bar-fill ' + stat;
        if (percent < 20) {
            pctEl.classList.add('critical');
            bar.classList.add('critical');
        } else if (percent < 40) {
            pctEl.classList.add('low');
            bar.classList.add('low');
        }
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
        this.clearElement(container);

        if (!effects || effects.length === 0) {
            const div = document.createElement('div');
            div.className = 'effect-item stable';
            div.textContent = 'None';
            container.appendChild(div);
            return;
        }

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

            container.appendChild(div);
        });
    }

    renderInjuries(injuries, bloodPercent) {
        const container = document.getElementById('injuriesList');
        this.clearElement(container);

        let hasItems = false;

        if (bloodPercent && bloodPercent < 95) {
            hasItems = true;
            const div = document.createElement('div');
            div.className = `injury-item ${this.getInjurySeverityClass(bloodPercent)}`;
            div.textContent = 'Blood loss ';
            const pctSpan = document.createElement('span');
            pctSpan.className = 'injury-pct';
            pctSpan.textContent = `(${bloodPercent}%)`;
            div.appendChild(pctSpan);
            container.appendChild(div);
        }

        if (injuries && injuries.length > 0) {
            hasItems = true;
            injuries.forEach(i => {
                const div = document.createElement('div');
                div.className = `injury-item ${this.getInjurySeverityClass(i.conditionPercent)}`;
                const label = i.isOrgan ? `${i.partName} (organ) ` : `${i.partName} `;
                div.textContent = label;
                const pctSpan = document.createElement('span');
                pctSpan.className = 'injury-pct';
                pctSpan.textContent = `(${i.conditionPercent}%)`;
                div.appendChild(pctSpan);
                container.appendChild(div);
            });
        }

        if (!hasItems) {
            const div = document.createElement('div');
            div.className = 'injury-item minor';
            div.textContent = 'None';
            container.appendChild(div);
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

    renderInput(input, statusText, progress) {
        const actionsArea = document.getElementById('actionsArea');
        const statusTextEl = document.getElementById('statusText');
        const progressContainer = document.getElementById('progressBarContainer');
        const progressFill = document.getElementById('progressFill');
        const progressPercent = document.getElementById('progressPercent');

        // Update progress/status display
        if (progress && progress.total > 0) {
            const pct = Math.round(progress.current / progress.total * 100);
            statusTextEl.textContent = statusText || 'Working...';
            progressContainer.style.display = '';
            progressFill.style.width = pct + '%';
            progressFill.className = 'progress-fill' + (pct >= 100 ? ' complete' : '');
            progressPercent.style.display = '';
            progressPercent.textContent = pct + '%';
        } else if (statusText) {
            statusTextEl.textContent = statusText;
            progressContainer.style.display = 'none';
            progressPercent.style.display = 'none';
        } else {
            statusTextEl.textContent = '—';
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
            const promptDiv = document.createElement('div');
            promptDiv.className = 'action-prompt';
            promptDiv.textContent = input.prompt;
            actionsArea.appendChild(promptDiv);

            const btn = document.createElement('button');
            btn.className = 'action-btn primary';
            btn.textContent = 'Continue';
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

    showInventory(inv) {
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
