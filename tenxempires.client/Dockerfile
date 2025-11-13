# Build stage
FROM node:20-alpine AS build
WORKDIR /app

# Accept build argument for API base URL (optional - if not set, uses relative paths via nginx proxy)
# For production with nginx proxy, leave this unset so frontend uses relative /api paths
ARG VITE_API_BASE_URL
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}

# Copy package files
COPY package*.json ./
RUN npm ci

# Copy source and build
COPY . .
RUN npm run build

# Production stage with nginx
FROM nginx:alpine
WORKDIR /usr/share/nginx/html

# Copy built files
COPY --from=build /app/dist /usr/share/nginx/html

# Copy nginx config (optional, for SPA routing)
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Expose port 80
EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]

