# Digital Ocean Deployment Guide for Unity WebGL Game

This guide will help you deploy your Unity WebGL game (`MyFirstGameForHosting`) to Digital Ocean.

## Option 1: Static Hosting (Unity Game Only)

This is the simplest option if you only need to host the Unity WebGL build.

### Step 1: Create a Digital Ocean Droplet

1. Log in to Digital Ocean
2. Click "Create" → "Droplets"
3. Choose:
   - **Image**: Ubuntu 22.04 LTS (or latest)
   - **Plan**: Basic ($4/month is fine for testing)
   - **Region**: Choose closest to your users
   - **Authentication**: SSH keys (recommended) or password
4. Click "Create Droplet"

### Step 2: Connect to Your Droplet

```bash
ssh root@YOUR_DROPLET_IP
```

### Step 3: Install Nginx

```bash
# Update package list
apt update

# Install Nginx
apt install nginx -y

# Start and enable Nginx
systemctl start nginx
systemctl enable nginx
```

### Step 4: Upload Your Unity Build

**Option A: Using SCP (from your local machine)**
```bash
# From your local machine (Windows PowerShell)
scp -r MyFirstGameForHosting root@YOUR_DROPLET_IP:/var/www/html/
```

**Option B: Using Git**
```bash
# On the droplet
cd /var/www/html
git clone YOUR_REPO_URL
cd YOUR_REPO_NAME
mv MyFirstGameForHosting/* .
```

**Option C: Using SFTP client** (like FileZilla or WinSCP)

### Step 5: Configure Nginx

The nginx configuration file is already created for you (`nginx-unity.conf`). Copy it:

```bash
cp /var/www/html/nginx-unity.conf /etc/nginx/sites-available/unity-game
ln -s /etc/nginx/sites-available/unity-game /etc/nginx/sites-enabled/
rm /etc/nginx/sites-enabled/default  # Remove default site
nginx -t  # Test configuration
systemctl reload nginx
```

### Step 6: Set Up SSL (Optional but Recommended)

```bash
# Install Certbot
apt install certbot python3-certbot-nginx -y

# Get SSL certificate (replace with your domain)
certbot --nginx -d yourdomain.com -d www.yourdomain.com

# Auto-renewal is set up automatically
```

### Step 7: Access Your Game

Visit `http://YOUR_DROPLET_IP` or `https://yourdomain.com` in your browser!

---

## Option 2: Full Stack Deployment (Unity Game + Backend Server)

If you need to host both the Unity game and your Node.js backend server.

### Step 1-3: Same as Option 1

### Step 4: Install Node.js

```bash
# Install Node.js 20.x
curl -fsSL https://deb.nodesource.com/setup_20.x | bash -
apt install -y nodejs

# Verify installation
node --version
npm --version
```

### Step 5: Upload Your Project

```bash
# Create project directory
mkdir -p /var/www/game-server
cd /var/www/game-server

# Upload your project files (using SCP, Git, or SFTP)
# Make sure to upload:
# - app.js
# - package.json
# - All routes, middleware, db folders
# - MyFirstGameForHosting folder
```

### Step 6: Set Up Backend Server

```bash
cd /var/www/game-server

# Install dependencies
npm install

# Create .env file with your database credentials
nano .env
# Add your MySQL and MongoDB connection strings

# Test the server
npm start
```

### Step 7: Set Up PM2 (Process Manager)

```bash
# Install PM2 globally
npm install -g pm2

# Start your server with PM2
cd /var/www/game-server
pm2 start app.js --name "game-server"

# Make PM2 start on boot
pm2 startup
pm2 save
```

### Step 8: Configure Nginx as Reverse Proxy

Use the `nginx-fullstack.conf` configuration file:

```bash
cp /var/www/game-server/nginx-fullstack.conf /etc/nginx/sites-available/game-server
ln -s /etc/nginx/sites-available/game-server /etc/nginx/sites-enabled/
rm /etc/nginx/sites-enabled/default
nginx -t
systemctl reload nginx
```

### Step 9: Set Up Database

If you're using Digital Ocean Managed Databases:
- Create MySQL and MongoDB databases in Digital Ocean dashboard
- Update your `.env` file with the connection strings

If hosting databases on the droplet:
```bash
# MySQL
apt install mysql-server -y
mysql_secure_installation

# MongoDB
curl -fsSL https://www.mongodb.org/static/pgp/server-7.0.asc | gpg -o /usr/share/keyrings/mongodb-server-7.0.gpg --dearmor
echo "deb [ arch=amd64,arm64 signed-by=/usr/share/keyrings/mongodb-server-7.0.gpg ] https://repo.mongodb.org/apt/ubuntu jammy/mongodb-org/7.0 multiverse" | tee /etc/apt/sources.list.d/mongodb-org-7.0.list
apt update
apt install -y mongodb-org
systemctl start mongod
systemctl enable mongod
```

---

## Important Notes

### MIME Types for Unity WebGL
Unity WebGL requires specific MIME types. The nginx config includes these:
- `.wasm` → `application/wasm`
- `.br` (Brotli) → `application/octet-stream` or `application/wasm` (for .wasm.br)
- `.data` → `application/octet-stream`

### CORS Configuration
If your Unity game makes API calls to your backend, ensure CORS is properly configured in your Express server.

### File Permissions
```bash
# Set proper permissions
chown -R www-data:www-data /var/www/html
chmod -R 755 /var/www/html
```

### Firewall Configuration
```bash
# Allow HTTP, HTTPS, and SSH
ufw allow 22/tcp
ufw allow 80/tcp
ufw allow 443/tcp
ufw enable
```

---

## Troubleshooting

### Check Nginx Logs
```bash
tail -f /var/log/nginx/error.log
tail -f /var/log/nginx/access.log
```

### Check PM2 Logs (if using backend)
```bash
pm2 logs game-server
```

### Test Nginx Configuration
```bash
nginx -t
```

### Restart Services
```bash
systemctl restart nginx
pm2 restart game-server  # If using backend
```

---

## Quick Reference Commands

```bash
# View running processes
pm2 list

# Restart server
pm2 restart game-server

# View logs
pm2 logs game-server

# Stop server
pm2 stop game-server

# Reload Nginx
systemctl reload nginx

# Check Nginx status
systemctl status nginx
```

