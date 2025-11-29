require('dotenv').config();
const express = require('express');
const session = require('express-session');
const MongoStore = require('connect-mongo');

// Validate environment variables
const { validateEnv } = require('./db/validateEnv');
try {
  validateEnv();
} catch (error) {
  console.error('Environment validation failed:', error.message);
  process.exit(1);
}

// Database connections
const { connectMongoDB, closeMongoDB } = require('./db/mongo/connectMongoDB');
const { connectMySQL, closeMySQL } = require('./db/mysql/connectMySQL');

// Routes
const authRoutes = require('./routes/authRoutes');
const gameRoutes = require('./routes/gameRoutes');

// Constants
const { ERRORS, SUCCESS, CONSOLE, QUERIES, CONFIG } = require('./constants');

const app = express();
const port = process.env.PORT || CONFIG.DEFAULT_PORT;
const path = require('path');
const fs = require('fs');

// ---------- Middleware ----------

// Serve static files (CSS, JS, images, etc.)
app.use('/css', express.static(path.join(__dirname, 'public', 'css')));

// CORS Configuration for Unity client
// If CORS_ORIGIN env var is set, use it (supports comma-separated list)
// Otherwise: allow all in dev, require specific origin in production
const corsOrigin = process.env.CORS_ORIGIN 
  ? process.env.CORS_ORIGIN.split(',').map(origin => origin.trim())
  : CONFIG.CORS_ORIGIN;

app.use(cors({
  origin: corsOrigin,
  credentials: CONFIG.CORS_CREDENTIALS,
  methods: CONFIG.CORS_METHODS,
  allowedHeaders: CONFIG.CORS_ALLOWED_HEADERS,
}));

app.use(express.json());
app.use(express.urlencoded({ extended: true }));

// ---- Sessions (Cookies + Mongo) ----
// Sessions are stored in MongoDB as requested
app.use(
  session({
    secret: process.env.SESSION_SECRET,
    resave: false,
    saveUninitialized: false,
    cookie: {
      httpOnly: true,
      secure: CONFIG.SESSION_COOKIE_SECURE,
      sameSite: CONFIG.SESSION_COOKIE_SAME_SITE,
      maxAge: CONFIG.SESSION_MAX_AGE,
    },
    store: MongoStore.create({
      mongoUrl: process.env.MONGO_URL,
      crypto: { secret: process.env.SESSION_CRYPTO_SECRET }, // encrypted in Mongo
    }),
  })
);

// ---------- Routes ----------

// Status endpoint
app.get('/', (req, res) => {
  res.send(SUCCESS.SERVER_RUNNING);
});

// Authentication routes
app.use('/auth', authRoutes);

// Game routes (points, game data, etc.)
app.use('/game', gameRoutes);

// Leaderboard page
app.get('/leaderboard', async (req, res) => {
  try {
    const { getPool } = require('./db/mysql/connectMySQL');
    const { QUERIES, CONFIG } = require('./constants');
    const pool = getPool();
    
    const limit = Number.parseInt(req.query.limit, 10) || CONFIG.DEFAULT_LEADERBOARD_LIMIT;
    
    const [users] = await pool.execute(
      QUERIES.SELECT_LEADERBOARD,
      [limit]
    );

    // Render HTML page with separate template
    const html = generateLeaderboardHTML(users);
    res.send(html);
  } catch (error) {
    console.error('Leaderboard error:', error);
    res.status(CONFIG.STATUS.INTERNAL_SERVER_ERROR).send('<h1>Error loading leaderboard</h1>');
  }
});

