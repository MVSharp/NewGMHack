const translations = {
    "en": {
        "title": "BUNDAM BATTLE TRACK SYSTEM",
        "date_format": "YYYY-MM-DD",
        "scanning": "SCANNING...",
        "scan": "SCAN",
        "waiting": "WAITING...",
        "no_data": "NO DATA",
        "latest_report": "LATEST BATTLE REPORT",
        "perf_score": "PERFORMANCE SCORE",
        "kills": "KILLS",
        "deaths": "DEATHS",
        "assist": "ASSIST",
        "points": "POINTS",
        "trend_header": "ECONOMIC TREND (LAST 10 BATTLES)",
        "pilot_efficiency": "PILOT EFFICIENCY",
        "metric": "METRIC",
        "hourly": "HOURLY",
        "today": "TODAY",
        "total_gain": "TOTAL GAIN",
        "base_gb": "BASE GB",
        "bonus": "BONUS",
        "exp": "EXP",
        "sorties": "BATTLES",
        "combat_log": "COMBAT LOG",
        "kpi_battle": "TOTAL BATTLE",
        "kpi_gb": "GB EARNED",
        "kpi_bonus": "TOTAL BONUS",
        "kpi_total_gain": "TOTAL GAIN (INC BONUS)",
        "kpi_exp": "TOTAL MACHINE EXP GAINED",
        "kpi_max": "MAX SCORE",
        "kpi_kills": "TOTAL KILLS",
        "kpi_deaths": "TOTAL DEATHS",
        "kpi_support": "TOTAL SUPPORT",
        "gain": "GAIN"
    },
    "zh-CN": {
        "title": "敢达对战追踪系统",
        "date_format": "YYYY年MM月DD日",
        "scanning": "扫描中...",
        "scan": "扫描",
        "waiting": "等待中...",
        "no_data": "无数据",
        "latest_report": "最新战斗报告",
        "perf_score": "表现评分",
        "kills": "击坠",
        "deaths": "被击坠",
        "assist": "助攻",
        "points": "得分",
        "trend_header": "经济趋势 (最近10场)",
        "pilot_efficiency": "机师效率",
        "metric": "指标",
        "hourly": "每小时",
        "today": "今日",
        "total_gain": "总收益",
        "base_gb": "基础GB",
        "bonus": "奖励",
        "exp": "经验值",
        "sorties": "战斗次数",
        "combat_log": "战斗日志",
        "kpi_battle": "总战斗数",
        "kpi_gb": "GB获取",
        "kpi_bonus": "总奖励",
        "kpi_total_gain": "总收益 (含奖励)",
        "kpi_exp": "总获得机体经验",
        "kpi_max": "最高得分",
        "kpi_kills": "总击坠",
        "kpi_deaths": "总被击坠",
        "kpi_support": "总助攻",
        "gain": "收益"
    },
    "zh-TW": {
        "title": "鋼彈對戰追蹤系統",
        "date_format": "YYYY年MM月DD日",
        "scanning": "掃描中...",
        "scan": "掃描",
        "waiting": "等待中...",
        "no_data": "無數據",
        "latest_report": "最新戰鬥報告",
        "perf_score": "表現評分",
        "kills": "擊墜",
        "deaths": "被擊墜",
        "assist": "助攻",
        "points": "得分",
        "trend_header": "經濟趨勢 (最近10場)",
        "pilot_efficiency": "機師效率",
        "metric": "指標",
        "hourly": "每小時",
        "today": "今日",
        "total_gain": "總收益",
        "base_gb": "基礎GB",
        "bonus": "獎勵",
        "exp": "經驗值",
        "sorties": "戰鬥次數",
        "combat_log": "戰鬥日誌",
        "kpi_battle": "總戰鬥數",
        "kpi_gb": "GB獲取",
        "kpi_bonus": "總獎勵",
        "kpi_total_gain": "總收益 (含獎勵)",
        "kpi_exp": "總獲得機體經驗",
        "kpi_max": "最高得分",
        "kpi_kills": "總擊墜",
        "kpi_deaths": "總被擊墜",
        "kpi_support": "總助攻",
        "gain": "收益"
    }
};

let currentLang = localStorage.getItem("lang") || "en";

function setLanguage(lang) {
    if (!translations[lang]) return;
    currentLang = lang;
    localStorage.setItem("lang", lang);
    applyTranslations();
}

function applyTranslations() {
    const t = translations[currentLang];
    document.querySelectorAll("[data-i18n]").forEach(el => {
        const key = el.getAttribute("data-i18n");
        if (t[key]) {
            if (el.tagName === "INPUT" && el.type === "placeholder") {
                el.placeholder = t[key];
            } else {
                el.innerText = t[key];
            }
        }
    });

    // Update dynamic button text if not currently loading
    const scanBtn = document.getElementById("btn-search");
    if (scanBtn && scanBtn.innerText !== "SCANNING..." && scanBtn.innerText !== "扫描中..." && scanBtn.innerText !== "掃描中...") {
        scanBtn.innerText = t["scan"];
    }
}

// Export for usage in dashboard.js
window.i18n = {
    get: (key) => translations[currentLang][key] || key,
    setLanguage,
    applyTranslations,
    currentLang: () => currentLang
};
