const { ERRORS } = require('../constants');

function validateEnv() {
  const required = [
    'MONGO_URL',
    'SESSION_SECRET',
    'SESSION_CRYPTO_SECRET',
    'MYSQL_HOST',
    'MYSQL_USER',
    'MYSQL_PASSWORD',
    'MYSQL_DATABASE'
  ];

  const missing = required.filter(key => !process.env[key]);

  if (missing.length > 0) {
    throw new Error(
      `Missing required environment variables: ${missing.join(', ')}\n` +
      `Please check your .env file.`
    );
  }
}

module.exports = { validateEnv };

