-- Migration script to add role column to existing User table
-- Run this if you already have a User table without the role column

-- IMPORTANT: Change the database name below to match your actual database name
-- Common names: unity_game_db, defaultdb, or check your .env file for MYSQL_DATABASE
-- USE unity_game_db;  -- Uncomment and use your database name
-- USE defaultdb;       -- Or use this if your database is named 'defaultdb'

-- Check if role column exists, if not add it
-- Note: MySQL doesn't support IF NOT EXISTS for ALTER TABLE ADD COLUMN
-- So we'll use a stored procedure approach or just run the ALTER TABLE directly
-- If column already exists, you'll get an error - that's okay, just ignore it

-- Add role column with default value 'player'
ALTER TABLE User 
ADD COLUMN role VARCHAR(50) DEFAULT 'player' COMMENT 'User role: player or admin';

-- Update existing users to have 'player' role if they don't have one
UPDATE User SET role = 'player' WHERE role IS NULL;

-- Add index for role lookups (will fail if index exists - that's okay)
CREATE INDEX idx_user_role ON User(role);

