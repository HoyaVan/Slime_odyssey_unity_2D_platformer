const express = require('express');
const router = express.Router();
const { getPool } = require('../db/mysql/connectMySQL');
const { requireAuth, requireAdmin } = require('../middleware/auth');
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

// Update user points (set absolute value) - ADMIN ONLY
// 일반 사용자는 포인트를 직접 수정할 수 없음 (게임 플레이 중 점수 추가만 가능)
router.put('/points', requireAuth, requireAdmin, async (req, res) => {
  const { points, userId } = req.body;
  
  // Admin must specify which user to update
  const targetUserId = userId || req.session[CONFIG.SESSION_KEYS.USER_INDEX_ID];

  if (!targetUserId) {
    return res.status(CONFIG.STATUS.BAD_REQUEST).json({ 
      error: ERRORS.USER_ID_REQUIRED
    });
  }

  if (typeof points !== 'number' || points < 0) {
    return res.status(CONFIG.STATUS.BAD_REQUEST).json({ error: ERRORS.INVALID_POINTS_VALUE });
  }

  try {
    const pool = getPool();

    // Check if user exists
    const [users] = await pool.execute(
      QUERIES.SELECT_USER_BY_INDEX_ID,
      [targetUserId]
    );

    if (users.length === 0) {
      return res.status(CONFIG.STATUS.NOT_FOUND).json({ error: ERRORS.USER_NOT_FOUND });
    }

    await pool.execute(
      QUERIES.UPDATE_USER_POINTS,
      [points, targetUserId]
    );

    res.json({ 
      message: SUCCESS.POINTS_UPDATED, 
      userId: targetUserId,
      points 
    });
  } catch (error) {
    console.error(CONSOLE.UPDATE_POINTS_ERROR, error);
    res.status(CONFIG.STATUS.INTERNAL_SERVER_ERROR).json({ error: ERRORS.INTERNAL_SERVER_ERROR });
  }
});

