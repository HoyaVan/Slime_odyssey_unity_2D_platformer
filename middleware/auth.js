const { ERRORS, CONFIG } = require('../constants');
const { getPool } = require('../db/mysql/connectMySQL');
const { QUERIES } = require('../constants');

function requireAuth(req, res, next) {
  // Since sessions are handled by MongoDB, check if user is authenticated
  // Check for userIndexId (primary key) in session
  if (!req.session?.[CONFIG.SESSION_KEYS.USER_INDEX_ID]) {
    return res.status(CONFIG.STATUS.UNAUTHORIZED).json({ error: ERRORS.NOT_LOGGED_IN });
  }
  next();
}

// For routes where user can only edit their own data
function requireOwner(req, res, next) {
  const loggedInUserIndexId = req.session[CONFIG.SESSION_KEYS.USER_INDEX_ID];
  const targetUserIndexId = Number.parseInt(req.params.id, 10); // e.g. /users/:id (expecting index_id)

  if (loggedInUserIndexId !== targetUserIndexId) {
    return res
      .status(CONFIG.STATUS.BAD_REQUEST)
      .json({ error: ERRORS.NOT_ALLOWED_EDIT_OTHER });
  }
  next();
}

// For routes that require admin role
async function requireAdmin(req, res, next) {
  // First check if user is authenticated
  if (!req.session?.[CONFIG.SESSION_KEYS.USER_INDEX_ID]) {
    return res.status(CONFIG.STATUS.UNAUTHORIZED).json({ error: ERRORS.NOT_LOGGED_IN });
  }

  try {
    const pool = getPool();
    const userIndexId = req.session[CONFIG.SESSION_KEYS.USER_INDEX_ID];

    // Get user's role from database
    const [users] = await pool.execute(
      QUERIES.SELECT_USER_BY_INDEX_ID,
      [userIndexId]
    );

    if (users.length === 0) {
      return res.status(CONFIG.STATUS.NOT_FOUND).json({ error: ERRORS.USER_NOT_FOUND });
    }

    const userRole = users[0][CONFIG.COLUMNS.ROLE] || CONFIG.ROLES.PLAYER;

    // Check if user has admin role
    if (userRole !== CONFIG.ROLES.ADMIN) {
      return res.status(CONFIG.STATUS.UNAUTHORIZED).json({ error: ERRORS.ADMIN_ONLY });
    }

    // Store role in request for use in route handlers
    req.userRole = userRole;
    next();
  } catch (error) {
    console.error('Error checking admin role:', error);
    res.status(CONFIG.STATUS.INTERNAL_SERVER_ERROR).json({ error: ERRORS.INTERNAL_SERVER_ERROR });
  }
}

module.exports = {
  requireAuth,
  requireOwner,
  requireAdmin
};

