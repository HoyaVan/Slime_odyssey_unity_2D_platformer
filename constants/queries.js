const QUERIES = {
  // User queries
  SELECT_USER_BY_ID: 'SELECT index_id FROM User WHERE ID = ?',
  SELECT_USER_BY_LOGIN_ID: 'SELECT index_id, ID, PW, role, total_points FROM User WHERE ID = ?',
  SELECT_USER_BY_INDEX_ID: 'SELECT index_id, ID, role, total_points FROM User WHERE index_id = ?',
  SELECT_USER_TOTAL_POINTS: 'SELECT total_points FROM User WHERE index_id = ?',
  SELECT_LEADERBOARD: 'SELECT ID, total_points FROM User ORDER BY total_points DESC LIMIT ?',
  INSERT_USER: 'INSERT INTO User (ID, PW, role, total_points) VALUES (?, ?, ?, ?)',
  UPDATE_USER_POINTS: 'UPDATE User SET total_points = ? WHERE index_id = ?',
  UPDATE_USER_POINTS_INCREMENT: 'UPDATE User SET total_points = total_points + ? WHERE index_id = ?',
  
  // Point queries
  INSERT_POINT: 'INSERT INTO Point (point_num) VALUES (?)',
  
  // UserPoint queries
  INSERT_USER_POINT: 'INSERT INTO UserPoint (user_index_id, point_index_id) VALUES (?, ?)',
  SELECT_POINT_HISTORY: `SELECT 
    UP.index_id as user_point_id,
    P.index_id as point_id,
    P.point_num,
    P.created_at,
    UP.created_at as transaction_date
  FROM UserPoint UP
  INNER JOIN Point P ON UP.point_index_id = P.index_id
  WHERE UP.user_index_id = ?
  ORDER BY P.created_at DESC
  LIMIT ?`,
};

module.exports = {
  QUERIES,
};

