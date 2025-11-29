-- MySQL Schema for Unity Game Database
-- Run this script on DigitalOcean MySQL database

CREATE DATABASE IF NOT EXISTS unity_game_db;
USE unity_game_db;

-- User table
CREATE TABLE IF NOT EXISTS User (
    index_id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(255),
    ID VARCHAR(255) NOT NULL UNIQUE COMMENT 'Login ID',
    PW VARCHAR(255) NOT NULL COMMENT 'Password hash',
    total_points INT DEFAULT 0,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);

-- Point table
CREATE TABLE IF NOT EXISTS Point (
    index_id INT AUTO_INCREMENT PRIMARY KEY,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    point_num INT NOT NULL COMMENT 'Amount of points earned or spent (can be negative)'
);

-- UserPoint junction/transaction table
CREATE TABLE IF NOT EXISTS UserPoint (
    index_id INT AUTO_INCREMENT PRIMARY KEY,
    user_index_id INT NOT NULL,
    point_index_id INT NOT NULL,
    FOREIGN KEY (user_index_id) REFERENCES User(index_id) ON DELETE CASCADE,
    FOREIGN KEY (point_index_id) REFERENCES Point(index_id) ON DELETE CASCADE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Indexes for faster lookups
CREATE INDEX idx_user_login_id ON User(ID);
CREATE INDEX idx_userpoint_user ON UserPoint(user_index_id);
CREATE INDEX idx_userpoint_point ON UserPoint(point_index_id);

