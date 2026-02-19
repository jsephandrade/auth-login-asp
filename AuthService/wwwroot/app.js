const qs = (id) => document.getElementById(id);

const accessTokenKey = 'ps_access_token';
const tenantKey = 'ps_tenant_id';
const meKey = 'ps_me';
const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const otpLength = 6;

const verifyState = {
  email: '',
  resendTimer: null
};

const els = {
  tabLogin: qs('tabLogin'),
  tabRegister: qs('tabRegister'),
  loginForm: qs('loginForm'),
  registerForm: qs('registerForm'),
  message: qs('message'),
  emailVerifyPanel: qs('emailVerifyPanel'),
  emailVerifyText: qs('emailVerifyText'),
  verifyEmailInput: qs('verifyEmailInput'),
  verifyCodeBtn: qs('verifyCodeBtn'),
  resendCodeBtn: qs('resendCodeBtn'),
  loginEmail: qs('loginEmail'),
  loginPassword: qs('loginPassword'),
  forgotBtn: qs('forgotBtn'),
  loginBtn: qs('loginBtn'),
  regEmail: qs('regEmail'),
  regPassword: qs('regPassword'),
  regConfirmPassword: qs('regConfirmPassword'),
  regWorkspace: qs('regWorkspace'),
  registerBtn: qs('registerBtn')
};

const otpInputs = Array.from(document.querySelectorAll('[data-otp-index]'));

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

  if (flow === 'verify') {
    if (code.includes('invalid_verification_code')) {
      return 'That verification code is invalid.';
    }
    if (code.includes('verification_code_expired')) {
      return 'Your verification code expired. Request a new code.';
    }
    if (code.includes('rate_limited') || status === 429) {
      return 'Too many verification attempts. Please wait and try again.';
    }
  }

  if (flow === 'verify_resend') {
    if (code.includes('rate_limited') || status === 429) {
      return 'Too many resend attempts. Please wait and try again.';
    }
  }

  if (code.includes('database_auth_failed')) {
    return 'Server database connection is not configured correctly.';
  }

  return body?.detail || 'Something went wrong. Please try again.';
};

