const params = new URLSearchParams(window.location.search);
const email = (params.get('email') || '').trim();
const query = email ? `?email=${encodeURIComponent(email)}` : '';

window.location.replace(`/forgot-password.html${query}`);
