-- ============================================================
-- FitForge — Migration: notification system
-- Run this ONCE against your EXISTING fitforgedb database.
-- Safe to re-run — every step checks before it changes anything.
-- ============================================================
USE fitforgedb;

-- ── 1. Notification preference on users ────────────────────────
-- 'All' = workout reminders + coach encouragement/injury check-ins,
-- 'WorkoutOnly' = only workout reminders, 'Off' = nothing sent.
SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA='fitforgedb' AND TABLE_NAME='users' AND COLUMN_NAME='notification_pref');
SET @sql := IF(@col=0, "ALTER TABLE users ADD COLUMN notification_pref VARCHAR(20) NOT NULL DEFAULT 'All'", 'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- What time of day the workout/rest-day reminder should go out. Stored as TIME
-- (not a hardcoded server hour) so it's per-user, set from Settings.
SET @col2 := (SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA='fitforgedb' AND TABLE_NAME='users' AND COLUMN_NAME='reminder_time');
SET @sql2 := IF(@col2=0, "ALTER TABLE users ADD COLUMN reminder_time TIME NOT NULL DEFAULT '17:00:00'", 'SELECT 1');
PREPARE stmt FROM @sql2; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- IANA timezone (e.g. 'Asia/Karachi'), captured from the browser when the person enables
-- notifications, so "reminder_time" means THEIR local wall-clock time, not the server's.
SET @col3 := (SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA='fitforgedb' AND TABLE_NAME='users' AND COLUMN_NAME='timezone');
SET @sql3 := IF(@col3=0, "ALTER TABLE users ADD COLUMN timezone VARCHAR(64) NOT NULL DEFAULT 'UTC'", 'SELECT 1');
PREPARE stmt FROM @sql3; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ── 2. Push subscriptions ────────────────────────────────────
-- One row per device/browser the user has enabled notifications on
-- (a user can have several — phone + desktop, etc). Endpoint is the
-- unique handle the browser's push service gave us for that device.
CREATE TABLE IF NOT EXISTS push_subscriptions (
    sub_id      INT AUTO_INCREMENT PRIMARY KEY,
    user_id     INT NOT NULL,
    endpoint    VARCHAR(512) NOT NULL,
    p256dh      VARCHAR(255) NOT NULL,
    auth        VARCHAR(255) NOT NULL,
    created_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uq_endpoint (endpoint(255)),
    KEY idx_user (user_id),
    CONSTRAINT fk_push_user FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── 3. Notification send log (dedupe) ───────────────────────
-- Prevents the background service from sending the same type of
-- notification to the same user twice in one day (or, for injury
-- check-ins, more than once per few days — enforced in code by
-- checking the most recent row's date, not by a unique key here).
CREATE TABLE IF NOT EXISTS notification_log (
    log_id      INT AUTO_INCREMENT PRIMARY KEY,
    user_id     INT NOT NULL,
    type        VARCHAR(30) NOT NULL, -- 'WorkoutReminder' | 'CoachEncouragement' | 'InjuryCheckin'
    sent_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    KEY idx_user_type_date (user_id, type, sent_at),
    CONSTRAINT fk_notiflog_user FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Done. Existing users default to notification_pref='All' but won't
-- actually receive anything until they tap "Enable notifications" in
-- Settings, since that's what creates their push_subscriptions row.
