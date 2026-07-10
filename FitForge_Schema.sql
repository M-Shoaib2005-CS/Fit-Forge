-- ============================================================
-- FitForge — Full Database Schema v2
-- MySQL 8.0+ | Run on a fresh database named: fitforgedb
-- ============================================================
CREATE DATABASE IF NOT EXISTS fitforgedb CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE fitforgedb;

-- ─────────────────────────────────────────────────────────────
-- USERS
-- ─────────────────────────────────────────────────────────────
CREATE TABLE users (
    user_id    INT          AUTO_INCREMENT PRIMARY KEY,
    name       VARCHAR(100) NOT NULL,
    username   VARCHAR(50)  NOT NULL UNIQUE,
    email      VARCHAR(255) NOT NULL UNIQUE,
    password   VARCHAR(255) NOT NULL,
    theme      VARCHAR(10)  NOT NULL DEFAULT 'dark',
    water_goal_ml  INT        NOT NULL DEFAULT 2500,
    login_attempts INT        NOT NULL DEFAULT 0,
    lockout_until  DATETIME   NULL,
    email_verified TINYINT(1) NOT NULL DEFAULT 0,
    created_at TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE user_profile (
    profile_id    INT          AUTO_INCREMENT PRIMARY KEY,
    user_id       INT          NOT NULL UNIQUE,
    dob           DATE         NULL,
    gender        VARCHAR(10)  NULL,
    height_cm     DECIMAL(5,2) NULL,
    weight_kg     DECIMAL(5,2) NULL,
    fitness_level ENUM('Beginner','Intermediate','Advanced') NOT NULL DEFAULT 'Beginner',
    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE
);

CREATE TABLE weight_history (
    weight_id   INT          AUTO_INCREMENT PRIMARY KEY,
    user_id     INT          NOT NULL,
    weight_kg   DECIMAL(5,2) NOT NULL,
    recorded_at DATE         NOT NULL DEFAULT (CURDATE()),
    notes       VARCHAR(255) NULL,
    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE
);

CREATE TABLE user_streaks (
    streak_id         INT  AUTO_INCREMENT PRIMARY KEY,
    user_id           INT  NOT NULL UNIQUE,
    current_streak    INT  NOT NULL DEFAULT 0,
    longest_streak    INT  NOT NULL DEFAULT 0,
    last_workout_date DATE NULL,
    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE
);

-- ─────────────────────────────────────────────────────────────
-- EXERCISE LIBRARY
-- ─────────────────────────────────────────────────────────────
CREATE TABLE muscle_groups (
    group_id INT         AUTO_INCREMENT PRIMARY KEY,
    name     VARCHAR(50) NOT NULL UNIQUE,
    icon     VARCHAR(10) NOT NULL DEFAULT '💪'
);

CREATE TABLE exercises (
    exercise_id     INT          AUTO_INCREMENT PRIMARY KEY,
    name            VARCHAR(100) NOT NULL,
    muscle_group_id INT          NOT NULL,
    exercise_type   ENUM('Calisthenics','Gym','Cardio','Mobility') NOT NULL DEFAULT 'Calisthenics',
    tracking_mode   ENUM('reps_only','reps_weight','duration')     NOT NULL DEFAULT 'reps_only',
    difficulty      ENUM('Beginner','Intermediate','Advanced')      NOT NULL DEFAULT 'Beginner',
    description     TEXT NULL,
    image_url       VARCHAR(255) NULL,
    is_active       TINYINT(1)   NOT NULL DEFAULT 1,
    FOREIGN KEY (muscle_group_id) REFERENCES muscle_groups(group_id)
);

-- ─────────────────────────────────────────────────────────────
-- INJURY SYSTEM
-- Two categories: Muscle Pull/Strain | Joint Pain/Stiffness
-- Body parts map to affected exercises automatically
-- ─────────────────────────────────────────────────────────────
CREATE TABLE body_parts (
    part_id   INT         AUTO_INCREMENT PRIMARY KEY,
    name      VARCHAR(50) NOT NULL UNIQUE,  -- e.g. Shoulder, Knee, Wrist
    part_type ENUM('Muscle','Joint')        NOT NULL,
    region    VARCHAR(50) NOT NULL           -- Upper Body / Lower Body / Core
);

CREATE TABLE injury_categories (
    category_id  INT         AUTO_INCREMENT PRIMARY KEY,
    name         VARCHAR(50) NOT NULL,  -- 'Muscle Pull / Strain' | 'Joint Pain / Stiffness'
    description  TEXT        NULL,
    severity_tip TEXT        NULL       -- general advice shown to user
);

-- Each body_part + injury_category combo = a specific injury instance
-- e.g. Shoulder + Joint Pain, Hamstring + Muscle Pull
-- Maps to which exercises to flag
CREATE TABLE injury_exercise_flags (
    flag_id     INT          AUTO_INCREMENT PRIMARY KEY,
    part_id     INT          NOT NULL,
    category_id INT          NOT NULL,
    exercise_id INT          NOT NULL,
    reason      VARCHAR(255) NULL,
    UNIQUE KEY uq_flag (part_id, category_id, exercise_id),
    FOREIGN KEY (part_id)     REFERENCES body_parts(part_id)         ON DELETE CASCADE,
    FOREIGN KEY (category_id) REFERENCES injury_categories(category_id) ON DELETE CASCADE,
    FOREIGN KEY (exercise_id) REFERENCES exercises(exercise_id)       ON DELETE CASCADE
);

-- Exercise alternatives: if exercise X is flagged, suggest Y instead
CREATE TABLE exercise_alternatives (
    alt_id         INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    exercise_id    INT NOT NULL,   -- the flagged exercise
    alternative_id INT NOT NULL,   -- the suggested safer one
    reason         VARCHAR(255) NULL,
    FOREIGN KEY (exercise_id)    REFERENCES exercises(exercise_id) ON DELETE CASCADE,
    FOREIGN KEY (alternative_id) REFERENCES exercises(exercise_id) ON DELETE CASCADE
);

-- User's active injuries
CREATE TABLE user_injuries (
    ui_id         INT  AUTO_INCREMENT PRIMARY KEY,
    user_id       INT  NOT NULL,
    part_id       INT  NOT NULL,
    category_id   INT  NOT NULL,
    occurred_date DATE NOT NULL DEFAULT (CURDATE()),
    status        ENUM('Active','Recovering','Resolved') NOT NULL DEFAULT 'Active',
    notes         VARCHAR(255) NULL,
    UNIQUE KEY uq_injury (user_id, part_id, category_id),
    FOREIGN KEY (user_id)     REFERENCES users(user_id)               ON DELETE CASCADE,
    FOREIGN KEY (part_id)     REFERENCES body_parts(part_id),
    FOREIGN KEY (category_id) REFERENCES injury_categories(category_id)
);

-- ─────────────────────────────────────────────────────────────
-- PROGRAM BUILDER
-- ─────────────────────────────────────────────────────────────
CREATE TABLE programs (
    program_id        INT          AUTO_INCREMENT PRIMARY KEY,
    user_id           INT          NOT NULL,
    name              VARCHAR(100) NOT NULL,
    description       TEXT         NULL,
    goal_type         ENUM('Strength','Hypertrophy','Endurance','Fat Loss','Skill','General') NOT NULL DEFAULT 'General',
    progression_style ENUM('Conservative','Moderate','Aggressive','Adaptive') NOT NULL DEFAULT 'Adaptive',
    is_public         TINYINT(1)   NOT NULL DEFAULT 0,
    created_at        TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE
);

CREATE TABLE program_days (
    day_id     INT         AUTO_INCREMENT PRIMARY KEY,
    program_id INT         NOT NULL,
    day_order  INT         NOT NULL,
    name       VARCHAR(50) NOT NULL,
    day_type   ENUM('Workout','Rest','Active Recovery') NOT NULL DEFAULT 'Workout',
    notes      TEXT        NULL,
    FOREIGN KEY (program_id) REFERENCES programs(program_id) ON DELETE CASCADE
);

CREATE TABLE program_day_exercises (
    pde_id           INT          AUTO_INCREMENT PRIMARY KEY,
    day_id           INT          NOT NULL,
    exercise_id      INT          NOT NULL,
    exercise_order   INT          NOT NULL DEFAULT 1,
    target_sets      INT          NOT NULL DEFAULT 3,
    target_reps      INT          NOT NULL DEFAULT 10,
    target_weight_kg DECIMAL(6,2) NULL,
    rest_seconds     INT          NOT NULL DEFAULT 90,
    notes            TEXT         NULL,
    FOREIGN KEY (day_id)      REFERENCES program_days(day_id)  ON DELETE CASCADE,
    FOREIGN KEY (exercise_id) REFERENCES exercises(exercise_id)
);

-- ─────────────────────────────────────────────────────────────
-- WEEKLY SCHEDULE CALENDAR
-- ─────────────────────────────────────────────────────────────
CREATE TABLE user_schedules (
    schedule_id INT          AUTO_INCREMENT PRIMARY KEY,
    user_id     INT          NOT NULL,
    name        VARCHAR(100) NOT NULL DEFAULT 'My Schedule',
    is_active   TINYINT(1)   NOT NULL DEFAULT 1,
    created_at  TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE
);

CREATE TABLE schedule_slots (
    slot_id     INT     AUTO_INCREMENT PRIMARY KEY,
    schedule_id INT     NOT NULL,
    week_day    TINYINT NOT NULL,   -- 0=Mon … 6=Sun
    day_id      INT     NULL,       -- NULL = rest day
    UNIQUE KEY uq_slot (schedule_id, week_day),
    FOREIGN KEY (schedule_id) REFERENCES user_schedules(schedule_id) ON DELETE CASCADE,
    FOREIGN KEY (day_id)      REFERENCES program_days(day_id)        ON DELETE SET NULL
);

-- ─────────────────────────────────────────────────────────────
-- WORKOUT LOGGING
-- ─────────────────────────────────────────────────────────────
CREATE TABLE workout_sessions (
    session_id   INT       AUTO_INCREMENT PRIMARY KEY,
    user_id      INT       NOT NULL,
    day_id       INT       NOT NULL,
    started_at   TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    finished_at  TIMESTAMP NULL,
    duration_secs INT      NULL,
    notes        TEXT      NULL,
    FOREIGN KEY (user_id) REFERENCES users(user_id)        ON DELETE CASCADE,
    FOREIGN KEY (day_id)  REFERENCES program_days(day_id)
);

CREATE TABLE workout_sets (
    set_id      INT          AUTO_INCREMENT PRIMARY KEY,
    session_id  INT          NOT NULL,
    exercise_id INT          NOT NULL,
    pde_id      INT          NOT NULL,
    set_number  INT          NOT NULL,
    target_reps INT          NOT NULL,
    actual_reps INT          NOT NULL DEFAULT 0,
    weight_kg   DECIMAL(6,2) NULL,
    rpe         TINYINT      NULL,      -- Rate of Perceived Exertion 1-10
    was_skipped TINYINT(1)   NOT NULL DEFAULT 0,
    logged_at   TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (session_id)  REFERENCES workout_sessions(session_id) ON DELETE CASCADE,
    FOREIGN KEY (exercise_id) REFERENCES exercises(exercise_id),
    FOREIGN KEY (pde_id)      REFERENCES program_day_exercises(pde_id)
);

-- ─────────────────────────────────────────────────────────────
-- ADAPTIVE PROGRESSION ENGINE
-- ─────────────────────────────────────────────────────────────
CREATE TABLE user_exercise_targets (
    target_id            INT          AUTO_INCREMENT PRIMARY KEY,
    user_id              INT          NOT NULL,
    pde_id               INT          NOT NULL,
    current_target_reps  INT          NOT NULL,
    current_target_weight DECIMAL(6,2) NULL,
    consecutive_hits     INT          NOT NULL DEFAULT 0,
    last_updated         TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_user_pde (user_id, pde_id),
    FOREIGN KEY (user_id) REFERENCES users(user_id)                 ON DELETE CASCADE,
    FOREIGN KEY (pde_id)  REFERENCES program_day_exercises(pde_id)  ON DELETE CASCADE
);

CREATE TABLE target_history (
    history_id INT          AUTO_INCREMENT PRIMARY KEY,
    user_id    INT          NOT NULL,
    pde_id     INT          NOT NULL,
    session_id INT          NOT NULL,
    old_target INT          NOT NULL,
    new_target INT          NOT NULL,
    reason     VARCHAR(100) NOT NULL,
    changed_at TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id)    REFERENCES users(user_id)            ON DELETE CASCADE,
    FOREIGN KEY (session_id) REFERENCES workout_sessions(session_id) ON DELETE CASCADE
);

