// Simple Node.js static server for Unity WebGL with proper Brotli headers
// Use this if Digital Ocean App Platform static hosting doesn't support _headers file

const express = require('express');
const path = require('path');
const app = express();
const PORT = process.env.PORT || 3000;

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

// Serve index.html for root
app.get('/', (req, res) => {
  res.sendFile(path.join(__dirname, 'index.html'));
});

app.listen(PORT, () => {
  console.log(`Unity WebGL static server running on port ${PORT}`);
  console.log(`Serving files from: ${__dirname}`);
});

