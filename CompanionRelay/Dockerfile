FROM node:22-alpine AS build
WORKDIR /app/CompanionRelay
COPY CompanionRelay/package*.json ./
RUN npm ci
COPY CompanionRelay/ ./
COPY Resources/Companion/ /app/Resources/Companion/
RUN npm run build && npm prune --omit=dev
FROM node:22-alpine
ENV HOST=0.0.0.0 PORT=8787 NODE_ENV=production
WORKDIR /app/CompanionRelay
COPY --from=build /app /app
USER node
EXPOSE 8787
CMD ["node","dist/server.js"]
