// ── Credential quick-fill ────────────────────────────────────────────────────
function fillCred(email, password) {
    const emailInput = document.getElementById('email') || document.querySelector('[name="Form.Email"]');
    const pwInput    = document.getElementById('password') || document.querySelector('[name="Form.Password"]');
    if (emailInput) emailInput.value = email;
    if (pwInput)    pwInput.value    = password;
}

// ── Render bar chart ─────────────────────────────────────────────────────────
function renderChart(containerId, dataAttr, colorClass) {
    const container = document.getElementById(containerId);
    if (!container) return;

    const rawData = container.getAttribute(dataAttr);
    if (!rawData) return;

    try {
        const points = JSON.parse(rawData);
        const values = points.map(p => p.value);
        const max    = Math.max(...values, 1);

        container.innerHTML = '';
        points.forEach(p => {
            const pct = Math.round((p.value / max) * 100);
            const col = document.createElement('div');
            col.className = 'bar-col';
            col.innerHTML = `
                <span class="bar-val">${p.value}</span>
                <div class="bar-wrap">
                    <div class="bar ${colorClass}" style="height:${pct}%"></div>
                </div>
                <span class="bar-label">${p.label}</span>`;
            container.appendChild(col);
        });
    } catch (e) {
        console.error('Chart render error:', e);
    }
}

// ── Auto-initialize charts on page load ──────────────────────────────────────
document.addEventListener('DOMContentLoaded', function () {
    renderChart('chart-tasks',  'data-points', 'bar-purple');
    renderChart('chart-hours',  'data-points', 'bar-teal');

    // Velocity select auto-submit
    const velSelect = document.getElementById('vel-days');
    if (velSelect) {
        velSelect.addEventListener('change', function () {
            window.location.href = '/dashboard/velocity?days=' + this.value;
        });
    }
});