-- ─────────────────────────────────────────────────────────────
-- PERSONAL RECORDS
-- ─────────────────────────────────────────────────────────────
CREATE TABLE personal_records (
    pr_id       INT          AUTO_INCREMENT PRIMARY KEY,
    user_id     INT          NOT NULL,
    exercise_id INT          NOT NULL,
    record_type ENUM('max_reps','max_weight','max_volume','best_time') NOT NULL,
    value       DECIMAL(8,2) NOT NULL,
    weight_kg   DECIMAL(6,2) NULL,
    achieved_at DATE         NOT NULL,
    session_id  INT          NULL,
    UNIQUE KEY uq_pr (user_id, exercise_id, record_type),
    FOREIGN KEY (user_id)    REFERENCES users(user_id)              ON DELETE CASCADE,
    FOREIGN KEY (exercise_id)REFERENCES exercises(exercise_id),
    FOREIGN KEY (session_id) REFERENCES workout_sessions(session_id) ON DELETE SET NULL
);

-- ─────────────────────────────────────────────────────────────
-- SKILLS SYSTEM
-- ─────────────────────────────────────────────────────────────
CREATE TABLE skills (
    skill_id   INT         AUTO_INCREMENT PRIMARY KEY,
    name       VARCHAR(100)NOT NULL,
    description TEXT       NULL,
    category   VARCHAR(50) NOT NULL DEFAULT 'Strength',
    difficulty ENUM('Beginner','Intermediate','Advanced','Elite') NOT NULL DEFAULT 'Advanced'
);

