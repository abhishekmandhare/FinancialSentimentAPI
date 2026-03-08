// Shared chart utilities used by both dashboard and admin pages

function createSparklineSvg(data, isPositive) {
    if (!data || data.length < 2) return '';

    const width = 80;
    const height = 28;
    const padding = 2;

    const min = Math.min(...data);
    const max = Math.max(...data);
    const range = max - min || 1;

    const points = data.map((val, i) => {
        const x = padding + (i / (data.length - 1)) * (width - 2 * padding);
        const y = padding + (1 - (val - min) / range) * (height - 2 * padding);
        return `${x.toFixed(1)},${y.toFixed(1)}`;
    });

    const color = isPositive ? '#00c48c' : '#ff5c5c';
    const gradientId = 'sg' + Math.random().toString(36).substring(2, 8);

    const lastPoint = points[points.length - 1];
    const firstPoint = points[0];
    const lastX = parseFloat(lastPoint.split(',')[0]);
    const firstX = parseFloat(firstPoint.split(',')[0]);

    return `<svg width="${width}" height="${height}" viewBox="0 0 ${width} ${height}" xmlns="http://www.w3.org/2000/svg">
        <defs>
            <linearGradient id="${gradientId}" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stop-color="${color}" stop-opacity="0.25"/>
                <stop offset="100%" stop-color="${color}" stop-opacity="0.02"/>
            </linearGradient>
        </defs>
        <polygon points="${points.join(' ')} ${lastX},${height} ${firstX},${height}" fill="url(#${gradientId})"/>
        <polyline points="${points.join(' ')}" fill="none" stroke="${color}" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>
    </svg>`;
}

function createTrendChart(dataPoints) {
    if (!dataPoints || dataPoints.length < 2) {
        return '<div class="trend-chart-empty">Not enough data for trend chart</div>';
    }

    const sorted = [...dataPoints].sort((a, b) => new Date(a.date) - new Date(b.date));

    const width = 700;
    const height = 220;
    const padTop = 20;
    const padBottom = 40;
    const padLeft = 45;
    const padRight = 15;
    const chartW = width - padLeft - padRight;
    const chartH = height - padTop - padBottom;

    const scores = sorted.map(d => d.score);
    const dates = sorted.map(d => new Date(d.date));

    const yMin = -1;
    const yMax = 1;
    const yRange = yMax - yMin;

    const firstScore = scores[0];
    const lastScore = scores[scores.length - 1];
    const isPositiveTrend = lastScore >= firstScore;
    const lineColor = isPositiveTrend ? '#00c48c' : '#ff5c5c';

    const gradientId = 'tg' + Math.random().toString(36).substring(2, 8);

    const points = sorted.map((d, i) => {
        const x = padLeft + (i / (sorted.length - 1)) * chartW;
        const y = padTop + (1 - (d.score - yMin) / yRange) * chartH;
        return { x, y };
    });

    const polyline = points.map(p => `${p.x.toFixed(1)},${p.y.toFixed(1)}`).join(' ');

    const zeroY = padTop + (1 - (0 - yMin) / yRange) * chartH;
    const fillPoints = points.map(p => `${p.x.toFixed(1)},${p.y.toFixed(1)}`).join(' ')
        + ` ${points[points.length - 1].x.toFixed(1)},${zeroY.toFixed(1)}`
        + ` ${points[0].x.toFixed(1)},${zeroY.toFixed(1)}`;

    const yTicks = [-1, -0.5, 0, 0.5, 1];
    const yLabels = yTicks.map(v => {
        const y = padTop + (1 - (v - yMin) / yRange) * chartH;
        return `<text x="${padLeft - 8}" y="${y.toFixed(1)}" text-anchor="end" dominant-baseline="middle" fill="#8b8fa3" font-size="10">${v.toFixed(1)}</text>
                <line x1="${padLeft}" y1="${y.toFixed(1)}" x2="${width - padRight}" y2="${y.toFixed(1)}" stroke="#2e3345" stroke-width="0.5" ${v === 0 ? 'stroke-dasharray="4,3" stroke-width="1" stroke="#5b8def" opacity="0.5"' : ''}/>`;
    }).join('\n');

    const isMobile = typeof window !== 'undefined' && window.innerWidth < 600;
    const labelCount = isMobile ? 3 : Math.min(6, sorted.length);
    const xLabels = [];
    for (let i = 0; i < labelCount; i++) {
        const idx = Math.round(i * (sorted.length - 1) / (labelCount - 1));
        const x = padLeft + (idx / (sorted.length - 1)) * chartW;
        const d = dates[idx];
        const label = d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
        xLabels.push(`<text x="${x.toFixed(1)}" y="${height - 8}" text-anchor="middle" fill="#8b8fa3" font-size="10">${label}</text>`);
    }

    const dotInterval = Math.max(1, Math.floor(sorted.length / 15));
    const dots = points
        .filter((_, i) => i % dotInterval === 0 || i === points.length - 1)
        .map(p => `<circle cx="${p.x.toFixed(1)}" cy="${p.y.toFixed(1)}" r="2.5" fill="${lineColor}" stroke="#1a1d27" stroke-width="1"/>`);

    return `<svg viewBox="0 0 ${width} ${height}" xmlns="http://www.w3.org/2000/svg" preserveAspectRatio="xMidYMid meet" style="max-height:280px;">
        <defs>
            <linearGradient id="${gradientId}" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stop-color="${lineColor}" stop-opacity="0.20"/>
                <stop offset="100%" stop-color="${lineColor}" stop-opacity="0.02"/>
            </linearGradient>
        </defs>
        ${yLabels}
        ${xLabels.join('\n')}
        <polygon points="${fillPoints}" fill="url(#${gradientId})"/>
        <polyline points="${polyline}" fill="none" stroke="${lineColor}" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
        ${dots.join('\n')}
    </svg>`;
}

