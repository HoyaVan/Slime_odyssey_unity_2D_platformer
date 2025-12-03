require('dotenv').config();
const express = require('express');
const session = require('express-session');
const MongoStore = require('connect-mongo');
const cors = require('cors');

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
const leaderboardRoutes = require('./routes/leaderboardRoutes');

// Constants
const { ERRORS, SUCCESS, CONSOLE, QUERIES, CONFIG } = require('./constants');

const app = express();
const port = process.env.PORT || CONFIG.DEFAULT_PORT;
const path = require('path');

// Set view engine
app.set('view engine', CONFIG.VIEW_ENGINE);
app.set('views', path.join(__dirname, CONFIG.VIEWS_DIR));

// ---------- Middleware ----------

// Serve static files (CSS, JS, images, etc.)
app.use(CONFIG.STATIC_CSS_PATH, express.static(path.join(__dirname, CONFIG.PUBLIC_DIR, 'css')));

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
const sessionDbName = process.env.MONGO_DB_NAME || CONFIG.DEFAULT_MONGO_DB_NAME;
console.log(`[Session Config] Using MongoDB database: ${sessionDbName}`);

app.use(
  session({
    secret: process.env.SESSION_SECRET,
    resave: false,
    saveUninitialized: true, // Changed to true to save all sessions
    cookie: {
      httpOnly: true,
      secure: CONFIG.SESSION_COOKIE_SECURE,
      sameSite: CONFIG.SESSION_COOKIE_SAME_SITE,
      maxAge: CONFIG.SESSION_MAX_AGE,
    },
    store: (() => {
      const storeConfig = {
        mongoUrl: process.env.MONGO_URL,
        dbName: process.env.MONGO_DB_NAME || CONFIG.DEFAULT_MONGO_DB_NAME,
        collectionName: CONFIG.MONGO_SESSION_COLLECTION,
      };
      
      console.log('[MongoStore] Creating store with config:', {
        dbName: storeConfig.dbName,
        collectionName: storeConfig.collectionName,
        hasCrypto: false // Temporarily disabled due to connect-mongo bug
      });
      
      const store = MongoStore.create(storeConfig);
      
      // Add event listeners for debugging
      store.on('error', (error) => {
        console.error('[MongoStore Error]', error);
        console.error('[MongoStore Error Details]', {
          message: error.message,
          stack: error.stack,
          name: error.name
        });
      });
      
      store.on('connected', () => {
        console.log('[MongoStore] Connected to MongoDB for sessions');
        console.log(`[MongoStore] Database: ${process.env.MONGO_DB_NAME || CONFIG.DEFAULT_MONGO_DB_NAME}`);
        console.log(`[MongoStore] Collection: ${CONFIG.MONGO_SESSION_COLLECTION}`);
      });
      
      store.on('disconnected', () => {
        console.warn('[MongoStore] Disconnected from MongoDB');
      });
      
      return store;
    })(),
  })
);

// ---------- Routes ----------

// Status endpoint
app.get('/', (req, res) => {
  res.send(SUCCESS.SERVER_RUNNING);
});

// Test endpoint to verify session creation
app.get('/test-session', async (req, res) => {
  try {
    // Create a test session
    req.session.testData = { timestamp: new Date().toISOString(), test: true };
    req.session.visited = (req.session.visited || 0) + 1;
    
    console.log(`${CONSOLE.LOG_PREFIX_SESSION_TEST} Session ID: ${req.sessionID}`);
    console.log(`${CONSOLE.LOG_PREFIX_SESSION_TEST} Session data:`, req.session);
    console.log(`${CONSOLE.LOG_PREFIX_SESSION_TEST} Store type:`, req.sessionStore?.constructor?.name);
    
    // Explicitly save the session with promise wrapper
    await new Promise((resolve, reject) => {
      req.session.save((err) => {
        if (err) {
          console.error(`${CONSOLE.LOG_PREFIX_SESSION_TEST} Save error:`, err);
          reject(err);
        } else {
          console.log(`${CONSOLE.LOG_PREFIX_SESSION_TEST} Session saved successfully: ${req.sessionID}`);
          resolve();
        }
      });
    });
    
    // Verify session was saved by trying to get it
    await new Promise((resolve, reject) => {
      req.sessionStore.get(req.sessionID, (err, session) => {
        if (err) {
          console.error(`${CONSOLE.LOG_PREFIX_SESSION_TEST} Get error:`, err);
        } else {
          console.log(`${CONSOLE.LOG_PREFIX_SESSION_TEST} Retrieved session:`, session ? 'Found' : 'Not found');
        }
        resolve(); // Don't reject, just log
      });
    });
    
    res.json({ 
      message: 'Session created! Check MongoDB.',
      sessionId: req.sessionID,
      sessionData: req.session.testData,
      visited: req.session.visited,
      database: process.env.MONGO_DB_NAME || CONFIG.DEFAULT_MONGO_DB_NAME,
      storeType: req.sessionStore?.constructor?.name
    });
  } catch (error) {
    console.error(`${CONSOLE.LOG_PREFIX_SESSION_TEST} Error:`, error);
    res.status(500).json({ 
      error: 'Failed to save session', 
      details: error.message,
      stack: process.env.NODE_ENV === 'development' ? error.stack : undefined
    });
  }
});

// Authentication routes
app.use('/auth', authRoutes);

// Game routes (points, game data, etc.)
app.use('/game', gameRoutes);

// Leaderboard routes
app.use('/leaderboard', leaderboardRoutes);

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

    // 디버깅: 세션 생성 확인
    console.log(`${CONSOLE.LOG_PREFIX_LOGIN} Session created:`, req.sessionID);
    console.log(`${CONSOLE.LOG_PREFIX_LOGIN} Session cookie will be set:`, req.headers.cookie || 'none');
    console.log(`${CONSOLE.LOG_PREFIX_LOGIN} User logged in:`, user[CONFIG.COLUMNS.ID], 'Index:', user[CONFIG.COLUMNS.INDEX_ID]);

    res.json({ 
      message: `${SUCCESS.LOGIN_SUCCESS} ${user[CONFIG.COLUMNS.ID]}.`,
      role: user[CONFIG.COLUMNS.ROLE] || CONFIG.ROLES.PLAYER,
      totalPoints: user[CONFIG.COLUMNS.TOTAL_POINTS],
      sessionId: req.sessionID // 디버깅용: 세션 ID를 응답에 포함
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
