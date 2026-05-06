# Deployment Guide (Render + Vercel)

This project is split into:

- Backend API: `DataEntry.Api` (ASP.NET Core)
- Frontend UI: `dataentry-ui` (Vite + React)

## 1) Deploy backend to Render

1. Push your repo to GitHub.
2. In Render, create a **Web Service** from your GitHub repo.
3. Use these settings:
   - Runtime: Docker
   - Root Directory: `DataEntry.Api`
   - Dockerfile Path: `./Dockerfile`
4. Add environment variables:
   - `ASPNETCORE_ENVIRONMENT=Production`
   - `ASPNETCORE_HTTP_PORTS=8080`
   - `Jwt__Key=<long-random-secret>`
   - `Jwt__Issuer=DataEntry.Api`
   - `Jwt__Audience=DataEntry.UI`
   - `CORS_ORIGINS=https://<your-vercel-domain>`
5. Add a persistent disk:
   - Mount path: `/data`
   - Size: 1 GB (or higher)
6. Set health check path to `/health`.
7. Deploy and copy the Render URL, for example:
   - `https://dataentry-api.onrender.com`

## 2) Deploy frontend to Vercel

1. In Vercel, import the same GitHub repo.
2. Configure project:
   - Framework Preset: Vite
   - Root Directory: `dataentry-ui`
   - Build Command: `npm run build`
   - Output Directory: `dist`
3. Add environment variable:
   - `VITE_API_BASE_URL=https://<your-render-api-domain>/api`
4. Deploy.

## 3) Final CORS update

After Vercel gives you your final production domain:

1. Go back to Render service environment variables.
2. Update `CORS_ORIGINS` to the exact Vercel domain, for example:
   - `https://my-dataentry.vercel.app`
3. Redeploy Render.

## 4) Optional preview domains

If you want Vercel preview deployments to call the API, add multiple comma-separated origins in Render:

`CORS_ORIGINS=https://my-dataentry.vercel.app,https://my-dataentry-git-main-akumar.vercel.app`

## 5) Quick verification

- Backend health: `GET https://<render-domain>/health`
- Login request: `POST https://<render-domain>/api/auth/login`
- Frontend loads and can authenticate without CORS errors
