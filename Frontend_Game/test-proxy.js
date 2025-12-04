// Test script to verify proxy configuration
// This helps debug proxy routing issues

const express = require('express');
const { createProxyMiddleware } = require('http-proxy-middleware');

const app = express();
const BACKEND_URL = process.env.BACKEND_URL || 'https://backend-server-xsazd.ondigitalocean.app';

// Test: Log all incoming requests
app.use((req, res, next) => {
  console.log(`[Request] ${req.method} ${req.path} | originalUrl: ${req.originalUrl}`);
  next();
});

// Proxy /auth with explicit path handling
app.use('/auth', createProxyMiddleware({
  target: BACKEND_URL,
  changeOrigin: true,
  logLevel: 'debug',
  secure: false,
  onProxyReq: (proxyReq, req, res) => {
    console.log(`[Proxy] Forwarding: ${req.method} ${req.originalUrl}`);
    console.log(`[Proxy] Target: ${BACKEND_URL}${req.originalUrl}`);
  },
}));

app.listen(3000, () => {
  console.log('Test proxy server running on port 3000');
  console.log(`Backend URL: ${BACKEND_URL}`);
});

