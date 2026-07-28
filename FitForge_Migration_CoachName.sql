-- ============================================================
-- FitForge — Migration: customizable coach name
-- Run this ONCE against your EXISTING fitforgedb database.
-- Safe to re-run — checks before it changes anything.
-- ============================================================
USE fitforgedb;

SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA='fitforgedb' AND TABLE_NAME='users' AND COLUMN_NAME='coach_name');
SET @sql := IF(@col=0, 'ALTER TABLE users ADD COLUMN coach_name VARCHAR(30) NOT NULL DEFAULT ''Coach''', 'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
