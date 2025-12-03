// Quick script to check if the role column exists in the database
// Run with: node test-db-schema.js

require('dotenv').config();
const mysql = require('mysql2/promise');
const { CONFIG } = require('./constants');

async function checkSchema() {
  let connection;
  try {
    connection = await mysql.createConnection({
      host: process.env.MYSQL_HOST,
      port: CONFIG.MYSQL_PORT,
      user: process.env.MYSQL_USER,
      password: process.env.MYSQL_PASSWORD,
      database: process.env.MYSQL_DATABASE,
      ssl: CONFIG.MYSQL_SSL_CONFIG
    });

    console.log('✓ Connected to database');
    
    // Check if role column exists
    const [columns] = await connection.execute(`
      SELECT COLUMN_NAME, DATA_TYPE, COLUMN_DEFAULT 
      FROM INFORMATION_SCHEMA.COLUMNS 
      WHERE TABLE_SCHEMA = ? AND TABLE_NAME = 'User' AND COLUMN_NAME = 'role'
    `, [process.env.MYSQL_DATABASE]);

    if (columns.length > 0) {
      console.log('✓ Role column EXISTS in User table');
      console.log('  Column details:', columns[0]);
    } else {
      console.log('✗ Role column DOES NOT EXIST in User table');
      console.log('\n⚠️  You need to run the migration script!');
      console.log('   Run this SQL on your database:');
      console.log('\n   ALTER TABLE User ADD COLUMN role VARCHAR(50) DEFAULT \'player\';');
      console.log('   UPDATE User SET role = \'player\' WHERE role IS NULL;');
      console.log('   CREATE INDEX idx_user_role ON User(role);');
    }

    // Show all columns in User table
    const [allColumns] = await connection.execute(`
      SELECT COLUMN_NAME, DATA_TYPE, COLUMN_DEFAULT 
      FROM INFORMATION_SCHEMA.COLUMNS 
      WHERE TABLE_SCHEMA = ? AND TABLE_NAME = 'User'
      ORDER BY ORDINAL_POSITION
    `, [process.env.MYSQL_DATABASE]);

    console.log('\nAll columns in User table:');
    allColumns.forEach(col => {
      console.log(`  - ${col.COLUMN_NAME} (${col.DATA_TYPE})`);
    });

  } catch (error) {
    console.error('Error:', error.message);
    console.error('Full error:', error);
  } finally {
    if (connection) {
      await connection.end();
      console.log('\n✓ Connection closed');
    }
  }
}

checkSchema();






