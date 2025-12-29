"use strict";

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/rewardHub")
    .configureLogging(signalR.LogLevel.Information)
    .build();

let currentPlayerId = 0;
let featuresData = [];

// Start SignalR
async function start() {
    try {
        await connection.start();
        console.log("SignalR Connected.");
    } catch (err) {
        console.log("SignalR Error: " + err);
        setTimeout(start, 5000);
    }
}

connection.onclose(start);
start();

// Listen for Reward updates
connection.on("ReceiveReward", (notification) => {
    console.log("Reward Received:", notification);

    const latestPanel = document.getElementById("latest-panel");
    if (latestPanel) {
        latestPanel.classList.add("active-update");
        setTimeout(() => latestPanel.classList.remove("active-update"), 1000);
    }

    updateText("latest-points", notification.points);
    updateText("latest-kills", notification.kills);
    updateText("latest-deaths", notification.deaths);
    updateText("latest-supports", notification.supports);
    updateText("perf-score", notification.points);

    if (currentPlayerId == notification.playerId || currentPlayerId == 0) {
        if (currentPlayerId == 0) {
            const input = document.getElementById("player-input");
            if (input) input.value = notification.playerId;
            currentPlayerId = notification.playerId;
        }
        refreshStats(currentPlayerId);
    }
});

// Listen for Person Info updates
connection.on("UpdatePersonInfo", (info) => {
    updatePilotInfo(info);
});

// Listen for Roommates updates
connection.on("UpdateRoommates", (list) => {
    updateLobbyList(list);
});


function updateText(id, val) {
    const el = document.getElementById(id);
    if (el) el.innerText = val;
}

// Search / Load Stats
async function loadPlayer() {
    const btn = document.getElementById("btn-search");
    const input = document.getElementById("player-input");

    if (!input || !input.value) return;

    if (btn) btn.innerText = i18n.get("scanning");

    currentPlayerId = input.value;
    await refreshStats(currentPlayerId);

    if (btn) btn.innerText = i18n.get("scan");
}

const ctx = document.getElementById('trendChart').getContext('2d');
let trendChart;

async function refreshStats(pid) {
    try {
        const response = await fetch(`/api/stats/${pid}`, { cache: 'no-store' });
        if (!response.ok) {
            console.error("Failed to fetch stats");
            return;
        }
        const data = await response.json();

        // --- Date Range ---
        if (data.stats && data.stats.FirstSortieDate && data.stats.LastSortieDate) {
            const start = new Date(data.stats.FirstSortieDate).toLocaleDateString();
            const end = new Date(data.stats.LastSortieDate).toLocaleDateString();
            updateText("date-range", `${start} - ${end}`);
        } else {
            updateText("date-range", i18n.get("no_data"));
        }

        // --- Total Stats (KPIs) ---
        const stats = data.stats || {};
        const totalGB = stats.TotalGBGain || 0;
        const totalBonus = stats.TotalBonusGB || 0;

        updateText("total-matches", stats.Matches || 0);
        updateText("max-points", stats.MaxPoints || 0);
        updateText("total-gb", totalGB);
        updateText("total-bonus", totalBonus);
        updateText("total-gain-all", totalGB + totalBonus);
        updateText("total-machine-exp", stats.TotalMachineExp || 0);
        updateText("total-kills", stats.TotalKills || 0);
        updateText("total-deaths", stats.TotalDeaths || 0);
        updateText("total-supports", stats.TotalSupports || 0);

        // --- Hourly Stats ---
        const hourly = data.hourly || {};
        const hourGB = hourly.GBGainLastHour || 0;
        const hourBonus = hourly.BonusLastHour || 0;

        updateText("hour-gb", hourGB);
        updateText("hour-bonus", hourBonus);
        updateText("hour-total", hourGB + hourBonus);
        updateText("hour-machine", hourly.MachineExpLastHour || 0);
        updateText("hour-matches", hourly.MatchesLastHour || 0);

        // --- Today Stats ---
        const today = data.today || {};
        const todayGB = today.GBGainToday || 0;
        const todayBonus = today.BonusToday || 0;

        updateText("today-gb", todayGB);
        updateText("today-bonus", todayBonus);
        updateText("today-total", todayGB + todayBonus);
        updateText("today-machine", today.MachineExpToday || 0);
        updateText("today-matches", today.MatchesToday || 0);

        updateText("last-refresh", "SYNC: " + new Date().toLocaleTimeString());

        loadHistory(pid);

    } catch (e) {
        console.error(e);
        updateText("last-refresh", "ERROR");
    }
}

