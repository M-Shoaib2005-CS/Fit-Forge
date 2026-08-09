-- ============================================================
-- FitForge — Migration: Test Your Max (max_test_attempts table)
-- Run this ONCE against your database. Safe to re-run — checks
-- before it changes anything. Never hardcodes a schema name;
-- uses TABLE_SCHEMA=DATABASE() (real schema is `defaultdb`).
-- ============================================================

SET @mt_t := (SELECT COUNT(*) FROM information_schema.TABLES
           WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='max_test_attempts');
SET @mt_sql := IF(@mt_t=0,
  'CREATE TABLE max_test_attempts (
      attempt_id   INT          AUTO_INCREMENT PRIMARY KEY,
      user_id      INT          NOT NULL,
      exercise_id  INT          NOT NULL,
      reps         INT          NULL,
      weight_kg    DECIMAL(6,2) NULL,
      attempted_at TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
      FOREIGN KEY (user_id)     REFERENCES users(user_id)     ON DELETE CASCADE,
      FOREIGN KEY (exercise_id) REFERENCES exercises(exercise_id)
  )',
  'SELECT 1');
PREPARE stmt FROM @mt_sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