// Helper function to generate leaderboard HTML
function generateLeaderboardHTML(users) {
  // Read HTML template
  const templatePath = path.join(__dirname, 'views', 'leaderboard.html');
  let html = fs.readFileSync(templatePath, 'utf8');
  
  // Generate table rows
  const rows = users.map((user, index) => {
    const rank = index + 1;
    const displayName = user.name || user.ID || 'Anonymous';
    const points = user.total_points || 0;
    
    // Medal emojis for top 3
    let rankDisplay = rank;
    if (rank === 1) rankDisplay = '🥇 1';
    else if (rank === 2) rankDisplay = '🥈 2';
    else if (rank === 3) rankDisplay = '🥉 3';
    
    return `
      <tr>
        <td class="rank">${rankDisplay}</td>
        <td class="name">${escapeHtml(displayName)}</td>
        <td class="points">${points.toLocaleString()}</td>
      </tr>
    `;
  }).join('');

  // Generate table HTML
  const tableHTML = users.length > 0 ? `
    <table>
      <thead>
        <tr>
          <th>Rank</th>
          <th>Player</th>
          <th class="points-header">Points</th>
        </tr>
      </thead>
      <tbody>
        ${rows}
      </tbody>
    </table>
  ` : `
    <div class="empty">
      No players yet. Be the first to join!
    </div>
  `;

  // Replace placeholder with actual table
  html = html.replace('{{LEADERBOARD_TABLE}}', tableHTML);
  
  return html;
}

// Helper function to escape HTML
function escapeHtml(text) {
  const str = String(text);
  return str
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}

// Unity-compatible endpoints (matching your GameLogic.cs)
// POST endpoint for login (Unity sends form data to root)
// This matches the Unity GameLogic.cs which posts to root URL
app.post('/', async (req, res, next) => {
  // Extract id and password from form data
  const { id, password } = req.body;
  
  if (!id || !password) {
    return res.status(CONFIG.STATUS.BAD_REQUEST).json({ error: ERRORS.ID_PASSWORD_REQUIRED });
  }

  try {
    const { getPool } = require('./db/mysql/connectMySQL');
    const bcrypt = require('bcrypt');
    const pool = getPool();
    
    // Find user by login ID (using new schema)
    const [users] = await pool.execute(
      QUERIES.SELECT_USER_BY_LOGIN_ID,
      [id]
    );

    if (users.length === 0) {
      return res.status(CONFIG.STATUS.UNAUTHORIZED).json({ error: ERRORS.INVALID_CREDENTIALS });
    }

    const user = users[0];

    // Verify password
    const isValid = await bcrypt.compare(password, user[CONFIG.COLUMNS.PW]);
    if (!isValid) {
      return res.status(CONFIG.STATUS.UNAUTHORIZED).json({ error: ERRORS.INVALID_CREDENTIALS });
    }

    // Create session (handled by MongoDB session store)
    req.session[CONFIG.SESSION_KEYS.USER_INDEX_ID] = user[CONFIG.COLUMNS.INDEX_ID];
    req.session[CONFIG.SESSION_KEYS.USER_ID] = user[CONFIG.COLUMNS.ID]; // Also store login ID for convenience

    res.json({ 
      message: `${SUCCESS.LOGIN_SUCCESS} ${user[CONFIG.COLUMNS.NAME] || user[CONFIG.COLUMNS.ID]}.`,
      totalPoints: user[CONFIG.COLUMNS.TOTAL_POINTS]
    });
  } catch (error) {
    console.error(CONSOLE.LOGIN_ERROR, error);
    res.status(CONFIG.STATUS.INTERNAL_SERVER_ERROR).json({ error: ERRORS.INTERNAL_SERVER_ERROR });
  }
});

// ---------- Initialize connections and start server ----------
async function startServer() {
  try {
    // Connect to MongoDB (for sessions)
    await connectMongoDB();
    
    // Connect to MySQL (for user data and points)
    await connectMySQL();

    // Start Express server
    app.listen(port, () => {
      console.log(`${CONSOLE.SERVER_LISTENING}${port}`);
      console.log(CONSOLE.UNITY_SERVER_READY);
      console.log(`- ${CONSOLE.MONGODB_CONNECTED}`);
      console.log(`- ${CONSOLE.MYSQL_CONNECTED}`);
    });
  } catch (error) {
    console.error(CONSOLE.FAILED_TO_START_SERVER, error);
    process.exit(1);
  }
}

// Graceful shutdown
process.on('SIGTERM', async () => {
  console.log(CONSOLE.SIGTERM_RECEIVED);
  await closeMongoDB();
  await closeMySQL();
  process.exit(0);
});

process.on('SIGINT', async () => {
  console.log(CONSOLE.SIGINT_RECEIVED);
  await closeMongoDB();
  await closeMySQL();
  process.exit(0);
});

// Start the server
startServer();
