const qs = (id) => document.getElementById(id);

const accessTokenKey = 'ps_access_token';
const tenantKey = 'ps_tenant_id';
const meKey = 'ps_me';
let refreshRequestInFlight = null;
let meRequestInFlight = null;

const els = {
  status: qs('status'),
  emailValue: qs('emailValue'),
  tenantValue: qs('tenantValue'),
  userValue: qs('userValue'),
  rolesList: qs('rolesList'),
  permissionsList: qs('permissionsList'),
  refreshBtn: qs('refreshBtn'),
  logoutBtn: qs('logoutBtn')
};

const setStatus = (kind, text) => {
  els.status.className = `status ${kind}`;
  els.status.textContent = text;
  els.status.classList.remove('hidden');
};

const clearStatus = () => {
  els.status.classList.add('hidden');
  els.status.textContent = '';
};

const setBusy = (button, busy) => {
  button.disabled = busy;
  button.textContent = busy ? 'Please wait...' : button.dataset.label;
};

const getAccessToken = () => sessionStorage.getItem(accessTokenKey);

const buildApiUrl = (path) =>
  typeof window.apiUrl === 'function' ? window.apiUrl(path) : `${window.location.origin}${path}`;

const parseResponse = async (res) => {
  const text = await res.text();
  if (!text) return {};
  try {
    return JSON.parse(text);
  } catch {
    return { detail: text };
  }
};

const clearSession = () => {
  sessionStorage.removeItem(accessTokenKey);
  sessionStorage.removeItem(tenantKey);
  sessionStorage.removeItem(meKey);
};

const tryRefreshToken = async () => {
  if (refreshRequestInFlight) {
    return refreshRequestInFlight;
  }

  refreshRequestInFlight = (async () => {
    const res = await fetch(buildApiUrl('/auth/refresh'), {
      method: 'POST',
      credentials: 'include'
    });

    const body = await parseResponse(res);
    if (!res.ok || !body?.accessToken) {
      return null;
    }

    sessionStorage.setItem(accessTokenKey, body.accessToken);
    return body.accessToken;
  })();

  try {
    return await refreshRequestInFlight;
  } finally {
    refreshRequestInFlight = null;
  }
};

const renderPills = (target, values, emptyLabel) => {
  target.innerHTML = '';
  if (!values || values.length === 0) {
    const li = document.createElement('li');
    li.className = 'pill muted';
    li.textContent = emptyLabel;
    target.appendChild(li);
    return;
  }

  for (const value of values) {
    const li = document.createElement('li');
    li.className = 'pill';
    li.textContent = value;
    target.appendChild(li);
  }
};

const renderMe = (me) => {
  els.emailValue.textContent = me?.email || '-';
  els.tenantValue.textContent = me?.tenantId || '-';
  els.userValue.textContent = me?.userId || '-';
  renderPills(els.rolesList, me?.roles, 'No roles');
  renderPills(els.permissionsList, me?.permissions, 'No permissions');
};

const fetchMeWithToken = async (accessToken) => {
  if (!accessToken) {
    return null;
  }

  const res = await fetch(buildApiUrl('/auth/me'), {
    method: 'GET',
    headers: {
      Authorization: `Bearer ${accessToken}`
    },
    credentials: 'include'
  });

  const body = await parseResponse(res);
  if (res.ok) {
    sessionStorage.setItem(meKey, JSON.stringify(body));
    if (body?.tenantId) {
      sessionStorage.setItem(tenantKey, body.tenantId);
    }
    return body;
  }

  if (res.status === 401 || res.status === 403) {
    return null;
  }

  throw new Error(body?.detail || 'Unable to load your account data.');
};

const fetchMe = async () => {
  if (meRequestInFlight) {
    return meRequestInFlight;
  }

  meRequestInFlight = (async () => {
  const accessToken = getAccessToken();
  const direct = await fetchMeWithToken(accessToken);
  if (direct) {
      return direct;
  }

  const refreshedToken = await tryRefreshToken();
  if (!refreshedToken) {
      clearSession();
      window.location.href = '/?msg=expired';
      return null;
  }

  const refreshed = await fetchMeWithToken(refreshedToken);
  if (!refreshed) {
      clearSession();
      window.location.href = '/?msg=expired';
      return null;
  }

  return refreshed;
  })();

  try {
    return await meRequestInFlight;
  } finally {
    meRequestInFlight = null;
  }
};

const handleRefresh = async () => {
  clearStatus();
  setBusy(els.refreshBtn, true);
  try {
    const res = await fetch(buildApiUrl('/auth/refresh'), {
      method: 'POST',
      credentials: 'include'
    });

    const body = await parseResponse(res);
    if (!res.ok || !body?.accessToken) {
      clearSession();
      window.location.href = '/?msg=expired';
      return;
    }

    sessionStorage.setItem(accessTokenKey, body.accessToken);
    const me = await fetchMe();
    if (me) {
      renderMe(me);
      setStatus('success', 'Session refreshed.');
    }
  } catch {
    setStatus('error', 'Could not refresh session. Please sign in again.');
  } finally {
    setBusy(els.refreshBtn, false);
  }
};

const handleLogout = async () => {
  clearStatus();
  setBusy(els.logoutBtn, true);
  try {
    const accessToken = getAccessToken();
    if (accessToken) {
      const tenantId = sessionStorage.getItem(tenantKey);
      const headers = {
        Authorization: `Bearer ${accessToken}`
      };
      if (tenantId) {
        headers['X-Tenant-Id'] = tenantId;
      }

      await fetch(buildApiUrl('/auth/logout'), {
        method: 'POST',
        headers,
        credentials: 'include'
      });
    }
  } finally {
    clearSession();
    window.location.href = '/?msg=signedout';
  }
};

const init = async () => {
  els.refreshBtn.dataset.label = els.refreshBtn.textContent;
  els.logoutBtn.dataset.label = els.logoutBtn.textContent;
  els.refreshBtn.addEventListener('click', handleRefresh);
  els.logoutBtn.addEventListener('click', handleLogout);

  try {
    const me = await fetchMe();
    if (me) {
      renderMe(me);
    }
  } catch (error) {
    setStatus('error', error.message || 'Unable to load dashboard.');
  }
};

init();
