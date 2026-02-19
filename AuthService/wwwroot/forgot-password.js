const qs = (id) => document.getElementById(id);

const otpLength = 6;

const state = {
  email: '',
  codeSent: false,
  verifiedCode: '',
  verifying: false,
  resendTimer: null
};

const els = {
  message: qs('message'),
  emailInput: qs('emailInput'),
  sendCodeBtn: qs('sendCodeBtn'),
  resendCodeBtn: qs('resendCodeBtn'),
  flipCard: qs('resetFlipCard'),
  verifiedEmailText: qs('verifiedEmailText'),
  newPassword: qs('newPassword'),
  confirmPassword: qs('confirmPassword'),
  updatePasswordBtn: qs('updatePasswordBtn')
};

const otpInputs = Array.from(document.querySelectorAll('[data-reset-otp-index]'));

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

  if (flow === 'forgot_request') {
    if (code.includes('rate_limited') || status === 429) {
      return 'Too many reset requests. Please wait and try again.';
    }
  }

  if (flow === 'forgot_verify') {
    if (code.includes('invalid_reset_code')) {
      return 'That reset code is invalid.';
    }
    if (code.includes('reset_code_expired')) {
      return 'Reset code expired. Request a new code.';
    }
    if (code.includes('rate_limited') || status === 429) {
      return 'Too many attempts. Please wait and try again.';
    }
  }

  if (flow === 'forgot_submit') {
    if (code.includes('invalid_reset_code')) {
      return 'That reset code is invalid.';
    }
    if (code.includes('reset_code_expired')) {
      return 'Reset code expired. Request a new code.';
    }
    if (code.includes('rate_limited') || status === 429) {
      return 'Too many attempts. Please wait and try again.';
    }
  }

  if (code.includes('database_auth_failed')) {
    return 'Server database connection is not configured correctly.';
  }

  return body?.detail || 'Something went wrong. Please try again.';
};

const buildApiUrl = (path) =>
  typeof window.apiUrl === 'function' ? window.apiUrl(path) : `${window.location.origin}${path}`;

const sanitizeDigit = (value) => (value || '').replace(/\D/g, '').slice(0, 1);

const readCode = () => otpInputs.map((input) => input.value).join('');

const clearOtp = (focusFirst) => {
  otpInputs.forEach((input) => {
    input.value = '';
  });
  if (focusFirst && otpInputs.length > 0) {
    otpInputs[0].focus();
  }
};

const setOtpEnabled = (enabled) => {
  otpInputs.forEach((input) => {
    input.disabled = !enabled;
  });
};

const formatDevCodeHint = (code) => {
  const value = String(code || '').trim();
  return value ? ` Dev code: ${value}` : '';
};

const stopCooldown = () => {
  if (state.resendTimer) {
    clearInterval(state.resendTimer);
    state.resendTimer = null;
  }
  els.resendCodeBtn.disabled = false;
  els.resendCodeBtn.textContent = els.resendCodeBtn.dataset.label;
};

const startCooldown = (seconds) => {
  stopCooldown();
  let remaining = seconds;
  const tick = () => {
    if (remaining <= 0) {
      stopCooldown();
      return;
    }
    els.resendCodeBtn.disabled = true;
    els.resendCodeBtn.textContent = `${els.resendCodeBtn.dataset.label} (${remaining}s)`;
    remaining -= 1;
  };
  tick();
  state.resendTimer = window.setInterval(tick, 1000);
};

const resetFlip = () => {
  state.verifiedCode = '';
  els.flipCard.classList.remove('is-flipped');
  els.newPassword.value = '';
  els.confirmPassword.value = '';
  els.verifiedEmailText.textContent = '';
};

const sendCode = async (messageText, triggerButton) => {
  clearMessage();
  const email = (els.emailInput.value || '').trim();
  if (!email) {
    setMessage('error', 'Please enter your email.');
    return false;
  }

  setBusy(triggerButton, true);
  let cooldownStarted = false;
  try {
    const res = await fetch(buildApiUrl('/auth/forgot-password'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email })
    });

    const body = await parseProblem(res);
    if (!res.ok) {
      setMessage('error', normalizeError(res.status, body, 'forgot_request'));
      return false;
    }

    state.email = email;
    state.codeSent = true;
    state.verifiedCode = '';
    resetFlip();
    setOtpEnabled(true);
    clearOtp(true);
    startCooldown(30);
    cooldownStarted = true;
    setMessage('info', `${messageText}${formatDevCodeHint(body?.resetCode)}`);
    return true;
  } catch {
    setMessage('error', 'Network error. Please try again.');
    return false;
  } finally {
    if (triggerButton !== els.resendCodeBtn || !cooldownStarted) {
      setBusy(triggerButton, false);
    }
  }
};

