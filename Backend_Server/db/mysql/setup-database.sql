-- Setup script for defaultdb database
-- Run this if the User table doesn't exist yet

USE defaultdb;

-- Create User table if it doesn't exist
CREATE TABLE IF NOT EXISTS User (
    index_id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(255),
    ID VARCHAR(255) NOT NULL UNIQUE COMMENT 'Login ID',
    PW VARCHAR(255) NOT NULL COMMENT 'Password hash',
    role VARCHAR(50) DEFAULT 'player' COMMENT 'User role: player or admin',
    total_points INT DEFAULT 0,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);

-- Create Point table
CREATE TABLE IF NOT EXISTS Point (
    index_id INT AUTO_INCREMENT PRIMARY KEY,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    point_num INT NOT NULL COMMENT 'Amount of points earned or spent (can be negative)'
);

-- Create UserPoint junction/transaction table
CREATE TABLE IF NOT EXISTS UserPoint (
    index_id INT AUTO_INCREMENT PRIMARY KEY,
    user_index_id INT NOT NULL,
    point_index_id INT NOT NULL,
    FOREIGN KEY (user_index_id) REFERENCES User(index_id) ON DELETE CASCADE,
    FOREIGN KEY (point_index_id) REFERENCES Point(index_id) ON DELETE CASCADE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create indexes
CREATE INDEX IF NOT EXISTS idx_user_login_id ON User(ID);
CREATE INDEX IF NOT EXISTS idx_user_role ON User(role);
CREATE INDEX IF NOT EXISTS idx_userpoint_user ON UserPoint(user_index_id);
CREATE INDEX IF NOT EXISTS idx_userpoint_point ON UserPoint(point_index_id);