function createPriceChart(timestamps, closes, dateRange) {
    const filteredData = [];
    for (let i = 0; i < timestamps.length; i++) {
        if (closes[i] === null || closes[i] === undefined) continue;
        const ts = new Date(timestamps[i] * 1000);
        if (dateRange && (ts < dateRange.min || ts > dateRange.max)) continue;
        filteredData.push({ date: ts, price: closes[i] });
    }

    if (filteredData.length < 2) {
        return '<div class="price-chart-empty">Not enough price data for this period</div>';
    }

    const width = 700;
    const height = 220;
    const padTop = 20;
    const padBottom = 40;
    const padLeft = 55;
    const padRight = 15;
    const chartW = width - padLeft - padRight;
    const chartH = height - padTop - padBottom;

    const prices = filteredData.map(d => d.price);
    const dates = filteredData.map(d => d.date);

    const yMin = Math.min(...prices);
    const yMax = Math.max(...prices);
    const yPadding = (yMax - yMin) * 0.1 || 1;
    const yLow = yMin - yPadding;
    const yHigh = yMax + yPadding;
    const yRange = yHigh - yLow;

    const firstPrice = prices[0];
    const lastPrice = prices[prices.length - 1];
    const isPositive = lastPrice >= firstPrice;
    const lineColor = isPositive ? '#00c48c' : '#ff5c5c';

    const gradientId = 'pg' + Math.random().toString(36).substring(2, 8);

    const timeMin = dateRange ? dateRange.min.getTime() : dates[0].getTime();
    const timeMax = dateRange ? dateRange.max.getTime() : dates[dates.length - 1].getTime();
    const timeRange = timeMax - timeMin || 1;

    const points = filteredData.map(d => {
        const x = padLeft + ((d.date.getTime() - timeMin) / timeRange) * chartW;
        const y = padTop + (1 - (d.price - yLow) / yRange) * chartH;
        return { x, y };
    });

    const polyline = points.map(p => `${p.x.toFixed(1)},${p.y.toFixed(1)}`).join(' ');

    const baseY = padTop + chartH;
    const fillPoints = points.map(p => `${p.x.toFixed(1)},${p.y.toFixed(1)}`).join(' ')
        + ` ${points[points.length - 1].x.toFixed(1)},${baseY.toFixed(1)}`
        + ` ${points[0].x.toFixed(1)},${baseY.toFixed(1)}`;

    const yTicks = [];
    for (let i = 0; i <= 4; i++) {
        yTicks.push(yLow + (yRange * i / 4));
    }
    const yLabels = yTicks.map(v => {
        const y = padTop + (1 - (v - yLow) / yRange) * chartH;
        const label = v >= 1000 ? v.toFixed(0) : v >= 1 ? v.toFixed(2) : v.toFixed(4);
        return `<text x="${padLeft - 8}" y="${y.toFixed(1)}" text-anchor="end" dominant-baseline="middle" fill="#8b8fa3" font-size="10">$${label}</text>
                <line x1="${padLeft}" y1="${y.toFixed(1)}" x2="${width - padRight}" y2="${y.toFixed(1)}" stroke="#2e3345" stroke-width="0.5"/>`;
    }).join('\n');

    const isMobile = typeof window !== 'undefined' && window.innerWidth < 600;
    const labelCount = isMobile ? 3 : Math.min(6, filteredData.length);
    const xLabels = [];
    for (let i = 0; i < labelCount; i++) {
        const idx = Math.round(i * (filteredData.length - 1) / (labelCount - 1));
        const x = points[idx].x;
        const d = dates[idx];
        const label = d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
        xLabels.push(`<text x="${x.toFixed(1)}" y="${height - 8}" text-anchor="middle" fill="#8b8fa3" font-size="10">${label}</text>`);
    }

    const dotInterval = Math.max(1, Math.floor(filteredData.length / 15));
    const dots = points
        .filter((_, i) => i % dotInterval === 0 || i === points.length - 1)
        .map(p => `<circle cx="${p.x.toFixed(1)}" cy="${p.y.toFixed(1)}" r="2.5" fill="${lineColor}" stroke="#1a1d27" stroke-width="1"/>`);

    return `<svg viewBox="0 0 ${width} ${height}" xmlns="http://www.w3.org/2000/svg" preserveAspectRatio="xMidYMid meet" style="max-height:280px;">
        <defs>
            <linearGradient id="${gradientId}" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stop-color="${lineColor}" stop-opacity="0.20"/>
                <stop offset="100%" stop-color="${lineColor}" stop-opacity="0.02"/>
            </linearGradient>
        </defs>
        ${yLabels}
        ${xLabels.join('\n')}
        <polygon points="${fillPoints}" fill="url(#${gradientId})"/>
        <polyline points="${polyline}" fill="none" stroke="${lineColor}" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
        ${dots.join('\n')}
    </svg>`;
}

// Shared helpers
function formatDate(iso) {
    const d = new Date(iso);
    const now = new Date();
    const diffMs = now - d;
    const diffMin = Math.floor(diffMs / 60000);
    if (diffMin < 1) return 'just now';
    if (diffMin < 60) return `${diffMin}m ago`;
    const diffH = Math.floor(diffMin / 60);
    if (diffH < 24) return `${diffH}h ago`;
    return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
}

function escapeHtml(str) {
    const div = document.createElement('div');
    div.appendChild(document.createTextNode(str));
    return div.innerHTML;
}

function extractDomain(url) {
    try {
        const hostname = new URL(url).hostname.replace(/^www\./, '');
        return hostname;
    } catch {
        return 'Source';
    }
}

function setStatus(ok) {
    const dot = document.getElementById('statusDot');
    const txt = document.getElementById('statusText');
    dot.className = 'status-dot ' + (ok ? 'ok' : 'err');
    txt.textContent = ok ? 'Connected' : 'Disconnected';
    if (ok) {
        document.getElementById('lastUpdated').textContent = 'Updated ' + new Date().toLocaleTimeString();
    }
}
