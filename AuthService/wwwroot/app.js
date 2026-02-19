const qs = (id) => document.getElementById(id);

const accessTokenKey = 'ps_access_token';
const tenantKey = 'ps_tenant_id';
const meKey = 'ps_me';
const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

const els = {
  tabLogin: qs('tabLogin'),
  tabRegister: qs('tabRegister'),
  loginForm: qs('loginForm'),
  registerForm: qs('registerForm'),
  message: qs('message'),
  emailVerifyBanner: qs('emailVerifyBanner'),
  emailVerifyText: qs('emailVerifyText'),
  verifyEmailBtn: qs('verifyEmailBtn'),
  loginEmail: qs('loginEmail'),
  loginPassword: qs('loginPassword'),
  loginBtn: qs('loginBtn'),
  regEmail: qs('regEmail'),
  regPassword: qs('regPassword'),
  regConfirmPassword: qs('regConfirmPassword'),
  regWorkspace: qs('regWorkspace'),
  registerBtn: qs('registerBtn')
};

const setMessage = (kind, text) => {
  els.message.className = `message ${kind}`;
  els.message.textContent = text;
  els.message.classList.remove('hidden');
};

const clearMessage = () => {
  els.message.classList.add('hidden');
  els.message.textContent = '';
};

const setBusy = (button, busy) => {
  button.disabled = busy;
  button.textContent = busy ? 'Please wait...' : button.dataset.label;
};

const setView = (view) => {
  const loginActive = view === 'login';
  els.tabLogin.classList.toggle('active', loginActive);
  els.tabRegister.classList.toggle('active', !loginActive);
  els.tabLogin.setAttribute('aria-selected', String(loginActive));
  els.tabRegister.setAttribute('aria-selected', String(!loginActive));
  els.loginForm.classList.toggle('hidden', !loginActive);
  els.registerForm.classList.toggle('hidden', loginActive);
  clearMessage();
};

const parseProblem = async (res) => {
  const text = await res.text();
  if (!text) return {};
  try {
    return JSON.parse(text);
  } catch {
    return { detail: text };
  }
};

const normalizeError = (status, body, flow) => {
  const code = String(body?.code || body?.title || '').toLowerCase();

  if (flow === 'login') {
    if (code.includes('invalid_credentials') || status === 401) {
      return 'Email or password is incorrect.';
    }
    if (code.includes('email_not_verified')) {
      return 'Please verify your email before logging in.';
    }
    if (code.includes('tenant_access_denied')) {
      return 'This account is not linked to a valid workspace yet.';
    }
  }

  if (flow === 'register') {
    if (code.includes('email_in_use') || status === 409) {
      return 'That email is already registered. Try signing in instead.';
    }
    if (code.includes('tenant_name_in_use')) {
      return 'Workspace name is already taken. Choose a different name.';
    }
  }

  if (code.includes('database_auth_failed')) {
    return 'Server database connection is not configured correctly.';
  }

  return body?.detail || 'Something went wrong. Please try again.';
};

const getTenant = () => {
  const raw = sessionStorage.getItem(tenantKey);
  if (!raw) {
    return null;
  }

  const value = raw.trim();
  if (!value || value.toLowerCase() === 'null' || value.toLowerCase() === 'undefined') {
    sessionStorage.removeItem(tenantKey);
    return null;
  }

  if (!guidPattern.test(value)) {
    sessionStorage.removeItem(tenantKey);
    return null;
  }

  return value;
};
const setTenant = (tenantId) => {
  if (tenantId) {
    sessionStorage.setItem(tenantKey, tenantId);
  }
};

const buildApiUrl = (path) => `${window.location.origin}${path}`;

const parseJwtPayload = (token) => {
  try {
    const parts = token.split('.');
    if (parts.length !== 3) return null;
    const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64 + '='.repeat((4 - (base64.length % 4)) % 4);
    const json = atob(padded);
    return JSON.parse(json);
  } catch {
    return null;
  }
};

