// Dashboard page — sentiment trending and detail panel

const API = '';
let refreshTimer;
const priceCache = {};

const SYMBOLS = {
    'AAPL':    'Apple Inc.',
    'MSFT':    'Microsoft Corp.',
    'GOOGL':   'Alphabet Inc.',
    'TSLA':    'Tesla Inc.',
    'NVDA':    'NVIDIA Corp.',
    'AMZN':    'Amazon.com Inc.',
    'META':    'Meta Platforms Inc.',
    'NFLX':    'Netflix Inc.',
    'AMD':     'Advanced Micro Devices',
    'INTC':    'Intel Corp.',
    'JPM':     'JPMorgan Chase & Co.',
    'BAC':     'Bank of America Corp.',
    'SPY':     'S&P 500 ETF',
    'QQQ':     'Nasdaq-100 ETF',
    'BTC-USD': 'Bitcoin',
};

function symbolName(sym) {
    return SYMBOLS[sym] || sym;
}

// -- Stock Price Fetching -----------------------------------------

async function fetchStockPrice(symbol) {
    try {
        const url = `${API}/api/prices/${encodeURIComponent(symbol)}/chart?range=5d&interval=1h`;
        const res = await fetch(url);
        if (!res.ok) return null;
        const data = await res.json();
        const result = data.chart.result;
        if (!result || !result.length) return null;

        const meta = result[0].meta;
        const closes = result[0].indicators.quote[0].close;
        const validCloses = closes.filter(c => c !== null && c !== undefined);
        if (!validCloses.length) return null;

        const currentPrice = meta.regularMarketPrice;
        const previousClose = meta.chartPreviousClose || meta.previousClose;
        const change = previousClose ? currentPrice - previousClose : 0;
        const changePercent = previousClose ? (change / previousClose) * 100 : 0;

        return {
            price: currentPrice,
            change: change,
            changePercent: changePercent,
            sparklineData: validCloses
        };
    } catch (e) {
        return null;
    }
}

async function fetchAllPrices(symbols) {
    const promises = symbols.map(async (sym) => {
        const data = await fetchStockPrice(sym);
        if (data) {
            priceCache[sym] = data;
        }
    });
    await Promise.allSettled(promises);
}

// -- Trending -----------------------------------------------------

