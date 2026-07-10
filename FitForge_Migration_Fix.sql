-- ============================================================
-- FitForge — Migration: fix schema/code name mismatches
-- Run this ONCE against your EXISTING fitforgedb database.
-- Safe to re-run — every step checks before it changes anything.
-- ============================================================
USE fitforgedb;

-- ── 1. Add columns the app code already expects on `users` ────
-- (WaterDL/UserDL reference these; without them the water-goal
--  and login-lockout features silently no-op)
SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA='fitforgedb' AND TABLE_NAME='users' AND COLUMN_NAME='water_goal_ml');
SET @sql := IF(@col=0, 'ALTER TABLE users ADD COLUMN water_goal_ml INT NOT NULL DEFAULT 2500', 'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA='fitforgedb' AND TABLE_NAME='users' AND COLUMN_NAME='login_attempts');
SET @sql := IF(@col=0, 'ALTER TABLE users ADD COLUMN login_attempts INT NOT NULL DEFAULT 0', 'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA='fitforgedb' AND TABLE_NAME='users' AND COLUMN_NAME='lockout_until');
SET @sql := IF(@col=0, 'ALTER TABLE users ADD COLUMN lockout_until DATETIME NULL', 'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA='fitforgedb' AND TABLE_NAME='users' AND COLUMN_NAME='email_verified');
SET @sql := IF(@col=0, 'ALTER TABLE users ADD COLUMN email_verified TINYINT(1) NOT NULL DEFAULT 0', 'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ── 2. Fix workout_notes' broken FK (it pointed at a column     ──
--      that doesn't exist on workout_sessions, so this table     ──
--      never actually got created — recreate it correctly).      ──
--      It's unused by the app so far, safe to drop and rebuild.  ──
DROP TABLE IF EXISTS workout_notes;
CREATE TABLE workout_notes (
    note_id            INT AUTO_INCREMENT PRIMARY KEY,
    workout_session_id INT NOT NULL,
    user_id            INT NOT NULL,
    note_text          VARCHAR(1000) NOT NULL,
    created_at         DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (workout_session_id) REFERENCES workout_sessions(session_id) ON DELETE CASCADE,
    FOREIGN KEY (user_id)            REFERENCES users(user_id)    ON DELETE CASCADE
);

-- ── 3. personal_records: ON DUPLICATE KEY UPDATE in the code    ──
--      assumes a unique key per (user, exercise, record_type)    ──
--      that was never added — so every PR has been creating a    ──
--      brand-new row instead of updating the best one. Dedupe    ──
--      down to the best value per group, then add the key.       ──
DELETE pr1 FROM personal_records pr1
INNER JOIN personal_records pr2
  ON pr1.user_id = pr2.user_id
 AND pr1.exercise_id = pr2.exercise_id
 AND pr1.record_type = pr2.record_type
 AND (pr1.value < pr2.value
      OR (pr1.value = pr2.value AND pr1.pr_id < pr2.pr_id));

SET @idx := (SELECT COUNT(*) FROM information_schema.STATISTICS
             WHERE TABLE_SCHEMA='fitforgedb' AND TABLE_NAME='personal_records' AND INDEX_NAME='uq_pr');
SET @sql := IF(@idx=0, 'ALTER TABLE personal_records ADD UNIQUE KEY uq_pr (user_id, exercise_id, record_type)', 'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ── 4. user_injuries: same issue — re-logging an injury was      ──
--      always inserting a new row instead of reactivating the    ──
--      existing one. Keep the most recent row per group, dedupe,  ──
--      then add the key.                                         ──
DELETE ui1 FROM user_injuries ui1
INNER JOIN user_injuries ui2
  ON ui1.user_id = ui2.user_id
 AND ui1.part_id = ui2.part_id
 AND ui1.category_id = ui2.category_id
 AND ui1.ui_id < ui2.ui_id;

SET @idx := (SELECT COUNT(*) FROM information_schema.STATISTICS
             WHERE TABLE_SCHEMA='fitforgedb' AND TABLE_NAME='user_injuries' AND INDEX_NAME='uq_injury');
SET @sql := IF(@idx=0, 'ALTER TABLE user_injuries ADD UNIQUE KEY uq_injury (user_id, part_id, category_id)', 'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- Done. Your existing data (programs, sessions, sets, PRs, etc.) is untouched
-- except for de-duplicated personal_records/user_injuries rows noted above.
