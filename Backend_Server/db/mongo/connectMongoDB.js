require('dotenv').config();
const { MongoClient, ServerApiVersion } = require('mongodb');
const { ERRORS, CONSOLE, CONFIG } = require('../../constants');

// Use environment variable for MongoDB URI (set in .env file)
const uri = process.env.MONGO_URL;
if (!uri) {
  throw new Error(ERRORS.MONGO_URL_REQUIRED);
}

// Create a MongoClient with a MongoClientOptions object to set the Stable API version
const client = new MongoClient(uri, {
  serverApi: {
    version: ServerApiVersion.v1,
    strict: true,
    deprecationErrors: true,
  }
});

let db = null;

async function connectMongoDB() {
  try {
    if (!db) {
      await client.connect();
      db = client.db(process.env.MONGO_DB_NAME || CONFIG.DEFAULT_MONGO_DB_NAME);
      // Send a ping to confirm a successful connection
      await db.admin().command({ ping: 1 });
      console.log(CONSOLE.MONGODB_SUCCESS);
    }
    return db;
  } catch (error) {
    console.error(CONSOLE.MONGODB_CONNECTION_ERROR, error);
    throw error;
  }
}

async function closeMongoDB() {
  try {
    if (client) {
      await client.close();
      db = null;
      console.log(CONSOLE.MONGODB_CLOSED);
    }
  } catch (error) {
    console.error(CONSOLE.ERROR_CLOSING_MONGODB, error);
    throw error;
  }
}

module.exports = {
  connectMongoDB,
  closeMongoDB
};
