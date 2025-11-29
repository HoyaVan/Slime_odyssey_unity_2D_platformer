const CONFIG = {
  // Session
  SESSION_COOKIE_NAME: 'connect.sid',
  SESSION_MAX_AGE: 1000 * 60 * 60 * 24, // 1 day in milliseconds
  SESSION_COOKIE_SECURE: process.env.NODE_ENV === 'production', // true if https in prod
  SESSION_COOKIE_SAME_SITE: 'lax', // CSRF protection
  
  // Server
  DEFAULT_PORT: 3000,
  
  // CORS Configuration
  // Allow all origins in dev, require specific origin in production
  CORS_ORIGIN: process.env.CORS_ORIGIN || (process.env.NODE_ENV === 'production' ? undefined : true),
  CORS_CREDENTIALS: true, // Allow cookies/sessions for Unity client
  CORS_METHODS: ['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS'],
  CORS_ALLOWED_HEADERS: ['Content-Type', 'Authorization'],
  
  // Database
  DEFAULT_MONGO_DB_NAME: 'game-session-db',
  MYSQL_PORT: process.env.MYSQL_PORT ? Number.parseInt(process.env.MYSQL_PORT, 10) : 3306,
  MYSQL_CONNECTION_LIMIT: 10,
  MYSQL_QUEUE_LIMIT: 0,
  MYSQL_SSL_ENABLED_VALUE: 'true', // String value for MYSQL_SSL env var
  MYSQL_SSL_CONFIG: process.env.MYSQL_SSL === 'true' ? { rejectUnauthorized: false } : false,
  
  // Validation
  PASSWORD_REGEX: /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{10,}$/,
  BCRYPT_ROUNDS: 10,
  
  // Pagination
  DEFAULT_POINT_HISTORY_LIMIT: 50,
  DEFAULT_LEADERBOARD_LIMIT: 100,
  
  // HTTP Status Codes
  STATUS: {
    OK: 200,
    CREATED: 201,
    BAD_REQUEST: 400,
    UNAUTHORIZED: 401,
    NOT_FOUND: 404,
    INTERNAL_SERVER_ERROR: 500,
  },
  
  // Session keys
  SESSION_KEYS: {
    USER_INDEX_ID: 'userIndexId',
    USER_ID: 'userId',
  },
  
  // Table names
  TABLES: {
    USER: 'User',
    POINT: 'Point',
    USER_POINT: 'UserPoint',
  },
  
  // Column names
  COLUMNS: {
    INDEX_ID: 'index_id',
    ID: 'ID',
    PW: 'PW',
    NAME: 'name',
    TOTAL_POINTS: 'total_points',
    POINT_NUM: 'point_num',
    CREATED_AT: 'created_at',
    USER_INDEX_ID: 'user_index_id',
    POINT_INDEX_ID: 'point_index_id',
  },
};

module.exports = {
  CONFIG,
};

