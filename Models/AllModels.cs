using System;
using System.Collections.Generic;
using System.Linq;

namespace FitForge.Models
{
    // ── Core Entities ─────────────────────────────────────────
    public class UserModel
    {
        public int UserId { get; set; }
        public string Name { get; set; } = "";
        public string Username { get; set; } = "";
        public string Email { get; set; } = "";
        public string Theme { get; set; } = "dark";
        public string FitnessLevel { get; set; } = "Beginner";
        public int Age { get; set; }
        public string Gender { get; set; } = "";
        public double Height { get; set; }
        public double Weight { get; set; }
        public int CurrentStreak { get; set; }
        public int LongestStreak { get; set; }
        public bool EmailVerified { get; set; }
        public int WaterGoalMl { get; set; } = 2500;
        public string Initials => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w[0])).ToUpper();
        public double BMI => Height > 0 ? Math.Round(Weight / Math.Pow(Height / 100.0, 2), 1) : 0;
        public string BMICat => BMI < 18.5 ? "Underweight" : BMI < 25 ? "Normal" : BMI < 30 ? "Overweight" : "Obese";
    }

    public class WeightEntryModel
    {
        public int WeightId { get; set; }
        public double WeightKg { get; set; }
        public DateTime RecordedAt { get; set; }
        public string Notes { get; set; } = "";
    }

    public class MuscleGroupModel
    {
        public int GroupId { get; set; }
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "💪";
    }

    public class ExerciseModel
    {
        public int ExerciseId { get; set; }
        public string Name { get; set; } = "";
        public int MuscleGroupId { get; set; }
        public string MuscleGroup { get; set; } = "";
        public string ExerciseType { get; set; } = "Calisthenics";
        public string TrackingMode { get; set; } = "reps_only";
        public string Difficulty { get; set; } = "Beginner";
        public string Description { get; set; } = "";
        public bool IsActive { get; set; } = true;
        public bool TrackWeight => TrackingMode == "reps_weight";
        public bool TrackDuration => TrackingMode == "duration";
    }

    public class BodyPartModel
    {
        public int PartId { get; set; }
        public string Name { get; set; } = "";
        public string PartType { get; set; } = "";
        public string Region { get; set; } = "";
    }

    public class InjuryCategoryModel
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string SeverityTip { get; set; } = "";
    }

    public class UserInjuryModel
    {
        public int UiId { get; set; }
        public int PartId { get; set; }
        public int CategoryId { get; set; }
        public string BodyPart { get; set; } = "";
        public string PartType { get; set; } = "";
        public string Category { get; set; } = "";
        public DateTime OccurredDate { get; set; }
        public string Status { get; set; } = "Active";
        public string Notes { get; set; } = "";
    }

    public class ExerciseFlagModel
    {
        public int ExerciseId { get; set; }
        public string ExerciseName { get; set; } = "";
        public string Reason { get; set; } = "";
        public int? AlternativeId { get; set; }
        public string AlternativeName { get; set; } = "";
    }

    // ── Water & Hydration ─────────────────────────────────────
    public class WaterIntakeModel
    {
        public int IntakeId { get; set; }
        public int AmountMl { get; set; }
        public DateTime LoggedAt { get; set; }
    }

    public class WaterSummaryModel
    {
        public int TotalMl { get; set; }
        public int GoalMl { get; set; } = 2500;
        public List<WaterIntakeModel> Entries { get; set; } = new();
        public int Percent => GoalMl > 0 ? Math.Min(100, TotalMl * 100 / GoalMl) : 0;
        public bool GoalMet => TotalMl >= GoalMl;
    }

    // ── Body Measurements ─────────────────────────────────────
    public class BodyMeasurementModel
    {
        public int MeasurementId { get; set; }
        public double? ChestCm { get; set; }
        public double? WaistCm { get; set; }
        public double? HipsCm { get; set; }
        public double? LeftArmCm { get; set; }
        public double? RightArmCm { get; set; }
        public double? LeftThighCm { get; set; }
        public double? RightThighCm { get; set; }
        public double? NeckCm { get; set; }
        public double? ShouldersCm { get; set; }
        public double? BodyFatPct { get; set; }
        public string Notes { get; set; } = "";
        public DateTime RecordedAt { get; set; }
    }

    // ── Achievements ──────────────────────────────────────────
    public class AchievementModel
    {
        public int AchievementId { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Icon { get; set; } = "🏅";
        public string Category { get; set; } = "workout";
        public string Rarity { get; set; } = "common";
        public bool IsUnlocked { get; set; }
        public DateTime? UnlockedAt { get; set; }
        public bool Seen { get; set; }
        // Points scale with rarity — rarer achievements are harder to earn
        public int Points => Rarity switch
        {
            "legendary" => 50,
            "epic" => 30,
            "rare" => 20,
            "common" => 10,
            _ => 10
        };
    }

    // ── Exercise Badges ───────────────────────────────────────
    public class ExerciseBadgeModel
    {
        public int ExerciseId { get; set; }
        public string ExerciseName { get; set; } = "";
        public string MuscleGroup { get; set; } = "";
        public string Tier { get; set; } = "none";
        public int SessionCount { get; set; }
        public int NextTierAt { get; set; }
        public int ProgressPct => NextTierAt > 0
            ? Math.Min(100, SessionCount * 100 / NextTierAt) : 100;
        // Thresholds: bronze=1, silver=10, gold=25, diamond=50, legend=100
        public static string CalcTier(int count) =>
            count >= 100 ? "legend" :
            count >= 50 ? "diamond" :
            count >= 25 ? "gold" :
            count >= 10 ? "silver" :
            count >= 1 ? "bronze" : "none";
        public static int NextThreshold(int count) =>
            count < 1 ? 1 :
            count < 10 ? 10 :
            count < 25 ? 25 :
            count < 50 ? 50 :
            count < 100 ? 100 : 100;
        // Points scale with how hard the tier is to reach
        public int Points => Tier switch
        {
            "legend" => 100,
            "diamond" => 50,
            "gold" => 30,
            "silver" => 20,
            "bronze" => 10,
            _ => 0
        };
    }

    // ── Program Models ────────────────────────────────────────
    public class ProgramModel
    {
        public int ProgramId { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string GoalType { get; set; } = "General";
        public string ProgressionStyle { get; set; } = "Adaptive";
        public bool IsPublic { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<ProgramDayModel> Days { get; set; } = new();
        public bool IsPreset => UserId == 1;
    }

    public class ProgramDayModel
    {
        public int DayId { get; set; }
        public int ProgramId { get; set; }
        public int DayOrder { get; set; }
        public string Name { get; set; } = "";
        public string DayType { get; set; } = "Workout";
        public string Notes { get; set; } = "";
        public bool IsRest => DayType != "Workout";
        public List<ProgramDayExerciseModel> Exercises { get; set; } = new();
    }

    public class ProgramDayExerciseModel
    {
        public int PdeId { get; set; }
        public int DayId { get; set; }
        public int ExerciseId { get; set; }
        public string ExerciseName { get; set; } = "";
        public string MuscleGroup { get; set; } = "";
        public string TrackingMode { get; set; } = "reps_only";
        public string ExerciseType { get; set; } = "Calisthenics";
        public int ExerciseOrder { get; set; }
        public int TargetSets { get; set; }
        public int TargetReps { get; set; }
        public double? TargetWeightKg { get; set; }
        public int RestSeconds { get; set; } = 90;
        public string Notes { get; set; } = "";
        public bool TrackWeight => TrackingMode == "reps_weight";
        public bool TrackDuration => TrackingMode == "duration";
    }

    // ── Schedule Models ───────────────────────────────────────
    public class UserScheduleModel
    {
        public int ScheduleId { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; } = "My Schedule";
        public bool IsActive { get; set; }
        public List<ScheduleSlotModel> Slots { get; set; } = new();
    }

    public class ScheduleSlotModel
    {
        public int SlotId { get; set; }
        public int ScheduleId { get; set; }
        public int WeekDay { get; set; }
        public int? DayId { get; set; }
        public string DayName { get; set; } = "Rest";
        public string ProgramName { get; set; } = "";
        public string DayType { get; set; } = "Rest";
        public string WeekDayName => new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }[WeekDay];
        public bool IsRest => DayId == null;
    }

    // ── Session Models ────────────────────────────────────────
    public class SessionModel
    {
        public int SessionId { get; set; }
        public int DayId { get; set; }
        public string DayName { get; set; } = "";
        public string ProgramName { get; set; } = "";
        public DateTime StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
        public int? DurationSecs { get; set; }
        public string Notes { get; set; } = "";
        public int TotalSets { get; set; }
        public int TotalExercises { get; set; }
        public string DurationDisplay => DurationSecs.HasValue
            ? $"{DurationSecs.Value / 60}m {DurationSecs.Value % 60}s" : "—";
    }

    public class ActiveSessionExerciseModel
    {
        public int PdeId { get; set; }
        public int ExerciseId { get; set; }
        public string ExerciseName { get; set; } = "";
        public string MuscleGroup { get; set; } = "";
        public string TrackingMode { get; set; } = "reps_only";
        public int TargetSets { get; set; }
        public int TargetReps { get; set; }
        public double? TargetWeight { get; set; }
        public int RestSeconds { get; set; }
        public string Notes { get; set; } = "";
        public bool IsFlagged { get; set; }
        public string FlagReason { get; set; } = "";
        public int? AlternativeId { get; set; }
        public string AlternativeName { get; set; } = "";
        public bool TrackWeight => TrackingMode == "reps_weight";
        public bool TrackDuration => TrackingMode == "duration";
        public int? LastReps { get; set; }
        public double? LastWeightKg { get; set; }
        public DateTime? LastDate { get; set; }
    }

    public class ExerciseTargetModel
    {
        public int PdeId { get; set; }
        public int ExerciseId { get; set; }
        public string ExerciseName { get; set; } = "";
        public string TrackingMode { get; set; } = "reps_only";
        public int CurrentTargetReps { get; set; }
        public double? CurrentTargetWeight { get; set; }
        public int ConsecutiveHits { get; set; }
        public int TargetSets { get; set; }
        public int RestSeconds { get; set; }
        public bool TrackWeight => TrackingMode == "reps_weight";
    }

    public class PersonalRecordModel
    {
        public int PrId { get; set; }
        public int ExerciseId { get; set; }
        public string ExerciseName { get; set; } = "";
        public string RecordType { get; set; } = "";
        public double Value { get; set; }
        public double? WeightKg { get; set; }
        public DateTime AchievedAt { get; set; }
    }

    // ── Skill Models ──────────────────────────────────────────
    public class SkillModel
    {
        public int SkillId { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "";
        public string Difficulty { get; set; } = "";
        public bool IsUnlocked { get; set; }
        public int CurrentStepId { get; set; }
        public int CurrentStep { get; set; }
        public int TotalSteps { get; set; }
        public bool RequirementsMet { get; set; }
        public string CurrentStepTitle { get; set; } = "";
        public string CurrentStepInstructions { get; set; } = "";
        public string CurrentTip { get; set; } = "";
        public DateTime? UnlockedAt { get; set; }
        public DateTime? MasteredAt { get; set; }
        public List<SkillStepModel> Steps { get; set; } = new();
        public List<string> Tips { get; set; } = new();
        public List<SkillRequirementModel> Requirements { get; set; } = new();
        public int Percent => TotalSteps > 0 ? (int)Math.Round((double)CurrentStep / TotalSteps * 100) : 0;
        public bool IsMastered => TotalSteps > 0 && CurrentStep >= TotalSteps;
    }

    public class SkillStepModel
    {
        public int StepId { get; set; }
        public int StepOrder { get; set; }
        public string Title { get; set; } = "";
        public string Instructions { get; set; } = "";
    }

    public class SkillRequirementModel
    {
        public int ExerciseId { get; set; }
        public string ExerciseName { get; set; } = "";
        public int RequiredReps { get; set; }
        public int UserBest { get; set; }
        public bool Met => UserBest >= RequiredReps;
    }

    // ── Workout Calendar ──────────────────────────────────────
    public class CalendarDayModel
    {
        public DateTime Date { get; set; }
        public bool HasWorkout { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsRestDay { get; set; }
        public bool IsToday => Date.Date == DateTime.Today;
        public bool IsFuture => Date.Date > DateTime.Today;
        public int? SessionId { get; set; }
        public int SetCount { get; set; }
        public int WaterMl { get; set; }
        public int WaterGoalMl { get; set; }
        public bool HasWater => WaterMl > 0;
        public double? WeightKg { get; set; }
        public bool HasWeight => WeightKg.HasValue;
    }

    // ── View Models ───────────────────────────────────────────
    public class DashboardVM
    {
        public UserModel User { get; set; } = new();
        public SessionModel? TodaySession { get; set; }
        public ScheduleSlotModel? TodaySlot { get; set; }
        public ProgramDayModel? TodayDay { get; set; }
        public List<ActiveSessionExerciseModel> TodayExercises { get; set; } = new();
        public List<SessionModel> RecentSessions { get; set; } = new();
        public List<PersonalRecordModel> RecentPRs { get; set; } = new();
        public List<SkillModel> ActiveSkills { get; set; } = new();
        public List<WeightEntryModel> WeightHistory { get; set; } = new();
        public List<UserInjuryModel> ActiveInjuries { get; set; } = new();
        public WaterSummaryModel Water { get; set; } = new();
        public List<AchievementModel> NewAchievements { get; set; } = new();
        public List<ExerciseBadgeModel> TopBadges { get; set; } = new();
        public string BmiWarning { get; set; } = "";
        public bool HasSchedule { get; set; }
        public bool IsRestDay { get; set; }
        public string RestMessage { get; set; } = "";
        public int TotalWorkouts { get; set; }
        public double AvgSessionMins { get; set; }
    }

    public class ProgramsVM
    {
        public List<ProgramModel> MyPrograms { get; set; } = new();
        public List<ProgramModel> PresetPrograms { get; set; } = new();
        public UserScheduleModel? ActiveSchedule { get; set; }
        public List<MuscleGroupModel> MuscleGroups { get; set; } = new();
        public List<ExerciseModel> AllExercises { get; set; } = new();
    }

    public class WorkoutsVM
    {
        public List<SessionModel> History { get; set; } = new();
        public List<PersonalRecordModel> PRs { get; set; } = new();
        public List<ExerciseModel> Exercises { get; set; } = new();
        public List<CalendarDayModel> CalendarDays { get; set; } = new();
        public List<ExerciseBadgeModel> Badges { get; set; } = new();
    }

    public class ActiveWorkoutVM
    {
        public int SessionId { get; set; }
        public int DayId { get; set; }
        public string DayName { get; set; } = "";
        public string ProgramName { get; set; } = "";
        public List<ActiveSessionExerciseModel> Exercises { get; set; } = new();
    }

    public class SkillsVM
    {
        public List<SkillModel> Skills { get; set; } = new();
        public List<PersonalRecordModel> PRs { get; set; } = new();
    }

    public class ProfileVM
    {
        public UserModel User { get; set; } = new();
        public List<UserInjuryModel> Injuries { get; set; } = new();
        public List<BodyPartModel> BodyParts { get; set; } = new();
        public List<InjuryCategoryModel> Categories { get; set; } = new();
        public List<WeightEntryModel> WeightHistory { get; set; } = new();
        public List<PersonalRecordModel> TopPRs { get; set; } = new();
        public List<BodyMeasurementModel> Measurements { get; set; } = new();
        public BodyMeasurementModel? LatestMeasurement { get; set; }
        public List<AchievementModel> Achievements { get; set; } = new();
        public List<ExerciseBadgeModel> Badges { get; set; } = new();

        // ── Journey / points system ──────────────────────────────
        public int TotalPoints =>
            Achievements.Where(a => a.IsUnlocked).Sum(a => a.Points) +
            Badges.Sum(b => b.Points);

        public string JourneyTitle => JourneyTier.TitleFor(TotalPoints);
        public int JourneyNextAt => JourneyTier.NextThreshold(TotalPoints);
        public int JourneyProgressPct => JourneyTier.ProgressPct(TotalPoints);
    }

    /// <summary>
    /// Maps a user's total earned points (from achievements + exercise badges)
    /// to a "journey" title, so progress feels like a narrative, not just a number.
    /// </summary>
    public static class JourneyTier
    {
        // (minimum points required, title)
        private static readonly (int min, string title)[] Tiers = {
            (0,    "Beginner"),
            (50,   "Rookie"),
            (150,  "Contender"),
            (300,  "Warrior"),
            (500,  "Veteran"),
            (800,  "Elite"),
            (1200, "Champion"),
            (2000, "Alpha")
        };

        public static string TitleFor(int points)
        {
            string title = Tiers[0].title;
            foreach (var t in Tiers)
                if (points >= t.min) title = t.title; else break;
            return title;
        }

        public static int NextThreshold(int points)
        {
            foreach (var t in Tiers)
                if (points < t.min) return t.min;
            return Tiers[^1].min; // already at max tier
        }

        public static int ProgressPct(int points)
        {
            for (int i = 0; i < Tiers.Length; i++)
            {
                if (i == Tiers.Length - 1) return 100; // maxed out
                if (points < Tiers[i + 1].min)
                {
                    int span = Tiers[i + 1].min - Tiers[i].min;
                    int into = points - Tiers[i].min;
                    return span > 0 ? Math.Min(100, into * 100 / span) : 100;
                }
            }
            return 100;
        }
    }

    public class InjuryReportVM
    {
        public List<BodyPartModel> JointParts { get; set; } = new();
        public List<BodyPartModel> MuscleParts { get; set; } = new();
        public List<InjuryCategoryModel> Categories { get; set; } = new();
    }

    // ── AJAX Request Models ───────────────────────────────────
    public class ThemeReq { public string Theme { get; set; } = "dark"; }
    public class SkillActionReq { public int SkillId { get; set; } }
    public class StartSessionReq { public int DayId { get; set; } }
    public class LogInjuryReq { public int PartId { get; set; } public int CategoryId { get; set; } public string Notes { get; set; } = ""; }
    public class WaterLogReq { public int AmountMl { get; set; } = 250; }

    public class FinishSessionReq
    {
        public int SessionId { get; set; }
        public int DayId { get; set; }
        public List<LoggedSet> Sets { get; set; } = new();
        public string? Notes { get; set; }
    }

    public class LoggedSet
    {
        public int PdeId { get; set; }
        public int ExerciseId { get; set; }
        public int SetNumber { get; set; }
        public int TargetReps { get; set; }
        public int ActualReps { get; set; }
        public double? WeightKg { get; set; }
        public int? Rpe { get; set; }
        public bool Skipped { get; set; }
    }

    public class CreateProgramReq
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string GoalType { get; set; } = "General";
        public string ProgressionStyle { get; set; } = "Adaptive";
        public List<DayReq> Days { get; set; } = new();
    }

    public class DayReq
    {
        public string Name { get; set; } = "";
        public string DayType { get; set; } = "Workout";
        public List<ExerciseReq> Exercises { get; set; } = new();
    }

    public class ExerciseReq
    {
        public int ExerciseId { get; set; }
        public int Sets { get; set; } = 3;
        public int Reps { get; set; } = 10;
        public double? WeightKg { get; set; }
        public int RestSeconds { get; set; } = 90;
    }

    public class SaveScheduleReq
    {
        public int ScheduleId { get; set; }
        public string Name { get; set; } = "My Schedule";
        public List<SlotReq> Slots { get; set; } = new();
    }

    public class SlotReq
    {
        public int WeekDay { get; set; }
        public int? DayId { get; set; }
    }

    public class LogMeasurementReq
    {
        public double? ChestCm { get; set; }
        public double? WaistCm { get; set; }
        public double? HipsCm { get; set; }
        public double? LeftArmCm { get; set; }
        public double? RightArmCm { get; set; }
        public double? LeftThighCm { get; set; }
        public double? RightThighCm { get; set; }
        public double? NeckCm { get; set; }
        public double? ShouldersCm { get; set; }
        public double? BodyFatPct { get; set; }
        public string Notes { get; set; } = "";
    }

    public class OneRMCalcReq
    {
        public double Weight { get; set; }
        public int Reps { get; set; }
    }
}