CREATE TABLE skill_steps (
    step_id      INT          AUTO_INCREMENT PRIMARY KEY,
    skill_id     INT          NOT NULL,
    step_order   INT          NOT NULL,
    title        VARCHAR(100) NOT NULL,
    instructions TEXT         NULL,
    FOREIGN KEY (skill_id) REFERENCES skills(skill_id) ON DELETE CASCADE
);

CREATE TABLE skill_tips (
    tip_id   INT  AUTO_INCREMENT PRIMARY KEY,
    skill_id INT  NOT NULL,
    tip_text TEXT NOT NULL,
    FOREIGN KEY (skill_id) REFERENCES skills(skill_id) ON DELETE CASCADE
);

CREATE TABLE skill_requirements (
    req_id        INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    skill_id      INT NOT NULL,
    exercise_id   INT NOT NULL,
    required_reps INT NOT NULL,
    FOREIGN KEY (skill_id)    REFERENCES skills(skill_id)      ON DELETE CASCADE,
    FOREIGN KEY (exercise_id) REFERENCES exercises(exercise_id)
);

CREATE TABLE user_skills (
    us_id           INT       AUTO_INCREMENT PRIMARY KEY,
    user_id         INT       NOT NULL,
    skill_id        INT       NOT NULL,
    is_unlocked     TINYINT(1)NOT NULL DEFAULT 0,
    current_step_id INT       NULL,
    unlocked_at     TIMESTAMP NULL,
    mastered_at     TIMESTAMP NULL,
    UNIQUE KEY uq_user_skill (user_id, skill_id),
    FOREIGN KEY (user_id)         REFERENCES users(user_id)       ON DELETE CASCADE,
    FOREIGN KEY (skill_id)        REFERENCES skills(skill_id)     ON DELETE CASCADE,
    FOREIGN KEY (current_step_id) REFERENCES skill_steps(step_id) ON DELETE SET NULL
);

-- ─────────────────────────────────────────────────────────────
-- INDEXES
-- ─────────────────────────────────────────────────────────────
CREATE INDEX idx_ws_user       ON workout_sessions(user_id, started_at DESC);
CREATE INDEX idx_wset_session  ON workout_sets(session_id, exercise_id);
CREATE INDEX idx_uet_user_pde  ON user_exercise_targets(user_id, pde_id);
CREATE INDEX idx_pr_user       ON personal_records(user_id, exercise_id);
CREATE INDEX idx_wh_user       ON weight_history(user_id, recorded_at DESC);
CREATE INDEX idx_ex_type       ON exercises(exercise_type, difficulty);

-- ═════════════════════════════════════════════════════════════
-- SEED DATA
-- ═════════════════════════════════════════════════════════════

-- System user for preset programs
INSERT INTO users (name, username, email, password) VALUES ('System','system','system@fitforge.internal','N/A');

-- Muscle Groups
INSERT INTO muscle_groups (name, icon) VALUES
('Chest','🫁'),('Back','🔙'),('Shoulders','🏋️'),('Biceps','💪'),
('Triceps','💪'),('Core','⚡'),('Legs','🦵'),('Glutes','🍑'),
('Calves','🦵'),('Forearms','💪'),('Full Body','🏃'),('Cardio','❤️');