// Add points to user (increment/decrement)
// Creates a Point record and links it via UserPoint junction table
router.post('/points/add', requireAuth, async (req, res) => {
  // 디버깅: 쿠키와 세션 확인
  console.log(`${CONSOLE.LOG_PREFIX_SCORE_SUBMIT} Cookie header:`, req.headers.cookie);
  console.log(`${CONSOLE.LOG_PREFIX_SCORE_SUBMIT} Session ID:`, req.sessionID);
  console.log(`${CONSOLE.LOG_PREFIX_SCORE_SUBMIT} Session data:`, req.session);
  
  // Support both JSON (number) and form data (string)
  let amount = req.body.amount;
  
  // Convert string to number if needed (for form data from Unity)
  if (typeof amount === 'string') {
    amount = Number.parseInt(amount, 10);
    if (Number.isNaN(amount)) {
      return res.status(CONFIG.STATUS.BAD_REQUEST).json({ error: ERRORS.INVALID_POINTS_AMOUNT });
    }
  }
  
  const userIndexId = req.session[CONFIG.SESSION_KEYS.USER_INDEX_ID];

  if (!userIndexId) {
    console.log(`${CONSOLE.LOG_PREFIX_SCORE_SUBMIT} ERROR: No userIndexId in session. Session keys:`, Object.keys(req.session));
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

// Get point history for user (public - anyone can view any user's history)
// 웹페이지에서 모든 사용자의 포인트 히스토리를 확인할 수 있음
router.get('/points/history', requireAuth, async (req, res) => {
  // userId 파라미터가 있으면 해당 사용자의 히스토리, 없으면 자신의 히스토리
  let targetUserId = req.query.userId || req.query.userIndexId;
  const limit = Number.parseInt(req.query.limit, 10) || CONFIG.DEFAULT_POINT_HISTORY_LIMIT;

  // userId가 문자열(ID)인 경우 index_id로 변환
  let targetUserIndexId = null;
  
  try {
    const pool = getPool();
    
    if (targetUserId) {
      // userId가 숫자면 index_id로 간주
      if (!Number.isNaN(Number.parseInt(targetUserId, 10))) {
        targetUserIndexId = Number.parseInt(targetUserId, 10);
      } else {
        // userId가 문자열(ID)인 경우 User 테이블에서 조회
        const [users] = await pool.execute(
          QUERIES.SELECT_USER_BY_ID,
          [targetUserId]
        );
        if (users.length === 0) {
          return res.status(CONFIG.STATUS.NOT_FOUND).json({ error: ERRORS.USER_NOT_FOUND });
        }
        targetUserIndexId = users[0].index_id;
      }
    } else {
      // 파라미터가 없으면 자신의 히스토리
      targetUserIndexId = req.session[CONFIG.SESSION_KEYS.USER_INDEX_ID];
    }

    if (!targetUserIndexId) {
      return res.status(CONFIG.STATUS.UNAUTHORIZED).json({ error: ERRORS.SESSION_INVALID });
    }

    // 보안: limit 값 검증
    const safeLimit = Number(limit);
    if (safeLimit < CONFIG.MIN_LIMIT_VALUE || safeLimit > CONFIG.MAX_LIMIT_VALUE || Number.isNaN(safeLimit)) {
      return res.status(CONFIG.STATUS.BAD_REQUEST).json({ error: ERRORS.INVALID_LIMIT_VALUE });
    }

    // Get point history with Point details via UserPoint junction
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
      [targetUserIndexId]
    );

    // Get user info
    const [users] = await pool.execute(
      QUERIES.SELECT_USER_BY_INDEX_ID,
      [targetUserIndexId]
    );

    res.json({ 
      userId: users[0]?.ID || null,
      userIndexId: targetUserIndexId,
      history 
    });
  } catch (error) {
    console.error(CONSOLE.GET_POINT_HISTORY_ERROR, error);
    res.status(CONFIG.STATUS.INTERNAL_SERVER_ERROR).json({ error: ERRORS.INTERNAL_SERVER_ERROR });
  }
});

// ========== ADMIN ONLY ROUTES ==========
// 게임 디테일 수정은 어드민만 가능

// Admin: Get all users (for game management)
router.get('/admin/users', requireAuth, requireAdmin, async (req, res) => {
  try {
    const pool = getPool();
    // Get users with highest points calculated from point history
    // First get all users
    const [users] = await pool.execute(
      QUERIES.SELECT_ALL_USERS_ADMIN
    );
    
    // Calculate highest points for each user
    const usersWithHighestPoints = await Promise.all(users.map(async (user) => {
      let highestPoints = 0;
      try {
        // Get all point transactions for this user ordered chronologically
        const [history] = await pool.execute(
          `SELECT P.point_num, P.created_at
           FROM UserPoint UP
           INNER JOIN Point P ON UP.point_index_id = P.index_id
           WHERE UP.user_index_id = ?
           ORDER BY P.created_at ASC`,
          [user.index_id]
        );
        
        // Calculate running total and find maximum
        let runningTotal = 0;
        for (const transaction of history) {
          runningTotal += transaction.point_num;
          if (runningTotal > highestPoints) {
            highestPoints = runningTotal;
          }
        }
      } catch (err) {
        console.error(`${CONSOLE.LOG_PREFIX_ADMIN} Error calculating highest points for user ${user.index_id}:`, err);
        highestPoints = 0;
      }
      
      return {
        ...user,
        highest_points: highestPoints
      };
    }));
    
    res.json({ users: usersWithHighestPoints });
  } catch (error) {
    console.error(`${CONSOLE.LOG_PREFIX_ADMIN} Error getting users:`, error);
    res.status(CONFIG.STATUS.INTERNAL_SERVER_ERROR).json({ error: ERRORS.INTERNAL_SERVER_ERROR });
  }
});

// Admin: Update any user's points (game detail modification)
router.put('/admin/points/:userId', requireAuth, requireAdmin, async (req, res) => {
  const targetUserId = Number.parseInt(req.params.userId, 10);
  let points = req.body.points;
  
  // Support both JSON (number) and form data (string)
  if (typeof points === 'string') {
    points = Number.parseInt(points, 10);
    if (Number.isNaN(points)) {
      return res.status(CONFIG.STATUS.BAD_REQUEST).json({ error: ERRORS.INVALID_POINTS_VALUE });
    }
  }

  if (Number.isNaN(targetUserId) || targetUserId <= 0) {
    return res.status(CONFIG.STATUS.BAD_REQUEST).json({ error: ERRORS.INVALID_USER_ID });
  }

  if (typeof points !== 'number' || points < 0) {
    return res.status(CONFIG.STATUS.BAD_REQUEST).json({ error: ERRORS.INVALID_POINTS_VALUE });
  }

  try {
    const pool = getPool();
    
    // Check if user exists
    const [users] = await pool.execute(
      QUERIES.SELECT_USER_BY_INDEX_ID,
      [targetUserId]
    );

    if (users.length === 0) {
      return res.status(CONFIG.STATUS.NOT_FOUND).json({ error: ERRORS.USER_NOT_FOUND });
    }

    // Update user's points
    await pool.execute(
      QUERIES.UPDATE_USER_POINTS,
      [points, targetUserId]
    );

    res.json({ 
      message: SUCCESS.POINTS_UPDATED_BY_ADMIN,
      userId: targetUserId,
      points 
    });
  } catch (error) {
    console.error(`${CONSOLE.LOG_PREFIX_ADMIN} Error updating user points:`, error);
    res.status(CONFIG.STATUS.INTERNAL_SERVER_ERROR).json({ error: ERRORS.INTERNAL_SERVER_ERROR });
  }
});

// Admin: Add points to any user
router.post('/admin/points/:userId/add', requireAuth, requireAdmin, async (req, res) => {
  const targetUserId = Number.parseInt(req.params.userId, 10);
  let amount = req.body.amount;

  if (Number.isNaN(targetUserId) || targetUserId <= 0) {
    return res.status(CONFIG.STATUS.BAD_REQUEST).json({ error: ERRORS.INVALID_USER_ID });
  }

  if (typeof amount === 'string') {
    amount = Number.parseInt(amount, 10);
    if (Number.isNaN(amount)) {
      return res.status(CONFIG.STATUS.BAD_REQUEST).json({ error: ERRORS.INVALID_POINTS_AMOUNT });
    }
  }

  if (typeof amount !== 'number' || amount === 0) {
    return res.status(CONFIG.STATUS.BAD_REQUEST).json({ error: ERRORS.INVALID_POINTS_AMOUNT });
  }

  const connection = await getPool().getConnection();
  
  try {
    // Check if user exists
    const [users] = await connection.execute(
      QUERIES.SELECT_USER_BY_INDEX_ID,
      [targetUserId]
    );

    if (users.length === 0) {
      return res.status(CONFIG.STATUS.NOT_FOUND).json({ error: ERRORS.USER_NOT_FOUND });
    }

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
      [targetUserId, pointIndexId]
    );

    // Update user's total_points
    await connection.execute(
      QUERIES.UPDATE_USER_POINTS_INCREMENT,
      [amount, targetUserId]
    );

    // Get updated total points
    const [updatedUsers] = await connection.execute(
      QUERIES.SELECT_USER_TOTAL_POINTS,
      [targetUserId]
    );

    await connection.commit();

    res.json({ 
      message: SUCCESS.POINTS_ADDED_BY_ADMIN,
      userId: targetUserId,
      pointsAdded: amount,
      totalPoints: updatedUsers[0][CONFIG.COLUMNS.TOTAL_POINTS],
      pointIndexId: pointIndexId
    });
  } catch (error) {
    await connection.rollback();
    console.error(`${CONSOLE.LOG_PREFIX_ADMIN} Error adding points:`, error);
    res.status(CONFIG.STATUS.INTERNAL_SERVER_ERROR).json({ error: ERRORS.INTERNAL_SERVER_ERROR });
  } finally {
    connection.release();
  }
});

// Admin: Get point history for any user
router.get('/admin/points/:userId/history', requireAuth, requireAdmin, async (req, res) => {
  const targetUserId = Number.parseInt(req.params.userId, 10);
  const limit = Number.parseInt(req.query.limit, 10) || CONFIG.DEFAULT_POINT_HISTORY_LIMIT;

  if (Number.isNaN(targetUserId) || targetUserId <= 0) {
    return res.status(CONFIG.STATUS.BAD_REQUEST).json({ error: ERRORS.INVALID_USER_ID });
  }

  try {
    const pool = getPool();
    
    // Check if user exists
    const [users] = await pool.execute(
      QUERIES.SELECT_USER_BY_INDEX_ID,
      [targetUserId]
    );

    if (users.length === 0) {
      return res.status(CONFIG.STATUS.NOT_FOUND).json({ error: ERRORS.USER_NOT_FOUND });
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
      [targetUserId]
    );

    res.json({ userId: targetUserId, history });
  } catch (error) {
    console.error(`${CONSOLE.LOG_PREFIX_ADMIN} Error getting point history:`, error);
    res.status(CONFIG.STATUS.INTERNAL_SERVER_ERROR).json({ error: ERRORS.INTERNAL_SERVER_ERROR });
  }
});

// Admin: Get all point history from database (전체 포인트 기록 조회)
router.get('/admin/points/all-history', requireAuth, requireAdmin, async (req, res) => {
  let limit = Number.parseInt(req.query.limit, 10) || CONFIG.DEFAULT_ADMIN_ALL_HISTORY_LIMIT;
  
  // 보안: limit 값 검증 (최대 1000으로 제한)
  if (limit < CONFIG.MIN_LIMIT_VALUE || limit > CONFIG.MAX_LIMIT_VALUE || Number.isNaN(limit)) {
    limit = CONFIG.DEFAULT_ADMIN_ALL_HISTORY_LIMIT;
  }

  try {
    const pool = getPool();
    
    // 모든 사용자의 포인트 기록을 시간순으로 조회
    // LIMIT은 플레이스홀더를 사용할 수 없으므로 직접 삽입 (값은 이미 검증됨)
    // Number()로 한 번 더 변환하여 안전하게 처리
    const safeLimit = Number(limit);
    const [history] = await pool.query(
      `SELECT 
        UP.index_id as user_point_id,
        P.index_id as point_id,
        P.point_num,
        P.created_at,
        UP.created_at as transaction_date,
        U.ID as user_id,
        U.index_id as user_index_id
      FROM UserPoint UP
      INNER JOIN Point P ON UP.point_index_id = P.index_id
      INNER JOIN User U ON UP.user_index_id = U.index_id
      ORDER BY P.created_at DESC
      LIMIT ${safeLimit}`
    );

    res.json({ 
      total: history.length,
      history: history 
    });
  } catch (error) {
    console.error(`${CONSOLE.LOG_PREFIX_ADMIN} Error getting all point history:`, error);
    res.status(CONFIG.STATUS.INTERNAL_SERVER_ERROR).json({ error: ERRORS.INTERNAL_SERVER_ERROR });
  }
});

module.exports = router;

