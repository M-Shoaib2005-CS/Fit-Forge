-- ============================================================
-- FitForge — Migration: tempo notation field for Tempo sets
-- Run this ONCE against your database. Safe to re-run — checks
-- before it changes anything. Never hardcodes a schema name;
-- uses TABLE_SCHEMA=DATABASE() so it runs against whatever
-- database is currently connected (this host's real schema is
-- `defaultdb`, not `fitforgedb`).
-- ============================================================

SET @tmp_c1 := (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='workout_sets' AND COLUMN_NAME='tempo');
SET @tmp_s1 := IF(@tmp_c1=0, 'ALTER TABLE workout_sets ADD COLUMN tempo VARCHAR(11) NULL', 'SELECT 1');
PREPARE stmt FROM @tmp_s1; EXECUTE stmt; DEALLOCATE PREPARE stmt;