const getTenant = () => {
  const raw = sessionStorage.getItem(tenantKey);
  if (!raw) return null;

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

const sanitizeDigit = (value) => (value || '').replace(/\D/g, '').slice(0, 1);

const clearOtpInputs = (focusFirst) => {
  otpInputs.forEach((input) => {
    input.value = '';
  });
  if (focusFirst && otpInputs.length > 0) {
    otpInputs[0].focus();
  }
};

const getOtpCode = () => otpInputs.map((input) => input.value).join('');

const stopResendCooldown = () => {
  if (verifyState.resendTimer) {
    clearInterval(verifyState.resendTimer);
    verifyState.resendTimer = null;
  }
  els.resendCodeBtn.disabled = false;
  els.resendCodeBtn.textContent = els.resendCodeBtn.dataset.label;
};

const startResendCooldown = (seconds) => {
  stopResendCooldown();
  let remaining = seconds;
  const tick = () => {
    if (remaining <= 0) {
      stopResendCooldown();
      return;
    }
    els.resendCodeBtn.disabled = true;
    els.resendCodeBtn.textContent = `${els.resendCodeBtn.dataset.label} (${remaining}s)`;
    remaining -= 1;
  };
  tick();
  verifyState.resendTimer = window.setInterval(tick, 1000);
};

const formatDevCodeHint = (code) => {
  const value = String(code || '').trim();
  return value ? ` Dev code: ${value}` : '';
};

const getVerificationEmail = () => {
  const panelEmail = (els.verifyEmailInput.value || '').trim();
  if (panelEmail) return panelEmail;
  if (verifyState.email) return verifyState.email;

  const loginEmail = (els.loginEmail.value || '').trim();
  if (loginEmail) return loginEmail;

  return (els.regEmail.value || '').trim();
};

const openVerificationPanel = (email, text) => {
  const normalizedEmail = (email || '').trim();
  verifyState.email = normalizedEmail;
  els.verifyEmailInput.value = normalizedEmail;
  els.emailVerifyText.textContent = text || 'Enter the 6-digit code sent to your email.';
  els.emailVerifyPanel.classList.remove('hidden');
  clearOtpInputs(true);
};

const setView = (view) => {
  const loginActive = view === 'login';
  els.tabLogin.classList.toggle('active', loginActive);
  els.tabRegister.classList.toggle('active', !loginActive);
  els.tabLogin.setAttribute('aria-selected', String(loginActive));
  els.tabRegister.setAttribute('aria-selected', String(!loginActive));
  els.loginForm.classList.toggle('hidden', !loginActive);
  els.registerForm.classList.toggle('hidden', loginActive);
  if (!loginActive) {
    els.emailVerifyPanel.classList.add('hidden');
  } else if (verifyState.email || (els.verifyEmailInput.value || '').trim()) {
    els.emailVerifyPanel.classList.remove('hidden');
  }
  clearMessage();
};

const initOtpInputs = () => {
  otpInputs.forEach((input, index) => {
    input.addEventListener('input', () => {
      input.value = sanitizeDigit(input.value);
      if (input.value && index < otpInputs.length - 1) {
        otpInputs[index + 1].focus();
      }
    });

    input.addEventListener('keydown', (event) => {
      if (event.key === 'Backspace' && !input.value && index > 0) {
        otpInputs[index - 1].focus();
      }
    });

    input.addEventListener('paste', (event) => {
      const pasted = event.clipboardData?.getData('text') || '';
      const digits = pasted.replace(/\D/g, '').slice(0, otpLength);
      if (!digits) return;

      event.preventDefault();
      digits.split('').forEach((digit, offset) => {
        const target = otpInputs[index + offset];
        if (target) {
          target.value = digit;
        }
      });
      const focusIndex = Math.min(index + digits.length, otpInputs.length - 1);
      otpInputs[focusIndex].focus();
    });
  });
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
    const res = await fetch(buildApiUrl('/auth/register'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        email,
        password,
        tenantName: workspace,
        tenantId: null
      })
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
    openVerificationPanel(email, 'Account created. Enter the 6-digit code sent to your email.');
    setMessage('success', `Account created. Verify your email to continue.${formatDevCodeHint(body?.verificationCode)}`);
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
    const res = await fetch(buildApiUrl('/auth/login'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify({
        email,
        password,
        tenantId: tenantId || null
      })
    });

    const body = await parseProblem(res);
    if (!res.ok) {
      const code = String(body?.code || body?.title || '').toLowerCase();
      if (code.includes('email_not_verified')) {
        openVerificationPanel(email, 'Your email is not verified. Enter the latest 6-digit code we sent.');
        setMessage('info', 'Enter your verification code, then sign in again.');
        return;
      }

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

const handleForgotPassword = () => {
  const email = (els.loginEmail.value || '').trim();
  const query = email ? `?email=${encodeURIComponent(email)}` : '';
  window.location.href = `/forgot-password.html${query}`;
};

const handleVerifyCode = async () => {
  clearMessage();

  const email = getVerificationEmail();
  if (!email) {
    setMessage('error', 'Enter your email first.');
    return;
  }

  const code = getOtpCode();
  if (code.length !== otpLength) {
    setMessage('error', 'Please enter the full 6-digit verification code.');
    return;
  }

  setBusy(els.verifyCodeBtn, true);
  try {
    const res = await fetch(buildApiUrl('/auth/verify-email'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, code })
    });

    const body = await parseProblem(res);
    if (!res.ok) {
      setMessage('error', normalizeError(res.status, body, 'verify'));
      return;
    }

    verifyState.email = '';
    els.verifyEmailInput.value = '';
    els.emailVerifyPanel.classList.add('hidden');
    stopResendCooldown();
    els.loginEmail.value = email;
    clearOtpInputs(false);
    setMessage('success', 'Email verified successfully. You can now sign in.');
  } catch {
    setMessage('error', 'Network error. Please try again.');
  } finally {
    setBusy(els.verifyCodeBtn, false);
  }
};

const handleResendCode = async () => {
  clearMessage();

  const email = getVerificationEmail();
  if (!email) {
    setMessage('error', 'Enter your email first.');
    return;
  }

  setBusy(els.resendCodeBtn, true);
  let cooldownStarted = false;
  try {
    const res = await fetch(buildApiUrl('/auth/resend-verification-code'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email })
    });

    const body = await parseProblem(res);
    if (!res.ok) {
      setMessage('error', normalizeError(res.status, body, 'verify_resend'));
      return;
    }

    verifyState.email = email;
    els.verifyEmailInput.value = email;
    els.emailVerifyPanel.classList.remove('hidden');
    els.emailVerifyText.textContent = 'Enter the latest 6-digit code sent to your email.';
    clearOtpInputs(true);
    setMessage('info', `If an unverified account exists for this email, a new code has been sent.${formatDevCodeHint(body?.verificationCode)}`);
    startResendCooldown(30);
    cooldownStarted = true;
  } catch {
    setMessage('error', 'Network error. Please try again.');
  } finally {
    if (!cooldownStarted) {
      setBusy(els.resendCodeBtn, false);
    }
  }
};

const init = () => {
  els.loginBtn.dataset.label = els.loginBtn.textContent;
  els.forgotBtn.dataset.label = els.forgotBtn.textContent;
  els.registerBtn.dataset.label = els.registerBtn.textContent;
  els.verifyCodeBtn.dataset.label = els.verifyCodeBtn.textContent;
  els.resendCodeBtn.dataset.label = els.resendCodeBtn.textContent;

  els.tabLogin.addEventListener('click', () => setView('login'));
  els.tabRegister.addEventListener('click', () => setView('register'));
  els.forgotBtn.addEventListener('click', handleForgotPassword);
  els.loginForm.addEventListener('submit', handleLogin);
  els.registerForm.addEventListener('submit', handleRegister);
  els.verifyCodeBtn.addEventListener('click', handleVerifyCode);
  els.resendCodeBtn.addEventListener('click', handleResendCode);

  initOtpInputs();
  setView('login');

  const params = new URLSearchParams(window.location.search);
  const legacyToken = params.get('token');
  if (legacyToken) {
    const legacyEmail = (params.get('email') || '').trim();
    const query = legacyEmail ? `?email=${encodeURIComponent(legacyEmail)}` : '';
    window.location.replace(`/forgot-password.html${query}`);
    return;
  }

  const messageCode = params.get('msg');
  const hasToken = !!sessionStorage.getItem(accessTokenKey);
  if (messageCode === 'signedout') {
    setMessage('success', 'You are signed out.');
    window.history.replaceState({}, document.title, window.location.pathname);
  } else if (messageCode === 'expired' && !hasToken) {
    setMessage('info', 'Your session expired. Please sign in again.');
    window.history.replaceState({}, document.title, window.location.pathname);
  } else if (messageCode === 'resetdone') {
    setMessage('success', 'Password reset complete. You can now sign in.');
    window.history.replaceState({}, document.title, window.location.pathname);
  } else if (messageCode) {
    window.history.replaceState({}, document.title, window.location.pathname);
  }
};

init();