-- Exercises (Calisthenics)
INSERT INTO exercises (name, muscle_group_id, exercise_type, tracking_mode, difficulty, description) VALUES
('Push-Up',               1,'Calisthenics','reps_only','Beginner',    'Standard push-up — chest, shoulders, triceps'),
('Wide Push-Up',          1,'Calisthenics','reps_only','Beginner',    'Wide grip for greater chest activation'),
('Diamond Push-Up',       5,'Calisthenics','reps_only','Intermediate','Close grip targeting triceps'),
('Decline Push-Up',       1,'Calisthenics','reps_only','Intermediate','Feet elevated, hits upper chest'),
('Archer Push-Up',        1,'Calisthenics','reps_only','Advanced',    'Unilateral push-up progression toward one-arm'),
('Pike Push-Up',          3,'Calisthenics','reps_only','Intermediate','Shoulder-dominant push variation'),
('Pseudo Planche Push-Up',1,'Calisthenics','reps_only','Advanced',    'Forward lean push-up building planche strength'),
('Pull-Up',               2,'Calisthenics','reps_only','Intermediate','Overhand pull-up'),
('Chin-Up',               4,'Calisthenics','reps_only','Beginner',    'Underhand grip, bicep emphasis'),
('Australian Pull-Up',    2,'Calisthenics','reps_only','Beginner',    'Horizontal row using a bar'),
('Archer Pull-Up',        2,'Calisthenics','reps_only','Advanced',    'Unilateral pull-up progression'),
('Commando Pull-Up',      2,'Calisthenics','reps_only','Advanced',    'Alternating sides pull-up'),
('Plank',                 6,'Calisthenics','duration', 'Beginner',    'Isometric core hold'),
('Hollow Body Hold',      6,'Calisthenics','duration', 'Intermediate','Full-body tension for gymnastics base'),
('L-Sit',                 6,'Calisthenics','duration', 'Advanced',    'Bar/parallette hold, legs extended'),
('Dragon Flag',           6,'Calisthenics','reps_only','Advanced',    'Extreme core movement'),
('Hanging Leg Raise',     6,'Calisthenics','reps_only','Intermediate','Legs to 90 from hang'),
('Ab Wheel Rollout',      6,'Calisthenics','reps_only','Intermediate','Ab wheel from knees or toes'),
('Squat',                 7,'Calisthenics','reps_only','Beginner',    'Bodyweight squat'),
('Bulgarian Split Squat', 7,'Calisthenics','reps_only','Intermediate','Rear-foot elevated split squat'),
('Pistol Squat',          7,'Calisthenics','reps_only','Advanced',    'Single leg squat to full depth'),
('Nordic Curl',           7,'Calisthenics','reps_only','Advanced',    'Eccentric hamstring developer'),
('Jump Squat',            7,'Calisthenics','reps_only','Intermediate','Explosive squat jump'),
('Calf Raise',            9,'Calisthenics','reps_only','Beginner',    'Single or double leg'),
('Handstand Hold',        3,'Calisthenics','duration', 'Advanced',    'Wall or freestanding'),
('Wall Handstand Push-Up',3,'Calisthenics','reps_only','Advanced',    'Vertical pressing strength'),
('Burpee',               11,'Calisthenics','reps_only','Intermediate','Full body conditioning'),
('Mountain Climber',     11,'Calisthenics','reps_only','Beginner',    'Core + cardio in plank'),
('Bear Crawl',           11,'Calisthenics','duration', 'Intermediate','Quadrupedal movement'),
-- Gym exercises
('Bench Press',           1,'Gym','reps_weight','Beginner',    'Barbell flat bench press'),
('Incline Bench Press',   1,'Gym','reps_weight','Beginner',    'Upper chest barbell press'),
('Overhead Press',        3,'Gym','reps_weight','Beginner',    'Standing barbell shoulder press'),
('Deadlift',              2,'Gym','reps_weight','Intermediate','Hip hinge compound movement'),
('Barbell Row',           2,'Gym','reps_weight','Intermediate','Bent-over row for back thickness'),
('Barbell Squat',         7,'Gym','reps_weight','Beginner',    'Back squat with barbell'),
('Dumbbell Curl',         4,'Gym','reps_weight','Beginner',    'Alternating dumbbell curl'),
('Tricep Pushdown',       5,'Gym','reps_weight','Beginner',    'Cable tricep pushdown'),
('Cable Row',             2,'Gym','reps_weight','Beginner',    'Seated cable row'),
('Lat Pulldown',          2,'Gym','reps_weight','Beginner',    'Cable lat pulldown'),
('Leg Press',             7,'Gym','reps_weight','Beginner',    'Machine leg press'),
('Leg Curl',              7,'Gym','reps_weight','Beginner',    'Lying hamstring curl'),
('Dumbbell Lateral Raise',3,'Gym','reps_weight','Beginner',    'Medial delt isolation'),
-- Cardio
('Jump Rope',            12,'Cardio','duration','Beginner',    'Skipping rope cardio'),
('Box Jump',              7,'Cardio','reps_only','Intermediate','Explosive jump onto box'),
('Sprint',               12,'Cardio','duration','Intermediate','Max effort sprint');

-- ─────────────────────────────────────────────────────────────
-- INJURY SYSTEM SEED
-- ─────────────────────────────────────────────────────────────
INSERT INTO injury_categories (name, description, severity_tip) VALUES
('Muscle Pull / Strain',
 'A muscle has been overstretched or partially torn. Common after sudden movements.',
 'Avoid exercises that stretch or contract the affected muscle under load. Light movement and blood flow are OK.'),
('Joint Pain / Stiffness',
 'Discomfort or reduced range of motion in a joint. Can be from overuse, inflammation, or posture.',
 'Avoid exercises that compress or rotate the affected joint. Focus on mobility and blood flow instead.');

-- Body Parts (Joints + Muscles)
INSERT INTO body_parts (name, part_type, region) VALUES
-- Joints
('Shoulder Joint',  'Joint',  'Upper Body'),
('Elbow Joint',     'Joint',  'Upper Body'),
('Wrist Joint',     'Joint',  'Upper Body'),
('Hip Joint',       'Joint',  'Lower Body'),
('Knee Joint',      'Joint',  'Lower Body'),
('Ankle Joint',     'Joint',  'Lower Body'),
-- Muscles
('Chest (Pec)',     'Muscle', 'Upper Body'),
('Upper Back',      'Muscle', 'Upper Body'),
('Lower Back',      'Muscle', 'Core'),
('Bicep',           'Muscle', 'Upper Body'),
('Tricep',          'Muscle', 'Upper Body'),
('Shoulder (Delt)', 'Muscle', 'Upper Body'),
('Hamstring',       'Muscle', 'Lower Body'),
('Quadricep',       'Muscle', 'Lower Body'),
('Calf',            'Muscle', 'Lower Body'),
('Glute',           'Muscle', 'Lower Body'),
('Core / Abs',      'Muscle', 'Core');

-- ── Injury Exercise Flags ─────────────────────────────────────
-- Format: (part_id, category_id, exercise_id, reason)
-- part_id refs body_parts, category_id refs injury_categories (1=Muscle Pull, 2=Joint Pain)

-- SHOULDER JOINT (part_id=1) + JOINT PAIN (cat=2)
INSERT INTO injury_exercise_flags (part_id, category_id, exercise_id, reason) VALUES
(1,2,1, 'Shoulder joint load'),
(1,2,4, 'Shoulder joint load'),
(1,2,5, 'Shoulder joint stress'),
(1,2,6, 'Overhead shoulder position'),
(1,2,7, 'Forward lean stresses shoulder joint'),
(1,2,8, 'Overhead hang compresses shoulder'),
(1,2,9, 'Overhead hang'),
(1,2,10,'Shoulder retraction under load'),
(1,2,11,'Overhead hang — advanced'),
(1,2,25,'Inverted shoulder compression'),
(1,2,26,'Vertical pressing — shoulder joint'),
(1,2,32,'Overhead barbell press'),
(1,2,30,'Shoulder joint under barbell load');

