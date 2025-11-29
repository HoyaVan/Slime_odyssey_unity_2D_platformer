const { ERRORS, CONFIG } = require('../constants');

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

module.exports = {
  requireAuth,
  requireOwner
};

