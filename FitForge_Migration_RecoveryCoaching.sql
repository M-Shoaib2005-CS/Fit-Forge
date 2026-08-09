-- ============================================================
-- FitForge — Migration: recovery-aware volume coaching
-- Run this ONCE against your database. Safe to re-run — checks
-- before it changes anything. Never hardcodes a schema name;
-- uses TABLE_SCHEMA=DATABASE() (real schema is `defaultdb`).
-- ============================================================

-- Append-only log: one row per finished session, recording how many
-- of that day's scheduled sets actually got done (excluding warm-ups
-- and skipped sets). Detection logic derives everything (streaks,
-- recency, trends) by querying this history fresh each time, rather
-- than maintaining fragile running counters that can drift out of
-- sync with the real data.
SET @rc_t1 := (SELECT COUNT(*) FROM information_schema.TABLES
           WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='day_volume_log');
SET @rc_s1 := IF(@rc_t1=0,
  'CREATE TABLE day_volume_log (
      log_id          INT          AUTO_INCREMENT PRIMARY KEY,
      user_id         INT          NOT NULL,
      session_id      INT          NOT NULL,
      day_id          INT          NOT NULL,
      sets_completed  INT          NOT NULL,
      sets_scheduled  INT          NOT NULL,
      ratio           DECIMAL(6,3) NOT NULL,
      logged_at       TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
      UNIQUE KEY uq_dvl_session (session_id),
      KEY idx_user_day (user_id, day_id, logged_at),
      FOREIGN KEY (user_id)    REFERENCES users(user_id)              ON DELETE CASCADE,
      FOREIGN KEY (session_id) REFERENCES workout_sessions(session_id) ON DELETE CASCADE,
      FOREIGN KEY (day_id)     REFERENCES program_days(day_id)         ON DELETE CASCADE
  )',
  'SELECT 1');
PREPARE stmt FROM @rc_s1; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- Per (user, day) cooldown. When a suggestion involving a day is
-- resolved (accepted or declined), that day's cooldown_until is set
-- 14 days out. Detection ignores any day_volume_log history before
-- cooldown_until when looking at that day — this single field does
-- double duty as both "reset the streak" and "suppress re-asking",
-- rather than needing separate reset/cooldown bookkeeping.
SET @rc_t2 := (SELECT COUNT(*) FROM information_schema.TABLES
           WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='day_suggestion_cooldown');
SET @rc_s2 := IF(@rc_t2=0,
  'CREATE TABLE day_suggestion_cooldown (
      user_id         INT      NOT NULL,
      day_id          INT      NOT NULL,
      cooldown_until  DATETIME NOT NULL,
      PRIMARY KEY (user_id, day_id),
      FOREIGN KEY (user_id) REFERENCES users(user_id)      ON DELETE CASCADE,
      FOREIGN KEY (day_id)  REFERENCES program_days(day_id) ON DELETE CASCADE
  )',
  'SELECT 1');
PREPARE stmt FROM @rc_s2; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- One row per pending/resolved coach suggestion. `payload` holds the
-- deterministically-computed detail (anchor/bundled day ids, the
-- specific set/exercise changes, or add-exercise candidates) as JSON
-- — Gemini only phrases this into a message, it never invents the
-- numbers or picks the exercises itself.
SET @rc_t3 := (SELECT COUNT(*) FROM information_schema.TABLES
           WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='coach_suggestions');
SET @rc_s3 := IF(@rc_t3=0,
  'CREATE TABLE coach_suggestions (
      suggestion_id INT          AUTO_INCREMENT PRIMARY KEY,
      user_id       INT          NOT NULL,
      type          VARCHAR(20)  NOT NULL, -- day_fix | whole_program_fix | add_exercise
      payload       TEXT         NOT NULL, -- JSON
      status        VARCHAR(10)  NOT NULL DEFAULT ''pending'', -- pending | accepted | declined
      created_at    TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
      resolved_at   TIMESTAMP    NULL,
      KEY idx_user_status (user_id, status),
      FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE
  )',
  'SELECT 1');
PREPARE stmt FROM @rc_s3; EXECUTE stmt; DEALLOCATE PREPARE stmt;
