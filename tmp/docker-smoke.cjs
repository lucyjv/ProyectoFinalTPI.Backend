const { createRequire } = require('node:module');
const { resolve } = require('node:path');
const requireFrontend = createRequire(resolve('../Web/ProyectoFinalTPI/package.json'));
const { chromium, expect } = requireFrontend('@playwright/test');

(async () => {
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  try {
    const context = await browser.newContext({ permissions: ['geolocation'], geolocation: { latitude: -34.6, longitude: -58.4 } });
    const page = await context.newPage();
    const failures = [];
    page.on('response', response => {
      if (response.url().startsWith('http://localhost:5199') && response.status() >= 500)
        failures.push({ url: response.url(), status: response.status() });
    });
    await page.goto('http://localhost:3001/auth/login?returnTo=%2Fexplorar%3Fera%3D1990');
    await page.getByRole('textbox', { name: 'Email', exact: true }).fill('miguel@example.com');
    await page.locator('input[name=contrasena]').fill('password123');
    const login = page.waitForResponse(r => r.url() === 'http://localhost:5199/api/auth/login' && r.request().method() === 'POST');
    await page.getByRole('button', { name: 'Ingresar', exact: true }).click();
    const loginResponse = await login;
    expect(loginResponse.status()).toBe(200);
    const session = await loginResponse.json();
    await expect(page.getByRole('button', { name: 'Abrir menú de usuario', exact: true }).first()).toBeVisible({ timeout: 30000 });
    const cookies = await context.cookies('http://localhost:5199');
    expect(cookies.some(c => c.name === '__Host-Nostalgiar.Refresh' && c.httpOnly && c.secure)).toBe(true);
    const checks = await page.evaluate(async ({ token, usuarioId }) => {
      const base = 'http://localhost:5199';
      const headers = { Authorization: `Bearer ${token}`, 'X-Nostalgiar-Request': '1' };
      const profile = await fetch(base + '/api/usuario/me/perfil', { headers, credentials: 'include' });
      const map = await fetch(base + '/api/publicaciones/mapa?minLatitud=-35&maxLatitud=-34&minLongitud=-59&maxLongitud=-58');
      const mapData = await map.json();
      const feed = await fetch(base + '/api/feed/para-vos?limite=5', { headers });
      const target = usuarioId === 1 ? 2 : 1;
      const follow = await fetch(base + `/api/seguimientos/${target}`, { method: 'POST', headers });
      const unfollow = await fetch(base + `/api/seguimientos/${target}`, { method: 'DELETE', headers });
      const refresh = await fetch(base + '/api/auth/refresh', { method: 'POST', headers: { 'X-Nostalgiar-Request': '1' }, credentials: 'include' });
      return { profile: profile.status, map: map.status, mapKeys: Object.keys(mapData), feed: feed.status, follow: follow.status, unfollow: unfollow.status, refresh: refresh.status };
    }, session);
    for (const key of ['profile', 'map', 'feed', 'refresh']) expect(checks[key], key).toBe(200);
    expect([200, 201]).toContain(checks.follow);
    expect([200, 204]).toContain(checks.unfollow);
    await page.reload();
    await expect(page.getByRole('button', { name: 'Abrir menú de usuario', exact: true }).first()).toBeVisible({ timeout: 30000 });
    await page.screenshot({ path: 'tmp/docker-smoke.png', fullPage: true });
    expect(failures).toEqual([]);
    console.log(JSON.stringify({ login: loginResponse.status(), cookie: 'HttpOnly/Secure persistida', ...checks, reloadAuthenticated: true, backend500: failures }, null, 2));
  } finally { await browser.close(); }
})().catch(error => { console.error(error.message); process.exitCode = 1; });