-- ELBOW JOINT (part_id=2) + JOINT PAIN (cat=2)
INSERT INTO injury_exercise_flags (part_id, category_id, exercise_id, reason) VALUES
(2,2,1, 'Elbow extension under load'),
(2,2,3, 'Elbow flexion — tricep stress'),
(2,2,9, 'Elbow flexion — chin-up'),
(2,2,36,'Barbell curl — elbow flex'),
(2,2,37,'Tricep pushdown — elbow extension');

-- WRIST JOINT (part_id=3) + JOINT PAIN (cat=2)
INSERT INTO injury_exercise_flags (part_id, category_id, exercise_id, reason) VALUES
(3,2,1, 'Wrist extension under bodyweight'),
(3,2,2, 'Wrist under load'),
(3,2,3, 'Wrist compression'),
(3,2,7, 'Wrist compression — forward lean'),
(3,2,13,'Wrist under bodyweight in plank'),
(3,2,15,'Wrist compression — L-sit');

-- KNEE JOINT (part_id=5) + JOINT PAIN (cat=2)
INSERT INTO injury_exercise_flags (part_id, category_id, exercise_id, reason) VALUES
(5,2,19,'Deep knee flexion'),
(5,2,20,'Single leg deep knee flexion'),
(5,2,21,'Full depth knee flexion'),
(5,2,23,'Impact on knee at landing'),
(5,2,35,'Barbell squat — knee load'),
(5,2,40,'Leg press — knee flexion'),
(5,2,44,'Box jump — impact on landing');

-- HIP JOINT (part_id=4) + JOINT PAIN (cat=2)
INSERT INTO injury_exercise_flags (part_id, category_id, exercise_id, reason) VALUES
(4,2,17,'Hanging leg raise — hip flexor tension'),
(4,2,19,'Squat — hip joint compression'),
(4,2,21,'Pistol — deep hip flexion'),
(4,2,33,'Deadlift — hip hinge under load');

-- ANKLE JOINT (part_id=6) + JOINT PAIN (cat=2)
INSERT INTO injury_exercise_flags (part_id, category_id, exercise_id, reason) VALUES
(6,2,21,'Ankle dorsiflexion — pistol squat'),
(6,2,23,'Landing impact'),
(6,2,24,'Calf raise — ankle load'),
(6,2,44,'Landing impact — box jump');

-- CHEST MUSCLE (part_id=7) + MUSCLE PULL (cat=1)
INSERT INTO injury_exercise_flags (part_id, category_id, exercise_id, reason) VALUES
(7,1,1, 'Direct chest contraction'),
(7,1,2, 'Chest muscle stretch and contraction'),
(7,1,4, 'Upper chest contraction'),
(7,1,5, 'Chest contraction — archer'),
(7,1,30,'Bench press — full pec contraction'),
(7,1,31,'Incline bench — upper pec');

-- UPPER BACK MUSCLE (part_id=8) + MUSCLE PULL (cat=1)
INSERT INTO injury_exercise_flags (part_id, category_id, exercise_id, reason) VALUES
(8,1,8, 'Lat stretch and contraction'),
(8,1,9, 'Lat contraction'),
(8,1,10,'Row movement'),
(8,1,34,'Barbell row — back contraction'),
(8,1,38,'Cable row');

-- LOWER BACK MUSCLE (part_id=9) + MUSCLE PULL (cat=1)
INSERT INTO injury_exercise_flags (part_id, category_id, exercise_id, reason) VALUES
(9,1,13,'Spinal erector isometric hold'),
(9,1,16,'Dragon flag — lumbar stress'),
(9,1,33,'Deadlift — lumbar load'),
(9,1,34,'Bent-over row — lower back load'),
(9,1,35,'Barbell squat — spinal compression');

-- BICEP MUSCLE (part_id=10) + MUSCLE PULL (cat=1)
INSERT INTO injury_exercise_flags (part_id, category_id, exercise_id, reason) VALUES
(10,1,9, 'Chin-up — direct bicep contraction'),
(10,1,36,'Dumbbell curl — direct bicep'),
(10,1,38,'Cable row — bicep involvement');

-- TRICEP MUSCLE (part_id=11) + MUSCLE PULL (cat=1)
INSERT INTO injury_exercise_flags (part_id, category_id, exercise_id, reason) VALUES
(11,1,1, 'Tricep extension at top of push-up'),
(11,1,3, 'Diamond — direct tricep load'),
(11,1,37,'Tricep pushdown — direct load');

-- SHOULDER DELT MUSCLE (part_id=12) + MUSCLE PULL (cat=1)
INSERT INTO injury_exercise_flags (part_id, category_id, exercise_id, reason) VALUES
(12,1,6, 'Shoulder — pike push-up'),
(12,1,26,'Handstand push-up — shoulder'),
(12,1,32,'OHP — direct delt load'),
(12,1,42,'Lateral raise — direct delt');

-- HAMSTRING (part_id=13) + MUSCLE PULL (cat=1)
INSERT INTO injury_exercise_flags (part_id, category_id, exercise_id, reason) VALUES
(13,1,22,'Nordic curl — heavy eccentric'),
(13,1,33,'Deadlift — hamstring stretch'),
(13,1,41,'Leg curl — direct hamstring');

-- QUADRICEP (part_id=14) + MUSCLE PULL (cat=1)
INSERT INTO injury_exercise_flags (part_id, category_id, exercise_id, reason) VALUES
(14,1,19,'Squat — quad contraction'),
(14,1,20,'Split squat — single leg quad'),
(14,1,21,'Pistol squat — deep quad'),
(14,1,40,'Leg press — quad load');

-- CALF (part_id=15) + MUSCLE PULL (cat=1)
INSERT INTO injury_exercise_flags (part_id, category_id, exercise_id, reason) VALUES
(15,1,24,'Direct calf contraction'),
(15,1,43,'Jump rope — calf impact'),
(15,1,44,'Box jump — calf landing');