async function loadHistory(pid) {
    try {
        const response = await fetch(`/api/history/${pid}`, { cache: 'no-store' });
        if (!response.ok) return;
        const history = await response.json();

        const list = document.getElementById("history-list");
        if (list) {
            list.innerHTML = "";

            if (history.length > 0) {
                const latest = history[0];
                const d = new Date(latest.CreatedAtUtc);
                const formatted = d.toLocaleDateString() + " " + d.toLocaleTimeString();
                updateText("latest-ts", formatted);

                updateText("perf-score", latest.Points);
                updateText("latest-kills", latest.Kills);
                updateText("latest-deaths", latest.Deaths);
                updateText("latest-supports", latest.Supports);
                updateText("latest-points", latest.Points);
            } else {
                updateText("latest-ts", "--");
                updateText("perf-score", "--");
                updateText("latest-kills", 0);
                updateText("latest-deaths", 0);
                updateText("latest-supports", 0);
                updateText("latest-points", 0);
            }

            const chartLabels = [];
            const chartDataGB = [];
            const chartDataExp = [];
            const reversedHistory = [...history].reverse();

            history.forEach(match => {
                const li = document.createElement("li");
                li.className = "history-item";
                if (match.Points > 2000) li.classList.add("high-score");

                const totalBonus = (match.TotalBonus || 0);
                const totalGain = match.GBGain + totalBonus;

                li.innerHTML = `
                    <span class="date">${new Date(match.CreatedAtUtc).toLocaleTimeString()}</span>
                    <span class="val" style="color:#fff">
                        <span style="color:var(--neon-cyan)">${i18n.get('gain')}: ${totalGain}</span> 
                        <span style="color:var(--bonus-orange); font-size:0.8rem; margin-left:5px;">(${i18n.get('bonus')}: ${totalBonus})</span>
                    </span>
                    <span class="val">${i18n.get('exp')}: ${match.MachineAddedExp}</span>
                `;
                list.appendChild(li);
            });

            reversedHistory.forEach(match => {
                chartLabels.push(new Date(match.CreatedAtUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }));
                chartDataGB.push(match.GBGain + (match.TotalBonus || 0));
                chartDataExp.push(match.MachineAddedExp);
            });

            updateChart(chartLabels, chartDataGB, chartDataExp);
        }
    } catch (e) {
        console.error("History error", e);
    }
}

function updateChart(labels, dataGB, dataExp) {
    const labelTotalGain = i18n.get('total_gain');
    const labelMachineExp = i18n.get('kpi_exp');

    if (trendChart) {
        trendChart.data.labels = labels;
        trendChart.data.datasets[0].data = dataGB;
        trendChart.data.datasets[0].label = labelTotalGain;
        trendChart.data.datasets[1].data = dataExp;
        trendChart.data.datasets[1].label = labelMachineExp;
        trendChart.update();
    } else {
        trendChart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [{
                    label: labelTotalGain,
                    data: dataGB,
                    borderColor: '#66fcf1',
                    backgroundColor: 'rgba(102, 252, 241, 0.1)',
                    tension: 0.4,
                    fill: true
                }, {
                    label: labelMachineExp,
                    data: dataExp,
                    borderColor: '#c5c6c7',
                    backgroundColor: 'rgba(197, 198, 199, 0.1)',
                    tension: 0.4,
                    fill: true
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: { labels: { color: 'white', font: { family: 'Rajdhani' } } }
                },
                scales: {
                    x: { ticks: { color: '#888' }, grid: { color: '#333' } },
                    y: { ticks: { color: '#888' }, grid: { color: '#333' } }
                }
            }
        });
    }
}

document.getElementById("btn-search").addEventListener("click", loadPlayer);

document.getElementById("player-input").addEventListener("keypress", function (event) {
    if (event.key === "Enter") {
        event.preventDefault();
        document.getElementById("btn-search").click();
    }
});

window.addEventListener('DOMContentLoaded', () => {
    i18n.applyTranslations();

    const params = new URLSearchParams(window.location.search);
    const pid = params.get("pid");
    if (pid && pid !== "0") {
        const input = document.getElementById("player-input");
        if (input) input.value = pid;
        loadPlayer();
    }

    // Initial fetches for other tabs (if web server is up, data might be ready or requires tab click)
    // We can also poll or wait for signalR push.
});


// === TAB & NEW FEATURES LOGIC ===