async function loadTrending() {
    try {
        const res = await fetch(`${API}/api/sentiment/trending?hours=24&limit=20`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const data = await res.json();
        setStatus(true);
        hideError();
        renderTrending(data);
        const symbols = data.map(t => t.symbol);
        fetchAllPrices(symbols).then(() => updatePriceCells(data));
    } catch (e) {
        setStatus(false);
        showError(`Failed to load trending data: ${e.message}`);
    }
}

function renderTrending(items) {
    const tbody = document.getElementById('trendingBody');
    if (!items.length) {
        tbody.innerHTML = '<tr><td colspan="7" class="empty-state"><p>No sentiment data yet.</p><p>The ingestion pipeline will populate this automatically.</p></td></tr>';
        return;
    }
    tbody.innerHTML = items.map(t => {
        const scoreClass = t.currentAvgScore > 0.1 ? 'positive' : t.currentAvgScore < -0.1 ? 'negative' : 'neutral';
        const deltaClass = t.delta > 0.01 ? 'up' : t.delta < -0.01 ? 'down' : 'flat';
        const deltaSign = t.delta > 0 ? '+' : '';
        const cached = priceCache[t.symbol];
        const priceHtml = cached ? formatPriceCell(cached) : '<span style="color:var(--text-dim)">&mdash;</span>';
        const sparkHtml = cached ? createSparklineSvg(cached.sparklineData, cached.change >= 0) : '';
        return `<tr onclick="openDetail('${t.symbol}')">
            <td><strong>${t.symbol}</strong><br><span style="font-size:0.75rem;color:var(--text-dim);font-weight:normal">${symbolName(t.symbol)}</span></td>
            <td class="price-cell" data-price-symbol="${t.symbol}">${priceHtml}</td>
            <td class="sparkline-cell" data-sparkline-symbol="${t.symbol}">${sparkHtml}</td>
            <td class="score ${scoreClass}">${t.currentAvgScore.toFixed(3)}</td>
            <td class="score" style="color:var(--text-dim)">${t.previousAvgScore.toFixed(3)}</td>
            <td class="delta ${deltaClass}">${deltaSign}${t.delta.toFixed(3)}</td>
            <td><span class="direction-badge ${t.direction}">${t.direction}</span></td>
        </tr>`;
    }).join('');
}

function formatPriceCell(data) {
    const changeClass = data.change > 0.005 ? 'up' : data.change < -0.005 ? 'down' : 'flat';
    const changeSign = data.change > 0 ? '+' : '';
    const priceStr = data.price >= 1000 ? data.price.toFixed(0) : data.price >= 1 ? data.price.toFixed(2) : data.price.toFixed(4);
    return `<div class="price-value">$${priceStr}</div>
            <div class="price-change ${changeClass}">${changeSign}${data.changePercent.toFixed(2)}%</div>`;
}

function updatePriceCells(items) {
    items.forEach(t => {
        const cached = priceCache[t.symbol];
        if (!cached) return;

        const priceCell = document.querySelector(`[data-price-symbol="${t.symbol}"]`);
        if (priceCell) {
            priceCell.innerHTML = formatPriceCell(cached);
        }

        const sparkCell = document.querySelector(`[data-sparkline-symbol="${t.symbol}"]`);
        if (sparkCell) {
            sparkCell.innerHTML = createSparklineSvg(cached.sparklineData, cached.change >= 0);
        }
    });
}

// -- Detail Panel -------------------------------------------------

async function openDetail(symbol) {
    const panel = document.getElementById('detailPanel');
    document.getElementById('detailSymbol').innerHTML = `${symbol} <span style="font-size:0.85rem;color:var(--text-dim);font-weight:normal;margin-left:8px">${symbolName(symbol)}</span>`;
    document.getElementById('statsGrid').innerHTML = '<div class="stat-cell"><span class="stat-label">Loading...</span></div>';
    document.getElementById('trendChartSection').style.display = 'none';
    document.getElementById('trendChartContainer').innerHTML = '';
    document.getElementById('priceChartSection').style.display = 'none';
    document.getElementById('priceChartContainer').innerHTML = '';
    document.getElementById('historyList').innerHTML = '';
    panel.classList.add('visible');
    panel.scrollIntoView({ behavior: 'smooth', block: 'start' });

    const [stats, history, trendHistory] = await Promise.allSettled([
        fetch(`${API}/api/sentiment/${symbol}/stats?days=30`).then(r => r.ok ? r.json() : null),
        fetch(`${API}/api/sentiment/${symbol}/history?page=1&pageSize=15`).then(r => r.ok ? r.json() : null),
        fetch(`${API}/api/sentiment/${symbol}/history?page=1&pageSize=100`).then(r => r.ok ? r.json() : null)
    ]);

    if (stats.status === 'fulfilled' && stats.value) renderStats(stats.value);
    else document.getElementById('statsGrid').innerHTML = '<div class="stat-cell"><span class="stat-label">No stats available</span></div>';

    let sentimentDateRange = null;

    if (trendHistory.status === 'fulfilled' && trendHistory.value && trendHistory.value.items) {
        const chartData = trendHistory.value.items.map(h => ({ date: h.analyzedAt, score: h.score }));
        const section = document.getElementById('trendChartSection');
        const container = document.getElementById('trendChartContainer');
        container.innerHTML = createTrendChart(chartData);
        section.style.display = 'block';

        if (chartData.length >= 2) {
            const sortedDates = chartData.map(d => new Date(d.date)).sort((a, b) => a - b);
            sentimentDateRange = { min: sortedDates[0], max: sortedDates[sortedDates.length - 1] };
        }
    }

    try {
        const range = sentimentDateRange ? '1mo' : '5d';
        const interval = sentimentDateRange ? '1d' : '1h';
        const priceUrl = `${API}/api/prices/${encodeURIComponent(symbol)}/chart?range=${range}&interval=${interval}`;
        const priceRes = await fetch(priceUrl);
        if (priceRes.ok) {
            const priceData = await priceRes.json();
            const result = priceData.chart.result;
            if (result && result.length) {
                const timestamps = result[0].timestamp;
                const closes = result[0].indicators.quote[0].close;
                if (timestamps && closes) {
                    const priceSection = document.getElementById('priceChartSection');
                    const priceContainer = document.getElementById('priceChartContainer');
                    priceContainer.innerHTML = createPriceChart(timestamps, closes, sentimentDateRange);
                    priceSection.style.display = 'block';
                }
            }
        }
    } catch (e) {
        // Price chart is non-critical
    }

    if (history.status === 'fulfilled' && history.value) renderHistory(history.value.items);
    else document.getElementById('historyList').innerHTML = '<p style="color:var(--text-dim);font-size:0.85rem;">No history available</p>';
}

function renderStats(s) {
    const scoreClass = s.averageScore > 0.1 ? 'positive' : s.averageScore < -0.1 ? 'negative' : 'neutral';
    const trendColor = s.trend.direction === 'Improving' ? 'var(--green)' :
                       s.trend.direction === 'Deteriorating' ? 'var(--red)' : 'var(--text-dim)';

    document.getElementById('statsGrid').innerHTML = `
        <div class="stat-cell">
            <div class="stat-label">Avg Score</div>
            <div class="stat-value score ${scoreClass}">${s.averageScore.toFixed(3)}</div>
        </div>
        <div class="stat-cell">
            <div class="stat-label">Latest Score</div>
            <div class="stat-value">${s.latestScore.toFixed(3)}</div>
        </div>
        <div class="stat-cell">
            <div class="stat-label">Analyses</div>
            <div class="stat-value">${s.totalAnalyses}</div>
        </div>
        <div class="stat-cell">
            <div class="stat-label">Confidence</div>
            <div class="stat-value">${(s.averageConfidence * 100).toFixed(0)}%</div>
        </div>
        <div class="stat-cell">
            <div class="stat-label">Trend</div>
            <div class="stat-value" style="color:${trendColor}">${s.trend.direction}</div>
        </div>
        <div class="stat-cell">
            <div class="stat-label">Distribution</div>
            <div style="font-size:0.75rem;color:var(--text-dim);margin-bottom:4px;">
                ${s.distribution.positivePercent.toFixed(0)}% / ${s.distribution.neutralPercent.toFixed(0)}% / ${s.distribution.negativePercent.toFixed(0)}%
            </div>
            <div class="distribution-bar">
                <div class="pos" style="width:${s.distribution.positivePercent}%"></div>
                <div class="neu" style="width:${s.distribution.neutralPercent}%"></div>
                <div class="neg" style="width:${s.distribution.negativePercent}%"></div>
            </div>
        </div>
        <div class="stat-cell">
            <div class="stat-label">Highest</div>
            <div class="stat-value score positive">${s.highestScore.score.toFixed(3)}</div>
            <div style="font-size:0.7rem;color:var(--text-dim)">${formatDate(s.highestScore.date)}</div>
        </div>
        <div class="stat-cell">
            <div class="stat-label">Lowest</div>
            <div class="stat-value score negative">${s.lowestScore.score.toFixed(3)}</div>
            <div style="font-size:0.7rem;color:var(--text-dim)">${formatDate(s.lowestScore.date)}</div>
        </div>
    `;
}

function renderHistory(items) {
    const list = document.getElementById('historyList');
    if (!items || !items.length) {
        list.innerHTML = '<p style="color:var(--text-dim);font-size:0.85rem;padding:12px 0;">No history entries</p>';
        return;
    }
    list.innerHTML = '<p class="section-title" style="margin-top:4px;">Recent Analyses</p>' +
        items.map(h => {
            const scoreClass = h.score > 0.1 ? 'positive' : h.score < -0.1 ? 'negative' : 'neutral';
            const confWidth = Math.round(h.confidence * 100);
            return `<div class="history-item">
                <div class="history-top">
                    <span>
                        <span class="score ${scoreClass}">${h.score.toFixed(3)}</span>
                        <span style="color:var(--text-dim);font-size:0.78rem;margin-left:6px;">${h.label}</span>
                        <span class="confidence-bar"><span class="fill" style="width:${confWidth}%"></span></span>
                    </span>
                    <span class="history-time">${formatDate(h.analyzedAt)}</span>
                </div>
                ${h.keyReasons && h.keyReasons.length ? `<div class="history-reasons">${h.keyReasons.join(' &middot; ')}</div>` : ''}
                ${h.sourceUrl ? `<div class="history-source"><a href="${escapeHtml(h.sourceUrl)}" target="_blank" rel="noopener noreferrer">${extractDomain(h.sourceUrl)}</a></div>` : ''}
            </div>`;
        }).join('');
}

function closeDetail() {
    document.getElementById('detailPanel').classList.remove('visible');
}

// -- Error handling -----------------------------------------------

function showError(msg) {
    const el = document.getElementById('errorBanner');
    el.textContent = msg;
    el.classList.add('visible');
}

function hideError() {
    document.getElementById('errorBanner').classList.remove('visible');
}

// -- Init ---------------------------------------------------------

function initDashboard() {
    loadTrending();
    refreshTimer = setInterval(loadTrending, 30000);
}
