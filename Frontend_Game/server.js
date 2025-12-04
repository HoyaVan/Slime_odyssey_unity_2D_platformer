// Simple Node.js static server for Unity WebGL with proper Brotli headers
// Use this if Digital Ocean App Platform static hosting doesn't support _headers file

const express = require('express');
const path = require('path');
const { createProxyMiddleware } = require('http-proxy-middleware');
const app = express();
const PORT = process.env.PORT || 3000;

// Backend URL - Set this via environment variable in Digital Ocean
// Remove trailing slash to prevent double slashes in proxied URLs
const BACKEND_URL = (process.env.BACKEND_URL || 'https://your-backend-app.ondigitalocean.app').replace(/\/+$/, '');

// Proxy API requests to backend server FIRST (before static files)
// This allows Unity to use relative URLs (e.g., /auth/login) without hardcoding backend URL
// Unity can make requests to /auth, /game, /leaderboard, or POST to / and they'll be forwarded to backend

// Proxy /auth routes - preserve full path when forwarding
app.use('/auth', createProxyMiddleware({
  target: BACKEND_URL,
  changeOrigin: true,
  logLevel: 'debug',
  secure: false,
  // When app.use('/auth', ...) is used, Express strips /auth from req.path
  // So req.path becomes /register, but req.originalUrl is /auth/register
  // We need to add /auth back when forwarding to backend
  pathRewrite: function (path, req) {
    // path is /register (without /auth prefix)
    // We need to forward /auth/register to backend
    return '/auth' + path; // Add /auth prefix back
  },
  onProxyReq: (proxyReq, req, res) => {
    // Log proxy requests for debugging
    const forwardedPath = req.originalUrl; // /auth/register
    const targetUrl = `${BACKEND_URL}${forwardedPath}`;
    console.log(`[Proxy /auth] ${req.method} ${req.originalUrl} -> ${targetUrl}`);
    if (req.body && Object.keys(req.body).length > 0) {
      console.log(`[Proxy /auth] Request body:`, JSON.stringify(req.body));
    }
  },
  onProxyRes: (proxyRes, req, res) => {
    // Log proxy responses
    console.log(`[Proxy /auth] Response ${proxyRes.statusCode} for ${req.method} ${req.originalUrl}`);
  },
  onError: (err, req, res) => {
    console.error('[Proxy Error /auth]', err.message);
    console.error('[Proxy Error] Request was:', req.method, req.originalUrl);
    console.error('[Proxy Error] Backend URL:', BACKEND_URL);
    if (!res.headersSent) {
      res.status(500).json({ error: 'Backend connection failed', details: err.message });
    }
  },
}));

app.use('/game', createProxyMiddleware({
  target: BACKEND_URL,
  changeOrigin: true,
  logLevel: 'debug',
  secure: false,
  onError: (err, req, res) => {
    console.error('[Proxy Error /game]', err.message);
    res.status(500).json({ error: 'Backend connection failed' });
  },
}));

app.use('/leaderboard', createProxyMiddleware({
  target: BACKEND_URL,
  changeOrigin: true,
  logLevel: 'debug',
  secure: false,
  onError: (err, req, res) => {
    console.error('[Proxy Error /leaderboard]', err.message);
    res.status(500).json({ error: 'Backend connection failed' });
  },
}));

// Proxy root POST requests (Unity login endpoint)
// Must be before static files and GET / route
app.post('/', createProxyMiddleware({
  target: BACKEND_URL,
  changeOrigin: true,
  logLevel: 'debug',
  secure: false,
  onError: (err, req, res) => {
    console.error('[Proxy Error]', err.message);
    res.status(500).json({ error: 'Proxy error: ' + err.message });
  },
}));

// Serve static files with custom headers for .br files
app.use((req, res, next) => {
  // Check if the file is a .br file
  if (req.path.endsWith('.br')) {
    // Set Content-Encoding header for Brotli
    res.setHeader('Content-Encoding', 'br');
    
    // Set appropriate Content-Type based on file extension
    if (req.path.endsWith('.wasm.br')) {
      res.setHeader('Content-Type', 'application/wasm');
    } else if (req.path.endsWith('.js.br') || req.path.endsWith('.framework.js.br')) {
      res.setHeader('Content-Type', 'application/javascript');
    } else if (req.path.endsWith('.data.br')) {
      res.setHeader('Content-Type', 'application/octet-stream');
    }
  }
  
  // Disable gzip compression for .br files (they're already compressed)
  if (req.path.endsWith('.br')) {
    res.setHeader('Content-Encoding', 'br');
  }
  
  next();
});

// Serve static files from current directory
app.use(express.static(__dirname, {
  // Don't compress .br files (they're already compressed)
  setHeaders: (res, filePath) => {
    if (filePath.endsWith('.br')) {
      res.setHeader('Content-Encoding', 'br');
      
      if (filePath.endsWith('.wasm.br')) {
        res.setHeader('Content-Type', 'application/wasm');
      } else if (filePath.endsWith('.js.br') || filePath.endsWith('.framework.js.br')) {
        res.setHeader('Content-Type', 'application/javascript');
      } else if (filePath.endsWith('.data.br')) {
        res.setHeader('Content-Type', 'application/octet-stream');
      }
    }
  }
}));

// Serve index.html for root GET (must be last)
app.get('/', (req, res) => {
  res.sendFile(path.join(__dirname, 'index.html'));
});

app.listen(PORT, () => {
  console.log(`Unity WebGL static server running on port ${PORT}`);
  console.log(`Serving files from: ${__dirname}`);
  console.log(`Backend URL: ${BACKEND_URL}`);
});