-- ── Exercise Alternatives ─────────────────────────────────────
-- (flagged_exercise_id, alternative_id, reason)
INSERT INTO exercise_alternatives (exercise_id, alternative_id, reason) VALUES
(1, 10,  'Australian pull-up — reduced shoulder load'),
(1, 13,  'Plank hold — no shoulder strain'),
(8, 10,  'Australian pull-up — same muscle, lower intensity'),
(9, 10,  'Australian pull-up — easier on bicep'),
(6, 28,  'Mountain climber — no overhead position'),
(26,6,   'Pike push-up — less wrist compression'),
(19,28,  'Mountain climber — no knee compression'),
(20,19,  'Regular squat — less single-leg stress'),
(21,19,  'Regular squat — remove single-leg demand'),
(33,34,  'Barbell row — hip hinge variation'),
(30,1,   'Push-up — reduce joint load'),
(31,4,   'Decline push-up — bodyweight alternative');

-- ─────────────────────────────────────────────────────────────
-- SKILLS SEED DATA
-- ─────────────────────────────────────────────────────────────
INSERT INTO skills (name, description, category, difficulty) VALUES
('Muscle-Up',       'Pull-up combined with a dip above the bar',           'Pull',    'Advanced'),
('Handstand',       'Freestanding handstand balance',                       'Balance', 'Advanced'),
('Planche',         'Full body lever with straight arms',                   'Push',    'Elite'),
('Front Lever',     'Horizontal hold from a bar',                           'Pull',    'Elite'),
('One-Arm Push-Up', 'Single arm push-up with full control',                 'Push',    'Advanced'),
('Pistol Squat',    'Single leg squat to full depth unassisted',            'Legs',    'Intermediate'),
('L-Sit',           'Hold body parallel to ground on bars/floor',           'Core',    'Advanced'),
('Human Flag',      'Lateral body hold on a vertical pole',                 'Core',    'Elite');

INSERT INTO skill_steps (skill_id, step_order, title, instructions) VALUES
(1,1,'Scapular Pull-Ups',    'Hang from bar, depress and retract scapula without bending elbows. 3×10'),
(1,2,'Explosive Pull-Ups',   'Pull-up driving elbows back hard, chest to bar. 3×8'),
(1,3,'Negative Muscle-Ups',  'Start above bar, lower slowly through the transition. 3×5'),
(1,4,'Band-Assisted',        'Use resistance band for support through the kip. 3×5'),
(1,5,'Full Muscle-Up',       'Perform full muscle-up, strict or kipping. 3×3'),
(2,1,'Crow Pose Hold',       'Balance on hands with knees on elbows. Hold 30s'),
(2,2,'Pike Hold',            'Hands on floor, hips high, walk feet close. Hold 45s'),
(2,3,'Wall Handstand',       'Kick up to wall, hold 60s with body straight'),
(2,4,'Chest-to-Wall HS',     'Face wall, hands close, hold 45s. Better alignment.'),
(2,5,'Freestanding HS',      'Kick up and balance freely. 10s holds × 5'),
(3,1,'Planche Lean',         'Lean forward on hands from push-up position. 3×30s'),
(3,2,'Tuck Planche',         'Hold tuck off ground. 3×10s'),
(3,3,'Advanced Tuck',        'Extend hips slightly horizontal. 3×10s'),
(3,4,'Straddle Planche',     'Legs spread wide and horizontal. 3×5s'),
(3,5,'Full Planche',         'Both legs together, body parallel. 3×3s'),
(5,1,'Archer Push-Ups',      'One arm bent, one arm straight. 3×8 each side'),
(5,2,'Close-to-One-Arm',     'Hand centred, fingertips assist lightly. 3×5'),
(5,3,'Elevated One-Arm',     'One-arm push-up on elevated surface. 3×5'),
(5,4,'Full One-Arm Push-Up', 'Strict single arm. 3×3 each side'),
(6,1,'Assisted Pistol',      'Hold support, perform single leg squat. 3×8 each'),
(6,2,'Box Pistol',           'Pistol to box — limits depth. 3×6 each'),
(6,3,'Full Pistol Squat',    'Unassisted single leg squat to full depth. 3×5 each'),
(7,1,'Parallel Bar Support', 'Support hold on bars, legs hanging. 3×20s'),
(7,2,'Tuck L-Sit',           'Knees to chest in support hold. 3×10s'),
(7,3,'One-Leg L-Sit',        'One leg extended, one tucked. 3×8s each'),
(7,4,'Full L-Sit',           'Both legs extended parallel to ground. 3×5s');

INSERT INTO skill_tips (skill_id, tip_text) VALUES
(1,'Always warm up shoulders and wrists before muscle-up practice'),
(1,'The false grip is key for strict rings muscle-ups'),
(1,'Practice the transition separately — start above the bar and lower slowly'),
(2,'Protract your shoulders — actively push the floor away'),
(2,'Squeeze glutes and point toes — the whole body should be rigid'),
(3,'Straight arm strength takes months — consistency beats intensity'),
(3,'Use parallettes for wrist comfort on long holds'),
(5,'Film yourself — wrist, elbow, and shoulder must be stacked vertically'),
(5,'Strengthen your wrist extensors with reverse wrist curls'),
(6,'Ankle mobility is the most common limiting factor — stretch daily'),
(7,'Compress your hip flexors actively — do not just hang your legs');

INSERT INTO skill_requirements (skill_id, exercise_id, required_reps) VALUES
(1, 8, 15),   -- Muscle-Up needs 15 pull-ups
(1, 9, 20),   -- and 20 chin-ups
(2,25, 30),   -- Handstand needs 30s wall handstand hold
(3, 7, 20),   -- Planche needs 20 pseudo planche push-ups
(5, 5, 10),   -- One-Arm PU needs 10 archer push-ups each side
(6,21,  5),   -- Pistol needs 5 assisted pistols
(7,15, 10);   -- L-Sit needs 10s L-sit hold

-- ─────────────────────────────────────────────────────────────
-- PRESET PROGRAMS (user_id=1 = system)
-- ─────────────────────────────────────────────────────────────

-- Preset 1: Calisthenics PPL
INSERT INTO programs (user_id, name, description, goal_type, progression_style) VALUES
(1,'Calisthenics PPL','3-day push/pull/legs split — bodyweight only','Hypertrophy','Adaptive');
SET @ppl = LAST_INSERT_ID();

INSERT INTO program_days (program_id, day_order, name, day_type) VALUES
(@ppl,1,'Push','Workout'),(@ppl,2,'Pull','Workout'),(@ppl,3,'Legs','Workout'),
(@ppl,4,'Rest','Rest'),   (@ppl,5,'Push','Workout'),(@ppl,6,'Pull','Workout'),(@ppl,7,'Rest','Rest');

