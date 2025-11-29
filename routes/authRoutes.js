const express = require('express');
const bcrypt = require('bcrypt');
const router = express.Router();
const { getPool } = require('../db/mysql/connectMySQL');
const { requireAuth } = require('../middleware/auth');
const { ERRORS, SUCCESS, CONSOLE, QUERIES, CONFIG } = require('../constants');

// REGISTER (for testing / creating accounts)
router.post('/register', async (req, res) => {
  const { id, password, name } = req.body;

  if (!id || !password) {
    return res.status(CONFIG.STATUS.BAD_REQUEST).json({ error: ERRORS.ID_PASSWORD_REQUIRED });
  }

  if (!CONFIG.PASSWORD_REGEX.test(password)) {
    return res.status(CONFIG.STATUS.BAD_REQUEST).json({
      error: ERRORS.PASSWORD_REQUIREMENTS,
    });
  }

  try {
    const pool = getPool();
    
    // Check if user already exists
    const [existing] = await pool.execute(
      QUERIES.SELECT_USER_BY_ID,
      [id]
    );

    if (existing.length > 0) {
      return res.status(CONFIG.STATUS.BAD_REQUEST).json({ error: ERRORS.ID_ALREADY_EXISTS });
    }

    // Hash password
    const passwordHash = await bcrypt.hash(password, CONFIG.BCRYPT_ROUNDS);

    // Insert new user
    const [result] = await pool.execute(
      QUERIES.INSERT_USER,
      [id, passwordHash, name || null, 0]
    );

    res.status(CONFIG.STATUS.CREATED).json({ 
      message: SUCCESS.USER_REGISTERED,
      userIndexId: result.insertId
    });
  } catch (error) {
    console.error(CONSOLE.REGISTRATION_ERROR, error);
    res.status(CONFIG.STATUS.INTERNAL_SERVER_ERROR).json({ error: ERRORS.INTERNAL_SERVER_ERROR });
  }
});

// LOGIN
router.post('/login', async (req, res) => {
  const { id, password } = req.body;

  if (!id || !password) {
    return res.status(CONFIG.STATUS.BAD_REQUEST).json({ error: ERRORS.ID_PASSWORD_REQUIRED });
  }

  try {
    const pool = getPool();
    
    // Find user by login ID
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
    // Store user index_id in session
    req.session[CONFIG.SESSION_KEYS.USER_INDEX_ID] = user[CONFIG.COLUMNS.INDEX_ID];
    req.session[CONFIG.SESSION_KEYS.USER_ID] = user[CONFIG.COLUMNS.ID]; // Also store login ID for convenience

    res.json({ 
      message: `${SUCCESS.LOGIN_SUCCESS} ${user[CONFIG.COLUMNS.NAME] || user[CONFIG.COLUMNS.ID]}.`,
      userIndexId: user[CONFIG.COLUMNS.INDEX_ID],
      totalPoints: user[CONFIG.COLUMNS.TOTAL_POINTS]
    });
  } catch (error) {
    console.error(CONSOLE.LOGIN_ERROR, error);
    res.status(CONFIG.STATUS.INTERNAL_SERVER_ERROR).json({ error: ERRORS.INTERNAL_SERVER_ERROR });
  }
});

// LOGOUT
router.post('/logout', (req, res) => {
  req.session.destroy((err) => {
    if (err) {
      return res.status(CONFIG.STATUS.INTERNAL_SERVER_ERROR).json({ error: ERRORS.ERROR_LOGGING_OUT });
    }
    res.clearCookie(CONFIG.SESSION_COOKIE_NAME);
    res.json({ message: SUCCESS.LOGGED_OUT });
  });
});

// Get current user info
router.get('/me', requireAuth, async (req, res) => {
  try {
    const pool = getPool();
    const userIndexId = req.session[CONFIG.SESSION_KEYS.USER_INDEX_ID];

    if (!userIndexId) {
      return res.status(CONFIG.STATUS.UNAUTHORIZED).json({ error: ERRORS.SESSION_INVALID });
    }

    const [users] = await pool.execute(
      QUERIES.SELECT_USER_BY_INDEX_ID,
      [userIndexId]
    );

    if (users.length === 0) {
      return res.status(CONFIG.STATUS.NOT_FOUND).json({ error: ERRORS.USER_NOT_FOUND });
    }

    const user = users[0];
    res.json({ 
      user: {
        indexId: user[CONFIG.COLUMNS.INDEX_ID],
        id: user[CONFIG.COLUMNS.ID],
        name: user[CONFIG.COLUMNS.NAME],
        totalPoints: user[CONFIG.COLUMNS.TOTAL_POINTS]
      }
    });
  } catch (error) {
    console.error(CONSOLE.GET_USER_ERROR, error);
    res.status(CONFIG.STATUS.INTERNAL_SERVER_ERROR).json({ error: ERRORS.INTERNAL_SERVER_ERROR });
  }
});

module.exports = router;

