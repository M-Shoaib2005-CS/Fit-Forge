-- ============================================================
-- FitForge — Migration: set types + plate math toggle
-- Run this ONCE against your EXISTING fitforgedb database.
-- Safe to re-run — checks before it changes anything.
-- ============================================================
USE fitforgedb;

SET @c1 := (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA='fitforgedb' AND TABLE_NAME='workout_sets' AND COLUMN_NAME='set_type');
SET @s1 := IF(@c1=0, 'ALTER TABLE workout_sets ADD COLUMN set_type VARCHAR(10) NOT NULL DEFAULT ''Working''', 'SELECT 1');
PREPARE stmt FROM @s1; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @c2 := (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA='fitforgedb' AND TABLE_NAME='users' AND COLUMN_NAME='plate_math_enabled');
SET @s2 := IF(@c2=0, 'ALTER TABLE users ADD COLUMN plate_math_enabled TINYINT(1) NOT NULL DEFAULT 1', 'SELECT 1');
PREPARE stmt FROM @s2; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @c3 := (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA='fitforgedb' AND TABLE_NAME='users' AND COLUMN_NAME='plate_math_unit');
SET @s3 := IF(@c3=0, 'ALTER TABLE users ADD COLUMN plate_math_unit VARCHAR(3) NOT NULL DEFAULT ''kg''', 'SELECT 1');
PREPARE stmt FROM @s3; EXECUTE stmt; DEALLOCATE PREPARE stmt;
