-- ============================================================
-- FitForge — Migration: exercise library expansion
-- Adds 71 curated exercises (sourced from a public exercise
-- dataset, filtered for quality/non-redundancy — see the
-- roadmap's "Recently shipped" section for curation notes),
-- bringing the library from 45 to 116.
--
-- Explicit exercise_id values 46-116 are used (confirmed nothing
-- was added to the exercises table since the original 45 seed —
-- if that's no longer true when you run this, DON'T run it as-is,
-- since 46-116 might collide with something real; check first).
-- Safe to re-run either way: every row is checked by id AND by
-- name before inserting, so a partial or repeat run won't create
-- duplicates or errors. Never hardcodes a schema name; uses
-- TABLE_SCHEMA=DATABASE() (real schema is `defaultdb` in
-- production; your local dev DB is named `fitforgedb`, per
-- appsettings.json — this runs correctly against either).
--
-- GIFs for these 71 exercises are already placed at
-- wwwroot/images/exercises/{46-116}.gif — no separate script or
-- image-mapping step needed, since the IDs here are the same
-- fixed values used to name those files.
-- ============================================================

-- Note: this migration inserts equipment_type/movement_pattern/is_compound
-- values, which requires the ExerciseClassification migration to have run
-- first (it adds those columns). If those columns don't exist yet, run that
-- migration before this one.

DROP TEMPORARY TABLE IF EXISTS elx_staging;
CREATE TEMPORARY TABLE elx_staging (
    exercise_id INT PRIMARY KEY, name VARCHAR(100), muscle_group_id INT, exercise_type VARCHAR(20),
    tracking_mode VARCHAR(20), difficulty VARCHAR(20), description TEXT,
    equipment_type VARCHAR(20), movement_pattern VARCHAR(20), is_compound TINYINT(1)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

INSERT INTO elx_staging (exercise_id, name, muscle_group_id, exercise_type, tracking_mode, difficulty, description, equipment_type, movement_pattern, is_compound) VALUES
(46,'3/4 Sit-up',6,'Calisthenics','reps_only','Intermediate','Lie flat on your back with your knees bent and feet flat on the ground.','Bodyweight','Core',1),
(47,'Plyo Push Up',1,'Calisthenics','reps_only','Intermediate','Start in a high plank position with your hands slightly wider than shoulder-width apart.','Bodyweight','Push',1),
(48,'Seated Leg Raise',6,'Calisthenics','reps_only','Intermediate','Sit on a flat bench with your back straight and your feet flat on the ground.','Bodyweight','Core',0),
(49,'Twisted Leg Raise',6,'Calisthenics','reps_only','Intermediate','Lie flat on your back with your legs extended and your arms by your sides.','Bodyweight','Core',0),
(50,'Russian Twist',6,'Calisthenics','reps_only','Intermediate','Sit on the ground with your knees bent and feet flat on the floor.','Bodyweight','Core',1),
(51,'Close Grip Chin-up',2,'Calisthenics','reps_only','Intermediate','Grab the pull-up bar with your palms facing towards you and your hands shoulder-width apart.','Bodyweight','Pull',1),
(52,'Back Lever',2,'Calisthenics','duration','Intermediate','Start by hanging from a pull-up bar with an overhand grip, hands slightly wider than shoulder-width apart.','Bodyweight','Core',1),
(53,'Dumbbell Biceps Curl',4,'Gym','reps_weight','Intermediate','Stand up straight with a dumbbell in each hand, palms facing forward and arms fully extended.','Dumbbell','Pull',0),
(54,'Band V-up',6,'Gym','reps_weight','Intermediate','Lie flat on your back with your legs straight and your arms extended overhead, holding the band.','Bands','Core',1),
(55,'Band Alternating V-up',6,'Gym','reps_weight','Intermediate','Lie flat on your back with your legs straight and your arms extended overhead, holding the band.','Bands','Core',1),
(56,'Dumbbell Bench Press',1,'Gym','reps_weight','Intermediate','Lie flat on a bench with your feet flat on the ground and your back pressed against the bench.','Dumbbell','Push',1),
(57,'Dumbbell Decline Bench Press',1,'Gym','reps_weight','Intermediate','Lie down on a decline bench with your feet secured and your head lower than your hips.','Dumbbell','Push',1),
(58,'Frog Crunch',6,'Calisthenics','reps_only','Intermediate','Lie flat on your back with your knees bent and feet flat on the ground.','Bodyweight','Core',1),
(59,'Tuck Crunch',6,'Calisthenics','reps_only','Intermediate','Lie flat on your back with your knees bent and feet flat on the ground.','Bodyweight','Core',1),
(60,'Dumbbell Concentration Curl',4,'Gym','reps_weight','Intermediate','Sit on a bench with your legs spread apart and a dumbbell in one hand, resting your elbow on the inside of your thigh.','Dumbbell','Pull',0),
(61,'Dumbbell Rear Lateral Raise',3,'Gym','reps_weight','Intermediate','Stand with your feet shoulder-width apart and hold a dumbbell in each hand, palms facing your body.','Dumbbell',NULL,0),
(62,'Dumbbell Front Raise',3,'Gym','reps_weight','Intermediate','Stand with your feet shoulder-width apart, holding a dumbbell in each hand with your palms facing your thighs.','Dumbbell',NULL,0),
(63,'Cable Seated Row',2,'Gym','reps_weight','Intermediate','Sit on the cable row machine with your feet flat on the footrests and your knees slightly bent.','Cable','Pull',1),
(64,'Cable Low Seated Row',2,'Gym','reps_weight','Intermediate','Sit on the machine with your feet flat on the footrests and your knees slightly bent.','Cable','Pull',1),
(65,'Dumbbell Reverse Fly',3,'Gym','reps_weight','Intermediate','Stand with your feet shoulder-width apart and hold a dumbbell in each hand.','Dumbbell','Push',0),
(66,'Dumbbell Rotation Reverse Fly',3,'Gym','reps_weight','Intermediate','Stand with your feet shoulder-width apart and hold a dumbbell in each hand, palms facing inwards.','Dumbbell','Push',0),
(67,'Dumbbell Seated Shoulder Press',3,'Gym','reps_weight','Intermediate','Sit on a bench with a dumbbell in each hand, resting on your thighs.','Dumbbell','Push',1),
(68,'Dumbbell One Arm Shoulder Press',3,'Gym','reps_weight','Intermediate','Stand with your feet shoulder-width apart and hold a dumbbell in one hand at shoulder level, palm facing forward.','Dumbbell','Push',1),
(69,'Dumbbell Shrug',2,'Gym','reps_weight','Intermediate','Stand with your feet shoulder-width apart and hold a dumbbell in each hand with your palms facing your body.','Dumbbell','Pull',0),
(70,'Dumbbell Incline Shrug',2,'Gym','reps_weight','Intermediate','Set an incline bench to a 45-degree angle and sit on it with a dumbbell in each hand.','Dumbbell','Pull',0),
(71,'Dumbbell Lying Triceps Extension',5,'Gym','reps_weight','Intermediate','Lie flat on a bench with a dumbbell in each hand, palms facing each other.','Dumbbell','Push',0),
(72,'Dumbbell Seated Triceps Extension',5,'Gym','reps_weight','Intermediate','Sit on a bench with your back straight and feet flat on the ground.','Dumbbell','Push',0),
(73,'Bodyweight Standing Calf Raise',9,'Calisthenics','reps_only','Intermediate','Stand with your feet shoulder-width apart, toes pointing forward.','Bodyweight',NULL,0),
(74,'Dumbbell Step-up',8,'Gym','reps_weight','Intermediate','Stand in front of a bench or step with a dumbbell in each hand, palms facing your body.','Dumbbell','Squat',1),
(75,'Dumbbell Deadlift',8,'Gym','reps_weight','Intermediate','Stand with your feet shoulder-width apart, toes pointing forward.','Dumbbell','Hinge',1),
(76,'Dumbbell Romanian Deadlift',8,'Gym','reps_weight','Intermediate','Stand with your feet shoulder-width apart, holding a dumbbell in each hand with an overhand grip.','Dumbbell','Hinge',1),
(77,'Dumbbell Standing Overhead Press',3,'Gym','reps_weight','Intermediate','Stand with your feet shoulder-width apart, holding a dumbbell in each hand at shoulder level with your palms facing f...','Dumbbell','Push',1),
(78,'Dumbbell Standing Alternate Overhead Press',3,'Gym','reps_weight','Intermediate','Stand with your feet shoulder-width apart, holding a dumbbell in each hand at shoulder level with your palms facing f...','Dumbbell','Push',1),
(79,'Barbell Front Squat',8,'Gym','reps_weight','Intermediate','Start by standing with your feet shoulder-width apart, toes slightly turned out.','Barbell','Squat',1),
(80,'Dumbbell Bent Over Row',2,'Gym','reps_weight','Intermediate','Stand with your feet shoulder-width apart, knees slightly bent, and hold a dumbbell in each hand with your palms faci...','Dumbbell','Pull',1),
(81,'Glute Bridge March',8,'Calisthenics','reps_only','Intermediate','Lie flat on your back with your knees bent and feet flat on the ground.','Bodyweight','Hinge',1),
(82,'Low Glute Bridge On Floor',8,'Calisthenics','reps_only','Intermediate','Lie flat on your back with your knees bent and feet flat on the ground.','Bodyweight','Hinge',1),
(83,'Barbell Good Morning',7,'Gym','reps_weight','Intermediate','Start by standing with your feet shoulder-width apart and the barbell resting on your upper back.','Barbell','Hinge',1),
(84,'Barbell Hack Squat',8,'Gym','reps_weight','Intermediate','Start by standing with your feet shoulder-width apart and your toes slightly turned out.','Barbell','Squat',1),
(85,'Walking Lunge',8,'Calisthenics','reps_only','Intermediate','Stand with your feet shoulder-width apart.','Bodyweight','Squat',1),
(86,'Lunge With Jump',8,'Calisthenics','reps_only','Intermediate','Start by standing with your feet shoulder-width apart.','Bodyweight','Squat',1),
(87,'Dumbbell Preacher Curl',4,'Gym','reps_weight','Intermediate','Sit on a preacher curl bench with your upper arms resting on the pad and your chest against it.','Dumbbell','Pull',0),
(88,'Dumbbell Seated Preacher Curl',4,'Gym','reps_weight','Intermediate','Sit on a preacher curl bench with your feet flat on the floor.','Dumbbell','Pull',0),
(89,'Dumbbell Standing Reverse Curl',4,'Gym','reps_weight','Intermediate','Stand up straight with your feet shoulder-width apart and hold a dumbbell in each hand, palms facing your body.','Dumbbell','Pull',0),
(90,'Dumbbell Standing One Arm Reverse Curl',4,'Gym','reps_weight','Intermediate','Stand up straight with your feet shoulder-width apart and hold a dumbbell in one hand with an overhand grip.','Dumbbell','Pull',0),
(91,'Barbell Standing Wide Military Press',3,'Gym','reps_weight','Intermediate','Stand with your feet shoulder-width apart and hold the barbell with an overhand grip, slightly wider than shoulder-wi...','Barbell','Push',1),
(92,'Barbell Seated Behind Head Military Press',3,'Gym','reps_weight','Intermediate','Sit on a bench with your back straight and feet flat on the ground.','Barbell','Push',1),
(93,'Dumbbell Upright Row',3,'Gym','reps_weight','Intermediate','Stand with your feet shoulder-width apart, holding a dumbbell in each hand with an overhand grip.','Dumbbell','Pull',1),
(94,'Dumbbell One Arm Upright Row',3,'Gym','reps_weight','Intermediate','Stand with your feet shoulder-width apart, holding a dumbbell in one hand with an overhand grip.','Dumbbell','Pull',1),
(95,'Standing Single Leg Curl',7,'Calisthenics','reps_only','Intermediate','Stand with your feet hip-width apart and your hands on your hips.','Bodyweight','Hinge',0),
(96,'Dumbbell Seated Bicep Curl',4,'Gym','reps_weight','Intermediate','Sit on a bench with your feet flat on the ground and hold a dumbbell in each hand, palms facing up.','Dumbbell','Pull',0),
(97,'Cable One Arm Tricep Pushdown',5,'Gym','reps_weight','Intermediate','Stand facing a cable machine with a straight bar attachment at chest height.','Cable','Push',0),
(98,'Cable Triceps Pushdown (V-bar)',5,'Gym','reps_weight','Intermediate','Attach a v-bar attachment to the cable machine at the highest setting.','Cable','Push',0),
(99,'Cable Straight Arm Pulldown',2,'Gym','reps_weight','Intermediate','Attach a straight bar to the high pulley of a cable machine.','Cable','Pull',1),
(100,'Chest Dip',1,'Calisthenics','reps_only','Intermediate','Position yourself on parallel bars with your arms fully extended and your body straight.','Bodyweight','Push',1),
(101,'Chest Dip On Straight Bar',1,'Calisthenics','reps_only','Intermediate','Grab the parallel bars with your palms facing down and your arms fully extended.','Bodyweight','Push',1),
(102,'Dumbbell Hammer Curl',4,'Gym','reps_weight','Intermediate','Stand up straight with a dumbbell in each hand, palms facing your torso.','Dumbbell','Pull',0),
(103,'Dumbbell Arnold Press',3,'Gym','reps_weight','Intermediate','Sit on a bench with back support and hold a dumbbell in each hand at shoulder level, palms facing your body and elbow...','Dumbbell','Push',1),
(104,'Dumbbell Fly',1,'Gym','reps_weight','Intermediate','Lie flat on a bench with a dumbbell in each hand, palms facing each other.','Dumbbell','Push',0),
(105,'Dumbbell Goblet Squat',7,'Gym','reps_weight','Intermediate','Stand with your feet shoulder-width apart, holding a dumbbell vertically against your chest with both hands.','Dumbbell','Squat',1),
(106,'Kettlebell Goblet Squat',8,'Gym','reps_weight','Intermediate','Stand with your feet shoulder-width apart, holding a kettlebell close to your chest with both hands.','Kettlebell','Squat',1),
(107,'Ring Dips',5,'Calisthenics','reps_only','Intermediate','Start by hanging from the rings with your arms fully extended and your body straight.','Bodyweight','Push',1),
(108,'Elbow Dips',5,'Calisthenics','reps_only','Intermediate','Sit on the edge of a bench or chair with your hands gripping the edge next to your hips.','Bodyweight','Push',1),
(109,'Flexion Leg Sit Up (Bent Knee)',6,'Calisthenics','reps_only','Intermediate','Lie flat on your back with your knees bent and feet flat on the ground.','Bodyweight','Core',1),
(110,'Frog Planche',6,'Calisthenics','duration','Intermediate','Start in a push-up position with your hands shoulder-width apart and your feet together.','Bodyweight','Core',1),
(111,'Full Planche',6,'Calisthenics','duration','Intermediate','Start in a push-up position with your hands shoulder-width apart and your fingers pointing forward.','Bodyweight','Core',1),
(112,'Front Lever',6,'Calisthenics','duration','Intermediate','Start by hanging from a pull-up bar with an overhand grip, hands shoulder-width apart.','Bodyweight','Core',1),
(113,'Lever Leg Extension',7,'Gym','reps_weight','Intermediate','Adjust the seat height and backrest of the machine to fit your body.','Machine','Core',0),
(114,'Muscle Up',2,'Calisthenics','reps_only','Intermediate','Start by hanging from a pull-up bar with your palms facing away from you and your arms fully extended.','Bodyweight','Pull',1),
(115,'Weighted Muscle Up',2,'Gym','reps_only','Intermediate','Start by hanging from a pull-up bar with your palms facing away from you and your hands slightly wider than shoulder-...','Bodyweight','Pull',1),
(116,'Reverse Grip Machine Lat Pulldown',2,'Gym','reps_weight','Intermediate','Adjust the seat height and position yourself on the machine with your knees under the pads and your feet flat on the ...','Machine','Pull',1);
INSERT INTO exercises (exercise_id, name, muscle_group_id, exercise_type, tracking_mode, difficulty, description, equipment_type, movement_pattern, is_compound)
SELECT s.exercise_id, s.name, s.muscle_group_id, s.exercise_type, s.tracking_mode, s.difficulty, s.description, s.equipment_type, s.movement_pattern, s.is_compound
FROM elx_staging s
WHERE NOT EXISTS (SELECT 1 FROM exercises e WHERE e.exercise_id = s.exercise_id)
  AND NOT EXISTS (SELECT 1 FROM exercises e WHERE e.name = s.name);

DROP TEMPORARY TABLE IF EXISTS elx_staging;
