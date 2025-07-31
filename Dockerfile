FROM node:22-alpine AS base

WORKDIR /app

# Install dependencies only when needed
COPY package.json package-lock.json* ./
RUN npm ci --omit=dev

# Copy the rest of the app
COPY . .

# Set environment variables for production
ENV NODE_ENV=production

# Expose the port
EXPOSE 3001

# Start the application with tsx and preload New Relic
CMD ["npx", "tsx", "-r", "newrelic", "src/app.ts"]