const verifyCode = async () => {
  if (state.verifying || !state.codeSent) {
    return;
  }

  const email = (els.emailInput.value || '').trim();
  const code = readCode();
  if (!email || code.length !== otpLength) {
    return;
  }

  state.verifying = true;
  try {
    const res = await fetch(buildApiUrl('/auth/verify-reset-code'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, code })
    });

    const body = await parseProblem(res);
    if (!res.ok) {
      state.verifiedCode = '';
      setMessage('error', normalizeError(res.status, body, 'forgot_verify'));
      return;
    }

    state.email = email;
    state.verifiedCode = code;
    els.verifiedEmailText.textContent = `Verified: ${email}`;
    els.flipCard.classList.add('is-flipped');
    setMessage('success', 'Code verified. Set your new password.');
  } catch {
    setMessage('error', 'Network error. Please try again.');
  } finally {
    state.verifying = false;
  }
};

const handleUpdatePassword = async () => {
  clearMessage();

  const email = state.email || (els.emailInput.value || '').trim();
  if (!email) {
    setMessage('error', 'Email is required.');
    return;
  }
  if (!state.verifiedCode || state.verifiedCode.length !== otpLength) {
    setMessage('error', 'Verify the 6-digit code first.');
    return;
  }

  const newPassword = els.newPassword.value || '';
  const confirmPassword = els.confirmPassword.value || '';
  if (!newPassword) {
    setMessage('error', 'Please enter a new password.');
    return;
  }
  if (newPassword.length < 8) {
    setMessage('error', 'Password must be at least 8 characters.');
    return;
  }
  if (newPassword !== confirmPassword) {
    setMessage('error', 'Passwords do not match.');
    return;
  }

  setBusy(els.updatePasswordBtn, true);
  try {
    const res = await fetch(buildApiUrl('/auth/reset-password'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        email,
        code: state.verifiedCode,
        newPassword
      })
    });

    const body = await parseProblem(res);
    if (!res.ok) {
      setMessage('error', normalizeError(res.status, body, 'forgot_submit'));
      return;
    }

    stopCooldown();
    setMessage('success', 'Password reset complete. Redirecting to login...');
    window.setTimeout(() => {
      window.location.href = '/index.html?msg=resetdone';
    }, 1200);
  } catch {
    setMessage('error', 'Network error. Please try again.');
  } finally {
    setBusy(els.updatePasswordBtn, false);
  }
};

const initOtpInputs = () => {
  otpInputs.forEach((input, index) => {
    input.addEventListener('input', () => {
      input.value = sanitizeDigit(input.value);
      if (input.value && index < otpInputs.length - 1) {
        otpInputs[index + 1].focus();
      }
      if (readCode().length === otpLength) {
        void verifyCode();
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
      if (readCode().length === otpLength) {
        void verifyCode();
      }
    });
  });
};

const init = () => {
  els.sendCodeBtn.dataset.label = els.sendCodeBtn.textContent;
  els.resendCodeBtn.dataset.label = els.resendCodeBtn.textContent;
  els.updatePasswordBtn.dataset.label = els.updatePasswordBtn.textContent;

  setOtpEnabled(false);
  initOtpInputs();

  els.sendCodeBtn.addEventListener('click', () => {
    void sendCode('If an account exists for this email, a reset code has been sent.', els.sendCodeBtn);
  });
  els.resendCodeBtn.addEventListener('click', () => {
    void sendCode('If an account exists for this email, a new reset code has been sent.', els.resendCodeBtn);
  });
  els.updatePasswordBtn.addEventListener('click', handleUpdatePassword);

  els.emailInput.addEventListener('input', () => {
    const normalized = (els.emailInput.value || '').trim();
    if (normalized !== state.email) {
      state.email = normalized;
      state.codeSent = false;
      state.verifiedCode = '';
      stopCooldown();
      setOtpEnabled(false);
      clearOtp(false);
      resetFlip();
    }
  });

  const params = new URLSearchParams(window.location.search);
  const email = (params.get('email') || '').trim();
  if (email) {
    els.emailInput.value = email;
    state.email = email;
    void sendCode('If an account exists for this email, a reset code has been sent.', els.sendCodeBtn);
  }
};

init();
