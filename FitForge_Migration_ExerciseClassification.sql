-- ============================================================
-- FitForge — Migration: exercise classification
-- (equipment type, movement pattern, compound vs isolation)
-- Run this ONCE against your EXISTING fitforgedb database.
-- Safe to re-run — checks before it changes anything.
-- ============================================================
USE fitforgedb;

SET @c1 := (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA='fitforgedb' AND TABLE_NAME='exercises' AND COLUMN_NAME='equipment_type');
SET @s1 := IF(@c1=0,
  "ALTER TABLE exercises ADD COLUMN equipment_type ENUM('Barbell','Dumbbell','Cable','Machine','Bodyweight','Kettlebell','Bands') NULL",
  'SELECT 1');
PREPARE stmt FROM @s1; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @c2 := (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA='fitforgedb' AND TABLE_NAME='exercises' AND COLUMN_NAME='movement_pattern');
SET @s2 := IF(@c2=0,
  "ALTER TABLE exercises ADD COLUMN movement_pattern ENUM('Push','Pull','Squat','Hinge','Carry','Core') NULL",
  'SELECT 1');
PREPARE stmt FROM @s2; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @c3 := (SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA='fitforgedb' AND TABLE_NAME='exercises' AND COLUMN_NAME='is_compound');
SET @s3 := IF(@c3=0, 'ALTER TABLE exercises ADD COLUMN is_compound TINYINT(1) NULL', 'SELECT 1');
PREPARE stmt FROM @s3; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- Safe to re-run — just re-applies the same classification each time.

-- ── Equipment type ──────────────────────────────────────────
UPDATE exercises SET equipment_type='Barbell' WHERE name IN
    ('Bench Press','Incline Bench Press','Overhead Press','Deadlift','Barbell Row','Barbell Squat');
UPDATE exercises SET equipment_type='Dumbbell' WHERE name IN
    ('Dumbbell Curl','Dumbbell Lateral Raise');
UPDATE exercises SET equipment_type='Cable' WHERE name IN
    ('Tricep Pushdown','Cable Row','Lat Pulldown');
UPDATE exercises SET equipment_type='Machine' WHERE name IN
    ('Leg Press','Leg Curl');
UPDATE exercises SET equipment_type='Bodyweight' WHERE exercise_type='Calisthenics' OR exercise_type='Cardio';

-- ── Movement pattern (left NULL where the taxonomy doesn't cleanly apply) ──
UPDATE exercises SET movement_pattern='Push' WHERE name IN
    ('Push-Up','Wide Push-Up','Diamond Push-Up','Decline Push-Up','Archer Push-Up','Pike Push-Up',
     'Pseudo Planche Push-Up','Wall Handstand Push-Up','Bench Press','Incline Bench Press','Overhead Press');
UPDATE exercises SET movement_pattern='Pull' WHERE name IN
    ('Pull-Up','Chin-Up','Australian Pull-Up','Archer Pull-Up','Commando Pull-Up',
     'Barbell Row','Cable Row','Lat Pulldown','Dumbbell Curl');
UPDATE exercises SET movement_pattern='Squat' WHERE name IN
    ('Squat','Bulgarian Split Squat','Pistol Squat','Jump Squat','Barbell Squat','Leg Press','Box Jump');
UPDATE exercises SET movement_pattern='Hinge' WHERE name IN
    ('Deadlift','Nordic Curl','Leg Curl');
UPDATE exercises SET movement_pattern='Core' WHERE name IN
    ('Plank','Hollow Body Hold','L-Sit','Dragon Flag','Hanging Leg Raise','Ab Wheel Rollout','Mountain Climber');

-- ── Compound vs isolation ───────────────────────────────────
UPDATE exercises SET is_compound=1 WHERE name IN
    ('Push-Up','Wide Push-Up','Diamond Push-Up','Decline Push-Up','Archer Push-Up','Pike Push-Up',
     'Pseudo Planche Push-Up','Wall Handstand Push-Up','Pull-Up','Chin-Up','Australian Pull-Up',
     'Archer Pull-Up','Commando Pull-Up','Squat','Bulgarian Split Squat','Pistol Squat','Jump Squat',
     'Dragon Flag','Ab Wheel Rollout','Burpee','Mountain Climber','Bear Crawl','Handstand Hold',
     'Bench Press','Incline Bench Press','Overhead Press','Deadlift','Barbell Row','Barbell Squat',
     'Cable Row','Lat Pulldown','Leg Press','Box Jump');
UPDATE exercises SET is_compound=0 WHERE name IN
    ('Plank','Hollow Body Hold','L-Sit','Hanging Leg Raise','Calf Raise','Nordic Curl',
     'Dumbbell Curl','Tricep Pushdown','Leg Curl','Dumbbell Lateral Raise');