SET @d1=(SELECT day_id FROM program_days WHERE program_id=@ppl AND day_order=1);
INSERT INTO program_day_exercises (day_id,exercise_id,exercise_order,target_sets,target_reps) VALUES
(@d1,1,1,4,10),(@d1,4,2,3,8),(@d1,3,3,3,8),(@d1,6,4,3,8),(@d1,13,5,3,45);

SET @d2=(SELECT day_id FROM program_days WHERE program_id=@ppl AND day_order=2);
INSERT INTO program_day_exercises (day_id,exercise_id,exercise_order,target_sets,target_reps) VALUES
(@d2,9,1,4,6),(@d2,8,2,3,5),(@d2,10,3,3,10),(@d2,17,4,3,10),(@d2,18,5,3,8);

SET @d3=(SELECT day_id FROM program_days WHERE program_id=@ppl AND day_order=3);
INSERT INTO program_day_exercises (day_id,exercise_id,exercise_order,target_sets,target_reps) VALUES
(@d3,19,1,4,15),(@d3,20,2,3,10),(@d3,23,3,3,10),(@d3,24,4,3,20),(@d3,14,5,3,30);

SET @d5=(SELECT day_id FROM program_days WHERE program_id=@ppl AND day_order=5);
INSERT INTO program_day_exercises (day_id,exercise_id,exercise_order,target_sets,target_reps) VALUES
(@d5,1,1,4,12),(@d5,5,2,3,6),(@d5,7,3,3,6),(@d5,6,4,3,10),(@d5,16,5,3,4);

SET @d6=(SELECT day_id FROM program_days WHERE program_id=@ppl AND day_order=6);
INSERT INTO program_day_exercises (day_id,exercise_id,exercise_order,target_sets,target_reps) VALUES
(@d6,8,1,4,6),(@d6,11,2,3,4),(@d6,15,3,3,10),(@d6,16,4,3,5),(@d6,14,5,3,30);

-- Preset 2: Gym Strength 3x/Week
INSERT INTO programs (user_id, name, description, goal_type, progression_style) VALUES
(1,'Gym Strength 3×/Week','Full body strength training — compound movements','Strength','Adaptive');
SET @gym = LAST_INSERT_ID();

INSERT INTO program_days (program_id, day_order, name, day_type) VALUES
(@gym,1,'Workout A','Workout'),(@gym,2,'Rest','Rest'),(@gym,3,'Workout B','Workout'),
(@gym,4,'Rest','Rest'),        (@gym,5,'Workout A','Workout'),(@gym,6,'Rest','Rest'),(@gym,7,'Rest','Rest');

SET @ga=(SELECT day_id FROM program_days WHERE program_id=@gym AND day_order=1);
INSERT INTO program_day_exercises (day_id,exercise_id,exercise_order,target_sets,target_reps,target_weight_kg) VALUES
(@ga,35,1,3,5,60),(@ga,30,2,3,5,40),(@ga,33,3,1,5,70);

SET @gb=(SELECT day_id FROM program_days WHERE program_id=@gym AND day_order=3);
INSERT INTO program_day_exercises (day_id,exercise_id,exercise_order,target_sets,target_reps,target_weight_kg) VALUES
(@gb,35,1,3,5,62.5),(@gb,32,2,3,5,30),(@gb,34,3,3,5,50);

SET @gc=(SELECT day_id FROM program_days WHERE program_id=@gym AND day_order=5);
INSERT INTO program_day_exercises (day_id,exercise_id,exercise_order,target_sets,target_reps,target_weight_kg) VALUES
(@gc,35,1,3,5,65),(@gc,30,2,3,5,42.5),(@gc,33,3,1,5,72.5);

-- Preset 3: Beginner Full Body (Calisthenics)
INSERT INTO programs (user_id, name, description, goal_type, progression_style) VALUES
(1,'Beginner Full Body','3-day full body calisthenics for complete beginners','General','Conservative');
SET @beg = LAST_INSERT_ID();

INSERT INTO program_days (program_id, day_order, name, day_type) VALUES
(@beg,1,'Full Body A','Workout'),(@beg,2,'Rest','Rest'),(@beg,3,'Full Body B','Workout'),
(@beg,4,'Rest','Rest'),          (@beg,5,'Full Body A','Workout'),(@beg,6,'Rest','Rest'),(@beg,7,'Rest','Rest');

SET @ba=(SELECT day_id FROM program_days WHERE program_id=@beg AND day_order=1);
INSERT INTO program_day_exercises (day_id,exercise_id,exercise_order,target_sets,target_reps) VALUES
(@ba,1,1,3,8),(@ba,10,2,3,8),(@ba,19,3,3,12),(@ba,13,4,3,20),(@ba,24,5,3,15);

SET @bb=(SELECT day_id FROM program_days WHERE program_id=@beg AND day_order=3);
INSERT INTO program_day_exercises (day_id,exercise_id,exercise_order,target_sets,target_reps) VALUES
(@bb,2,1,3,8),(@bb,9,2,3,6),(@bb,20,3,3,8),(@bb,14,4,3,20),(@bb,17,5,3,8);


-- ============================================================
-- NEW TABLES: Phase 2 Feature Additions
-- ============================================================

-- Login security
ALTER TABLE users ADD COLUMN login_attempts   INT      DEFAULT 0     AFTER theme;
ALTER TABLE users ADD COLUMN lockout_until    DATETIME NULL          AFTER login_attempts;
ALTER TABLE users ADD COLUMN email_verified   TINYINT(1) DEFAULT 0  AFTER lockout_until;
ALTER TABLE users ADD COLUMN verify_token     VARCHAR(128) NULL      AFTER email_verified;
ALTER TABLE users ADD COLUMN verify_expires   DATETIME NULL          AFTER verify_token;
ALTER TABLE users ADD COLUMN water_goal_ml    INT DEFAULT 2500       AFTER verify_expires;

-- Water intake
CREATE TABLE water_intake (
    intake_id  INT AUTO_INCREMENT PRIMARY KEY,
    user_id    INT NOT NULL,
    amount_ml  INT NOT NULL DEFAULT 250,
    logged_at  DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE
);
CREATE INDEX idx_water_user ON water_intake(user_id, logged_at);

