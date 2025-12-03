const express = require('express');
const router = express.Router();
const { getPool } = require('../db/mysql/connectMySQL');
const { ERRORS, SUCCESS, CONSOLE, QUERIES, CONFIG } = require('../constants');

// Leaderboard page
router.get('/', async (req, res) => {
  try {
    const pool = getPool();
    
    let limit = Number.parseInt(req.query.limit, 10) || CONFIG.DEFAULT_LEADERBOARD_LIMIT;
    
    // 보안: limit 값 검증
    const safeLimit = Number(limit);
    if (safeLimit < 1 || safeLimit > 1000 || Number.isNaN(safeLimit)) {
      limit = CONFIG.DEFAULT_LEADERBOARD_LIMIT;
    } else {
      limit = safeLimit;
    }
    
    // LIMIT은 플레이스홀더를 사용할 수 없으므로 직접 삽입 (값은 이미 검증됨)
    const [users] = await pool.query(
      `SELECT ID, total_points FROM User ORDER BY total_points DESC LIMIT ${limit}`
    );

    // Render EJS template with data
    res.render('leaderboard', {
      users: users,
      title: SUCCESS.LEADERBOARD_TITLE,
      subtitle: SUCCESS.LEADERBOARD_SUBTITLE,
      emptyMessage: SUCCESS.LEADERBOARD_EMPTY,
      footer: SUCCESS.LEADERBOARD_FOOTER,
      rankLabel: SUCCESS.LEADERBOARD_RANK,
      playerLabel: SUCCESS.LEADERBOARD_PLAYER,
      pointsLabel: SUCCESS.LEADERBOARD_POINTS
    });
  } catch (error) {
    console.error(CONSOLE.GET_POINT_HISTORY_ERROR, error);
    res.status(CONFIG.STATUS.INTERNAL_SERVER_ERROR).render('leaderboard', {
      users: [],
      title: SUCCESS.LEADERBOARD_TITLE,
      subtitle: SUCCESS.LEADERBOARD_SUBTITLE,
      emptyMessage: ERRORS.LEADERBOARD_ERROR,
      footer: SUCCESS.LEADERBOARD_FOOTER,
      rankLabel: SUCCESS.LEADERBOARD_RANK,
      playerLabel: SUCCESS.LEADERBOARD_PLAYER,
      pointsLabel: SUCCESS.LEADERBOARD_POINTS
    });
  }
});

// API: Get point history for a user (public endpoint for leaderboard)
router.get('/api/history/:userId', async (req, res) => {
  try {
    const pool = getPool();
    const userId = req.params.userId;
    const limit = Number.parseInt(req.query.limit, 10) || CONFIG.DEFAULT_LEADERBOARD_API_LIMIT;

    // userId가 숫자면 index_id로 간주, 아니면 ID로 조회
    let userIndexId = null;
    if (!Number.isNaN(Number.parseInt(userId, 10))) {
      userIndexId = Number.parseInt(userId, 10);
    } else {
      const [users] = await pool.execute(
        QUERIES.SELECT_USER_BY_ID,
        [userId]
      );
      if (users.length === 0) {
        return res.status(CONFIG.STATUS.NOT_FOUND).json({ error: ERRORS.USER_NOT_FOUND });
      }
      userIndexId = users[0].index_id;
    }

    // 보안: limit 값 검증
    const safeLimit = Number(limit);
    if (safeLimit < CONFIG.MIN_LIMIT_VALUE || safeLimit > CONFIG.MAX_LIMIT_VALUE || Number.isNaN(safeLimit)) {
      return res.status(CONFIG.STATUS.BAD_REQUEST).json({ error: ERRORS.INVALID_LIMIT_VALUE });
    }

    // Get point history
    // LIMIT은 플레이스홀더를 사용할 수 없으므로 직접 삽입 (값은 이미 검증됨)
    const [history] = await pool.query(
      `SELECT 
        UP.index_id as user_point_id,
        P.index_id as point_id,
        P.point_num,
        P.created_at,
        UP.created_at as transaction_date
      FROM UserPoint UP
      INNER JOIN Point P ON UP.point_index_id = P.index_id
      WHERE UP.user_index_id = ?
      ORDER BY P.created_at DESC
      LIMIT ${safeLimit}`,
      [userIndexId]
    );

    // Get user info
    const [users] = await pool.execute(
      QUERIES.SELECT_USER_BY_INDEX_ID,
      [userIndexId]
    );

    res.json({
      userId: users[0]?.ID || null,
      userIndexId: userIndexId,
      history: history
    });
  } catch (error) {
    console.error(`${CONSOLE.LOG_PREFIX_LEADERBOARD} Error getting point history:`, error);
    res.status(CONFIG.STATUS.INTERNAL_SERVER_ERROR).json({ error: ERRORS.INTERNAL_SERVER_ERROR });
  }
});

module.exports = router;

