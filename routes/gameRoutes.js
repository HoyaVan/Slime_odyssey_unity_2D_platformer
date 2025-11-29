const express = require('express');
const router = express.Router();
const { getPool } = require('../db/mysql/connectMySQL');
const { requireAuth, requireOwner } = require('../middleware/auth');
const { ERRORS, SUCCESS, CONSOLE, QUERIES, CONFIG } = require('../constants');

// Get user points
router.get('/points', requireAuth, async (req, res) => {
  try {
    const pool = getPool();
    const userIndexId = req.session[CONFIG.SESSION_KEYS.USER_INDEX_ID];

    if (!userIndexId) {
      return res.status(CONFIG.STATUS.UNAUTHORIZED).json({ error: ERRORS.SESSION_INVALID });
    }

    const [users] = await pool.execute(
      QUERIES.SELECT_USER_TOTAL_POINTS,
      [userIndexId]
    );

    if (users.length === 0) {
      return res.status(CONFIG.STATUS.NOT_FOUND).json({ error: ERRORS.USER_NOT_FOUND });
    }

    res.json({ points: users[0][CONFIG.COLUMNS.TOTAL_POINTS] });
  } catch (error) {
    console.error(CONSOLE.GET_POINTS_ERROR, error);
    res.status(CONFIG.STATUS.INTERNAL_SERVER_ERROR).json({ error: ERRORS.INTERNAL_SERVER_ERROR });
  }
});

// Update user points (set absolute value)
router.put('/points', requireAuth, async (req, res) => {
  const { points } = req.body;
  const userIndexId = req.session[CONFIG.SESSION_KEYS.USER_INDEX_ID];

  if (!userIndexId) {
    return res.status(CONFIG.STATUS.UNAUTHORIZED).json({ error: ERRORS.SESSION_INVALID });
  }

  if (typeof points !== 'number' || points < 0) {
    return res.status(CONFIG.STATUS.BAD_REQUEST).json({ error: ERRORS.INVALID_POINTS_VALUE });
  }

  try {
    const pool = getPool();

    await pool.execute(
      QUERIES.UPDATE_USER_POINTS,
      [points, userIndexId]
    );

    res.json({ message: SUCCESS.POINTS_UPDATED, points });
  } catch (error) {
    console.error(CONSOLE.UPDATE_POINTS_ERROR, error);
    res.status(CONFIG.STATUS.INTERNAL_SERVER_ERROR).json({ error: ERRORS.INTERNAL_SERVER_ERROR });
  }
});

// Add points to user (increment/decrement)
// Creates a Point record and links it via UserPoint junction table
router.post('/points/add', requireAuth, async (req, res) => {
  const { amount } = req.body;
  const userIndexId = req.session[CONFIG.SESSION_KEYS.USER_INDEX_ID];

  if (!userIndexId) {
    return res.status(CONFIG.STATUS.UNAUTHORIZED).json({ error: ERRORS.SESSION_INVALID });
  }

  if (typeof amount !== 'number' || amount === 0) {
    return res.status(CONFIG.STATUS.BAD_REQUEST).json({ error: ERRORS.INVALID_POINTS_AMOUNT });
  }

  const connection = await getPool().getConnection();
  
  try {
    await connection.beginTransaction();

    // Create a Point record
    const [pointResult] = await connection.execute(
      QUERIES.INSERT_POINT,
      [amount]
    );
    const pointIndexId = pointResult.insertId;

    // Link user to point via UserPoint junction table
    await connection.execute(
      QUERIES.INSERT_USER_POINT,
      [userIndexId, pointIndexId]
    );

    // Update user's total_points
    await connection.execute(
      QUERIES.UPDATE_USER_POINTS_INCREMENT,
      [amount, userIndexId]
    );

    // Get updated total points
    const [users] = await connection.execute(
      QUERIES.SELECT_USER_TOTAL_POINTS,
      [userIndexId]
    );

    await connection.commit();

    res.json({ 
      message: amount > 0 ? SUCCESS.POINTS_ADDED : SUCCESS.POINTS_DEDUCTED,
      pointsAdded: amount,
      totalPoints: users[0][CONFIG.COLUMNS.TOTAL_POINTS],
      pointIndexId: pointIndexId
    });
  } catch (error) {
    await connection.rollback();
    console.error(CONSOLE.ADD_POINTS_ERROR, error);
    res.status(CONFIG.STATUS.INTERNAL_SERVER_ERROR).json({ error: ERRORS.INTERNAL_SERVER_ERROR });
  } finally {
    connection.release();
  }
});

// Game start endpoint
router.get('/start', requireAuth, (req, res) => {
  res.json({
    message: SUCCESS.GAME_CAN_START,
    userIndexId: req.session[CONFIG.SESSION_KEYS.USER_INDEX_ID],
    userId: req.session[CONFIG.SESSION_KEYS.USER_ID],
  });
});

// Get point history for user
router.get('/points/history', requireAuth, async (req, res) => {
  const userIndexId = req.session[CONFIG.SESSION_KEYS.USER_INDEX_ID];
  const limit = Number.parseInt(req.query.limit, 10) || CONFIG.DEFAULT_POINT_HISTORY_LIMIT;

  if (!userIndexId) {
    return res.status(CONFIG.STATUS.UNAUTHORIZED).json({ error: ERRORS.SESSION_INVALID });
  }

  try {
    const pool = getPool();
    
    // Get point history with Point details via UserPoint junction
    const [history] = await pool.execute(
      QUERIES.SELECT_POINT_HISTORY,
      [userIndexId, limit]
    );

    res.json({ history });
  } catch (error) {
    console.error(CONSOLE.GET_POINT_HISTORY_ERROR, error);
    res.status(CONFIG.STATUS.INTERNAL_SERVER_ERROR).json({ error: ERRORS.INTERNAL_SERVER_ERROR });
  }
});

module.exports = router;

