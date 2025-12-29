"use strict";

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/rewardHub")
    .configureLogging(signalR.LogLevel.Information)
    .build();

let currentPlayerId = 0;

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

// Listen for updates
connection.on("ReceiveReward", (notification) => {
    console.log("Reward Received:", notification);

    // Animate latest match panel
    const latestPanel = document.getElementById("latest-panel");
    if (latestPanel) {
        latestPanel.classList.add("active-update");
        setTimeout(() => latestPanel.classList.remove("active-update"), 1000);
    }

    // Update Latest Match Display
    updateText("latest-points", notification.points);
    updateText("latest-kills", notification.kills);
    updateText("latest-deaths", notification.deaths);
    updateText("latest-supports", notification.supports);
    updateText("perf-score", notification.points);

    // If watching this player, refresh aggregate stats
    if (currentPlayerId == notification.playerId || currentPlayerId == 0) {
        if (currentPlayerId == 0) {
            const input = document.getElementById("player-input");
            if (input) input.value = notification.playerId;
            currentPlayerId = notification.playerId;
        }
        refreshStats(currentPlayerId);
    }
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

    // i18n: Scanning
    if (btn) btn.innerText = i18n.get("scanning");

    currentPlayerId = input.value;
    await refreshStats(currentPlayerId);

    // i18n: Scan (Reset)
    if (btn) btn.innerText = i18n.get("scan");
}

const ctx = document.getElementById('trendChart').getContext('2d');
let trendChart;

async function refreshStats(pid) {
    try {
        const response = await fetch(`/api/stats/${pid}`);
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

        // New KPI: Total Gain (GB + Bonus)
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

        // Update History List & Chart
        loadHistory(pid);

    } catch (e) {
        console.error(e);
        updateText("last-refresh", "ERROR");
    }
}

async function loadHistory(pid) {
    try {
        const response = await fetch(`/api/history/${pid}`);
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

                // i18n for GAIN / BONUS / EXP labels inside history list
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
    // Re-create chart if language changed to update dataset labels
    const labelTotalGain = i18n.get('total_gain');
    const labelMachineExp = i18n.get('kpi_exp'); // or just 'exp'

    if (trendChart) {
        trendChart.data.labels = labels;
        trendChart.data.datasets[0].data = dataGB;
        trendChart.data.datasets[0].label = labelTotalGain; // Update label
        trendChart.data.datasets[1].data = dataExp;
        trendChart.data.datasets[1].label = labelMachineExp; // Update label
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

// Auto-Load from URL
window.addEventListener('DOMContentLoaded', () => {
    // Determine language from button click or just apply default from i18n.js load
    // i18n.js is loaded first, so window.i18n is available.
    i18n.applyTranslations();

    const params = new URLSearchParams(window.location.search);
    const pid = params.get("pid");
    if (pid && pid !== "0") {
        const input = document.getElementById("player-input");
        if (input) input.value = pid;
        loadPlayer();
    }
});
