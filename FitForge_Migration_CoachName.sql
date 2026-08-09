-- ============================================================
-- FitForge — Migration: customizable coach name
-- Run this ONCE against your database. Safe to re-run — checks
-- before it changes anything. Never hardcodes a schema name;
-- uses TABLE_SCHEMA=DATABASE() (real schema is `defaultdb`).
-- ============================================================

SET @cn_col := (SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='users' AND COLUMN_NAME='coach_name');
SET @cn_sql := IF(@cn_col=0, 'ALTER TABLE users ADD COLUMN coach_name VARCHAR(30) NOT NULL DEFAULT ''Coach''', 'SELECT 1');
PREPARE stmt FROM @cn_sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
