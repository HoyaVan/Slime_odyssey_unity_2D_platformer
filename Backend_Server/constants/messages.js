const ERRORS = {
  // Authentication
  ID_PASSWORD_REQUIRED: 'ID and password are required.',
  INVALID_CREDENTIALS: 'Invalid ID or password.',
  ID_ALREADY_EXISTS: 'ID already exists.',
  SESSION_INVALID: 'Session invalid.',
  NOT_LOGGED_IN: 'Not logged in',
  ERROR_LOGGING_OUT: 'Error logging out.',
  USER_NOT_FOUND: 'User not found.',
  LEADERBOARD_ERROR: 'Error loading leaderboard',
  
  // Validation
  PASSWORD_REQUIREMENTS: 'Password must be at least 10 characters and include upper, lower, number, and symbol.',
  INVALID_ROLE: 'Invalid role. Must be "player" or "admin".',
  INVALID_POINTS_VALUE: 'Invalid points value.',
  INVALID_POINTS_AMOUNT: 'Invalid points amount. Must be non-zero number.',
  INVALID_LIMIT_VALUE: 'Invalid limit value.',
  INVALID_USER_ID: 'Invalid user ID.',
  USER_ID_REQUIRED: 'User ID is required. Use /admin/points/:userId for specific user updates.',
  SESSION_NOT_FOUND: 'Session not found',
  
  // Authorization
  NOT_ALLOWED_EDIT_OTHER: "You are not allowed to edit another user's data.",
  ADMIN_ONLY: 'This action requires administrator privileges.',
  ADMIN_REGISTRATION_DISABLED: 'Admin accounts cannot be created through registration. Please contact an administrator.',
  
  // Server
  INTERNAL_SERVER_ERROR: 'Internal server error.',
  FAILED_TO_START_SERVER: 'Failed to start server:',
  MONGO_URL_REQUIRED: 'MONGO_URL environment variable is required',
  MYSQL_POOL_NOT_INITIALIZED: 'MySQL pool not initialized. Call connectMySQL() first.',
};

// Success Messages
const SUCCESS = {
  USER_REGISTERED: 'User registered successfully.',
  LOGIN_SUCCESS: 'Login successful! Welcome,',
  LOGGED_OUT: 'Logged out.',
  POINTS_UPDATED: 'Points updated successfully.',
  POINTS_ADDED: 'Points added successfully.',
  POINTS_DEDUCTED: 'Points deducted successfully.',
  POINTS_UPDATED_BY_ADMIN: 'User points updated successfully by admin',
  POINTS_ADDED_BY_ADMIN: 'Points added successfully by admin',
  GAME_CAN_START: 'Game can start.',
  SERVER_RUNNING: 'Server is running. Ready for Unity game connection.',
  
  // Leaderboard
  LEADERBOARD_TITLE: '🏆Leaderboard🏆',
  LEADERBOARD_SUBTITLE: 'Top Players by Points',
  LEADERBOARD_EMPTY: 'No players yet. Be the first to join!',
  LEADERBOARD_FOOTER: 'Unity Game Server - Leaderboard',
  LEADERBOARD_RANK: 'Rank',
  LEADERBOARD_PLAYER: 'Player',
  LEADERBOARD_POINTS: 'Points',
  LEADERBOARD_ERROR: 'Error loading leaderboard',
};

// Console Messages
const CONSOLE = {
  SERVER_LISTENING: 'Server listening at http://localhost:',
  UNITY_SERVER_READY: 'Unity game server ready.',
  MONGODB_CONNECTED: 'MongoDB: Connected (sessions)',
  MYSQL_CONNECTED: 'MySQL: Connected (user data & points)',
  MONGODB_SUCCESS: 'Successfully connected to MongoDB!',
  MONGODB_CLOSED: 'MongoDB connection closed.',
  MYSQL_SUCCESS: 'Successfully connected to MySQL!',
  MYSQL_CLOSED: 'MySQL connection pool closed.',
  SIGTERM_RECEIVED: 'SIGTERM signal received: closing connections',
  SIGINT_RECEIVED: 'SIGINT signal received: closing connections',
  MONGODB_CONNECTION_ERROR: 'MongoDB connection error:',
  MYSQL_CONNECTION_ERROR: 'MySQL connection error:',
  ERROR_CLOSING_MONGODB: 'Error closing MongoDB connection:',
  ERROR_CLOSING_MYSQL: 'Error closing MySQL connection:',
  FAILED_TO_START_SERVER: 'Failed to start server:',
  
  // Error logs
  REGISTRATION_ERROR: 'Registration error:',
  LOGIN_ERROR: 'Login error:',
  GET_USER_ERROR: 'Get user error:',
  GET_POINTS_ERROR: 'Get points error:',
  UPDATE_POINTS_ERROR: 'Update points error:',
  ADD_POINTS_ERROR: 'Add points error:',
  GET_POINT_HISTORY_ERROR: 'Get point history error:',
  
  // Log prefixes
  LOG_PREFIX_AUTH: '[Auth]',
  LOG_PREFIX_ADMIN: '[Admin]',
  LOG_PREFIX_SCORE_SUBMIT: '[Score Submit]',
  LOG_PREFIX_SESSION_TEST: '[Session Test]',
  LOG_PREFIX_LOGIN: '[Login]',
  LOG_PREFIX_LEADERBOARD: '[Leaderboard API]',
};

module.exports = {
  ERRORS,
  SUCCESS,
  CONSOLE,
};

