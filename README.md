# auth-login-asp

ASP.NET Core 9 auth service with static frontend pages for login, registration, email verification, and password reset.

## Current Auth UX
- Login and Create Account are on `index.html`.
- Email verification uses a 6-digit code input panel on login.
- Forgot Password opens a separate page: `forgot-password.html`.
- Forgot password flow:
1. Enter email
2. Send code
3. Enter 6-digit reset code in 6 boxes
4. Auto-verify after full code input
5. Card flips to new-password form
6. Submit new password
7. Auto-redirect back to login
- Legacy `/reset-password` page now redirects to the new forgot-password flow.

## UI Preview
![Image #1 - Login](docs/images/image-1-login.png)

![Image #2 - Create Account](docs/images/image-2-create-account.png)

## Tech Stack
- .NET 9 (`net9.0`)
- ASP.NET Core Web API + static file hosting
- EF Core + Pomelo MySQL provider
- JWT access tokens + refresh token cookie
- Optional Redis for cache/rate-limit/session validation
- MailKit for SMTP email sending

## Prerequisites
- .NET SDK 9
- MySQL 8+
- Optional Redis

## Configuration
App settings are in:
- `AuthService/appsettings.json`
- `AuthService/appsettings.Development.json`

Environment variables can be loaded from `.env`:
- `AUTH_DB_CONNECTION` (recommended)
- `MYSQL_ROOT_PASSWORD` (optional replacement if connection string uses `your_password`)

Example template: `AuthService/.env.example`.

Important placeholders to set:
- `Jwt:Keys:0:SecretBase64`
- `Security:Pepper`
- DB connection values
- SMTP values under `Email` if you want real email delivery

## Run Locally
From repo root:

```bash
dotnet restore
dotnet run --project AuthService/AuthService.csproj --launch-profile https
```

Default local URLs:
- App/UI: `https://localhost:7023/index.html`
- Swagger: `https://localhost:7023/swagger`

## Deploy on Render
This repo includes `render.yaml` for this architecture:
- Render Static Site: `auth-log-frontend`
- Render Web Service (API): `auth-log-api`
- Railway MySQL DB (external, connected via `AUTH_DB_CONNECTION`)

### One-time steps
1. Push this repo to GitHub.
2. In Render, create a new **Blueprint** and point it to your repo.
3. Confirm the generated services from `render.yaml`.
4. Deploy the Render services.
5. In Railway, create MySQL and copy its connection string.
6. In Render (`auth-log-api` service), set `AUTH_DB_CONNECTION` to the Railway MySQL URL (`MYSQL_URL` or `DATABASE_URL`) and redeploy.

### Important notes
- API health check path is `/healthz`.
- Static build writes `api-config.js` using `API_BASE_URL` (default in blueprint is `https://auth-log-api.onrender.com`).
- API CORS is set to `https://auth-log-frontend.onrender.com` in blueprint; update `Cors__Origins__0` if your frontend URL differs.
- For cross-site refresh cookies, blueprint sets:
  - `AuthCookie__SameSite=None`
  - `AuthCookie__Secure=true`
- App supports DB config via either:
  - `AUTH_DB_CONNECTION`, or
  - split vars (`DB_HOST`, `DB_PORT`, `DB_NAME`, `DB_USER`, `DB_PASSWORD`, `DB_SSLMODE`).
- `Jwt__Keys__0__SecretBase64` and `Security__Pepper` are generated in Render.
- If using Railway MySQL vars instead of URL, map them to the split DB vars above.
- Render Static + Render Web can run on free plans, but Railway database pricing is separate (check Railway current plan/credit limits before go-live).
- If you use SMTP in production, add:
  - `Email__SmtpHost`
  - `Email__SmtpPort`
  - `Email__UseSsl`
  - `Email__SmtpUser`
  - `Email__SmtpPass`
  - `Email__FromEmail`
  - `Email__FromName`

## API Endpoints (Auth)
Base route: `/auth`

- `POST /register`
- `POST /verify-email`
- `POST /resend-verification-code`
- `POST /login`
- `POST /refresh`
- `POST /logout`
- `GET /me`
- `POST /forgot-password`
- `POST /verify-reset-code`
- `POST /reset-password`

## Code-Based Verification Notes
- Email verification and password reset both use 6-digit codes.
- Reset and verification emails are code text, not links.
- In development with `NoopEmailSender`, debug codes are returned in API responses:
  - `register -> verificationCode`
  - `resend-verification-code -> verificationCode`
  - `forgot-password -> resetCode`
