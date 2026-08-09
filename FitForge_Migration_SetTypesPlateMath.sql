-- ============================================================
-- FitForge — Migration: set types + plate math toggle
-- Run this ONCE against your database. Safe to re-run — checks
-- before it changes anything. Never hardcodes a schema name;
-- uses TABLE_SCHEMA=DATABASE() so it runs against whatever
-- database is currently connected (this host's real schema is
-- `defaultdb`, not `fitforgedb`).
-- ============================================================

SET @stpm_c1 := (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='workout_sets' AND COLUMN_NAME='set_type');
SET @stpm_s1 := IF(@stpm_c1=0, 'ALTER TABLE workout_sets ADD COLUMN set_type VARCHAR(10) NOT NULL DEFAULT ''Working''', 'SELECT 1');
PREPARE stmt FROM @stpm_s1; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @stpm_c2 := (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='users' AND COLUMN_NAME='plate_math_enabled');
SET @stpm_s2 := IF(@stpm_c2=0, 'ALTER TABLE users ADD COLUMN plate_math_enabled TINYINT(1) NOT NULL DEFAULT 1', 'SELECT 1');
PREPARE stmt FROM @stpm_s2; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @stpm_c3 := (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='users' AND COLUMN_NAME='plate_math_unit');
SET @stpm_s3 := IF(@stpm_c3=0, 'ALTER TABLE users ADD COLUMN plate_math_unit VARCHAR(3) NOT NULL DEFAULT ''kg''', 'SELECT 1');
PREPARE stmt FROM @stpm_s3; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @stpm_c4 := (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='workout_sets' AND COLUMN_NAME='drop_index');
SET @stpm_s4 := IF(@stpm_c4=0, 'ALTER TABLE workout_sets ADD COLUMN drop_index INT NOT NULL DEFAULT 0', 'SELECT 1');
PREPARE stmt FROM @stpm_s4; EXECUTE stmt; DEALLOCATE PREPARE stmt;
