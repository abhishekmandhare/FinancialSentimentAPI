// Admin page — system stats from /api/admin/stats

let adminRefreshTimer = null;

async function loadAdminStats() {
    const container = document.getElementById('adminContent');
    try {
        const res = await fetch('/api/admin/stats');
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const stats = await res.json();
        setStatus(true);
        renderAdminStats(stats);
    } catch (e) {
        container.innerHTML = `<div class="admin-error">Failed to load system stats: ${escapeHtml(e.message)}</div>`;
    }
}

function renderAdminStats(stats) {
    const container = document.getElementById('adminContent');

    const symbolTags = stats.symbols.analyzedSymbols
        .map(s => `<span class="admin-symbol-tag">${escapeHtml(s)}</span>`)
        .join('');

    container.innerHTML = `
        <div class="admin-grid">
            <div class="admin-card">
                <div class="admin-card-header">
                    <h3>Analysis Counts</h3>
                </div>
                <div class="admin-card-body">
                    <div class="admin-stat-row">
                        <span class="admin-stat-label">Total analyses</span>
                        <span class="admin-stat-value">${stats.counts.total.toLocaleString()}</span>
                    </div>
                    <div class="admin-stat-row">
                        <span class="admin-stat-label">Last hour</span>
                        <span class="admin-stat-value highlight">${stats.counts.lastHour.toLocaleString()}</span>
                    </div>
                    <div class="admin-stat-row">
                        <span class="admin-stat-label">Last 24 hours</span>
                        <span class="admin-stat-value highlight">${stats.counts.last24Hours.toLocaleString()}</span>
                    </div>
                </div>
            </div>

            <div class="admin-card">
                <div class="admin-card-header">
                    <h3>Throughput</h3>
                </div>
                <div class="admin-card-body">
                    <div class="admin-stat-row">
                        <span class="admin-stat-label">Analyses / hour</span>
                        <span class="admin-stat-value">${stats.throughput.analysesPerHour.toFixed(1)}</span>
                    </div>
                    <div class="admin-stat-row">
                        <span class="admin-stat-label">Analyses / day</span>
                        <span class="admin-stat-value">${stats.throughput.analysesPerDay.toFixed(1)}</span>
                    </div>
                </div>
            </div>

            <div class="admin-card">
                <div class="admin-card-header">
                    <h3>Symbols</h3>
                </div>
                <div class="admin-card-body">
                    <div class="admin-stat-row">
                        <span class="admin-stat-label">Tracked symbols</span>
                        <span class="admin-stat-value">${stats.symbols.trackedSymbols}</span>
                    </div>
                    <div class="admin-stat-row">
                        <span class="admin-stat-label">Analyzed symbols</span>
                        <span class="admin-stat-value">${stats.symbols.distinctAnalyzedSymbols}</span>
                    </div>
                </div>
                ${symbolTags ? `<div class="admin-card-body" style="border-top: 1px solid var(--border); padding-top: 12px;">
                    <div class="admin-stat-label" style="margin-bottom: 8px;">Analyzed Symbols</div>
                    <div class="admin-symbol-list">${symbolTags}</div>
                </div>` : ''}
            </div>

            <div class="admin-card">
                <div class="admin-card-header">
                    <h3>Ingestion Config</h3>
                </div>
                <div class="admin-card-body">
                    <div class="admin-stat-row">
                        <span class="admin-stat-label">Polling interval</span>
                        <span class="admin-stat-value">${stats.ingestion.pollingIntervalMinutes} min</span>
                    </div>
                    <div class="admin-stat-row">
                        <span class="admin-stat-label">Max concurrent</span>
                        <span class="admin-stat-value">${stats.ingestion.maxConcurrentAnalyses}</span>
                    </div>
                    <div class="admin-stat-row">
                        <span class="admin-stat-label">Queue depth</span>
                        <span class="admin-stat-value">${stats.ingestion.queueDepth}</span>
                    </div>
                </div>
            </div>

            <div class="admin-card">
                <div class="admin-card-header">
                    <h3>Capacity Projection</h3>
                </div>
                <div class="admin-card-body">
                    <div class="admin-stat-row">
                        <span class="admin-stat-label">DB growth / month</span>
                        <span class="admin-stat-value">${escapeHtml(stats.projection.estimatedDbGrowthPerMonth)}</span>
                    </div>
                    <div class="admin-stat-row">
                        <span class="admin-stat-label">Est. rows / month</span>
                        <span class="admin-stat-value">${stats.projection.estimatedRowsPerMonth.toLocaleString(undefined, { maximumFractionDigits: 0 })}</span>
                    </div>
                    <div class="admin-stat-row">
                        <span class="admin-stat-label">Latency note</span>
                        <span class="admin-stat-value" style="font-size:0.85rem;font-weight:400;">${escapeHtml(stats.projection.analysisLatencyNote)}</span>
                    </div>
                </div>
            </div>
        </div>

        <div class="admin-refresh-info">Auto-refreshes every 30 seconds</div>
    `;
}

function initAdmin() {
    loadAdminStats();
    adminRefreshTimer = setInterval(loadAdminStats, 30000);
}

function stopAdmin() {
    if (adminRefreshTimer) {
        clearInterval(adminRefreshTimer);
        adminRefreshTimer = null;
    }
}