function switchTab(tabName) {
    // Hide all views
    document.querySelectorAll('.view-section').forEach(el => el.classList.remove('active'));
    // Deactivate all nav buttons
    document.querySelectorAll('.nav-btn').forEach(el => el.classList.remove('active'));

    // Show target
    const view = document.getElementById('view-' + tabName);
    if (view) view.classList.add('active');

    // Activate button
    const btns = document.querySelectorAll('.nav-btn');
    btns.forEach(btn => {
        if (btn.getAttribute('onclick').includes(tabName)) {
            btn.classList.add('active');
        }
    });

    // Special Logic on Tab Open
    if (tabName === 'features') {
        loadFeatures();
    }
    if (tabName === 'lobby') {
        loadLobby();
    }
    if (tabName === 'pilot') {
        loadPilot();
    }
}

// --- FEATURES ---

async function loadFeatures() {
    try {
        const res = await fetch('/api/features', { cache: 'no-store' });
        if (!res.ok) return;
        featuresData = await res.json();
        renderFeatures();
    } catch (e) {
        console.error("Load Features Fail", e);
    }
}

function renderFeatures() {
    const list = document.getElementById("features-list");
    if (!list) return;
    list.innerHTML = "";

    featuresData.forEach(f => {
        const card = document.createElement("div");
        card.className = "feature-card";

        // Backend returns String now due to JsonStringEnumConverter
        // E.g. "IsGodMode"
        const nameVal = (f.name !== undefined) ? f.name : f.Name;
        const isEnabled = (f.isEnabled !== undefined) ? f.isEnabled : f.IsEnabled;

        // Try i18n
        const displayName = i18n.get(nameVal) || nameVal;

        card.innerHTML = `
            <div class="feature-name">${displayName}</div>
            <div class="toggle-switch ${isEnabled ? 'active' : ''}" onclick="toggleFeature('${nameVal}', ${!isEnabled})">
                <div class="toggle-knob"></div>
            </div>
        `;
        list.appendChild(card);
    });
}

async function toggleFeature(featureNameStr, newState) {
    try {
        // Send string name (PascalCase likely needed for enum match if not case-insensitive configured,
        // but Name from object is likely PascalCase "IsGodMode" or whatever Enum is).
        const res = await fetch('/api/features', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ FeatureName: featureNameStr, IsEnabled: newState })
        });

        if (res.ok) {
            loadFeatures();
        }
    } catch (e) {
        console.error("Toggle Fail", e);
    }
}

// --- PILOT INFO ---

async function loadPilot() {
    try {
        const res = await fetch('/api/me', { cache: 'no-store' });
        if (!res.ok) return;
        const info = await res.json();
        updatePilotInfo(info);
    } catch (e) { }
}

function updatePilotInfo(info) {
    // CamelCase fallbacks
    const pid = info.personId || info.PersonId || "UNKNOWN";
    const unit = info.gundamId || info.GundamId || "--";
    const w1 = info.weapon1 || info.Weapon1 || "--";
    const w2 = info.weapon2 || info.Weapon2 || "--";
    const w3 = info.weapon3 || info.Weapon3 || "--";

    updateText("pilot-id", pid);
    updateText("pilot-unit", unit);

    // Weapons
    const wEl = document.getElementById("pilot-weapons");
    if (wEl) {
        wEl.innerHTML = `
            <div style="margin-bottom:5px; color:#aaa">WEAPON 1: <span style="color:#fff">${w1}</span></div>
            <div style="margin-bottom:5px; color:#aaa">WEAPON 2: <span style="color:#fff">${w2}</span></div>
            <div style="color:#aaa">WEAPON 3: <span style="color:#fff">${w3}</span></div>
        `;
    }
}

// --- LOBBY RECON ---

async function loadLobby() {
    try {
        const res = await fetch('/api/roommates', { cache: 'no-store' });
        if (!res.ok) return;
        const list = await res.json();
        updateLobbyList(list);
    } catch (e) { }
}

function updateLobbyList(list) {
    const tbody = document.getElementById("lobby-list");
    if (!tbody) return;
    tbody.innerHTML = "";

    if (!list || list.length === 0) {
        tbody.innerHTML = `<tr><td colspan="3" style="text-align:center; color:#555;">${i18n.get('no_data')}</td></tr>`;
        return;
    }

    list.forEach(p => {
        const name = p.name || p.Name;
        const id = p.id || p.Id;

        const tr = document.createElement("tr");
        tr.innerHTML = `
            <td style="color:var(--text-white); font-weight:bold;">${name}</td>
            <td style="font-family:'Roboto Mono'; color:var(--neon-cyan)">${id}</td>
            <td><span style="color:var(--neon-blue)">CONNECTED</span></td>
        `;
        tbody.appendChild(tr);
    });
}

// Expose switchTab globally
window.switchTab = switchTab;
window.toggleFeature = toggleFeature;