-- Body measurements
CREATE TABLE body_measurements (
    measurement_id INT AUTO_INCREMENT PRIMARY KEY,
    user_id        INT NOT NULL,
    chest_cm       DECIMAL(5,1) NULL,
    waist_cm       DECIMAL(5,1) NULL,
    hips_cm        DECIMAL(5,1) NULL,
    left_arm_cm    DECIMAL(5,1) NULL,
    right_arm_cm   DECIMAL(5,1) NULL,
    left_thigh_cm  DECIMAL(5,1) NULL,
    right_thigh_cm DECIMAL(5,1) NULL,
    neck_cm        DECIMAL(5,1) NULL,
    shoulders_cm   DECIMAL(5,1) NULL,
    body_fat_pct   DECIMAL(4,1) NULL,
    notes          VARCHAR(500) NULL,
    recorded_at    DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE
);
CREATE INDEX idx_meas_user ON body_measurements(user_id, recorded_at DESC);

-- Achievements catalogue
CREATE TABLE achievements (
    achievement_id INT AUTO_INCREMENT PRIMARY KEY,
    code           VARCHAR(60)  UNIQUE NOT NULL,
    name           VARCHAR(100) NOT NULL,
    description    VARCHAR(300) NOT NULL,
    icon           VARCHAR(10)  NOT NULL DEFAULT '🏅',
    category       VARCHAR(30)  NOT NULL DEFAULT 'workout',
    rarity         VARCHAR(20)  NOT NULL DEFAULT 'common'
);

-- User achievement unlocks
CREATE TABLE user_achievements (
    ua_id          INT AUTO_INCREMENT PRIMARY KEY,
    user_id        INT NOT NULL,
    achievement_id INT NOT NULL,
    unlocked_at    DATETIME DEFAULT CURRENT_TIMESTAMP,
    seen           TINYINT(1) DEFAULT 0,
    UNIQUE(user_id, achievement_id),
    FOREIGN KEY (user_id)        REFERENCES users(user_id)        ON DELETE CASCADE,
    FOREIGN KEY (achievement_id) REFERENCES achievements(achievement_id) ON DELETE CASCADE
);

-- Exercise badges (bronze/silver/gold/diamond/legend per user per exercise)
CREATE TABLE exercise_badges (
    badge_id      INT AUTO_INCREMENT PRIMARY KEY,
    user_id       INT NOT NULL,
    exercise_id   INT NOT NULL,
    session_count INT NOT NULL DEFAULT 0,
    tier          VARCHAR(20) NOT NULL DEFAULT 'none',
    awarded_at    DATETIME NULL,
    UNIQUE(user_id, exercise_id),
    FOREIGN KEY (user_id)     REFERENCES users(user_id)     ON DELETE CASCADE,
    FOREIGN KEY (exercise_id) REFERENCES exercises(exercise_id) ON DELETE CASCADE
);

-- Workout session notes
CREATE TABLE workout_notes (
    note_id            INT AUTO_INCREMENT PRIMARY KEY,
    workout_session_id INT NOT NULL,
    user_id            INT NOT NULL,
    note_text          VARCHAR(1000) NOT NULL,
    created_at         DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (workout_session_id) REFERENCES workout_sessions(session_id) ON DELETE CASCADE,
    FOREIGN KEY (user_id)            REFERENCES users(user_id)    ON DELETE CASCADE
);

-- Seed achievement catalogue
INSERT INTO achievements (code, name, description, icon, category, rarity) VALUES
('first_workout',    'First Rep',          'Complete your very first workout',           '💪', 'workout', 'common'),
('workouts_10',      'Getting Serious',    'Complete 10 total workouts',                 '🔥', 'workout', 'common'),
('workouts_50',      'Half Century',       'Complete 50 total workouts',                 '⚡', 'workout', 'rare'),
('workouts_100',     'Century Club',       'Complete 100 total workouts',                '🏆', 'workout', 'epic'),
('workouts_250',     'Grind Never Stops',  'Complete 250 total workouts',                '👑', 'workout', 'legendary'),
('streak_3',         'Three-Peat',         'Maintain a 3-day workout streak',            '✨', 'streak',  'common'),
('streak_7',         'Week Warrior',       'Maintain a 7-day workout streak',            '📅', 'streak',  'common'),
('streak_30',        'Month Master',       'Maintain a 30-day workout streak',           '🌟', 'streak',  'rare'),
('streak_100',       'Unstoppable',        'Maintain a 100-day workout streak',          '🔱', 'streak',  'legendary'),
('first_pr',         'Record Breaker',     'Set your first personal record',             '📈', 'workout', 'common'),
('pr_5',             'PR Machine',         'Set 5 personal records',                     '🎯', 'workout', 'common'),
('pr_25',            'Elite Performer',    'Set 25 personal records',                    '💎', 'workout', 'epic'),
('first_skill',      'Skill Seeker',       'Unlock your first skill',                    '🎓', 'skill',   'common'),
('skill_mastered',   'Skill Master',       'Fully master any skill',                     '🎖',  'skill',   'rare'),
('all_skills',       'Renaissance Body',   'Master every available skill',               '💫', 'skill',   'legendary'),
('weight_logged_10', 'Body Tracker',       'Log your weight 10 times',                   '⚖️', 'health',  'common'),
('hydrated_7',       'Hydration Hero',     'Hit your water goal 7 days straight',        '💧', 'health',  'common'),
('measurements_1',   'First Measurement',  'Log your first body measurement',            '📏', 'health',  'common'),
('early_bird',       'Early Bird',         'Complete a workout before 7 AM',             '🌅', 'workout', 'rare'),
('night_owl',        'Night Owl',          'Complete a workout after 10 PM',             '🌙', 'workout', 'rare'),
('volume_10000',     'Volume King',        'Lift over 10,000 kg in a single session',    '🦍', 'workout', 'epic'),
('perfect_week',     'Perfect Week',       'Complete every planned workout in a week',   '🏅', 'streak',  'rare'),
('bronze_collector', 'Bronze Collector',   'Earn a Bronze badge on any exercise',        '🥉', 'badge',   'common'),
('silver_collector', 'Silver Collector',   'Earn a Silver badge on any exercise',        '🥈', 'badge',   'common'),
('gold_collector',   'Gold Collector',     'Earn a Gold badge on any exercise',          '🥇', 'badge',   'rare'),
('diamond_miner',    'Diamond Miner',      'Earn a Diamond badge on any exercise',       '💎', 'badge',   'epic'),
('legend_born',      'Legend Born',        'Earn a Legend badge on any exercise',        '⚡', 'badge',   'legendary');
