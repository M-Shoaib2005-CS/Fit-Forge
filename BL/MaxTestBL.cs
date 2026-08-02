using FitForge.DL; using FitForge.Models; using System.Linq;
namespace FitForge.BL
{
    public class MaxTestBL(MaxTestDL dl, PersonalRecordDL prDL)
    {
        public MaxTestVM GetHub(int uid) => new MaxTestVM {
            SkillExercises = dl.GetSkillExerciseItems(uid),
            GymLifts = dl.GetGymLiftItems(uid)
        };

        public List<MaxTestAttemptModel> GetRecentAttempts(int uid, int exerciseId) =>
            dl.GetRecentAttempts(uid, exerciseId, 3);

        // Logs the attempt (kept forever, for history), then feeds the same
        // PR pipeline a real workout set would — sessionId is null since this
        // deliberately isn't tied to any workout session.
        public MaxTestHubItemModel LogAttempt(int uid, LogMaxTestReq req)
        {
            dl.LogAttempt(uid, req.ExerciseId, req.Reps, req.WeightKg);
            prDL.CheckAndSave(uid, req.ExerciseId, null, req.Reps, req.WeightKg);

            var skillMatch = dl.GetSkillExerciseItems(uid).FirstOrDefault(x => x.ExerciseId == req.ExerciseId);
            if (skillMatch != null) return skillMatch;

            var gymMatch = dl.GetGymLiftItems(uid).FirstOrDefault(x => x.ExerciseId == req.ExerciseId);
            return gymMatch ?? new MaxTestHubItemModel { ExerciseId = req.ExerciseId, BestReps = req.Reps, BestWeightKg = req.WeightKg };
        }
    }
}
