const { ERRORS, CONFIG, CONSOLE } = require('../constants');
const { getPool } = require('../db/mysql/connectMySQL');
const { QUERIES } = require('../constants');

// 세션 ID로 세션을 직접 조회하는 헬퍼 함수
async function loadSessionById(sessionStore, sessionId) {
  return new Promise((resolve, reject) => {
    sessionStore.get(sessionId, (err, session) => {
      if (err) {
        reject(err);
      } else {
        resolve(session);
      }
    });
  });
}

async function requireAuth(req, res, next) {
  // 디버깅: 요청 정보 로그
  console.log(`${CONSOLE.LOG_PREFIX_AUTH} Request received:`, req.method, req.path);
  console.log(`${CONSOLE.LOG_PREFIX_AUTH} Authorization header:`, req.headers.authorization || 'none');
  console.log(`${CONSOLE.LOG_PREFIX_AUTH} Cookie header:`, req.headers.cookie || 'none');
  console.log(`${CONSOLE.LOG_PREFIX_AUTH} Session ID from cookie:`, req.sessionID || 'none');
  console.log(`${CONSOLE.LOG_PREFIX_AUTH} Session data:`, req.session || 'none');

  // 방법 1: 쿠키에서 세션 확인 (기본 방식)
  if (req.session?.[CONFIG.SESSION_KEYS.USER_INDEX_ID]) {
    console.log(`${CONSOLE.LOG_PREFIX_AUTH} ✓ Authenticated via cookie session`);
    return next();
  }

  // 방법 2: Authorization 헤더에서 세션 ID 확인 (Unity용)
  const authHeader = req.headers.authorization;
  if (authHeader && authHeader.startsWith(CONFIG.AUTH_HEADER_PREFIX)) {
    const sessionId = authHeader.substring(CONFIG.AUTH_HEADER_PREFIX.length);
    console.log(`${CONSOLE.LOG_PREFIX_AUTH} Attempting to load session by ID:`, sessionId.substring(0, 30) + '...');
    
    try {
      // 세션 ID를 설정하고 express-session이 세션을 로드하도록 함
      req.sessionID = sessionId;
      
      // express-session이 세션을 로드할 때까지 기다림
      await new Promise((resolve, reject) => {
        req.session.reload((err) => {
          if (err) {
            // 세션이 없거나 로드 실패 시 MongoDB에서 직접 조회
            loadSessionById(req.sessionStore, sessionId)
              .then(session => {
                if (session && session[CONFIG.SESSION_KEYS.USER_INDEX_ID]) {
                  // 세션 데이터를 req.session에 복사 (속성만 업데이트)
                  Object.keys(session).forEach(key => {
                    if (key !== 'cookie') {
                      req.session[key] = session[key];
                    }
                  });
                  resolve();
                } else {
                  reject(new Error(ERRORS.SESSION_NOT_FOUND));
                }
              })
              .catch(reject);
          } else {
            resolve();
          }
        });
      });
      
      if (req.session[CONFIG.SESSION_KEYS.USER_INDEX_ID]) {
        console.log(`${CONSOLE.LOG_PREFIX_AUTH} ✓ Authenticated via Authorization header, userIndexId:`, req.session[CONFIG.SESSION_KEYS.USER_INDEX_ID]);
        return next();
      } else {
        console.log(`${CONSOLE.LOG_PREFIX_AUTH} ✗ Session loaded but no userIndexId found`);
      }
    } catch (error) {
      console.error(`${CONSOLE.LOG_PREFIX_AUTH} ✗ Error loading session by ID:`, error);
    }
  } else {
    console.log(`${CONSOLE.LOG_PREFIX_AUTH} ✗ No valid Authorization header found`);
  }

  // 둘 다 실패하면 인증 실패
  console.log(`${CONSOLE.LOG_PREFIX_AUTH} ✗ Authentication failed, returning 401`);
  return res.status(CONFIG.STATUS.UNAUTHORIZED).json({ error: ERRORS.NOT_LOGGED_IN });
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
  requireAdmin
};

