using FitForge.DL;
using FitForge.Models;

namespace FitForge.BL
{
    public class ProgramBL(ProgramDL dl, ScheduleDL schedDL, ExerciseDL exDL)
    {
        public ProgramsVM BuildVM(int uid)
        {
            var myProgs     = dl.GetByUserShallow(uid);
            var presets     = dl.GetPresetsShallow();
            var sched       = schedDL.GetActiveForUser(uid);

            // Attach day lists to programs so the schedule builder can see them
            foreach (var p in myProgs.Concat(presets))
                p.Days = dl.GetDaysForProgram(p.ProgramId, withExercises: false);

            return new ProgramsVM
            {
                MyPrograms     = myProgs,
                PresetPrograms = presets,
                ActiveSchedule = sched,
                MuscleGroups   = exDL.GetMuscleGroups(),
                AllExercises   = exDL.GetAll()
            };
        }

        public (bool ok, string msg, int programId) CreateProgram(int uid, CreateProgramReq req)
            => CreateProgram(uid, req, autoSchedule: false);

        // autoSchedule=true also assigns the newly created days onto the user's active weekly
        // schedule (Mon.. in day order), which is what the AI coach's "Apply" button uses so a
        // proposed program is immediately live on the week, not just sitting in the program list.
        public (bool ok, string msg, int programId) CreateProgram(int uid, CreateProgramReq req, bool autoSchedule)
        {
            if (string.IsNullOrWhiteSpace(req.Name))     return (false, "Program name required", 0);
            if (req.Days == null || req.Days.Count == 0) return (false, "Add at least one day", 0);

            int pid = dl.CreateProgram(uid, req.Name.Trim(), req.Description ?? "",
                req.GoalType, req.ProgressionStyle);

            var dayIds = new List<int>();
            for (int i = 0; i < req.Days.Count; i++)
            {
                var d   = req.Days[i];
                int did = dl.AddDay(pid, i + 1, d.Name.Trim(), d.DayType);
                dayIds.Add(did);
                if (d.DayType == "Workout" && d.Exercises != null)
                    for (int j = 0; j < d.Exercises.Count; j++)
                    {
                        var e = d.Exercises[j];
                        dl.AddExercise(did, e.ExerciseId, j + 1, e.Sets, e.Reps, e.WeightKg, e.RestSeconds);
                    }
            }

            string msg = "Program created!";
            if (autoSchedule && dayIds.Count > 0)
            {
                try
                {
                    int sid = schedDL.EnsureSchedule(uid);
                    var slots = new List<SlotReq>();
                    for (int i = 0; i < dayIds.Count && i < 7; i++)
                        slots.Add(new SlotReq { WeekDay = i, DayId = dayIds[i] });
                    schedDL.SaveSlots(sid, slots);
                    msg = "Program created and set as your weekly schedule!";
                }
                catch
                {
                    // Program itself was created successfully either way — scheduling is best-effort.
                    msg = "Program created! Head to Programs to add it to your weekly schedule.";
                }
            }
            return (true, msg, pid);
        }

        public (bool ok, string msg) DeleteProgram(int uid, int programId)
        {
            bool ok = dl.DeleteProgram(programId, uid);
            return ok ? (true, "Program deleted") : (false, "Could not delete — must be your own program");
        }

        public ProgramModel? GetFull(int programId) => dl.GetFull(programId);

        public (bool ok, string msg) SaveSchedule(int uid, SaveScheduleReq req)
        {
            if (req.Slots == null || req.Slots.Count == 0) return (false, "No slots provided");
            int sid = schedDL.EnsureSchedule(uid);
            schedDL.SaveSlots(sid, req.Slots);
            return (true, "Schedule saved!");
        }
    }
}