const handleRegister = async (event) => {
  event.preventDefault();
  clearMessage();

  const email = (els.regEmail.value || '').trim();
  const password = els.regPassword.value || '';
  const confirmPassword = els.regConfirmPassword.value || '';
  const workspace = (els.regWorkspace.value || '').trim();

  if (!email) {
    setMessage('error', 'Please enter your email.');
    return;
  }
  if (!password) {
    setMessage('error', 'Please create a password.');
    return;
  }
  if (password.length < 8) {
    setMessage('error', 'Password must be at least 8 characters.');
    return;
  }
  if (password !== confirmPassword) {
    setMessage('error', 'Passwords do not match.');
    return;
  }
  if (!workspace) {
    setMessage('error', 'Please enter your workspace name.');
    return;
  }

  setBusy(els.registerBtn, true);
  try {
    const payload = {
      email,
      password,
      tenantName: workspace,
      tenantId: null
    };

    const res = await fetch(buildApiUrl('/auth/register'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });

    const body = await parseProblem(res);
    if (!res.ok) {
      setMessage('error', normalizeError(res.status, body, 'register'));
      return;
    }

    setTenant(body?.tenantId);
    els.loginEmail.value = email;
    els.loginPassword.value = '';
    setView('login');
    setMessage(
      'success',
      'Account created. Check your email for the verification link, then sign in.'
    );
  } catch {
    setMessage('error', 'Network error. Please try again.');
  } finally {
    setBusy(els.registerBtn, false);
  }
};

const handleLogin = async (event) => {
  event.preventDefault();
  clearMessage();

  const email = (els.loginEmail.value || '').trim();
  const password = els.loginPassword.value || '';
  const tenantId = getTenant();

  if (!email) {
    setMessage('error', 'Please enter your email.');
    return;
  }
  if (!password) {
    setMessage('error', 'Please enter your password.');
    return;
  }
  setBusy(els.loginBtn, true);
  try {
    const payload = {
      email,
      password,
      tenantId: tenantId || null
    };
    const res = await fetch(buildApiUrl('/auth/login'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify(payload)
    });

    const body = await parseProblem(res);
    if (!res.ok) {
      setMessage('error', normalizeError(res.status, body, 'login'));
      return;
    }

    if (!body?.accessToken) {
      setMessage('error', 'Login succeeded but no access token was returned.');
      return;
    }

    sessionStorage.setItem(accessTokenKey, body.accessToken);
    const tokenPayload = parseJwtPayload(body.accessToken);
    const tokenTenantId = tokenPayload?.tid;
    if (typeof tokenTenantId === 'string' && guidPattern.test(tokenTenantId)) {
      setTenant(tokenTenantId);
    }
    sessionStorage.removeItem(meKey);
    window.location.href = '/dashboard.html';
  } catch {
    setMessage('error', 'Network error. Please try again.');
  } finally {
    setBusy(els.loginBtn, false);
  }
};

const setupEmailVerificationBanner = () => {
  const params = new URLSearchParams(window.location.search);
  const token = params.get('token');
  const email = params.get('email');

  if (!token) return;

  if (email) {
    els.loginEmail.value = email;
  }

  els.emailVerifyText.textContent = 'Your verification link is ready. Click below to verify this email.';
  els.emailVerifyBanner.classList.remove('hidden');

  els.verifyEmailBtn.addEventListener('click', async () => {
    clearMessage();
    setBusy(els.verifyEmailBtn, true);

    try {
      const res = await fetch(buildApiUrl('/auth/verify-email'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token })
      });

      const body = await parseProblem(res);
      if (!res.ok) {
        setMessage('error', normalizeError(res.status, body, 'verify'));
        return;
      }

      els.emailVerifyBanner.classList.add('hidden');
      setMessage('success', 'Email verified successfully. You can now sign in.');
      window.history.replaceState({}, document.title, window.location.pathname);
    } catch {
      setMessage('error', 'Unable to verify right now. Please try again.');
    } finally {
      setBusy(els.verifyEmailBtn, false);
    }
  });
};

const init = () => {
  els.loginBtn.dataset.label = els.loginBtn.textContent;
  els.registerBtn.dataset.label = els.registerBtn.textContent;
  els.verifyEmailBtn.dataset.label = els.verifyEmailBtn.textContent;

  els.tabLogin.addEventListener('click', () => setView('login'));
  els.tabRegister.addEventListener('click', () => setView('register'));
  els.loginForm.addEventListener('submit', handleLogin);
  els.registerForm.addEventListener('submit', handleRegister);

  setupEmailVerificationBanner();
  setView('login');
  const params = new URLSearchParams(window.location.search);
  const messageCode = params.get('msg');
  const hasToken = !!sessionStorage.getItem(accessTokenKey);
  if (messageCode === 'signedout') {
    setMessage('success', 'You are signed out.');
    window.history.replaceState({}, document.title, window.location.pathname);
  } else if (messageCode === 'expired' && !hasToken) {
    setMessage('info', 'Your session expired. Please sign in again.');
    window.history.replaceState({}, document.title, window.location.pathname);
  } else if (messageCode) {
    window.history.replaceState({}, document.title, window.location.pathname);
  }
};

init();
