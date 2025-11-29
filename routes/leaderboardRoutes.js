const express = require('express');
const router = express.Router();
const { getPool } = require('../db/mysql/connectMySQL');
const { ERRORS, SUCCESS, CONSOLE, QUERIES, CONFIG } = require('../constants');

// Leaderboard page
router.get('/', async (req, res) => {
  try {
    const pool = getPool();
    
    const limit = Number.parseInt(req.query.limit, 10) || CONFIG.DEFAULT_LEADERBOARD_LIMIT;
    
    const [users] = await pool.execute(
      QUERIES.SELECT_LEADERBOARD,
      [limit]
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

module.exports = router;

