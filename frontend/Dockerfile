# ── Stage 1: build Angular app ───────────────────────────────────────────────
FROM node:20-alpine AS build
WORKDIR /app

# Copy package manifests first for a layer-cached npm install.
# Source changes won't re-run npm ci unless package files change.
COPY package.json package-lock.json ./
RUN npm ci

# Copy the rest of the source and build in production mode
COPY . .
RUN npm run build -- --configuration production

# ── Stage 2: serve with nginx ─────────────────────────────────────────────────
FROM nginx:1.27-alpine AS runtime

# Angular 18 application builder outputs browser bundle to dist/frontend/browser
COPY --from=build /app/dist/frontend/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
