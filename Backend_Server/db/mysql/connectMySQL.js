require('dotenv').config();
const mysql = require('mysql2/promise');
const { CONSOLE, ERRORS, CONFIG } = require('../../constants');

let pool = null;

async function connectMySQL() {
  try {
    if (!pool) {
      pool = mysql.createPool({
        host: process.env.MYSQL_HOST,
        port: CONFIG.MYSQL_PORT,
        user: process.env.MYSQL_USER,
        password: process.env.MYSQL_PASSWORD,
        database: process.env.MYSQL_DATABASE,
        waitForConnections: true,
        connectionLimit: CONFIG.MYSQL_CONNECTION_LIMIT,
        queueLimit: CONFIG.MYSQL_QUEUE_LIMIT,
        ssl: CONFIG.MYSQL_SSL_CONFIG
      });

      // Test the connection
      const connection = await pool.getConnection();
      console.log(CONSOLE.MYSQL_SUCCESS);
      connection.release();
    }
    return pool;
  } catch (error) {
    console.error(CONSOLE.MYSQL_CONNECTION_ERROR, error);
    throw error;
  }
}

async function closeMySQL() {
  try {
    if (pool) {
      await pool.end();
      pool = null;
      console.log(CONSOLE.MYSQL_CLOSED);
    }
  } catch (error) {
    console.error(CONSOLE.ERROR_CLOSING_MYSQL, error);
    throw error;
  }
}

function getPool() {
  if (!pool) {
    throw new Error(ERRORS.MYSQL_POOL_NOT_INITIALIZED);
  }
  return pool;
}

module.exports = {
  connectMySQL,
  closeMySQL,
  getPool
};

